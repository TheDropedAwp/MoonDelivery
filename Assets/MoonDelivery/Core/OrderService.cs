using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MoonDelivery
{
    internal sealed class OrderService
    {
        private static readonly string[] OrderTitles =
        {
            "Рационы геологам",
            "Кислородные фильтры",
            "Медицинский груз",
            "Запчасти антенны",
            "Контейнеры воды",
            "Научные образцы",
        };

        private static readonly string[] PointPrefixes =
        {
            "База",
            "Пост",
            "Модуль",
            "Лагерь",
            "Станция",
        };

        private static readonly string[] PointNames =
        {
            "Феникс",
            "Орион",
            "Аврора",
            "Меридиан",
            "Вектор",
            "Полюс",
            "Зенит",
            "Спектр",
            "Рассвет",
        };

        private readonly IGameContext context;
        private readonly IRandomSource random;

        public OrderService(IGameContext context, IRandomSource random)
        {
            this.context = context;
            this.random = random;
        }

        public void InitializeNewGame()
        {
            GenerateOrder(true);
            context.State.nextOrderSpawnMinute = context.State.absoluteMinute + 100f;
        }

        public void MigrateActiveOrders()
        {
            foreach (
                Order order in context.State.orders.Where(item =>
                    item.status == OrderStatus.Available || item.status == OrderStatus.InProgress
                )
            )
            {
                MapPoint destination = context.Point(order.destinationId);
                if (destination != null)
                    order.reward = DeliveryReward(destination, order.weightKg);
            }
        }

        public void Decline(Order order)
        {
            if (order == null || order.status != OrderStatus.Available)
                return;

            int reputationLoss = order.reputation;
            order.status = OrderStatus.Declined;
            context.State.reputation -= reputationLoss;
            context.State.nextOrderSpawnMinute = Mathf.Max(
                context.State.nextOrderSpawnMinute,
                context.State.absoluteMinute + 30f
            );

            context.Log($"Заказ «{order.title}» отклонён: −{reputationLoss} репутации.");
            context.Emit(GameCue.Error);
            MaintainOrders();
            context.CheckGameOver();
            context.Save();
        }

        public void Complete(Order order)
        {
            order.status = OrderStatus.Completed;
            context.State.money += order.reward;
            context.State.reputation += order.reputation;
            context.State.completedOrders++;
            context.State.nextOrderSpawnMinute = Mathf.Max(
                context.State.nextOrderSpawnMinute,
                context.State.absoluteMinute + RestWindowMinutes()
            );

            context.Log(
                $"Доставка «{order.title}» выполнена: "
                    + $"+{order.reward} кр., +{order.reputation} репутации."
            );
            context.Emit(GameCue.Success);
        }

        public bool Fail(Order order, string reason)
        {
            bool active =
                order != null
                && (order.status == OrderStatus.Available || order.status == OrderStatus.InProgress);
            if (!active)
                return false;

            order.status = OrderStatus.Failed;
            context.State.reputation -= order.reputation;
            context.State.nextOrderSpawnMinute = context.State.absoluteMinute + 30f;
            context.Log($"{reason}: −{order.reputation} репутации.");
            context.Emit(GameCue.Error);
            context.CheckGameOver();
            return true;
        }

        public void FailExpiredOrders()
        {
            foreach (Order order in context.State.orders)
            {
                bool active =
                    order.status == OrderStatus.Available || order.status == OrderStatus.InProgress;

                if (!active || context.State.absoluteMinute <= order.deadlineMinute)
                    continue;

                Fail(order, $"Срок заказа «{order.title}» истёк");
            }
        }

        public void MaintainOrders()
        {
            int stage = ProgressionStage();
            int availableOrders = context.State.orders.Count(order =>
                order.status == OrderStatus.Available
            );
            int targetOrders =
                stage == 0 ? 1
                : stage == 1 ? 2
                : 3;
            bool restHours = context.MinuteOfDay < 180;
            bool activeWork =
                context.State.orders.Any(order => order.status == OrderStatus.InProgress)
                || context.State.deliveries.Any(delivery =>
                    delivery.status == DeliveryStatus.Travelling
                )
                || context.State.rescueMissions.Any(mission => !mission.completed);
            bool idle =
                availableOrders == 0
                && !activeWork
                && context.State.rovers.Any(rover =>
                    rover.status == RoverStatus.Ready || rover.status == RoverStatus.Charging
                );

            if (idle)
            {
                float maximumWait = restHours ? 20f : 8f;
                context.State.nextOrderSpawnMinute = Mathf.Min(
                    context.State.nextOrderSpawnMinute,
                    context.State.absoluteMinute + maximumWait
                );
            }

            if (
                availableOrders >= targetOrders
                || context.State.absoluteMinute < context.State.nextOrderSpawnMinute
                || restHours && !idle
            )
            {
                return;
            }

            GenerateOrder(stage == 0 || !HasRunnableOrder());

            float cooldown =
                stage == 0 ? 100f
                : stage == 1 ? 70f
                : 45f;
            context.State.nextOrderSpawnMinute =
                context.State.absoluteMinute + random.Range(cooldown * .8f, cooldown * 1.2f);

            if (availableOrders == 0)
            {
                context.Log("Поступил новый заказ.");
                context.Emit(GameCue.Notification);
            }
        }

        public void CleanupDynamicPoints()
        {
            context.State.dynamicPoints.RemoveAll(point =>
                !context.State.orders.Any(order =>
                    order.destinationId == point.id
                    && (
                        order.status == OrderStatus.Available
                        || order.status == OrderStatus.InProgress
                    )
                )
                && !context.State.deliveries.Any(delivery =>
                    (
                        delivery.status == DeliveryStatus.Travelling
                        || delivery.status == DeliveryStatus.Broken
                    ) && delivery.stops.Any(stop => stop.pointId == point.id)
                )
            );
        }

        private void GenerateOrder(bool guaranteedAccessible)
        {
            MapPoint destination = SelectDestination();
            int stage = ProgressionStage();
            float maximumCapacity =
                context.State.rovers.Count == 0
                    ? 85f
                    : context.State.rovers.Max(rover => rover.Stats.capacityKg);
            float maximumWeight =
                stage == 0 ? Mathf.Min(75f, maximumCapacity)
                : stage == 1 ? 115f
                : 165f;
            float weight = guaranteedAccessible
                ? random.Range(25f, Mathf.Max(31f, Mathf.Min(maximumWeight, maximumCapacity)))
                : random.Range(30f, maximumWeight);
            int reputation = random.Range(7, 17);
            float deadline =
                stage == 0 ? random.Range(900f, 1400f)
                : stage == 1 ? random.Range(600f, 1100f)
                : random.Range(420f, 900f);

            context.State.orders.Add(
                new Order
                {
                    id = Guid.NewGuid().ToString("N"),
                    title = OrderTitles[random.Range(0, OrderTitles.Length)],
                    destinationId = destination.id,
                    weightKg = weight,
                    reward = DeliveryReward(destination, weight),
                    reputation = reputation,
                    deadlineMinute = context.State.absoluteMinute + deadline,
                    cargoValue = Mathf.RoundToInt(weight * 3f),
                    status = OrderStatus.Available,
                }
            );
        }

        private MapPoint SelectDestination()
        {
            int stage = ProgressionStage();
            float randomPointChance =
                stage == 0 ? .08f
                : stage == 1 ? .24f
                : .4f;

            if (random.Value < randomPointChance)
            {
                MapPoint randomDestination = CreateRandomDestination();
                context.State.dynamicPoints.Add(randomDestination);
                return randomDestination;
            }

            List<MapPoint> destinations = GameCatalog
                .Points.Where(point => point.type == MapPointType.Destination)
                .ToList();
            var occupiedIds = new HashSet<string>(
                context
                    .State.orders.Where(order => order.status == OrderStatus.Available)
                    .Select(order => order.destinationId)
            );
            List<MapPoint> freeDestinations = destinations
                .Where(point => !occupiedIds.Contains(point.id))
                .ToList();
            List<MapPoint> candidates =
                freeDestinations.Count > 0 ? freeDestinations : destinations;

            return candidates[random.Range(0, candidates.Count)];
        }

        private MapPoint CreateRandomDestination()
        {
            Vector2 position = new Vector2(.5f, .5f);
            for (int attempt = 0; attempt < 12; attempt++)
            {
                position = new Vector2(random.Range(.08f, .92f), random.Range(.10f, .90f));

                bool farEnough = context
                    .AllMapPoints()
                    .All(point => Vector2.Distance(position, new Vector2(point.x, point.y)) > .08f);
                if (farEnough)
                    break;
            }

            return new MapPoint
            {
                id = "random_" + Guid.NewGuid().ToString("N"),
                name =
                    $"{PointPrefixes[random.Range(0, PointPrefixes.Length)]} "
                    + $"{PointNames[random.Range(0, PointNames.Length)]}-"
                    + $"{random.Range(2, 99):00}",
                x = position.x,
                y = position.y,
                type = MapPointType.Destination,
            };
        }

        private int DeliveryReward(MapPoint destination, float weightKg)
        {
            float distance = GameCatalog.Leg(GameCatalog.Base, destination).distance;
            return Mathf.RoundToInt(30f + distance * .5f + weightKg * .3f);
        }

        private bool HasRunnableOrder()
        {
            float maximumCapacity =
                context.State.rovers.Count == 0
                    ? 0f
                    : context.State.rovers.Max(rover => rover.Stats.capacityKg);

            return context.State.orders.Any(order =>
                order.status == OrderStatus.Available && order.weightKg <= maximumCapacity + .01f
            );
        }

        private int ProgressionStage()
        {
            if (context.State.completedOrders < 2)
                return 0;

            if (context.State.completedOrders < 6 || context.State.highestReputation < 35)
                return 1;

            return 2;
        }

        private float RestWindowMinutes()
        {
            int stage = ProgressionStage();
            return stage == 0 ? 75f
                : stage == 1 ? 45f
                : 25f;
        }
    }
}
