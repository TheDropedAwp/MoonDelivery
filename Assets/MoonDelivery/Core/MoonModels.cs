using System;
using System.Collections.Generic;

namespace MoonDelivery
{
    public enum RoverType
    {
        Fast,
        Standard,
        Heavy,
        Offroad,
        Solar,
    }

    public enum RoverStatus
    {
        Ready,
        Delivering,
        Broken,
        Charging,
    }

    public enum OrderStatus
    {
        Available,
        InProgress,
        Completed,
        Failed,
        Declined,
    }

    public enum TerrainType
    {
        Flat,
        Rough,
        Crater,
        Rocks,
    }

    public enum DeliveryStatus
    {
        Travelling,
        Broken,
        Completed,
        Failed,
    }

    public enum MapPointType
    {
        Base,
        Destination,
        ChargingStation,
    }

    public enum RoverUpgradeType
    {
        Speed,
        Capacity,
        Battery,
        Efficiency,
    }

    public enum GameCue
    {
        Click,
        Confirm,
        Notification,
        Success,
        Error,
        Breakdown,
        Rescue,
        Upgrade,
    }

    [Serializable]
    public class Rover
    {
        public string id;
        public string displayName;
        public RoverType type;
        public RoverStatus status;
        public float battery;
        public int level = 1;
        public int speedLevel = 1;
        public int capacityLevel = 1;
        public int batteryLevel = 1;
        public int efficiencyLevel = 1;
        public float chargeRemainingMinutes;
        public int purchaseCost;
        public int upgradeSpent;
        public RoverStats Stats => GameCatalog.GetStats(this);
    }

    [Serializable]
    public class RoverStats
    {
        public float capacityKg;
        public float maxBattery;
        public float speed;

        // Базовая вероятность для единственного броска по самому опасному участку маршрута.
        public float breakdownRisk;
        public float energyUseMultiplier = 1f;
        public bool cannotCrossRocks;
        public bool solarPowered;
    }

    [Serializable]
    public class Order
    {
        public string id;
        public string title;
        public string destinationId;
        public float weightKg;
        public int reward;
        public int reputation;
        public float deadlineMinute;
        public int cargoValue;
        public OrderStatus status;
    }

    [Serializable]
    public class RouteSegment
    {
        public string fromPointId;
        public string toPointId;
        public TerrainType terrain;
        public float distance;
    }

    [Serializable]
    public class MapPoint
    {
        public string id;
        public string name;
        public float x;
        public float y;
        public MapPointType type;
    }

    [Serializable]
    public class TerrainZone
    {
        public string id;
        public string name;
        public TerrainType terrain;
        public float x;
        public float y;
        public float radiusX;
        public float radiusY;
    }

    [Serializable]
    public class RouteStop
    {
        public string pointId;
        public string orderId;

        public RouteStop Clone() => new RouteStop { pointId = pointId, orderId = orderId };
    }

    [Serializable]
    public class Delivery
    {
        public string id;
        public string roverId;
        public DeliveryStatus status;
        public List<RouteStop> stops = new List<RouteStop>();
        public int currentStopIndex;
        public string currentPointId = "base";
        public float legProgress;
        public float startedMinute;
        public float stationRemainingMinutes;
        public float cargoWeight;

        // Результат единственного броска риска фиксируется при старте маршрута.
        public float rolledBreakdownChance;
        public bool breakdownScheduled;
        public int breakdownStopIndex = -1;
        public float breakdownAtDistance;
        public string cargoPickupPointId;
        public string cargoSourceDeliveryId;
        public float pendingPickupWeight;
        public bool cargoRecoveryDispatched;
        public bool cargoTransferred;
        public int prepaidStationCredits;
    }

    [Serializable]
    public class RescueMission
    {
        public string id;
        public string deliveryId;
        public float targetX;
        public float targetY;
        public int phase;
        public float progress;
        public float distance;
        public bool completed;
    }

    public class RouteForecast
    {
        public float distance;
        public float durationMinutes;
        public float energy;
        public float breakdownChance;
        public int stationCost;
        public bool missesDeadline;
        public string summary;
    }

    [Serializable]
    public class GameEvent
    {
        public float minute;
        public string text;

        public GameEvent(float atMinute, string message)
        {
            minute = atMinute;
            text = message;
        }
    }

    [Serializable]
    public class GameState
    {
        public int saveVersion = 2;
        public int money = 700;
        public int reputation;
        public int highestReputation;
        public float absoluteMinute = 480f;
        public float nextOrderSpawnMinute;
        public int completedOrders;
        public bool gameOver;
        public List<Rover> rovers = new List<Rover>();
        public List<Order> orders = new List<Order>();
        public List<MapPoint> dynamicPoints = new List<MapPoint>();
        public List<Delivery> deliveries = new List<Delivery>();
        public List<RescueMission> rescueMissions = new List<RescueMission>();
        public List<GameEvent> events = new List<GameEvent>();
    }
}
