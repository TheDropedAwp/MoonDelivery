using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoonDelivery
{
    public sealed partial class MoonCanvasUI
    {
        private Button Card(
            Transform parent,
            float height,
            Color color,
            UnityEngine.Events.UnityAction action
        )
        {
            Button b = Btn(
                "",
                parent,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                action
            );
            bool selected = color.g > .25f;
            Image image = b.GetComponent<Image>();
            image.sprite = SpriteOf(visuals != null ? visuals.compactPanel : null);
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            b.transition = Selectable.Transition.None;
            MoonCardTint tint = b.gameObject.AddComponent<MoonCardTint>();
            tint.Initialize(image, selected);
            LayoutElement layout = b.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;
            return b;
        }

        private Button Btn(
            string text,
            Transform parent,
            Vector2 amin,
            Vector2 amax,
            Vector2 omin,
            Vector2 omax,
            UnityEngine.Events.UnityAction action
        )
        {
            GameObject go = new GameObject(
                string.IsNullOrEmpty(text) ? "Card" : text,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)
            );
            go.transform.SetParent(parent, false);
            RectTransform r = (RectTransform)go.transform;
            r.anchorMin = amin;
            r.anchorMax = amax;
            r.offsetMin = omin;
            r.offsetMax = omax;
            bool compact = Mathf.Approximately(amin.x, amax.x) && Mathf.Abs(omax.x - omin.x) <= 150;
            Texture2D texture =
                visuals != null ? (compact ? visuals.smallButton : visuals.wideButton) : null;
            Image image = go.GetComponent<Image>();
            image.sprite = SpriteOf(texture);
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = image.sprite != null ? Color.white : new Color(.09f, .22f, .3f, .96f);
            Button button = go.GetComponent<Button>();
            if (action != null)
                button.onClick.AddListener(() =>
                {
                    audioController.PlayClick();
                    action();
                });
            if (!string.IsNullOrEmpty(text))
                Label(
                    text,
                    go.transform,
                    13,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    Vector2.zero,
                    Vector2.zero,
                    new Color(.78f, .95f, 1)
                );
            return button;
        }

        private TMP_Text Label(
            string value,
            Transform parent,
            int size,
            FontStyle style,
            TextAnchor anchor,
            Vector2 omin,
            Vector2 omax,
            Color color
        )
        {
            GameObject go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform r = (RectTransform)go.transform;
            float ax0 = 0,
                ax1 = 1,
                ay0 = 0,
                ay1 = 1;
            bool zeroX = omin.x == 0 && omax.x == 0;
            if (!zeroX && omin.x >= 0 && omax.x >= 0)
                ax0 = ax1 = 0;
            else if (!zeroX && omin.x <= 0 && omax.x <= 0)
                ax0 = ax1 = 1;
            if (omin.y < 0 && omax.y <= 0)
                ay0 = ay1 = 1;
            else if (omin.y >= 0 && omax.y > 0)
                ay0 = ay1 = 0;
            r.anchorMin = new Vector2(ax0, ay0);
            r.anchorMax = new Vector2(ax1, ay1);
            r.offsetMin = omin;
            r.offsetMax = omax;
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : FontStyles.Normal;
            text.alignment = TmpAlignment(anchor);
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            text.margin = new Vector4(1, 0, 1, 0);
            return text;
        }

        private RectTransform Panel(
            string name,
            Transform parent,
            Vector2 amin,
            Vector2 amax,
            Vector2 omin,
            Vector2 omax,
            Texture2D texture,
            float alpha
        )
        {
            RectTransform r = Rect(name, parent, amin, amax, omin, omax);
            Image image = r.gameObject.AddComponent<Image>();
            image.sprite = SpriteOf(texture);
            image.type = texture != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color =
                texture != null ? new Color(1, 1, 1, alpha) : new Color(.025f, .05f, .08f, alpha);
            return r;
        }

        private RectTransform Rect(
            string name,
            Transform parent,
            Vector2 amin,
            Vector2 amax,
            Vector2 omin,
            Vector2 omax
        )
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform r = (RectTransform)go.transform;
            r.anchorMin = amin;
            r.anchorMax = amax;
            r.offsetMin = omin;
            r.offsetMax = omax;
            return r;
        }

        private RectTransform Scroll(string name, Transform parent, Vector2 omin, Vector2 omax)
        {
            RectTransform view = Rect(name, parent, Vector2.zero, Vector2.one, omin, omax);
            Image bg = view.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, .12f);
            view.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = view.gameObject.AddComponent<ScrollRect>();
            RectTransform content = Rect(
                "Content",
                view,
                new Vector2(0, 1),
                new Vector2(1, 1),
                Vector2.zero,
                Vector2.zero
            );
            content.pivot = new Vector2(.5f, 1);
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.padding = new RectOffset(3, 3, 3, 3);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = view;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return content;
        }

        private void CreateBar(Transform parent, string name, float value, Color color)
        {
            value = Mathf.Clamp01(value);
            RectTransform track = Rect(
                name,
                parent,
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(10, 8),
                new Vector2(-10, 17)
            );
            Image background = track.gameObject.AddComponent<Image>();
            background.sprite = SpriteOf(visuals != null ? visuals.sliderTrack : null);
            background.type = Image.Type.Simple;
            background.color = background.sprite != null ? Color.white : new Color(.12f, .15f, .2f);
            RectTransform fill = Rect(
                "Fill",
                track,
                Vector2.zero,
                new Vector2(value, 1),
                Vector2.zero,
                Vector2.zero
            );
            Image image = fill.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            RectTransform handle = Rect(
                "Handle",
                track,
                new Vector2(value, .5f),
                new Vector2(value, .5f),
                new Vector2(-6, -7.5f),
                new Vector2(6, 7.5f)
            );
            Image knob = handle.gameObject.AddComponent<Image>();
            knob.sprite = SpriteOf(visuals != null ? visuals.sliderHandle : null);
            knob.preserveAspect = false;
            knob.color = Color.white;
            knob.raycastTarget = false;
            handle.gameObject.SetActive(value > .005f);
        }

        private void ImageBox(
            Texture2D texture,
            Transform parent,
            Vector2 omin,
            Vector2 omax,
            bool topAnchored = false,
            bool stretchWidth = false
        )
        {
            GameObject go = new GameObject("Preview", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform r = (RectTransform)go.transform;
            if (topAnchored)
            {
                r.anchorMin = new Vector2(0, 1);
                r.anchorMax = new Vector2(stretchWidth ? 1 : 0, 1);
            }
            else
            {
                r.anchorMin = Vector2.zero;
                r.anchorMax = new Vector2(0, 1);
            }
            r.offsetMin = omin;
            r.offsetMax = omax;
            Image image = go.GetComponent<Image>();
            image.sprite = SpriteOf(texture);
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        internal Sprite SpriteOf(Texture2D texture)
        {
            if (texture == null)
                return null;
            if (!sprites.TryGetValue(texture, out Sprite sprite))
            {
                sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(.5f, .5f),
                    100,
                    0,
                    SpriteMeshType.FullRect,
                    new Vector4(12, 12, 12, 12)
                );
                sprites[texture] = sprite;
            }
            return sprite;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        private static string FormatDuration(int minutes) =>
            minutes <= 0 ? "0 мин."
            : minutes >= 60 ? $"{minutes / 60} ч. {minutes % 60} мин."
            : $"{minutes} мин.";

        private static string StatusName(RoverStatus status)
        {
            switch (status)
            {
                case RoverStatus.Delivering:
                    return "В пути";
                case RoverStatus.Broken:
                    return "Авария";
                case RoverStatus.Charging:
                    return "Заряжается";
                default:
                    return "Готов";
            }
        }

        private static TextAlignmentOptions TmpAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                    return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter:
                    return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight:
                    return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft:
                    return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight:
                    return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft:
                    return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter:
                    return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight:
                    return TextAlignmentOptions.BottomRight;
                default:
                    return TextAlignmentOptions.Center;
            }
        }
    }
}
