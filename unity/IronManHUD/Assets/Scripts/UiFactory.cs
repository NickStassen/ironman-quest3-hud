using UnityEngine;
using UnityEngine.UI;

namespace IronManHud
{
    /// <summary>Builds simple uGUI elements in code so the project needs no prefabs.</summary>
    public static class UiFactory
    {
        public static readonly Color HudCyan = new Color(0.35f, 0.9f, 1f, 0.95f);
        public static readonly Color HudAmber = new Color(1f, 0.75f, 0.2f, 0.95f);
        public static readonly Color HudRed = new Color(1f, 0.25f, 0.2f, 0.95f);

        private static Font _font;

        public static Font DefaultFont
        {
            get
            {
                if (_font == null)
                {
                    // Unity 2022.2+ ships "LegacyRuntime.ttf" as the built-in runtime font (Arial.ttf was removed).
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _font;
            }
        }

        /// <summary>World-space canvas where 1 canvas unit = 1 mm.</summary>
        public static Canvas CreateWorldCanvas(string name, Vector2 sizeMm, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 4f;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizeMm;
            rt.localScale = Vector3.one * 0.001f;
            return canvas;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image CreateImage(string name, Transform parent, Color color, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var rt = CreateRect(name, parent);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Vector2 size, Color color)
        {
            var rt = CreateRect(name, parent);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var text = rt.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Adds four L-shaped corner brackets that stretch with the parent rect.</summary>
        public static Image[] AddCornerBrackets(Transform parent, Color color, float armLength, float thickness)
        {
            var images = new Image[8];
            var corners = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0), new Vector2(1, 1) };
            int i = 0;
            foreach (var c in corners)
            {
                // Horizontal arm
                images[i++] = CreateImage("BracketH", parent, color, c, c, Vector2.zero, new Vector2(armLength, thickness));
                // Vertical arm
                images[i++] = CreateImage("BracketV", parent, color, c, c, Vector2.zero, new Vector2(thickness, armLength));
            }
            return images;
        }
    }
}
