using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MoonDelivery
{
    public sealed class MoonGame : IGameContext
    {
        public const float StationCreditPerBatteryUnit =
            RoutePlanningService.StationCreditPerBatteryUnit;

        public const float StationDurationMinutes = RoutePlanningService.StationDurationMinutes;

        public const float BaseChargeDurationMinutes = FleetService.BaseChargeDurationMinutes;

        private readonly IGameStorage storage;
        private readonly RoutePlanningService routes;
        private readonly FleetService fleet;
        private readonly OrderService orders;
        private readonly DeliveryService deliveries;
        private readonly RescueService rescues;
        private readonly CargoRecoveryService cargoRecovery;

        public GameState State { get; private set; }
        public int Day => Mathf.FloorToInt(State.absoluteMinute / 1440f) + 1;
        public int MinuteOfDay => Mathf.FloorToInt(State.absoluteMinute) % 1440;

        public event Action<GameCue> OnCue;

        public MoonGame(bool fresh = false)
            : this(new JsonGameStorage(), new UnityRandomSource(), fresh) { }

        internal MoonGame(IGameStorage storage, IRandomSource random, bool fresh = false)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            routes = new RoutePlanningService(this, random);
            fleet = new FleetService(this);
            orders = new OrderService(this, random);
            deliveries = new DeliveryService(this, routes, orders);
            rescues = new RescueService(this, deliveries, orders);
            cargoRecovery = new CargoRecoveryService(this, routes, deliveries);

            State = fresh ? null : storage.Load();
            if (State == null || State.saveVersion < 2)
                CreateNewGame();
            else
                MigrateLoadedState();
        }

        public void CreateNewGame()
        {
            State = new GameState();
            fleet.AddRover(RoverType.Standard, GameCatalog.RoverCost(RoverType.Standard));
            fleet.AddRover(RoverType.Standard, GameCatalog.RoverCost(RoverType.Standard));
            orders.InitializeNewGame();
            Log("Компания зарегистрирована. Соберите маршрут из заказов и зарядных станций.");
            Save();
        }

        public string ValidatePlan(IList<RouteStop> stops, Rover rover)
        {
            return routes.Validate(stops, rover);
        }

        public bool StartDelivery(IList<RouteStop> stops, Rover rover, out string message)
        {
            return deliveries.Start(stops, rover, out message);
        }

        public void Tick(float gameMinutes)
        {
            if (State.gameOver || gameMinutes <= 0f)
                return;

            int previousDay = Day;
            State.absoluteMinute += gameMinutes;

            if (Day > previousDay)
                Log($"Начался день {Day}.");

            fleet.ProcessCharging(gameMinutes);
            deliveries.ProcessDeliveries(gameMinutes);
            rescues.ProcessRescueMissions(gameMinutes);
            orders.FailExpiredOrders();
            State.highestReputation = Mathf.Max(State.highestReputation, State.reputation);
            orders.MaintainOrders();
            orders.CleanupDynamicPoints();
            CheckGameOver();
        }

        public void Decline(Order order)
        {
            orders.Decline(order);
        }

        public int RescueCost(Delivery delivery)
        {
            return rescues.RescueCost(delivery);
        }

        public void Rescue(Delivery delivery)
        {
            rescues.Rescue(delivery);
        }

        public Vector2 DeliveryPosition(Delivery delivery)
        {
            return deliveries.Position(delivery);
        }

        public void StartBaseCharge(Rover rover)
        {
            fleet.StartBaseCharge(rover);
        }

        public int StationChargeCost(Rover rover, float battery)
        {
            return routes.StationChargeCost(rover, battery);
        }

        public int UpgradeLevel(Rover rover, RoverUpgradeType type)
        {
            return fleet.UpgradeLevel(rover, type);
        }

        public int UpgradeCost(Rover rover, RoverUpgradeType type)
        {
            return fleet.UpgradeCost(rover, type);
        }

        public bool Upgrade(Rover rover, RoverUpgradeType type)
        {
            return fleet.Upgrade(rover, type);
        }

        public bool CanRepair(Rover rover)
        {
            return fleet.CanRepair(rover);
        }

        public int RepairCost(Rover rover)
        {
            return fleet.RepairCost(rover);
        }

        public bool Repair(Rover rover)
        {
            return fleet.Repair(rover);
        }

        public static string UpgradeName(RoverUpgradeType type)
        {
            return FleetService.UpgradeName(type);
        }

        public RouteForecast Forecast(IList<RouteStop> stops, Rover rover)
        {
            return routes.Forecast(stops, rover);
        }

        public float RecoverableCargoWeight(Delivery source)
        {
            return deliveries.RecoverableCargoWeight(source);
        }

        public string ValidateCargoRecovery(
            Delivery source,
            Rover rover,
            out RouteForecast forecast
        )
        {
            return cargoRecovery.ValidateCargoRecovery(source, rover, out forecast);
        }

        public bool DispatchCargoRecovery(Delivery source, Rover rover, out string message)
        {
            return cargoRecovery.DispatchCargoRecovery(source, rover, out message);
        }

        public bool IsRoverUnlocked(RoverType type)
        {
            return fleet.IsUnlocked(type);
        }

        public bool PurchaseRover(RoverType type, out string message)
        {
            return fleet.Purchase(type, out message);
        }

        public MapPoint Point(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            return GameCatalog.Point(id) ?? State.dynamicPoints.Find(point => point.id == id);
        }

        public List<MapPoint> AllMapPoints()
        {
            return GameCatalog.Points.Concat(State.dynamicPoints).ToList();
        }

        public RouteSegment Leg(string fromId, string toId)
        {
            MapPoint from = Point(fromId) ?? GameCatalog.Base;
            MapPoint to = Point(toId) ?? GameCatalog.Base;
            return GameCatalog.Leg(from, to);
        }

        public Rover Rover(string id)
        {
            return string.IsNullOrEmpty(id) ? null : State.rovers.Find(rover => rover.id == id);
        }

        public Order Order(string id)
        {
            return string.IsNullOrEmpty(id) ? null : State.orders.Find(order => order.id == id);
        }

        public bool IsDaylight()
        {
            return IsSunlitAt(GameCatalog.Base.x, State.absoluteMinute);
        }

        public static float SunCenter(float absoluteMinute)
        {
            return LunarCycle.SunCenter(absoluteMinute);
        }

        public static bool IsSunlitAt(float normalizedX, float absoluteMinute)
        {
            return LunarCycle.IsSunlit(normalizedX, absoluteMinute);
        }

        public int MinutesLeft(Order order)
        {
            return Mathf.CeilToInt(order.deadlineMinute - State.absoluteMinute);
        }

        public void Save()
        {
            storage.Save(State);
        }

        public static string TerrainName(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Rough:
                    return "Неровности";
                case TerrainType.Crater:
                    return "Кратер";
                case TerrainType.Rocks:
                    return "Скалы";
                default:
                    return "Равнина";
            }
        }

        private void MigrateLoadedState()
        {
            State.rovers = State.rovers ?? new List<Rover>();
            State.orders = State.orders ?? new List<Order>();
            State.dynamicPoints = State.dynamicPoints ?? new List<MapPoint>();
            State.deliveries = State.deliveries ?? new List<Delivery>();
            State.rescueMissions = State.rescueMissions ?? new List<RescueMission>();
            State.events = State.events ?? new List<GameEvent>();
            State.highestReputation = Mathf.Max(State.highestReputation, State.reputation);

            foreach (Rover rover in State.rovers)
                fleet.MigrateRover(rover);

            orders.MigrateActiveOrders();
        }

        private void CheckGameOver()
        {
            if (State.reputation >= -100 || State.gameOver)
                return;

            State.gameOver = true;
            Log("Репутация упала ниже −100. Компания потеряла лицензию.");
        }

        private void Log(string message)
        {
            State.events.Insert(0, new GameEvent(State.absoluteMinute, message));
            if (State.events.Count > 30)
                State.events.RemoveAt(State.events.Count - 1);
        }

        private void Emit(GameCue cue)
        {
            OnCue?.Invoke(cue);
        }

        void IGameContext.Log(string message)
        {
            Log(message);
        }

        void IGameContext.Emit(GameCue cue)
        {
            Emit(cue);
        }

        void IGameContext.Save()
        {
            Save();
        }

        void IGameContext.CheckGameOver()
        {
            CheckGameOver();
        }
    }
}
