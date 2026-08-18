using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoonDelivery
{
    public sealed partial class MoonCanvasUI
    {
        private void RefreshGameOver()
        {
            if (!game.State.gameOver)
            {
                if (gameOverOverlay != null)
                {
                    Destroy(gameOverOverlay);
                    gameOverOverlay = null;
                }
                return;
            }
            if (gameOverOverlay != null)
                return;
            gameOverOverlay = new GameObject("Game Over", typeof(RectTransform), typeof(Image));
            gameOverOverlay.transform.SetParent(canvas.transform, false);
            RectTransform blocker = (RectTransform)gameOverOverlay.transform;
            Stretch(blocker);
            gameOverOverlay.GetComponent<Image>().color = new Color(0, 0, 0, .72f);
            RectTransform panel = Panel(
                "Company Closed",
                blocker,
                new Vector2(.5f, .5f),
                new Vector2(.5f, .5f),
                new Vector2(-230, -100),
                new Vector2(230, 100),
                visuals != null ? visuals.mainPanel : null,
                1
            );
            Label(
                "КОМПАНИЯ ЗАКРЫТА",
                panel,
                25,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(20, -64),
                new Vector2(-20, -20),
                new Color(1, .48f, .4f)
            );
            Label(
                "Репутация упала ниже −100.",
                panel,
                15,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Vector2(20, -105),
                new Vector2(-20, -68),
                Color.white
            );
            Btn(
                "НАЧАТЬ ЗАНОВО",
                panel,
                new Vector2(.5f, 0),
                new Vector2(.5f, 0),
                new Vector2(-105, 22),
                new Vector2(105, 60),
                () =>
                {
                    game.CreateNewGame();
                    plan.Clear();
                    selectedOrderId = selectedRoverId = garageRoverId = null;
                    Destroy(gameOverOverlay);
                    gameOverOverlay = null;
                    RefreshAll();
                }
            );
        }

        internal void ShowBroken(Delivery delivery, Vector2 screenPosition)
        {
            GameObject overlay = new GameObject(
                "Broken Rover Dialog",
                typeof(RectTransform),
                typeof(Image)
            );
            overlay.transform.SetParent(canvas.transform, false);
            RectTransform rect = (RectTransform)overlay.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0, .5f);
            rect.sizeDelta = new Vector2(430, 210);
            rect.position = screenPosition + new Vector2(30, 0);
            Image panelImage = overlay.GetComponent<Image>();
            panelImage.sprite = SpriteOf(visuals != null ? visuals.compactPanel : null);
            panelImage.type = panelImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            panelImage.color =
                panelImage.sprite != null
                    ? new Color(1, 1, 1, .98f)
                    : new Color(.025f, .05f, .08f, .98f);
            Rover rover = game.Rover(delivery.roverId);
            float cargo = game.RecoverableCargoWeight(delivery);
            int rescueCost = game.RescueCost(delivery);
            Label(
                $"АВАРИЯ: {rover.displayName}\nОсталось груза: {cargo:0} кг\nЭвакуация на базу: {rescueCost} кр.",
                rect,
                14,
                FontStyle.Bold,
                TextAnchor.UpperLeft,
                new Vector2(14, -86),
                new Vector2(-14, -10),
                Color.white
            );
            Btn(
                "Закрыть",
                rect,
                new Vector2(0, 0),
                new Vector2(0, 0),
                new Vector2(14, 14),
                new Vector2(112, 46),
                () => Destroy(overlay)
            );
            Button rescue = Btn(
                $"Эвакуатор: {rescueCost} кр.",
                rect,
                new Vector2(0, 0),
                new Vector2(0, 0),
                new Vector2(122, 14),
                new Vector2(276, 46),
                () =>
                {
                    game.Rescue(delivery);
                    Destroy(overlay);
                }
            );
            rescue.interactable =
                game.State.money >= rescueCost && !delivery.cargoRecoveryDispatched;
            Button transfer = Btn(
                "Передать груз",
                rect,
                new Vector2(0, 0),
                new Vector2(0, 0),
                new Vector2(286, 14),
                new Vector2(414, 46),
                () => ShowRecoveryChoices(delivery, overlay)
            );
            Image transferImage = transfer.GetComponent<Image>();
            transferImage.sprite = SpriteOf(visuals != null ? visuals.smallButton : null);
            transferImage.type =
                transferImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            transferImage.color =
                transferImage.sprite != null ? Color.white : new Color(.09f, .22f, .3f, .96f);
            transfer.interactable = cargo > 0 && !delivery.cargoRecoveryDispatched;
        }

        private void ShowRecoveryChoices(Delivery delivery, GameObject old)
        {
            Destroy(old);
            GameObject overlay = new GameObject(
                "Cargo Recovery Dialog",
                typeof(RectTransform),
                typeof(Image)
            );
            overlay.transform.SetParent(canvas.transform, false);
            RectTransform rect = (RectTransform)overlay.transform;
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(620, 430);
            Image panelImage = overlay.GetComponent<Image>();
            panelImage.sprite = SpriteOf(visuals != null ? visuals.mainPanel : null);
            panelImage.type = panelImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            panelImage.color =
                panelImage.sprite != null
                    ? new Color(1, 1, 1, .99f)
                    : new Color(.025f, .05f, .08f, .99f);
            Label(
                $"ПЕРЕХВАТ ГРУЗА — {game.RecoverableCargoWeight(delivery):0} кг",
                rect,
                18,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(18, -45),
                new Vector2(-18, -10),
                new Color(.45f, .85f, 1)
            );
            RectTransform content = Scroll(
                "Candidates",
                rect,
                new Vector2(14, 60),
                new Vector2(-14, -54)
            );
            foreach (
                Rover rover in game.State.rovers.Where(x =>
                    x.id != delivery.roverId && x.status == RoverStatus.Ready
                )
            )
            {
                Rover candidate = rover;
                RouteForecast forecast;
                string error = game.ValidateCargoRecovery(delivery, rover, out forecast);
                Button b = Card(
                    content,
                    72,
                    new Color(.07f, .11f, .16f, .96f),
                    () =>
                    {
                        if (game.DispatchCargoRecovery(delivery, candidate, out _))
                            Destroy(overlay);
                    }
                );
                b.interactable = error == null;
                Label(
                    rover.displayName,
                    b.transform,
                    14,
                    FontStyle.Bold,
                    TextAnchor.MiddleLeft,
                    new Vector2(10, 8),
                    new Vector2(130, -8),
                    Color.white
                );
                Label(
                    error ?? forecast.summary,
                    b.transform,
                    11,
                    FontStyle.Normal,
                    TextAnchor.MiddleLeft,
                    new Vector2(140, 8),
                    new Vector2(-10, -8),
                    error == null ? new Color(.75f, .85f, .92f) : new Color(1, .5f, .4f)
                );
            }
            Btn(
                "Закрыть",
                rect,
                new Vector2(0, 0),
                new Vector2(0, 0),
                new Vector2(14, 14),
                new Vector2(120, 46),
                () => Destroy(overlay)
            );
        }
    }
}
