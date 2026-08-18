using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MoonDelivery
{
    internal sealed class RoutePlanningService
    {
        public const float StationDurationMinutes = 10f;
        public const float StationCreditPerBatteryUnit = .5f;

        private readonly IGameContext context;
        private readonly IRandomSource random;

        public RoutePlanningService(IGameContext context, IRandomSource random)
        {
            this.context = context;
            this.random = random;
        }

        public string Validate(IList<RouteStop> stops, Rover rover)
        {
            if (context.State.gameOver)
                return "Компания закрыта";

            if (rover == null || rover.status != RoverStatus.Ready)
                return "Выберите свободный ровер";

            if (stops == null || stops.Count == 0)
                return "Добавьте заказ в маршрут";

            List<Order> orders = OrdersFor(stops);
            if (orders.Count == 0)
                return "В маршруте нет заказов";

            if (orders.Select(order => order.id).Distinct().Count() != orders.Count)
                return "Один заказ добавлен дважды";

            if (orders.Exists(order => order.status != OrderStatus.Available))
                return "Один из заказов уже недоступен";

            float load = orders.Sum(order => order.weightKg);
            if (load > rover.Stats.capacityKg + .01f)
                return $"Перегруз: {load:0}/{rover.Stats.capacityKg:0} кг";

            string currentPointId = "base";
            float battery = rover.battery;
            float simulatedMinute = context.State.absoluteMinute;
            int chargingCost = 0;

            foreach (RouteStop stop in stops)
            {
                MapPoint destination = context.Point(stop.pointId);
                if (destination == null)
                    return "В маршруте есть неизвестная точка";

                RouteSegment leg = context.Leg(currentPointId, stop.pointId);
                if (rover.Stats.cannotCrossRocks && leg.terrain == TerrainType.Rocks)
                    return $"{rover.displayName} не проходит участок со скалами";

                float duration;
                float energy = EstimateLegEnergy(
                    leg,
                    context.Point(currentPointId),
                    destination,
                    load,
                    rover,
                    simulatedMinute,
                    out duration
                );

                if (battery + .01f < energy)
                    return $"Не хватит заряда до «{destination.name}»: нужно ≈{energy:0}, есть {battery:0}";

                battery -= energy;
                simulatedMinute += duration;

                if (!string.IsNullOrEmpty(stop.orderId))
                {
                    Order order = context.Order(stop.orderId);
                    if (order == null || order.destinationId != stop.pointId)
                        return "Заказ привязан к неверной точке";

                    load -= order.weightKg;
                }

                if (destination.type == MapPointType.ChargingStation)
                {
                    chargingCost += StationChargeCost(rover, battery);
                    if (context.State.money < chargingCost)
                        return $"Не хватает денег на платную зарядку: нужно {chargingCost} кр.";

                    battery = rover.Stats.maxBattery;
                    simulatedMinute += StationDurationMinutes;
                }

                currentPointId = stop.pointId;
            }

            return null;
        }

        public RouteForecast Forecast(IList<RouteStop> stops, Rover rover)
        {
            var forecast = new RouteForecast();
            if (rover == null || stops == null || stops.Count == 0)
            {
                forecast.summary = "Маршрут не построен";
                return forecast;
            }

            float load = OrdersFor(stops).Sum(order => order.weightKg);
            float minute = context.State.absoluteMinute;
            float battery = rover.battery;
            string currentPointId = "base";

            foreach (RouteStop stop in stops)
            {
                MapPoint from = context.Point(currentPointId);
                MapPoint destination = context.Point(stop.pointId);
                RouteSegment leg = context.Leg(currentPointId, stop.pointId);
                float duration;
                float energy = EstimateLegEnergy(
                    leg,
                    from,
                    destination,
                    load,
                    rover,
                    minute,
                    out duration
                );

                forecast.distance += leg.distance;
                forecast.durationMinutes += duration;
                forecast.energy += energy;
                minute += duration;
                battery = Mathf.Max(0f, battery - energy);

                float loadRatio = load / rover.Stats.capacityKg;
                float risk = RouteRisk(leg.terrain, rover, loadRatio);
                forecast.breakdownChance = Mathf.Max(forecast.breakdownChance, risk);

                if (!string.IsNullOrEmpty(stop.orderId))
                {
                    Order order = context.Order(stop.orderId);
                    if (order != null)
                    {
                        forecast.missesDeadline |= minute > order.deadlineMinute;
                        load -= order.weightKg;
                    }
                }

                if (destination.type == MapPointType.ChargingStation)
                {
                    forecast.stationCost += StationChargeCost(rover, battery);
                    battery = rover.Stats.maxBattery;
                    forecast.durationMinutes += StationDurationMinutes;
                    minute += StationDurationMinutes;
                }

                currentPointId = stop.pointId;
            }

            forecast.summary = BuildForecastSummary(forecast);
            return forecast;
        }

        public int StationChargeCost(Rover rover, float battery)
        {
            if (rover == null)
                return 0;

            float currentBattery = Mathf.Clamp(battery, 0f, rover.Stats.maxBattery);
            float missingBattery = rover.Stats.maxBattery - currentBattery;
            return Mathf.CeilToInt(missingBattery * StationCreditPerBatteryUnit);
        }

        public void ScheduleBreakdown(Delivery delivery, Rover rover)
        {
            int dangerousStopIndex;
            delivery.rolledBreakdownChance = HighestRouteRisk(
                delivery,
                rover,
                out dangerousStopIndex
            );
            delivery.breakdownScheduled =
                dangerousStopIndex >= 0 && random.Value < delivery.rolledBreakdownChance;
            delivery.breakdownStopIndex = delivery.breakdownScheduled ? dangerousStopIndex : -1;

            if (!delivery.breakdownScheduled)
                return;

            string fromId =
                dangerousStopIndex == 0
                    ? delivery.currentPointId
                    : delivery.stops[dangerousStopIndex - 1].pointId;

            RouteSegment dangerousLeg = context.Leg(
                fromId,
                delivery.stops[dangerousStopIndex].pointId
            );
            delivery.breakdownAtDistance = dangerousLeg.distance * random.Range(.22f, .86f);
        }

        public float EstimateLegEnergy(
            RouteSegment leg,
            MapPoint from,
            MapPoint to,
            float load,
            Rover rover,
            float startMinute,
            out float duration
        )
        {
            float loadRatio = load / rover.Stats.capacityKg;
            float energy = 0f;
            duration = 0f;

            int slices = Mathf.Max(16, Mathf.CeilToInt(leg.distance));
            float sliceDistance = leg.distance / slices;

            for (int index = 0; index < slices; index++)
            {
                float progress = (index + .5f) / slices;
                TerrainType terrain = GameCatalog.TerrainAt(from, to, progress);
                float speed =
                    rover.Stats.speed
                    / 4f
                    * GameCatalog.TerrainSpeed(terrain, rover.type)
                    * Mathf.Max(.55f, 1f - loadRatio * .25f);
                float sliceDuration = sliceDistance / Mathf.Max(.001f, speed);
                float sampleMinute = startMinute + duration + sliceDuration * .5f;
                float positionX = Mathf.Lerp(from.x, to.x, progress);

                bool poweredBySun =
                    rover.Stats.solarPowered && LunarCycle.IsSunlit(positionX, sampleMinute);

                if (!poweredBySun)
                {
                    energy +=
                        EnergyForDistance(sliceDistance, terrain, loadRatio)
                        * rover.Stats.energyUseMultiplier;
                }

                duration += sliceDuration;
            }

            return energy;
        }

        public static float EnergyForDistance(float distance, TerrainType terrain, float loadRatio)
        {
            float terrainMultiplier;
            switch (terrain)
            {
                case TerrainType.Rough:
                    terrainMultiplier = 1.2f;
                    break;
                case TerrainType.Crater:
                    terrainMultiplier = 1.38f;
                    break;
                case TerrainType.Rocks:
                    terrainMultiplier = 1.5f;
                    break;
                default:
                    terrainMultiplier = 1f;
                    break;
            }

            return distance * .75f * terrainMultiplier * (1f + loadRatio * .55f);
        }

        public List<Order> OrdersFor(IList<RouteStop> stops)
        {
            if (stops == null)
                return new List<Order>();

            return stops
                .Where(stop => !string.IsNullOrEmpty(stop.orderId))
                .Select(stop => context.Order(stop.orderId))
                .Where(order => order != null)
                .ToList();
        }

        private float HighestRouteRisk(Delivery delivery, Rover rover, out int dangerousStopIndex)
        {
            dangerousStopIndex = -1;
            if (delivery?.stops == null || rover == null)
                return 0f;

            float load = delivery.cargoWeight;
            float highestRisk = 0f;
            string currentPointId = delivery.currentPointId;

            for (int index = 0; index < delivery.stops.Count; index++)
            {
                RouteStop stop = delivery.stops[index];
                RouteSegment leg = context.Leg(currentPointId, stop.pointId);
                float loadRatio = load / rover.Stats.capacityKg;
                float risk = RouteRisk(leg.terrain, rover, loadRatio);

                if (risk > highestRisk)
                {
                    highestRisk = risk;
                    dangerousStopIndex = index;
                }

                if (
                    stop.pointId == delivery.cargoPickupPointId
                    && delivery.pendingPickupWeight > 0f
                )
                    load = delivery.pendingPickupWeight;

                if (!string.IsNullOrEmpty(stop.orderId))
                {
                    Order order = context.Order(stop.orderId);
                    if (order != null)
                        load -= order.weightKg;
                }

                currentPointId = stop.pointId;
            }

            return highestRisk;
        }

        private static float RouteRisk(TerrainType terrain, Rover rover, float loadRatio)
        {
            return Mathf.Clamp(
                rover.Stats.breakdownRisk
                    * GameCatalog.TerrainRisk(terrain, rover.type)
                    * (1f + loadRatio * .3f),
                0f,
                .8f
            );
        }

        private static string BuildForecastSummary(RouteForecast forecast)
        {
            string charging =
                forecast.stationCost > 0 ? $" • зарядки {forecast.stationCost} кр." : string.Empty;
            string deadline = forecast.missesDeadline
                ? " • НЕ УСПЕЕТ К СРОКУ"
                : " • срок соблюдается";

            return $"Путь {forecast.distance:0} км"
                + $" • ≈{Mathf.CeilToInt(forecast.durationMinutes)} мин."
                + $" • расход ≈{forecast.energy:0}"
                + charging
                + $" • единый риск {forecast.breakdownChance * 100f:0.#}%"
                + deadline;
        }
    }
}
