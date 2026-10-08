using UnityEngine;
using UnityEngine.UI;

namespace IronManHud
{
    /// <summary>World-locked bracket + label for one target. 1 canvas unit = 1 mm.</summary>
    public class TargetMarker : MonoBehaviour
    {
        /// <summary>How long the hit reaction plays (s).</summary>
        public const float HitFxSeconds = 1.2f;

        private RectTransform _rect;
        private Image[] _brackets;
        private Text _label;
        private CanvasGroup _group;

        public static TargetMarker Create(Transform parent)
        {
            var canvas = UiFactory.CreateWorldCanvas("TargetMarker", new Vector2(300f, 300f), parent);
            var marker = canvas.gameObject.AddComponent<TargetMarker>();
            marker._rect = (RectTransform)canvas.transform;
            marker._group = canvas.gameObject.AddComponent<CanvasGroup>();
            marker._brackets = UiFactory.AddCornerBrackets(canvas.transform, UiFactory.HudCyan, 40f, 6f);
            marker._label = UiFactory.CreateText("Label", canvas.transform, 36, TextAnchor.LowerLeft,
                new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 8f), new Vector2(900f, 50f), UiFactory.HudCyan);
            marker._label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return marker;
        }

        /// <param name="locked">The repulsor is locked on this target: red, pulsing brackets.</param>
        /// <param name="hitAge">Seconds since the last repulsor hit (negative or large = none): flash, pop, NEUTRALIZED.</param>
        public void UpdateMarker(Vector3 position, Vector2 sizeM, Vector3 headPosition, string label, bool locked, float hitAge, float alpha)
        {
            transform.position = position;
            Vector3 fromHead = position - headPosition;
            if (fromHead.sqrMagnitude > 1e-6f)
            {
                transform.rotation = Quaternion.LookRotation(fromHead, Vector3.up);
            }
            float scale = 1f;
            var color = UiFactory.HudCyan;
            if (hitAge >= 0f && hitAge < HitFxSeconds)
            {
                float k = hitAge / HitFxSeconds;
                scale = 1f + 0.45f * Mathf.Sin(k * Mathf.PI);
                color = k < 0.12f ? Color.white : Color.Lerp(UiFactory.HudAmber, UiFactory.HudRed, k);
                label = "NEUTRALIZED  " + label;
                alpha = 1f;
            }
            else if (locked)
            {
                scale = 0.9f + 0.1f * Mathf.Sin(Time.time * 14f);
                color = UiFactory.HudRed;
            }
            _rect.sizeDelta = sizeM * 1000f * scale;

            // Keep the label readable regardless of distance: ~ constant angular size.
            float dist = fromHead.magnitude;
            float labelScale = Mathf.Clamp(dist / 1.5f, 0.5f, 3f);
            _label.rectTransform.localScale = Vector3.one * labelScale;
            _label.text = label;

            foreach (var b in _brackets)
            {
                b.color = color;
            }
            _label.color = color;
            _group.alpha = alpha;
        }
    }
}
