using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoonDelivery
{
    public sealed partial class MoonCanvasMap
    {
        private void BuildPoints()
        {
            Clear(points);
            foreach (MapPoint point in VisiblePoints())
            {
                MoonPointButton click = MakePoint(point);
                click.Initialize(ui, point);
            }
        }

        private MoonPointButton MakePoint(MapPoint point)
        {
            GameObject go = new GameObject(
                point.name,
                typeof(RectTransform),
                typeof(Image),
                typeof(MoonPointButton)
            );
            go.transform.SetParent(points, false);
            RectTransform r = (RectTransform)go.transform;
            Place(
                r,
                new Vector2(point.x * WorldSize.x, -point.y * WorldSize.y),
                new Vector2(62, 58)
            );
            r.localScale = Vector3.one / zoom;
            Image buttonImage = go.GetComponent<Image>();
            buttonImage.sprite = ui.SpriteOf(visuals?.hexButton);
            buttonImage.color =
                point.type == MapPointType.ChargingStation ? new Color(.3f, 1, .45f)
                : point.type == MapPointType.Base ? new Color(.2f, .9f, 1)
                : new Color(1, .72f, .25f);
            buttonImage.preserveAspect = true;
            Texture2D texture =
                point.type == MapPointType.ChargingStation ? visuals?.chargingStationIcon
                : point.type == MapPointType.Base ? visuals?.homeStationIcon
                : visuals?.destinationStationIcon;
            if (texture != null)
            {
                Image icon = MakeImage("Station Icon", go.transform, ui.SpriteOf(texture));
                RectTransform iconRect = icon.rectTransform;
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(.5f, .5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta =
                    point.type == MapPointType.Destination
                        ? new Vector2(38, 34)
                        : new Vector2(32, 32);
                icon.preserveAspect = true;
                icon.color = PointIconColor(point.type);
                icon.raycastTarget = false;
            }
            TMP_Text label = MakeText(
                point.type == MapPointType.ChargingStation
                    ? "ПЛАТНАЯ ЗАРЯДКА\n" + point.name
                    : point.name,
                go.transform,
                13
            );
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
            label.rectTransform.anchoredPosition = new Vector2(0, -58);
            label.rectTransform.sizeDelta = new Vector2(220, 74);
            return go.GetComponent<MoonPointButton>();
        }

        private static Color PointIconColor(MapPointType pointType)
        {
            switch (pointType)
            {
                case MapPointType.ChargingStation:
                    return Color.green;
                case MapPointType.Base:
                    return Color.yellow;
                default:
                    return new Color32(0x00, 0xA1, 0xFF, 0xFF);
            }
        }

        private List<MapPoint> VisiblePoints()
        {
            HashSet<string> ids = new HashSet<string> { "base" };
            foreach (
                MapPoint p in ui
                    .Game.AllMapPoints()
                    .Where(x => x.type == MapPointType.ChargingStation)
            )
                ids.Add(p.id);
            foreach (
                Order o in ui.Game.State.orders.Where(x =>
                    x.status == OrderStatus.Available || x.status == OrderStatus.InProgress
                )
            )
                ids.Add(o.destinationId);
            foreach (RouteStop s in ui.Plan)
                ids.Add(s.pointId);
            foreach (
                Delivery d in ui.Game.State.deliveries.Where(x =>
                    x.status == DeliveryStatus.Travelling || x.status == DeliveryStatus.Broken
                )
            )
            foreach (RouteStop s in d.stops)
                ids.Add(s.pointId);
            return ui.Game.AllMapPoints().Where(x => ids.Contains(x.id)).ToList();
        }

        public void RebuildRoutes()
        {
            if (routes == null)
                return;
            Clear(routes);
            if (ui.Plan.Any(x => !string.IsNullOrEmpty(x.orderId)))
            {
                string from = "base";
                foreach (RouteStop stop in ui.Plan)
                {
                    Line(
                        ui.Game.Point(from),
                        ui.Game.Point(stop.pointId),
                        ColorFor(ui.Game.Leg(from, stop.pointId).terrain),
                        5 / zoom
                    );
                    from = stop.pointId;
                }
            }
            foreach (
                Delivery d in ui.Game.State.deliveries.Where(x =>
                    x.status == DeliveryStatus.Travelling
                )
            )
            {
                string from = d.currentPointId;
                for (int i = d.currentStopIndex; i < d.stops.Count; i++)
                {
                    RouteStop stop = d.stops[i];
                    Line(
                        ui.Game.Point(from),
                        ui.Game.Point(stop.pointId),
                        ColorFor(ui.Game.Leg(from, stop.pointId).terrain),
                        4 / zoom
                    );
                    from = stop.pointId;
                }
            }
        }

        private void Line(MapPoint from, MapPoint to, Color color, float width)
        {
            if (from == null || to == null)
                return;
            Vector2 a = new Vector2(from.x * WorldSize.x, -from.y * WorldSize.y),
                b = new Vector2(to.x * WorldSize.x, -to.y * WorldSize.y),
                delta = b - a;
            Image image = MakeImage("Route", routes, null);
            RectTransform r = image.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, .5f);
            r.anchoredPosition = a;
            r.sizeDelta = new Vector2(delta.magnitude, width);
            r.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            image.color = color;
        }

        private void BuildMarkers()
        {
            Clear(markers);
            foreach (
                Delivery d in ui.Game.State.deliveries.Where(x =>
                    x.status == DeliveryStatus.Travelling || x.status == DeliveryStatus.Broken
                )
            )
            {
                Rover rover = ui.Game.Rover(d.roverId);
                GameObject go = new GameObject(
                    "Rover " + d.id,
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(MoonBrokenButton)
                );
                go.transform.SetParent(markers, false);
                RectTransform r = (RectTransform)go.transform;
                Place(r, Vector2.zero, new Vector2(58, 48));
                Image image = go.GetComponent<Image>();
                image.sprite = ui.SpriteOf(visuals?.MapIcon(rover.type));
                image.preserveAspect = true;
                image.color =
                    d.status == DeliveryStatus.Broken ? new Color(1, .35f, .3f) : Color.white;
                MoonBrokenButton click = go.GetComponent<MoonBrokenButton>();
                click.Initialize(ui, d);
                click.enabled = d.status == DeliveryStatus.Broken;
                image.raycastTarget = d.status == DeliveryStatus.Broken;
            }
            foreach (RescueMission mission in ui.Game.State.rescueMissions.Where(x => !x.completed))
            {
                GameObject go = new GameObject(
                    "Rescue " + mission.id,
                    typeof(RectTransform),
                    typeof(Image)
                );
                go.transform.SetParent(markers, false);
                RectTransform r = (RectTransform)go.transform;
                Place(r, Vector2.zero, new Vector2(62, 48));
                Image image = go.GetComponent<Image>();
                image.sprite = ui.SpriteOf(visuals?.mapRescue);
                image.preserveAspect = true;
                image.raycastTarget = false;
                if (mission.phase == 1)
                {
                    Delivery delivery = ui.Game.State.deliveries.FirstOrDefault(x =>
                        x.id == mission.deliveryId
                    );
                    Rover rover = delivery != null ? ui.Game.Rover(delivery.roverId) : null;
                    if (rover != null)
                    {
                        Image carried = MakeImage(
                            "Carried Rover",
                            go.transform,
                            ui.SpriteOf(visuals?.MapIcon(rover.type))
                        );
                        RectTransform cr = carried.rectTransform;
                        cr.anchorMin = cr.anchorMax = new Vector2(.5f, .5f);
                        cr.anchoredPosition = new Vector2(38, -2);
                        cr.sizeDelta = new Vector2(34, 30);
                        carried.preserveAspect = true;
                        carried.raycastTarget = false;
                    }
                }
            }
            UpdateMarkers();
        }

        private void UpdateMarkers()
        {
            foreach (
                Delivery d in ui.Game.State.deliveries.Where(x =>
                    x.status == DeliveryStatus.Travelling || x.status == DeliveryStatus.Broken
                )
            )
            {
                Transform t = markers.Find("Rover " + d.id);
                if (t == null)
                    continue;
                Vector2 p = ui.Game.DeliveryPosition(d);
                RectTransform r = (RectTransform)t;
                r.anchoredPosition = new Vector2(p.x * WorldSize.x, -p.y * WorldSize.y);
                if (d.currentStopIndex < d.stops.Count)
                {
                    MapPoint from = ui.Game.Point(d.currentPointId),
                        to = ui.Game.Point(d.stops[d.currentStopIndex].pointId);
                    SetDirection(
                        r,
                        new Vector2((to.x - from.x) * WorldSize.x, -(to.y - from.y) * WorldSize.y)
                    );
                }
            }
            foreach (RescueMission mission in ui.Game.State.rescueMissions.Where(x => !x.completed))
            {
                Transform t = markers.Find("Rescue " + mission.id);
                if (t == null)
                    continue;
                float progress = Mathf.Clamp01(
                    mission.progress / Mathf.Max(.001f, mission.distance)
                );
                Vector2 basePoint = new Vector2(GameCatalog.Base.x, GameCatalog.Base.y),
                    target = new Vector2(mission.targetX, mission.targetY),
                    start = mission.phase == 0 ? basePoint : target,
                    end = mission.phase == 0 ? target : basePoint,
                    p = Vector2.Lerp(start, end, progress);
                RectTransform r = (RectTransform)t;
                r.anchoredPosition = new Vector2(p.x * WorldSize.x, -p.y * WorldSize.y);
                SetDirection(
                    r,
                    new Vector2((end.x - start.x) * WorldSize.x, -(end.y - start.y) * WorldSize.y)
                );
            }
        }

        private void SetDirection(RectTransform r, Vector2 direction)
        {
            bool right = direction.x > 0;
            r.localScale = new Vector3(right ? -1 : 1, 1, 1) / zoom;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - (right ? 0 : 180);
            r.localRotation = Quaternion.Euler(0, 0, angle);
        }

        private void UpdateBillboards()
        {
            if (points != null)
                foreach (RectTransform r in points.Cast<Transform>().Select(x => (RectTransform)x))
                    r.localScale = Vector3.one / zoom;
            if (zones != null)
                foreach (Transform t in zones)
                    if (t.name == "Zone Label Backing")
                        t.localScale = Vector3.one / zoom;
        }
    }
}
