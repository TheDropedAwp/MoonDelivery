using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MoonDelivery
{
    public sealed partial class MoonCanvasMap
        : MonoBehaviour,
            IBeginDragHandler,
            IDragHandler,
            IScrollHandler
    {
        private static readonly Vector2 WorldSize = new Vector2(14400, 9600);
        private MoonCanvasUI ui;
        private MoonVisualCatalog visuals;
        private RectTransform viewport,
            world,
            decorations,
            sun,
            zones,
            routes,
            points,
            markers;
        private Vector2 cameraPosition;
        private float zoom = .30f;
        private string signature;
        private readonly Dictionary<string, Sprite> zoneSprites = new Dictionary<string, Sprite>();

        public void Initialize(MoonCanvasUI owner, MoonVisualCatalog catalog)
        {
            ui = owner;
            visuals = catalog;
            viewport = (RectTransform)transform;
            RawImage bg = MakeRaw("Surface", viewport, catalog?.moonBackground);
            world = bg.rectTransform;
            world.anchorMin = world.anchorMax = new Vector2(.5f, .5f);
            world.pivot = new Vector2(0, 1);
            world.sizeDelta = WorldSize;
            if (bg.texture != null)
                bg.uvRect = new Rect(0, 0, WorldSize.x / 620f, WorldSize.y / 620f);
            bg.raycastTarget = true;
            decorations = Layer("Moon Environment", world);
            sun = Layer("Sunlight Cycle", world);
            zones = Layer("Terrain Zones", world);
            routes = Layer("Routes", world);
            points = Layer("Points", world);
            markers = Layer("Rovers", world);
            BuildSurface();
            BuildSun();
            BuildZones();
            Order first = ui.Game.State.orders.FirstOrDefault(x =>
                x.status == OrderStatus.Available
            );
            MapPoint target = first != null ? ui.Game.Point(first.destinationId) : GameCatalog.Base;
            cameraPosition = Vector2.Lerp(
                new Vector2(GameCatalog.Base.x * WorldSize.x, GameCatalog.Base.y * WorldSize.y),
                new Vector2(target.x * WorldSize.x, target.y * WorldSize.y),
                .55f
            );
            ApplyCamera();
        }

        private void Update()
        {
            Vector2 move = Vector2.zero;
            if (Input.GetKey(KeyCode.W))
                move.y = -1;
            if (Input.GetKey(KeyCode.S))
                move.y = 1;
            if (Input.GetKey(KeyCode.A))
                move.x = -1;
            if (Input.GetKey(KeyCode.D))
                move.x = 1;
            if (move.sqrMagnitude > 0)
            {
                cameraPosition += move.normalized * (520 / zoom) * Time.unscaledDeltaTime;
                Clamp();
                ApplyCamera();
            }
            UpdateSun();
            UpdateMarkers();
        }

        public void RefreshAll()
        {
            RefreshDynamic(true);
            RebuildRoutes();
        }

        public void RefreshDynamic()
        {
            RefreshDynamic(false);
        }

        private void RefreshDynamic(bool force)
        {
            string next =
                string.Join(
                    "|",
                    ui.Game.State.orders.Where(x =>
                            x.status == OrderStatus.Available || x.status == OrderStatus.InProgress
                        )
                        .Select(x => x.id + ":" + x.status)
                )
                + "/"
                + string.Join(
                    "|",
                    ui.Game.State.deliveries.Where(x =>
                            x.status == DeliveryStatus.Travelling
                            || x.status == DeliveryStatus.Broken
                        )
                        .Select(x => x.id + ":" + x.status)
                )
                + "/"
                + string.Join(
                    "|",
                    ui.Game.State.rescueMissions.Where(x => !x.completed)
                        .Select(x => x.id + ":" + x.phase)
                );
            if (!force && next == signature)
                return;
            signature = next;
            BuildPoints();
            BuildMarkers();
            RebuildRoutes();
        }

        public void Focus(MapPoint point)
        {
            if (point == null)
                return;
            cameraPosition = new Vector2(point.x * WorldSize.x, point.y * WorldSize.y);
            Clamp();
            ApplyCamera();
        }

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right)
                return;
            cameraPosition += new Vector2(-eventData.delta.x, eventData.delta.y) / zoom;
            Clamp();
            ApplyCamera();
        }

        public void OnScroll(PointerEventData eventData)
        {
            zoom = Mathf.Clamp(zoom * (eventData.scrollDelta.y > 0 ? 1.16f : .86f), .16f, 1.35f);
            Clamp();
            ApplyCamera();
            UpdateBillboards();
            RebuildRoutes();
        }

        private void ApplyCamera()
        {
            world.localScale = Vector3.one * zoom;
            world.anchoredPosition = new Vector2(-cameraPosition.x * zoom, cameraPosition.y * zoom);
        }

        private void Clamp()
        {
            Vector2 size = viewport.rect.size;
            float hw = size.x / (2 * zoom),
                hh = size.y / (2 * zoom);
            cameraPosition.x = Mathf.Clamp(cameraPosition.x, hw, WorldSize.x - hw);
            cameraPosition.y = Mathf.Clamp(cameraPosition.y, hh, WorldSize.y - hh);
        }
    }
}
