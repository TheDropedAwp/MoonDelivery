using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MoonDelivery
{
    internal sealed class DeliveryService
    {
        private const float MaxSimulationStepMinutes = .02f;

        private readonly IGameContext context;
        private readonly RoutePlanningService routes;
        private readonly OrderService orders;

        public DeliveryService(
            IGameContext context,
            RoutePlanningService routes,
            OrderService orders
        )
        {
            this.context = context;
            this.routes = routes;
            this.orders = orders;
        }

        public bool Start(IList<RouteStop> stops, Rover rover, out string message)
        {
            message = routes.Validate(stops, rover);
            if (message != null)
                return false;

            RouteForecast forecast = routes.Forecast(stops, rover);
            List<Order> routeOrders = routes.OrdersFor(stops);

            foreach (Order order in routeOrders)
                order.status = OrderStatus.InProgress;

            rover.status = RoverStatus.Delivering;
            var delivery = new Delivery
            {
                id = Guid.NewGuid().ToString("N"),
                roverId = rover.id,
                status = DeliveryStatus.Travelling,
                startedMinute = context.State.absoluteMinute,
                cargoWeight = routeOrders.Sum(order => order.weightKg),
                prepaidStationCredits = forecast.stationCost,
            };

            foreach (RouteStop stop in stops)
                delivery.stops.Add(stop.Clone());

            context.State.money -= forecast.stationCost;
            routes.ScheduleBreakdown(delivery, rover);
            context.State.deliveries.Add(delivery);
            orders.MaintainOrders();

            message =
                $"{rover.displayName} отправлен. "
                + $"Остановок: {stops.Count}, заказов: {routeOrders.Count}.";
            if (forecast.stationCost > 0)
                message += $" Платные зарядки: −{forecast.stationCost} кр.";

            context.Log(message);
            context.Emit(GameCue.Confirm);
            context.Save();
            return true;
        }

        public void ProcessDeliveries(float minutes)
        {
            foreach (Delivery delivery in context.State.deliveries.ToList())
            {
                float remainingMinutes = minutes;
                while (
                    delivery.status == DeliveryStatus.Travelling
                    && remainingMinutes > .0001f
                )
                {
                    float stepMinutes = Mathf.Min(MaxSimulationStepMinutes, remainingMinutes);
                    ProcessDelivery(delivery, stepMinutes);
                    remainingMinutes -= stepMinutes;
                }
            }
        }

        public Vector2 Position(Delivery delivery)
        {
            if (delivery == null || delivery.currentStopIndex >= delivery.stops.Count)
            {
                MapPoint current =
                    delivery == null ? GameCatalog.Base : context.Point(delivery.currentPointId);
                return new Vector2(current.x, current.y);
            }

            MapPoint from = context.Point(delivery.currentPointId);
            MapPoint to = context.Point(delivery.stops[delivery.currentStopIndex].pointId);
            RouteSegment leg = context.Leg(from.id, to.id);
            float progress = Mathf.Clamp01(delivery.legProgress / leg.distance);
            return Vector2.Lerp(new Vector2(from.x, from.y), new Vector2(to.x, to.y), progress);
        }

        public float RecoverableCargoWeight(Delivery source)
        {
            if (
                source == null
                || source.status != DeliveryStatus.Broken
                || source.cargoWeight <= .01f
                || source.cargoTransferred
            )
            {
                return 0f;
            }

            float activeWeight = source
                .stops.Skip(source.currentStopIndex)
                .Where(stop => !string.IsNullOrEmpty(stop.orderId))
                .Select(stop => context.Order(stop.orderId))
                .Where(order => order != null && order.status == OrderStatus.InProgress)
                .Sum(order => order.weightKg);

            return Mathf.Min(source.cargoWeight, activeWeight);
        }

        private void ProcessDelivery(Delivery delivery, float minutes)
        {
            Rover rover = context.Rover(delivery.roverId);
            if (rover == null || delivery.currentStopIndex >= delivery.stops.Count)
                return;

            if (delivery.stationRemainingMinutes > 0f)
            {
                ProcessStationCharge(delivery, rover, minutes);
                return;
            }

            RouteStop stop = delivery.stops[delivery.currentStopIndex];
            RouteSegment leg = context.Leg(delivery.currentPointId, stop.pointId);
            MapPoint from = context.Point(delivery.currentPointId);
            MapPoint to = context.Point(stop.pointId);
            float terrainProgress = Mathf.Clamp01(
                delivery.legProgress / Mathf.Max(.001f, leg.distance)
            );
            TerrainType terrain = GameCatalog.TerrainAt(from, to, terrainProgress);
            float loadRatio = delivery.cargoWeight / rover.Stats.capacityKg;
            float speedPerMinute =
                rover.Stats.speed
                / 4f
                * GameCatalog.TerrainSpeed(terrain, rover.type)
                * Mathf.Max(.55f, 1f - loadRatio * .25f);
            float moved = Mathf.Min(speedPerMinute * minutes, leg.distance - delivery.legProgress);

            bool reachesBreakdown =
                delivery.breakdownScheduled
                && delivery.currentStopIndex == delivery.breakdownStopIndex
                && delivery.breakdownAtDistance <= delivery.legProgress + moved + .001f;

            if (reachesBreakdown)
            {
                moved = Mathf.Max(0f, delivery.breakdownAtDistance - delivery.legProgress);
            }

            float positionX = Mathf.Lerp(
                from.x,
                to.x,
                Mathf.Clamp01((delivery.legProgress + moved * .5f) / leg.distance)
            );
            bool poweredBySun =
                rover.Stats.solarPowered
                && LunarCycle.IsSunlit(positionX, context.State.absoluteMinute);

            if (!poweredBySun)
            {
                float requiredEnergy =
                    RoutePlanningService.EnergyForDistance(moved, terrain, loadRatio)
                    * rover.Stats.energyUseMultiplier;

                if (rover.battery + .01f < requiredEnergy)
                {
                    BreakDelivery(
                        delivery,
                        rover,
                        $"{rover.displayName} остановился: батарея разряжена. Нужен эвакуатор."
                    );
                    return;
                }

                rover.battery -= requiredEnergy;
            }

            delivery.legProgress += moved;

            if (reachesBreakdown)
            {
                BreakDelivery(
                    delivery,
                    rover,
                    $"Авария! {rover.displayName} сломался на участке "
                        + $"«{MoonGame.TerrainName(terrain)}»."
                );
                return;
            }

            if (delivery.legProgress + .01f < leg.distance)
                return;

            Arrive(delivery, rover, stop);
        }

        private void ProcessStationCharge(Delivery delivery, Rover rover, float minutes)
        {
            delivery.stationRemainingMinutes -= minutes;
            if (delivery.stationRemainingMinutes > 0f)
                return;

            rover.battery = rover.Stats.maxBattery;
            context.Log($"{rover.displayName} завершил быструю зарядку.");
            context.Emit(GameCue.Notification);

            if (delivery.currentStopIndex < delivery.stops.Count)
                return;

            delivery.status = DeliveryStatus.Completed;
            rover.status = RoverStatus.Ready;
            context.Log($"{rover.displayName} завершил маршрут и готов к новому заданию.");
        }

        private void Arrive(Delivery delivery, Rover rover, RouteStop stop)
        {
            delivery.currentPointId = stop.pointId;
            delivery.currentStopIndex++;
            delivery.legProgress = 0f;

            DeliverOrder(delivery, stop);
            TransferRecoveredCargo(delivery, rover, stop);

            MapPoint point = context.Point(stop.pointId);
            if (point.type == MapPointType.ChargingStation)
            {
                delivery.stationRemainingMinutes = RoutePlanningService.StationDurationMinutes;
                context.Log(
                    $"{rover.displayName} прибыл на «{point.name}»: "
                        + $"зарядка оплачена при отправке, ожидание "
                        + $"{RoutePlanningService.StationDurationMinutes:0} мин."
                );
                context.Emit(GameCue.Confirm);
            }

            if (
                delivery.currentStopIndex < delivery.stops.Count
                || delivery.stationRemainingMinutes > 0f
            )
            {
                return;
            }

            delivery.status = DeliveryStatus.Completed;
            rover.status = RoverStatus.Ready;
            context.Log($"{rover.displayName} завершил маршрут и готов к новому заданию.");
        }

        private void DeliverOrder(Delivery delivery, RouteStop stop)
        {
            if (string.IsNullOrEmpty(stop.orderId))
                return;

            Order order = context.Order(stop.orderId);
            if (order == null)
                return;

            delivery.cargoWeight = Mathf.Max(0f, delivery.cargoWeight - order.weightKg);
            if (order.status == OrderStatus.InProgress)
                orders.Complete(order);
        }

        private void TransferRecoveredCargo(Delivery delivery, Rover rover, RouteStop stop)
        {
            bool reachedPickup =
                !string.IsNullOrEmpty(delivery.cargoPickupPointId)
                && stop.pointId == delivery.cargoPickupPointId
                && delivery.pendingPickupWeight > 0f;
            if (!reachedPickup)
                return;

            Delivery source = context.State.deliveries.Find(item =>
                item.id == delivery.cargoSourceDeliveryId
            );
            delivery.cargoWeight =
                source != null ? RecoverableCargoWeight(source) : delivery.pendingPickupWeight;
            delivery.pendingPickupWeight = 0f;

            if (source == null)
                return;

            source.cargoWeight = 0f;
            source.cargoTransferred = true;
            source.cargoRecoveryDispatched = false;
            context.Log(
                $"{rover.displayName} забрал груз у аварийного ровера и продолжил маршрут."
            );
            context.Emit(GameCue.Confirm);
        }

        private void BreakDelivery(Delivery delivery, Rover rover, string message)
        {
            delivery.status = DeliveryStatus.Broken;
            rover.status = RoverStatus.Broken;

            if (
                delivery.pendingPickupWeight > 0f
                && !string.IsNullOrEmpty(delivery.cargoSourceDeliveryId)
            )
            {
                Delivery source = context.State.deliveries.Find(item =>
                    item.id == delivery.cargoSourceDeliveryId
                );
                if (source != null)
                    source.cargoRecoveryDispatched = false;
            }

            context.Log(message);
            context.Emit(GameCue.Breakdown);
        }
    }
}
