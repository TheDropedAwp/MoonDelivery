using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MoonDelivery
{
    internal sealed class CargoRecoveryService
    {
        private readonly IGameContext context;
        private readonly RoutePlanningService routes;
        private readonly DeliveryService deliveries;

        public CargoRecoveryService(
            IGameContext context,
            RoutePlanningService routes,
            DeliveryService deliveries
        )
        {
            this.context = context;
            this.routes = routes;
            this.deliveries = deliveries;
        }

        public string ValidateCargoRecovery(
            Delivery source,
            Rover rover,
            out RouteForecast forecast
        )
        {
            forecast = new RouteForecast();

            if (source == null || source.status != DeliveryStatus.Broken)
                return "Ровер больше не находится в аварии";

            if (source.cargoRecoveryDispatched)
                return "Другой ровер уже направлен за грузом";

            float pickupWeight = deliveries.RecoverableCargoWeight(source);
            if (pickupWeight <= .01f)
                return "На месте нет активного груза";

            if (rover == null || rover.status != RoverStatus.Ready)
                return "Ровер должен быть свободен и находиться на базе";

            if (rover.id == source.roverId)
                return "Нельзя выбрать аварийный ровер";

            if (pickupWeight > rover.Stats.capacityKg + .01f)
                return $"Не хватает вместимости: {pickupWeight:0}/{rover.Stats.capacityKg:0} кг";

            return SimulateCargoRecovery(source, rover, pickupWeight, forecast);
        }

        public bool DispatchCargoRecovery(Delivery source, Rover rover, out string message)
        {
            RouteForecast forecast;
            message = ValidateCargoRecovery(source, rover, out forecast);
            if (message != null)
            {
                context.Emit(GameCue.Error);
                return false;
            }

            MapPoint pickup = CargoPickupPoint(source);
            if (context.State.dynamicPoints.All(point => point.id != pickup.id))
                context.State.dynamicPoints.Add(pickup);

            var recovery = new Delivery
            {
                id = Guid.NewGuid().ToString("N"),
                roverId = rover.id,
                status = DeliveryStatus.Travelling,
                startedMinute = context.State.absoluteMinute,
                cargoWeight = 0f,
                pendingPickupWeight = deliveries.RecoverableCargoWeight(source),
                cargoPickupPointId = pickup.id,
                cargoSourceDeliveryId = source.id,
                prepaidStationCredits = forecast.stationCost,
            };

            recovery.stops.Add(new RouteStop { pointId = pickup.id });
            recovery.stops.AddRange(RecoverableStops(source));

            context.State.money -= forecast.stationCost;
            rover.status = RoverStatus.Delivering;
            source.cargoRecoveryDispatched = true;
            routes.ScheduleBreakdown(recovery, rover);
            context.State.deliveries.Add(recovery);

            message =
                $"{rover.displayName} отправлен за грузом. "
                + $"До завершения маршрута ≈{Mathf.CeilToInt(forecast.durationMinutes)} мин.";
            context.Log(message);
            context.Emit(GameCue.Confirm);
            context.Save();
            return true;
        }

        private string SimulateCargoRecovery(
            Delivery source,
            Rover rover,
            float pickupWeight,
            RouteForecast forecast
        )
        {
            List<RouteStop> remainingStops = RecoverableStops(source);
            MapPoint pickup = CargoPickupPoint(source);
            MapPoint from = GameCatalog.Base;
            float battery = rover.battery;
            float load = 0f;
            float minute = context.State.absoluteMinute;
            var targets = new List<MapPoint> { pickup };
            targets.AddRange(
                remainingStops
                    .Select(stop => context.Point(stop.pointId))
                    .Where(point => point != null)
            );

            for (int index = 0; index < targets.Count; index++)
            {
                MapPoint target = targets[index];
                RouteSegment leg = GameCatalog.Leg(from, target);
                if (rover.Stats.cannotCrossRocks && leg.terrain == TerrainType.Rocks)
                    return $"{rover.displayName} не проходит участок со скалами";

                float duration;
                float energy = routes.EstimateLegEnergy(
                    leg,
                    from,
                    target,
                    load,
                    rover,
                    minute,
                    out duration
                );

                if (battery + .01f < energy)
                    return $"Не хватит заряда до «{target.name}»: нужно ≈{energy:0}, есть {battery:0}";

                battery -= energy;
                minute += duration;
                forecast.distance += leg.distance;
                forecast.durationMinutes += duration;
                forecast.energy += energy;

                float loadRatio = load / rover.Stats.capacityKg;
                float risk = Mathf.Clamp(
                    rover.Stats.breakdownRisk
                        * GameCatalog.TerrainRisk(leg.terrain, rover.type)
                        * (1f + loadRatio * .3f),
                    0f,
                    .8f
                );
                forecast.breakdownChance = Mathf.Max(forecast.breakdownChance, risk);

                if (index == 0)
                {
                    load = pickupWeight;
                }
                else
                {
                    RouteStop stop = remainingStops[index - 1];
                    ProcessRecoveryStopForecast(
                        stop,
                        target,
                        ref load,
                        ref battery,
                        ref minute,
                        forecast,
                        rover
                    );
                    if (context.State.money < forecast.stationCost)
                        return $"Не хватает денег на платную зарядку: нужно {forecast.stationCost} кр.";
                }

                from = target;
            }

            string charging =
                forecast.stationCost > 0 ? $" • станции {forecast.stationCost} кр." : string.Empty;
            string deadline = forecast.missesDeadline ? " • срок будет сорван" : string.Empty;
            forecast.summary =
                $"{forecast.distance:0} км"
                + $" • ≈{Mathf.CeilToInt(forecast.durationMinutes)} мин."
                + $" • заряд ≈{forecast.energy:0}"
                + charging
                + $" • риск {forecast.breakdownChance * 100f:0.#}%"
                + deadline;

            return null;
        }

        private void ProcessRecoveryStopForecast(
            RouteStop stop,
            MapPoint target,
            ref float load,
            ref float battery,
            ref float minute,
            RouteForecast forecast,
            Rover rover
        )
        {
            if (!string.IsNullOrEmpty(stop.orderId))
            {
                Order order = context.Order(stop.orderId);
                if (order != null)
                {
                    forecast.missesDeadline |= minute > order.deadlineMinute;
                    load -= order.weightKg;
                }
            }

            if (target.type != MapPointType.ChargingStation)
                return;

            forecast.stationCost += routes.StationChargeCost(rover, battery);
            battery = rover.Stats.maxBattery;
            minute += RoutePlanningService.StationDurationMinutes;
            forecast.durationMinutes += RoutePlanningService.StationDurationMinutes;
        }

        private List<RouteStop> RecoverableStops(Delivery source)
        {
            return source
                .stops.Skip(source.currentStopIndex)
                .Where(stop =>
                    string.IsNullOrEmpty(stop.orderId)
                    || context.Order(stop.orderId)?.status == OrderStatus.InProgress
                )
                .Select(stop => stop.Clone())
                .ToList();
        }

        private MapPoint CargoPickupPoint(Delivery source)
        {
            Vector2 position = deliveries.Position(source);
            return new MapPoint
            {
                id = "cargo_pickup_" + source.id,
                name = "Точка аварии",
                x = position.x,
                y = position.y,
                type = MapPointType.Destination,
            };
        }
    }
}
