using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoonDelivery
{
    public sealed partial class MoonCanvasMap
    {
        private Color ColorFor(TerrainType t) =>
            t == TerrainType.Rocks ? new Color(.85f, .36f, .3f)
            : t == TerrainType.Crater ? new Color(.85f, .65f, .25f)
            : t == TerrainType.Rough ? new Color(.5f, .62f, .75f)
            : new Color(.25f, .75f, .9f);

        private static RectTransform Layer(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.sizeDelta = WorldSize;
            return r;
        }

        private static RawImage MakeRaw(string name, Transform parent, Texture texture)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            RawImage image = go.GetComponent<RawImage>();
            image.texture = texture;
            return image;
        }

        private static Image MakeImage(string name, Transform parent, Sprite sprite)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            return image;
        }

        private static TMP_Text MakeText(string value, Transform parent, int size)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static void Place(RectTransform r, Vector2 position, Vector2 size)
        {
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(.5f, .5f);
            r.anchoredPosition = position;
            r.sizeDelta = size;
        }

        private static void StretchLayer(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        private static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }
    }
}
