using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoonDelivery
{
    public sealed partial class MoonCanvasMap
    {
        private void BuildSurface()
        {
            Clear(decorations);
            if (visuals == null)
                return;
            if (visuals.craters != null)
                for (int i = 0; i < visuals.craters.Length; i++)
                {
                    Texture2D texture = visuals.craters[i];
                    if (texture == null)
                        continue;
                    Vector2 p = new Vector2(
                        (.08f + Mathf.Repeat(i * .237f, .84f)) * WorldSize.x,
                        -(.10f + Mathf.Repeat(i * .371f, .80f)) * WorldSize.y
                    );
                    EnvironmentImage("Crater", texture, p, .72f, new Color(1, 1, 1, .72f));
                }
            if (visuals.mountains != null)
                for (int i = 0; i < visuals.mountains.Length; i++)
                {
                    Texture2D texture = visuals.mountains[i];
                    if (texture == null)
                        continue;
                    Vector2 p = new Vector2(
                        (.06f + Mathf.Repeat(i * .319f, .88f)) * WorldSize.x,
                        -(.08f + Mathf.Repeat(i * .213f, .82f)) * WorldSize.y
                    );
                    EnvironmentImage("Mountains", texture, p, .85f, new Color(1, 1, 1, .85f));
                }
            if (visuals.grid != null)
            {
                RawImage grid = MakeRaw("Navigation Grid", decorations, visuals.grid);
                StretchLayer(grid.rectTransform);
                grid.color = new Color(.1f, .85f, 1f, .13f);
                grid.raycastTarget = false;
            }
        }

        private void BuildSun()
        {
            Clear(sun);
            const int columns = 64;
            for (int i = 0; i < columns; i++)
            {
                Image strip = MakeImage("Shadow " + i, sun, null);
                strip.raycastTarget = false;
                RectTransform r = strip.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0, 1);
                r.pivot = new Vector2(0, 1);
                r.anchoredPosition = new Vector2(i * WorldSize.x / columns, 0);
                r.sizeDelta = new Vector2(WorldSize.x / columns + 3, WorldSize.y);
            }
            UpdateSun();
        }

        private void UpdateSun()
        {
            if (sun == null)
                return;
            int count = sun.childCount;
            for (int i = 0; i < count; i++)
            {
                float x = (i + .5f) / count;
                float distance = Mathf.Abs(
                    Mathf.Repeat(x - MoonGame.SunCenter(ui.Game.State.absoluteMinute) + .5f, 1)
                        - .5f
                );
                float darkness = Mathf.SmoothStep(
                    .08f,
                    .7f,
                    Mathf.InverseLerp(.285f, .355f, distance)
                );
                sun.GetChild(i).GetComponent<Image>().color = new Color(
                    .015f,
                    .035f,
                    .08f,
                    darkness
                );
            }
        }

        private void BuildZones()
        {
            Clear(zones);
            foreach (TerrainZone zone in GameCatalog.TerrainZones)
            {
                Vector2 center = new Vector2(zone.x * WorldSize.x, -zone.y * WorldSize.y),
                    size = new Vector2(
                        zone.radiusX * 2 * WorldSize.x,
                        zone.radiusY * 2 * WorldSize.y
                    );
                Color tint =
                    zone.terrain == TerrainType.Rocks ? new Color(.48f, .16f, .12f, .10f)
                    : zone.terrain == TerrainType.Crater ? new Color(.62f, .42f, .12f, .085f)
                    : new Color(.12f, .38f, .52f, .07f);
                Image fill = MakeImage(zone.name, zones, ZoneSprite(zone, false));
                Place(fill.rectTransform, center, size);
                fill.color = tint;
                fill.raycastTarget = false;
                BuildZoneDecorations(zone);
                Image outline = MakeImage(zone.name + " Outline", zones, ZoneSprite(zone, true));
                Place(outline.rectTransform, center, size);
                outline.color = new Color(
                    Mathf.Min(1, tint.r * 1.55f),
                    Mathf.Min(1, tint.g * 1.55f),
                    Mathf.Min(1, tint.b * 1.55f),
                    .20f
                );
                outline.raycastTarget = false;
                string kind =
                    zone.terrain == TerrainType.Rocks ? "СКАЛЫ"
                    : zone.terrain == TerrainType.Crater ? "КРАТЕРЫ"
                    : "НЕРОВНОСТИ";
                Image backing = MakeImage("Zone Label Backing", zones, null);
                Place(
                    backing.rectTransform,
                    center + new Vector2(0, size.y * .30f),
                    new Vector2(196, 27)
                );
                backing.color = new Color(.015f, .025f, .035f, .84f);
                backing.raycastTarget = false;
                TMP_Text label = MakeText(kind + " — " + zone.name, backing.transform, 12);
                label.gameObject.name = "Zone Label Text";
                StretchLayer(label.rectTransform);
            }
            UpdateBillboards();
        }

        private void BuildZoneDecorations(TerrainZone zone)
        {
            if (visuals == null)
                return;
            Texture2D[] primary =
                zone.terrain == TerrainType.Crater ? visuals.craters : visuals.mountains;
            Texture2D[] secondary = zone.terrain == TerrainType.Rough ? visuals.craters : null;
            if (primary == null || primary.Length == 0)
                return;
            int count = zone.terrain == TerrainType.Crater ? 11
                : zone.terrain == TerrainType.Rocks ? 9
                : 7,
                seed = StableHash(zone.id);
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.Repeat((seed % 997) * .001f + i * .618034f, 1) * Mathf.PI * 2,
                    radius =
                        .16f
                        + .68f * Mathf.Sqrt(Mathf.Repeat((seed % 613) * .001f + i * .414214f, 1));
                Vector2 p = new Vector2(
                    (zone.x + Mathf.Cos(angle) * zone.radiusX * radius) * WorldSize.x,
                    -(zone.y + Mathf.Sin(angle) * zone.radiusY * radius) * WorldSize.y
                );
                Texture2D[] source =
                    secondary != null && secondary.Length > 0 && i % 3 == 0 ? secondary : primary;
                Texture2D texture = source[((seed % source.Length) + i * 7) % source.Length];
                EnvironmentImage(
                    "Zone Detail",
                    texture,
                    p,
                    (zone.terrain == TerrainType.Rocks ? .46f : .38f)
                        + Mathf.Repeat(i * .173f, .16f),
                    Color.white
                );
            }
        }

        private void EnvironmentImage(
            string name,
            Texture2D texture,
            Vector2 position,
            float scale,
            Color tint
        )
        {
            if (texture == null)
                return;
            Image image = MakeImage(name, decorations, ui.SpriteOf(texture));
            Place(
                image.rectTransform,
                position,
                new Vector2(texture.width * scale, texture.height * scale)
            );
            image.color = tint;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private Sprite ZoneSprite(TerrainZone zone, bool outline)
        {
            string key = zone.id + (outline ? "-outline" : "-fill");
            if (zoneSprites.TryGetValue(key, out Sprite sprite))
                return sprite;
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x / (size - 1f) - .5f) * 2,
                    dy = (.5f - y / (size - 1f)) * 2,
                    angle = Mathf.Atan2(dy, dx),
                    distance = Mathf.Sqrt(dx * dx + dy * dy),
                    coast = GameCatalog.ZoneBoundaryRadius(zone, angle);
                float alpha = outline
                    ? 1 - Mathf.SmoothStep(.018f, .065f, Mathf.Abs(distance - coast))
                    : 1 - Mathf.SmoothStep(coast - .12f, coast + .025f, distance);
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100);
            zoneSprites[key] = sprite;
            return sprite;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in value)
                    hash = hash * 31 + c;
                return hash & 0x7fffffff;
            }
        }
    }
}
