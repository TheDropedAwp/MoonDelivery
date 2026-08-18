using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoonDelivery
{
    public sealed partial class MoonCanvasUI
    {
        private void RebuildOrders()
        {
            ClearChildren(ordersContent);
            foreach (
                Order order in game.State.orders.Where(x =>
                    x.status == OrderStatus.Available || x.status == OrderStatus.InProgress
                )
            )
            {
                Order captured = order;
                bool planned = plan.Any(x => x.orderId == order.id);
                MapPoint point = game.Point(order.destinationId);
                Button button = Card(
                    ordersContent,
                    128,
                    planned ? new Color(.12f, .45f, .34f, .95f) : new Color(.07f, .11f, .16f, .96f),
                    () =>
                    {
                        if (captured.status == OrderStatus.Available)
                            ToggleOrder(captured, true);
                    }
                );
                button.gameObject.name = "Order " + order.id;
                Label(
                    order.title,
                    button.transform,
                    18,
                    FontStyle.Bold,
                    TextAnchor.UpperLeft,
                    new Vector2(10, -34),
                    new Vector2(-10, -6),
                    new Color(.48f, .88f, 1)
                );
                TMP_Text info = Label(
                    OrderInfoText(order, point),
                    button.transform,
                    14,
                    FontStyle.Normal,
                    TextAnchor.UpperLeft,
                    new Vector2(10, 8),
                    new Vector2(-10, -38),
                    new Color(.78f, .84f, .9f)
                );
                info.gameObject.name = "Order Info";
            }
        }

        private string OrderInfoText(Order order, MapPoint point) =>
            $"{point?.name}\n{order.weightKg:0} кг — {order.reward} кр. — +{order.reputation} реп.\n{(order.status == OrderStatus.InProgress ? "В ПУТИ" : "Осталось: " + FormatDuration(game.MinutesLeft(order)))}";

        private void UpdateOrderTexts()
        {
            if (ordersContent == null)
                return;
            foreach (
                Order order in game.State.orders.Where(x =>
                    x.status == OrderStatus.Available || x.status == OrderStatus.InProgress
                )
            )
            {
                Transform card = ordersContent.Find("Order " + order.id);
                Transform info = card != null ? card.Find("Order Info") : null;
                if (info != null)
                    info.GetComponent<TMP_Text>().text = OrderInfoText(
                        order,
                        game.Point(order.destinationId)
                    );
            }
        }

        private void RebuildRovers()
        {
            ClearChildren(roversContent);
            foreach (Rover rover in game.State.rovers)
            {
                Rover captured = rover;
                RoverStats stats = rover.Stats;
                Button button = Card(
                    roversContent,
                    130,
                    selectedRoverId == rover.id
                        ? new Color(.12f, .38f, .52f, .96f)
                        : new Color(.07f, .11f, .16f, .96f),
                    () =>
                    {
                        selectedRoverId = captured.id;
                        RebuildRovers();
                        RefreshPlanner();
                    }
                );
                button.gameObject.name = "Rover " + rover.id;
                Label(
                    $"{rover.displayName} — ур. {rover.level}",
                    button.transform,
                    18,
                    FontStyle.Bold,
                    TextAnchor.UpperLeft,
                    new Vector2(10, -32),
                    new Vector2(-10, -5),
                    new Color(.48f, .88f, 1)
                );
                TMP_Text info = Label(
                    "",
                    button.transform,
                    14,
                    FontStyle.Normal,
                    TextAnchor.UpperLeft,
                    new Vector2(10, 20),
                    new Vector2(-10, -35),
                    new Color(.78f, .84f, .9f)
                );
                info.gameObject.name = "Status";
                info.text = RoverStatusText(rover, stats);
                CreateBar(
                    button.transform,
                    "Battery",
                    rover.battery / stats.maxBattery,
                    new Color(.2f, .8f, .95f)
                );
            }
        }

        private void UpdateRoverTexts()
        {
            foreach (Rover rover in game.State.rovers)
            {
                Transform card = roversContent.Find("Rover " + rover.id);
                if (card != null)
                {
                    Transform status = card.Find("Status");
                    if (status != null)
                        status.GetComponent<TMP_Text>().text = RoverStatusText(rover, rover.Stats);
                }
            }
            UpdateRoverBars();
        }

        private void UpdateRoverBars()
        {
            if (roversContent == null)
                return;
            foreach (Rover rover in game.State.rovers)
            {
                Transform card = roversContent.Find("Rover " + rover.id);
                if (card == null)
                    continue;
                float value = Mathf.Clamp01(rover.battery / rover.Stats.maxBattery);
                Transform fill = card.Find("Battery/Fill");
                if (fill != null)
                    ((RectTransform)fill).anchorMax = new Vector2(value, 1);
                Transform handle = card.Find("Battery/Handle");
                if (handle != null)
                {
                    RectTransform rect = (RectTransform)handle;
                    rect.anchorMin = rect.anchorMax = new Vector2(value, .5f);
                    handle.gameObject.SetActive(value > .005f);
                }
            }
        }

        private string RoverStatusText(Rover rover, RoverStats stats) =>
            $"{StatusName(rover.status)}{(rover.status == RoverStatus.Charging ? $" ({rover.chargeRemainingMinutes:0} мин.)" : "")}\nБатарея {rover.battery:0}/{stats.maxBattery:0} — Груз {stats.capacityKg:0} кг\nСкорость {stats.speed:0.#} — Риск {stats.breakdownRisk * 100:0.#}%";

        private void ToggleOrder(Order order, bool focus)
        {
            selectedOrderId = order.id;
            int index = plan.FindIndex(x => x.orderId == order.id);
            if (index >= 0)
                plan.RemoveAt(index);
            else
                plan.Add(new RouteStop { pointId = order.destinationId, orderId = order.id });
            if (focus)
                map.Focus(game.Point(order.destinationId));
            RebuildOrders();
            RefreshPlanner();
            map.RefreshAll();
        }

        internal void SelectPoint(MapPoint point, bool right)
        {
            if (right)
            {
                int index = plan.FindLastIndex(x => x.pointId == point.id);
                if (index >= 0)
                    plan.RemoveAt(index);
                RebuildOrders();
            }
            else if (point.type == MapPointType.ChargingStation)
            {
                int last = plan.FindLastIndex(x =>
                    x.pointId == point.id && string.IsNullOrEmpty(x.orderId)
                );
                if (last >= 0 && last == plan.Count - 1)
                    plan.RemoveAt(last);
                else
                    plan.Add(new RouteStop { pointId = point.id });
            }
            else if (point.type == MapPointType.Destination)
            {
                Order order =
                    game.State.orders.Where(x =>
                            x.destinationId == point.id && x.status == OrderStatus.Available
                        )
                        .OrderBy(x => x.deadlineMinute)
                        .FirstOrDefault(x => !plan.Any(s => s.orderId == x.id))
                    ?? game.State.orders.FirstOrDefault(x =>
                        x.destinationId == point.id && x.status == OrderStatus.Available
                    );
                if (order != null)
                    ToggleOrder(order, false);
            }
            RefreshPlanner();
            map.RebuildRoutes();
        }

        private void RefreshPlanner()
        {
            Rover rover = game.Rover(selectedRoverId);
            float load = plan.Where(x => !string.IsNullOrEmpty(x.orderId))
                .Select(x => game.Order(x.orderId))
                .Where(x => x != null)
                .Sum(x => x.weightKg);
            routeText.text =
                $"План: База → {(plan.Count == 0 ? "маршрут пуст" : string.Join(" → ", plan.Select(StopName)))}\nСтартовый груз: {load:0} кг";
            string validation = game.ValidatePlan(plan, rover);
            forecastText.text = game.Forecast(plan, rover).summary;
            validationText.text = validation ?? "Маршрут готов к отправке";
            validationText.color =
                validation == null ? new Color(.42f, .92f, .72f) : new Color(1, .62f, .4f);
            if (startRouteButton != null)
                startRouteButton.interactable = validation == null;
            if (chargeButton != null)
            {
                chargeButton.interactable =
                    rover != null
                    && rover.status == RoverStatus.Ready
                    && rover.battery < rover.Stats.maxBattery - .01f;
                TMP_Text label = chargeButton.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = "Зарядить на базе — бесплатно";
            }
            Order selected = game.Order(selectedOrderId);
            if (declineButton != null)
                declineButton.interactable =
                    selected != null && selected.status == OrderStatus.Available;
        }

        private void StartRoute()
        {
            Rover rover = game.Rover(selectedRoverId);
            if (game.StartDelivery(plan, rover, out _))
            {
                plan.Clear();
                selectedOrderId = null;
                selectedRoverId = null;
                RefreshAll();
            }
        }

        private void DeclineSelectedOrder()
        {
            Order order = game.Order(selectedOrderId);
            if (order == null || order.status != OrderStatus.Available)
                return;
            plan.RemoveAll(x => x.orderId == order.id);
            game.Decline(order);
            selectedOrderId = null;
            RefreshAll();
        }

        private string StopName(RouteStop stop)
        {
            MapPoint point = game.Point(stop.pointId);
            if (!string.IsNullOrEmpty(stop.orderId))
            {
                Order order = game.Order(stop.orderId);
                return order?.title ?? point?.name;
            }
            return point?.type == MapPointType.ChargingStation
                ? "Платная зарядка: " + point.name
                : point?.name;
        }
    }
}
