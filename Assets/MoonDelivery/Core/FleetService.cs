using System;
using System.Linq;
using UnityEngine;

namespace MoonDelivery
{
    internal sealed class FleetService
    {
        public const float BaseChargeDurationMinutes = 60f;

        private readonly IGameContext context;

        public FleetService(IGameContext context)
        {
            this.context = context;
        }

        public Rover AddRover(RoverType type, int purchaseCost)
        {
            RoverStats stats = GameCatalog.GetStats(type, 1);
            var rover = new Rover
            {
                id = Guid.NewGuid().ToString("N"),
                displayName = GameCatalog.RoverName(type),
                type = type,
                status = RoverStatus.Ready,
                battery = stats.maxBattery,
                purchaseCost = purchaseCost,
            };

            context.State.rovers.Add(rover);
            return rover;
        }

        public void ProcessCharging(float minutes)
        {
            foreach (
                Rover rover in context.State.rovers.Where(item =>
                    item.status == RoverStatus.Charging
                )
            )
            {
                rover.chargeRemainingMinutes -= minutes;
                rover.battery = Mathf.Min(
                    rover.Stats.maxBattery,
                    rover.battery + rover.Stats.maxBattery * minutes / BaseChargeDurationMinutes
                );

                if (
                    rover.chargeRemainingMinutes > 0f
                    && rover.battery < rover.Stats.maxBattery - .01f
                )
                {
                    continue;
                }

                rover.battery = rover.Stats.maxBattery;
                rover.status = RoverStatus.Ready;
                context.Log($"{rover.displayName} зарядился и готов.");
                context.Emit(GameCue.Notification);
            }
        }

        public void StartBaseCharge(Rover rover)
        {
            if (
                rover == null
                || rover.status != RoverStatus.Ready
                || rover.battery >= rover.Stats.maxBattery - .01f
            )
            {
                return;
            }

            float missingPart = 1f - rover.battery / rover.Stats.maxBattery;
            rover.status = RoverStatus.Charging;
            rover.chargeRemainingMinutes = Mathf.Max(1f, BaseChargeDurationMinutes * missingPart);

            context.Log(
                $"{rover.displayName} поставлен на зарядку "
                    + $"({rover.chargeRemainingMinutes:0} мин.)."
            );
            context.Save();
        }

        public bool IsUnlocked(RoverType type)
        {
            return context.State.highestReputation >= GameCatalog.UnlockReputation(type);
        }

        public bool Purchase(RoverType type, out string message)
        {
            int requiredReputation = GameCatalog.UnlockReputation(type);
            int cost = GameCatalog.RoverCost(type);

            if (!IsUnlocked(type))
            {
                message = $"Нужно достичь {requiredReputation} репутации";
                return false;
            }

            if (context.State.money < cost)
            {
                message = $"Не хватает {cost - context.State.money} кредитов";
                return false;
            }

            context.State.money -= cost;
            AddRover(type, cost);
            message = $"Куплен ровер «{GameCatalog.RoverName(type)}» за {cost} кредитов.";

            context.Log(message);
            context.Emit(GameCue.Confirm);
            context.Save();
            return true;
        }

        public int UpgradeLevel(Rover rover, RoverUpgradeType type)
        {
            if (rover == null)
                return 0;

            switch (type)
            {
                case RoverUpgradeType.Speed:
                    return rover.speedLevel;
                case RoverUpgradeType.Capacity:
                    return rover.capacityLevel;
                case RoverUpgradeType.Battery:
                    return rover.batteryLevel;
                default:
                    return rover.efficiencyLevel;
            }
        }

        public int UpgradeCost(Rover rover, RoverUpgradeType type)
        {
            return 55 + UpgradeLevel(rover, type) * 65;
        }

        public bool Upgrade(Rover rover, RoverUpgradeType type)
        {
            if (rover == null)
                return false;

            int currentLevel = UpgradeLevel(rover, type);
            int cost = UpgradeCost(rover, type);
            bool canUpgrade =
                rover.status == RoverStatus.Ready || rover.status == RoverStatus.Charging;

            if (!canUpgrade || currentLevel >= 10 || context.State.money < cost)
                return false;

            float previousMaximumBattery = rover.Stats.maxBattery;
            context.State.money -= cost;
            rover.upgradeSpent += cost;

            ApplyUpgrade(rover, type);
            rover.level = Mathf.Max(
                rover.speedLevel,
                rover.capacityLevel,
                rover.batteryLevel,
                rover.efficiencyLevel
            );

            if (type == RoverUpgradeType.Battery)
                rover.battery += rover.Stats.maxBattery - previousMaximumBattery;

            context.Log(
                $"{rover.displayName}: улучшение «{UpgradeName(type)}» "
                    + $"до уровня {currentLevel + 1}."
            );
            context.Emit(GameCue.Upgrade);
            context.Save();
            return true;
        }

        public bool CanRepair(Rover rover)
        {
            if (rover == null || rover.status != RoverStatus.Broken)
                return false;

            return !context.State.deliveries.Any(delivery =>
                delivery.roverId == rover.id && delivery.status == DeliveryStatus.Broken
            );
        }

        public int RepairCost(Rover rover)
        {
            return rover == null
                ? 0
                : Mathf.CeilToInt(rover.purchaseCost * .4f + rover.upgradeSpent * .5f);
        }

        public bool Repair(Rover rover)
        {
            if (!CanRepair(rover))
                return false;

            int cost = RepairCost(rover);
            if (context.State.money < cost)
            {
                context.Emit(GameCue.Error);
                return false;
            }

            context.State.money -= cost;
            rover.status = RoverStatus.Ready;
            rover.battery = rover.Stats.maxBattery;

            context.Log($"{rover.displayName} полностью отремонтирован (−{cost} кр.).");
            context.Emit(GameCue.Confirm);
            context.Save();
            return true;
        }

        public void MigrateRover(Rover rover)
        {
            rover.purchaseCost = GameCatalog.RoverCost(rover.type);
            rover.speedLevel = Mathf.Max(1, rover.speedLevel);
            rover.capacityLevel = Mathf.Max(1, rover.capacityLevel);
            rover.batteryLevel = Mathf.Max(1, rover.batteryLevel);
            rover.efficiencyLevel = Mathf.Max(1, rover.efficiencyLevel);

            if (rover.upgradeSpent <= 0)
                rover.upgradeSpent = EstimateUpgradeSpend(rover);
        }

        public static string UpgradeName(RoverUpgradeType type)
        {
            switch (type)
            {
                case RoverUpgradeType.Speed:
                    return "Скорость";
                case RoverUpgradeType.Capacity:
                    return "Грузовместимость";
                case RoverUpgradeType.Battery:
                    return "Батарея";
                default:
                    return "Энергоэффективность";
            }
        }

        private static void ApplyUpgrade(Rover rover, RoverUpgradeType type)
        {
            switch (type)
            {
                case RoverUpgradeType.Speed:
                    rover.speedLevel++;
                    break;
                case RoverUpgradeType.Capacity:
                    rover.capacityLevel++;
                    break;
                case RoverUpgradeType.Battery:
                    rover.batteryLevel++;
                    break;
                default:
                    rover.efficiencyLevel++;
                    break;
            }
        }

        private static int EstimateUpgradeSpend(Rover rover)
        {
            int result = 0;
            int[] levels =
            {
                rover.speedLevel,
                rover.capacityLevel,
                rover.batteryLevel,
                rover.efficiencyLevel,
            };

            foreach (int level in levels)
            {
                for (int current = 1; current < level; current++)
                    result += 55 + current * 65;
            }

            return result;
        }
    }
}
