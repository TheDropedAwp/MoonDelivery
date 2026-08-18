using System.Collections.Generic;
using UnityEngine;

namespace MoonDelivery
{
    public static class GameCatalog
    {
        public static readonly MapPoint Base = PointData(
            "base",
            "База Артемида",
            .10f,
            .50f,
            MapPointType.Base
        );

        public static readonly List<MapPoint> Points = new List<MapPoint>
        {
            Base,
            PointData("dome", "Купол Тихо", .38f, .22f, MapPointType.Destination),
            PointData("mine", "Шахта Гелий-3", .78f, .23f, MapPointType.Destination),
            PointData("lab", "Лаборатория Кеплер", .86f, .70f, MapPointType.Destination),
            PointData("relay", "Ретранслятор", .45f, .82f, MapPointType.Destination),
            PointData("selene", "База Селена", .20f, .18f, MapPointType.Destination),
            PointData("copernicus", "Обсерватория Коперник", .68f, .84f, MapPointType.Destination),
            PointData("horizon", "Депо Горизонт", .91f, .43f, MapPointType.Destination),
            PointData("station_north", "Станция Север", .58f, .43f, MapPointType.ChargingStation),
            PointData("station_south", "Станция Юг", .38f, .62f, MapPointType.ChargingStation),
        };

        public static readonly List<TerrainZone> TerrainZones = new List<TerrainZone>
        {
            Zone("rocks_west", "Скалистая гряда", TerrainType.Rocks, .24f, .30f, .063f, .054f),
            Zone("crater_north", "Поле кратеров", TerrainType.Crater, .62f, .22f, .077f, .059f),
            Zone("rough_center", "Разломы", TerrainType.Rough, .50f, .52f, .072f, .063f),
            Zone("rocks_east", "Хребет Кеплера", TerrainType.Rocks, .82f, .60f, .059f, .081f),
            Zone("crater_south", "Кратеры Эхо", TerrainType.Crater, .43f, .80f, .081f, .054f),
        };

        private static MapPoint PointData(
            string id,
            string name,
            float x,
            float y,
            MapPointType type
        ) =>
            new MapPoint
            {
                id = id,
                name = name,
                x = x,
                y = y,
                type = type,
            };

        private static TerrainZone Zone(
            string id,
            string name,
            TerrainType terrain,
            float x,
            float y,
            float radiusX,
            float radiusY
        ) =>
            new TerrainZone
            {
                id = id,
                name = name,
                terrain = terrain,
                x = x,
                y = y,
                radiusX = radiusX,
                radiusY = radiusY,
            };

        public static RoverStats GetStats(Rover rover)
        {
            RoverStats stats = BaseStats(rover.type);
            stats.capacityKg *= 1f + Mathf.Max(0, rover.capacityLevel - 1) * .1f;
            stats.maxBattery *= 1f + Mathf.Max(0, rover.batteryLevel - 1) * .1f;
            stats.speed *= 1f + Mathf.Max(0, rover.speedLevel - 1) * .065f;
            stats.energyUseMultiplier = Mathf.Pow(.92f, Mathf.Max(0, rover.efficiencyLevel - 1));
            return stats;
        }

        public static RoverStats GetStats(RoverType type, int level)
        {
            RoverStats s = BaseStats(type);
            float factor = 1f + Mathf.Max(0, level - 1) * .1f;
            s.capacityKg *= factor;
            s.maxBattery *= factor;
            s.speed *= 1f + Mathf.Max(0, level - 1) * .06f;
            s.breakdownRisk *= Mathf.Pow(.88f, Mathf.Max(0, level - 1));
            return s;
        }

        private static RoverStats BaseStats(RoverType type)
        {
            RoverStats s;
            switch (type)
            {
                case RoverType.Fast:
                    s = NewStats(75, 70, 25, .07f);
                    break;
                case RoverType.Heavy:
                    s = NewStats(230, 150, 7, .10f);
                    s.cannotCrossRocks = true;
                    break;
                case RoverType.Offroad:
                    s = NewStats(90, 90, 15, .03f);
                    break;
                case RoverType.Solar:
                    s = NewStats(120, 130, 18, .05f);
                    s.solarPowered = true;
                    break;
                default:
                    s = NewStats(100, 100, 10, .05f);
                    break;
            }
            return s;
        }

        private static RoverStats NewStats(
            float capacity,
            float battery,
            float speed,
            float risk
        ) =>
            new RoverStats
            {
                capacityKg = capacity,
                maxBattery = battery,
                speed = speed,
                breakdownRisk = risk,
            };

        public static MapPoint Point(string id) => Points.Find(x => x.id == id);

        // Выбранные соседние точки определяют рельеф. Объезд через станцию может
        // заменить прямой скалистый путь на более длинную безопасную дорогу.
        public static RouteSegment Leg(string fromId, string toId)
        {
            return Leg(Point(fromId) ?? Base, Point(toId) ?? Base);
        }

