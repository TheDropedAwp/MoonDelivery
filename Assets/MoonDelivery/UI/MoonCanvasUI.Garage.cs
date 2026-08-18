using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoonDelivery
{
    public sealed partial class MoonCanvasUI
    {
        private void RebuildGarage()
        {
            ClearChildren(ownedContent);
            foreach (Rover rover in game.State.rovers)
            {
                Rover captured = rover;
                Button b = Card(
                    ownedContent,
                    116,
                    garageRoverId == rover.id
                        ? new Color(.12f, .38f, .52f, .96f)
                        : new Color(.07f, .11f, .16f, .96f),
                    () =>
                    {
                        garageRoverId = captured.id;
                        RebuildGarage();
                    }
                );
                b.gameObject.name = "Garage Rover " + rover.id;
                Texture2D preview = visuals?.Preview(rover.type);
                if (preview != null)
                    ImageBox(preview, b.transform, new Vector2(8, 8), new Vector2(100, -8));
                TMP_Text status = Label(
                    GarageRoverText(rover),
                    b.transform,
                    14,
                    FontStyle.Bold,
                    TextAnchor.MiddleLeft,
                    new Vector2(108, 8),
                    new Vector2(-8, -8),
                    Color.white
                );
                status.gameObject.name = "Garage Status";
            }
            if (string.IsNullOrEmpty(garageRoverId))
                garageRoverId = game.State.rovers.FirstOrDefault()?.id;
            BuildGarageDetails(game.Rover(garageRoverId));
            ClearChildren(shopContent);
            foreach (
                RoverType type in new[]
                {
                    RoverType.Standard,
                    RoverType.Fast,
                    RoverType.Heavy,
                    RoverType.Offroad,
                    RoverType.Solar,
                }
            )
                BuildShopCard(type);
        }

        private string GarageRoverText(Rover rover) =>
            $"{rover.displayName}\n{StatusName(rover.status)}\nГруз {rover.Stats.capacityKg:0} кг — {rover.battery:0}/{rover.Stats.maxBattery:0}";

        private string GarageDetailsText(Rover rover) =>
            $"Скорость {rover.Stats.speed:0.#} — Груз {rover.Stats.capacityKg:0} кг\nБатарея {rover.battery:0}/{rover.Stats.maxBattery:0} — Расход ×{rover.Stats.energyUseMultiplier:0.00}";

        private void UpdateGarageTexts()
        {
            if (ownedContent == null)
                return;
            foreach (Rover rover in game.State.rovers)
            {
                Transform card = ownedContent.Find("Garage Rover " + rover.id);
                Transform status = card != null ? card.Find("Garage Status") : null;
                if (status != null)
                    status.GetComponent<TMP_Text>().text = GarageRoverText(rover);
            }
            Rover selected = game.Rover(garageRoverId);
            Transform details =
                garageDetails != null ? garageDetails.Find("Garage Details Stats") : null;
            if (selected != null && details != null)
                details.GetComponent<TMP_Text>().text = GarageDetailsText(selected);
        }

        private void BuildGarageDetails(Rover rover)
        {
            ClearChildren(garageDetails);
            if (rover == null)
                return;
            Texture2D preview = visuals?.Preview(rover.type);
            if (preview != null)
                ImageBox(
                    preview,
                    garageDetails,
                    new Vector2(20, -245),
                    new Vector2(-20, -16),
                    true,
                    true
                );
            Label(
                $"{rover.displayName} — {StatusName(rover.status)}",
                garageDetails,
                18,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(18, -284),
                new Vector2(-18, -252),
                new Color(.45f, .85f, 1)
            );
            TMP_Text stats = Label(
                GarageDetailsText(rover),
                garageDetails,
                13,
                FontStyle.Normal,
                TextAnchor.UpperLeft,
                new Vector2(18, -330),
                new Vector2(-18, -286),
                Color.white
            );
            stats.gameObject.name = "Garage Details Stats";
            float y = -370;
            foreach (
                RoverUpgradeType type in new[]
                {
                    RoverUpgradeType.Speed,
                    RoverUpgradeType.Capacity,
                    RoverUpgradeType.Battery,
                    RoverUpgradeType.Efficiency,
                }
            )
            {
                RoverUpgradeType captured = type;
                int level = game.UpgradeLevel(rover, type),
                    cost = game.UpgradeCost(rover, type);
                Label(
                    $"{MoonGame.UpgradeName(type)} {level}/10",
                    garageDetails,
                    13,
                    FontStyle.Normal,
                    TextAnchor.MiddleLeft,
                    new Vector2(18, y - 30),
                    new Vector2(-150, y),
                    Color.white
                );
                Button b = Btn(
                    level >= 10 ? "MAX" : $"+ {cost} кр.",
                    garageDetails,
                    new Vector2(1, 1),
                    new Vector2(1, 1),
                    new Vector2(-138, y - 29),
                    new Vector2(-18, y),
                    () =>
                    {
                        game.Upgrade(rover, captured);
                        RebuildGarage();
                    }
                );
                b.interactable =
                    (rover.status == RoverStatus.Ready || rover.status == RoverStatus.Charging)
                    && level < 10
                    && game.State.money >= cost;
                y -= 36;
            }
            if (game.CanRepair(rover))
            {
                int cost = game.RepairCost(rover);
                Button b = Btn(
                    $"Отремонтировать — {cost} кр.",
                    garageDetails,
                    new Vector2(0, 1),
                    new Vector2(1, 1),
                    new Vector2(18, y - 38),
                    new Vector2(-18, y),
                    () =>
                    {
                        game.Repair(rover);
                        RebuildGarage();
                    }
                );
                b.interactable = game.State.money >= cost;
            }
            else if (rover.status == RoverStatus.Ready)
            {
                Button b = Btn(
                    "Зарядить на базе — бесплатно",
                    garageDetails,
                    new Vector2(0, 1),
                    new Vector2(1, 1),
                    new Vector2(18, y - 38),
                    new Vector2(-18, y),
                    () =>
                    {
                        game.StartBaseCharge(rover);
                        RebuildGarage();
                    }
                );
                b.interactable = rover.battery < rover.Stats.maxBattery - .01f;
            }
        }

        private void BuildShopCard(RoverType type)
        {
            RoverStats stats = GameCatalog.GetStats(type, 1);
            int cost = GameCatalog.RoverCost(type),
                unlock = GameCatalog.UnlockReputation(type);
            bool unlocked = game.IsRoverUnlocked(type);
            Button card = Card(shopContent, 194, new Color(.07f, .11f, .16f, .96f), null);
            Texture2D preview = visuals?.Preview(type);
            if (preview != null)
                ImageBox(preview, card.transform, new Vector2(8, -108), new Vector2(104, -8), true);
            Label(
                GameCatalog.RoverName(type),
                card.transform,
                18,
                FontStyle.Bold,
                TextAnchor.UpperLeft,
                new Vector2(112, -34),
                new Vector2(-10, -7),
                new Color(.45f, .85f, 1)
            );
            Label(
                $"Груз {stats.capacityKg:0} кг — Батарея {stats.maxBattery:0}\nСкорость {stats.speed:0.#} — Риск {stats.breakdownRisk * 100:0.#}%\n{(unlocked ? $"Цена: {cost} кр." : $"Нужно {unlock} репутации")}",
                card.transform,
                14,
                FontStyle.Normal,
                TextAnchor.UpperLeft,
                new Vector2(112, -108),
                new Vector2(-10, -38),
                Color.white
            );
            Label(
                GameCatalog.RoverDescription(type),
                card.transform,
                13,
                FontStyle.Normal,
                TextAnchor.UpperLeft,
                new Vector2(10, 8),
                new Vector2(-140, -116),
                new Color(.76f, .83f, .9f)
            );
            Button buy = Btn(
                "Купить",
                card.transform,
                new Vector2(1, 0),
                new Vector2(1, 0),
                new Vector2(-128, 14),
                new Vector2(-10, 54),
                () =>
                {
                    if (game.PurchaseRover(type, out _))
                        RebuildGarage();
                }
            );
            buy.interactable = unlocked && game.State.money >= cost;
        }

        private void RebuildJournal()
        {
            if (journalContent == null)
                return;
            ClearChildren(journalContent);
            foreach (GameEvent item in game.State.events)
            {
                int m = Mathf.FloorToInt(item.minute) % 1440;
                RectTransform row = Rect(
                    "Event",
                    journalContent,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero
                );
                Image image = row.gameObject.AddComponent<Image>();
                image.sprite = SpriteOf(visuals != null ? visuals.wideButton : null);
                image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                image.color =
                    image.sprite != null
                        ? new Color(1, 1, 1, .72f)
                        : new Color(.04f, .09f, .12f, .9f);
                LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = 46;
                layout.minHeight = 46;
                Label(
                    $"Д{Mathf.FloorToInt(item.minute / 1440) + 1} {m / 60:00}:{m % 60:00} — {item.text}",
                    row,
                    15,
                    FontStyle.Normal,
                    TextAnchor.MiddleLeft,
                    new Vector2(14, 6),
                    new Vector2(-14, -6),
                    new Color(.78f, .84f, .9f)
                );
            }
        }
    }
}
