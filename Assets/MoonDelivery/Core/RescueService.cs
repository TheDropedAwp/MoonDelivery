using System;
using System.Linq;
using UnityEngine;

namespace MoonDelivery
{
    internal sealed class RescueService
    {
        private const float RescueSpeed = 4.5f;

        private readonly IGameContext context;
        private readonly DeliveryService deliveries;
        private readonly OrderService orders;

        public RescueService(
            IGameContext context,
            DeliveryService deliveries,
            OrderService orders
        )
        {
            this.context = context;
            this.deliveries = deliveries;
            this.orders = orders;
        }

        public int RescueCost(Delivery delivery)
        {
            if (delivery == null)
                return 0;

            Rover rover = context.Rover(delivery.roverId);
            if (rover == null)
                return 0;

            int cargoValue =
                delivery.cargoWeight <= .01f || delivery.cargoTransferred
                    ? 0
                    : delivery
                        .stops.Skip(delivery.currentStopIndex)
                        .Where(stop => !string.IsNullOrEmpty(stop.orderId))
                        .Select(stop => context.Order(stop.orderId))
                        .Where(order => order != null && order.status == OrderStatus.InProgress)
                        .Sum(order => order.cargoValue);

            return Mathf.CeilToInt(rover.purchaseCost * .5f + cargoValue * .3f);
        }

        public void Rescue(Delivery delivery)
        {
            if (delivery == null || delivery.status != DeliveryStatus.Broken)
                return;

            bool rescueAlreadyDispatched = context.State.rescueMissions.Any(mission =>
                !mission.completed && mission.deliveryId == delivery.id
            );
            if (rescueAlreadyDispatched)
                return;

            if (delivery.cargoRecoveryDispatched)
            {
                context.Log("Сначала дождитесь ровера, отправленного за грузом.");
                return;
            }

            int cost = RescueCost(delivery);
            if (context.State.money < cost)
            {
                context.Log("Недостаточно денег для эвакуации.");
                return;
            }

            context.State.money -= cost;
            Vector2 target = deliveries.Position(delivery);
            float distance =
                Vector2.Distance(new Vector2(GameCatalog.Base.x, GameCatalog.Base.y), target) * 92f;

            context.State.rescueMissions.Add(
                new RescueMission
                {
                    id = Guid.NewGuid().ToString("N"),
                    deliveryId = delivery.id,
                    targetX = target.x,
                    targetY = target.y,
                    distance = Mathf.Max(1f, distance),
                }
            );

            context.Log(
                $"Эвакуатор отправлен к {context.Rover(delivery.roverId).displayName} "
                    + $"(−{cost} кр.)."
            );
            context.Emit(GameCue.Rescue);
            context.Save();
        }

        public void ProcessRescueMissions(float minutes)
        {
            foreach (
                RescueMission mission in context.State.rescueMissions.Where(item => !item.completed)
            )
            {
                mission.progress += RescueSpeed * minutes;
                if (mission.progress < mission.distance)
                    continue;

                if (mission.phase == 0)
                {
                    mission.phase = 1;
                    mission.progress = 0f;
                    context.Log("Эвакуатор добрался до аварийного ровера и начал буксировку.");
                    context.Emit(GameCue.Confirm);
                    continue;
                }

                FinishRescue(mission);
            }
        }

        private void FinishRescue(RescueMission mission)
        {
            Delivery delivery = context.State.deliveries.Find(item =>
                item.id == mission.deliveryId
            );
            if (delivery == null)
            {
                mission.completed = true;
                return;
            }

            Rover rover = context.Rover(delivery.roverId);
            delivery.status = DeliveryStatus.Failed;

            if (!delivery.cargoTransferred && delivery.pendingPickupWeight <= 0f)
            {
                foreach (RouteStop stop in delivery.stops)
                {
                    Order order = context.Order(stop.orderId);
                    if (order == null || order.status != OrderStatus.InProgress)
                        continue;

                    orders.Fail(order, $"Заказ «{order.title}» провален");
                }
            }

            rover.status = RoverStatus.Broken;
            mission.completed = true;
            context.Log(
                $"Эвакуатор доставил {rover.displayName} на базу. "
                    + "Перед новым рейсом нужен ремонт."
            );
            context.Emit(GameCue.Notification);
            context.CheckGameOver();
        }
    }
}