        public static RouteSegment Leg(MapPoint from, MapPoint to)
        {
            string fromId = from.id;
            string toId = to.id;
            float distance = Mathf.Max(
                1f,
                Vector2.Distance(new Vector2(from.x, from.y), new Vector2(to.x, to.y)) * 92f
            );
            TerrainType terrain = TerrainOnSegment(from, to);
            return new RouteSegment
            {
                fromPointId = fromId,
                toPointId = toId,
                terrain = terrain,
                distance = distance,
            };
        }

        private static TerrainType TerrainOnSegment(MapPoint from, MapPoint to)
        {
            TerrainType result = TerrainType.Flat;
            const int samples = 64;
            for (int sample = 1; sample < samples; sample++)
            {
                float t = sample / (float)samples;
                TerrainType sampled = TerrainAt(from, to, t);
                if ((int)sampled > (int)result)
                    result = sampled;
            }
            return result;
        }

        public static TerrainType TerrainAt(MapPoint from, MapPoint to, float progress)
        {
            return TerrainAt(
                Mathf.Lerp(from.x, to.x, Mathf.Clamp01(progress)),
                Mathf.Lerp(from.y, to.y, Mathf.Clamp01(progress))
            );
        }

        public static TerrainType TerrainAt(float x, float y)
        {
            TerrainType result = TerrainType.Flat;
            foreach (TerrainZone zone in TerrainZones)
            {
                float dx = (x - zone.x) / zone.radiusX;
                float dy = (y - zone.y) / zone.radiusY;
                if (InsideZone(zone, dx, dy) && (int)zone.terrain > (int)result)
                    result = zone.terrain;
            }
            return result;
        }

        private static bool InsideZone(TerrainZone zone, float dx, float dy)
        {
            float angle = Mathf.Atan2(dy, dx);
            return Mathf.Sqrt(dx * dx + dy * dy) <= ZoneBoundaryRadius(zone, angle);
        }

        public static float ZoneBoundaryRadius(TerrainZone zone, float angle)
        {
            int seed = StableHash(zone.id);
            float phase = (seed & 1023) * .017f;
            return
                .82f
                + Mathf.Sin(angle * 3f + phase) * .075f
                + Mathf.Sin(angle * 5f - phase * .63f) * .045f
                + Mathf.Sin(angle * 9f + phase * .31f) * .025f;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                foreach (char character in value)
                    hash = hash * 31 + character;
                return hash & 0x7fffffff;
            }
        }

        public static float TerrainSpeed(TerrainType terrain, RoverType rover)
        {
            if (rover == RoverType.Offroad)
                return 1f;
            switch (terrain)
            {
                case TerrainType.Rough:
                    return .62f;
                case TerrainType.Crater:
                    return .42f;
                case TerrainType.Rocks:
                    return .28f;
                default:
                    return 1f;
            }
        }

        public static float TerrainRisk(TerrainType terrain, RoverType rover)
        {
            float multiplier;
            switch (terrain)
            {
                case TerrainType.Rough:
                    multiplier = 1.15f;
                    break;
                case TerrainType.Crater:
                    multiplier = 1.4f;
                    break;
                case TerrainType.Rocks:
                    multiplier = 1.65f;
                    break;
                default:
                    multiplier = .7f;
                    break;
            }
            if (rover == RoverType.Offroad)
            {
                if (terrain == TerrainType.Rocks)
                    multiplier *= .12f;
                else if (terrain == TerrainType.Crater)
                    multiplier *= .28f;
                else if (terrain == TerrainType.Rough)
                    multiplier *= .45f;
            }
            if (rover == RoverType.Heavy)
                multiplier *= .75f;
            return multiplier;
        }

        public static string RoverName(RoverType type)
        {
            switch (type)
            {
                case RoverType.Fast:
                    return "Стриж";
                case RoverType.Heavy:
                    return "Титан";
                case RoverType.Offroad:
                    return "Следопыт";
                case RoverType.Solar:
                    return "Гелиос";
                default:
                    return "Курьер";
            }
        }

        public static int UnlockReputation(RoverType type)
        {
            switch (type)
            {
                case RoverType.Fast:
                    return 75;
                case RoverType.Heavy:
                    return 200;
                case RoverType.Offroad:
                    return 400;
                case RoverType.Solar:
                    return 800;
                default:
                    return 0;
            }
        }

        public static int RoverCost(RoverType type)
        {
            switch (type)
            {
                case RoverType.Fast:
                    return 450;
                case RoverType.Heavy:
                    return 600;
                case RoverType.Offroad:
                    return 500;
                case RoverType.Solar:
                    return 1500;
                default:
                    return 300;
            }
        }

        public static string RoverDescription(RoverType type)
        {
            switch (type)
            {
                case RoverType.Fast:
                    return "Очень быстрый ровер для коротких заказов, но с малым запасом батареи и повышенным риском.";
                case RoverType.Heavy:
                    return "Большая вместительность: уверенно берёт два и больше заказов. Медленный и не проходит скалы.";
                case RoverType.Offroad:
                    return "Не теряет скорость в неровностях, кратерах и скалах; уверенно держится на сложной поверхности.";
                case RoverType.Solar:
                    return "Эталонный ровер. На освещённых участках не расходует заряд солнечной батареи.";
                default:
                    return "Стандартный ровер — уверенный универсальный выбор без лишних особенностей.";
            }
        }
    }
}
