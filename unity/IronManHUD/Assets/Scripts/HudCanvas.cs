using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace IronManHud
{
    /// <summary>
    /// Iron Man style HUD on a world-space canvas. By default it is head-locked (fixed to the display, like a
    /// helmet visor); with HeadLocked off it lazily follows the head in world space instead.
    /// </summary>
    public class HudCanvas : MonoBehaviour
    {
        [Tooltip("Fixed to the display (moves exactly with the head). Off = lazily follows the head in world space.")]
        public bool HeadLocked = true;
        [Tooltip("Distance of the HUD plane in front of the eyes (m).")]
        public float Distance = 1.2f;
        [Tooltip("Horizontal field of view the HUD frame spans (degrees). Everything on it scales with this.")]
        public float WidthDeg = 70f;
        [Tooltip("Radius of the visor curve (m); the HUD wraps around the eyes. Equal to Distance = every element " +
                 "equidistant and facing the eye; larger = flatter; 0 = flat.")]
        public float CurveRadius = 1.6f;
        [Tooltip("HUD layout size in canvas units (the aspect ratio of the frame).")]
        public Vector2 SizeMm = new Vector2(900f, 560f);
        [Tooltip("Re-center when the head turns more than this many degrees away from the HUD.")]
        public float RecenterAngle = 12f;
        [Tooltip("Follow speed (higher = snappier).")]
        public float FollowSpeed = 4f;

        private Transform _head;
        private Canvas _canvas;
        private Text _clock;
        private Text _fps;
        private Text _status;
        private Text _debug;
        private Text _centerMessage;
        private Image _reticleDot;
        private bool _following;
        private float _fpsSmoothed;

        public bool DebugVisible
        {
            get => _debug != null && _debug.gameObject.activeSelf;
            set { if (_debug != null) _debug.gameObject.SetActive(value); }
        }

        public void Init(Transform head)
        {
            _head = head;
            _canvas = UiFactory.CreateWorldCanvas("HUD Canvas", SizeMm, transform);
            var root = _canvas.transform;

            UiFactory.AddCornerBrackets(root, UiFactory.HudCyan, 90f, 5f);

            // Reticle: four ticks around a centre dot.
            var reticle = UiFactory.CreateRect("Reticle", root);
            reticle.sizeDelta = new Vector2(60f, 60f);
            _reticleDot = UiFactory.CreateImage("Dot", reticle, UiFactory.HudCyan, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5f, 5f));
            UiFactory.CreateImage("TickTop", reticle, UiFactory.HudCyan, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(3f, 16f));
            UiFactory.CreateImage("TickBottom", reticle, UiFactory.HudCyan, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(3f, 16f));
            UiFactory.CreateImage("TickLeft", reticle, UiFactory.HudCyan, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(16f, 3f));
            UiFactory.CreateImage("TickRight", reticle, UiFactory.HudCyan, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(16f, 3f));

            _clock = UiFactory.CreateText("Clock", root, 26, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(300f, 40f), UiFactory.HudCyan);
            _fps = UiFactory.CreateText("FPS", root, 26, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -18f), new Vector2(300f, 40f), UiFactory.HudCyan);
            _status = UiFactory.CreateText("Status", root, 22, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(760f, 60f), UiFactory.HudCyan);
            _debug = UiFactory.CreateText("Debug", root, 16, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 84f), new Vector2(560f, 200f), new Color(0.75f, 1f, 1f, 0.9f));
            _centerMessage = UiFactory.CreateText("CenterMessage", root, 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(760f, 140f), UiFactory.HudAmber);
            _centerMessage.gameObject.SetActive(false);

            // Scale the whole layout so the frame spans WidthDeg at Distance, then bend it onto the visor curve.
            float halfAngle = 0.5f * WidthDeg * Mathf.Deg2Rad;
            float halfWidthM = CurveRadius > 0f ? ArcHalfWidth(Distance, CurveRadius, halfAngle) : Distance * Mathf.Tan(halfAngle);
            float metresPerUnit = 2f * halfWidthM / SizeMm.x;
            root.localScale = Vector3.one * metresPerUnit;
            if (CurveRadius > 0f)
            {
                foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                {
                    var curve = graphic.gameObject.AddComponent<CurvedHudVertex>();
                    curve.CanvasRoot = (RectTransform)root;
                    curve.RadiusUnits = CurveRadius / metresPerUnit;
                }
            }

            if (HeadLocked && _head != null)
            {
                root.SetParent(_head, false);
                root.localPosition = new Vector3(0f, 0f, Distance);
                root.localRotation = Quaternion.identity;
            }
            else
            {
                SnapToHead();
            }
        }

        public void SetStatus(string text) { if (_status != null) _status.text = text; }
        public void SetDebug(string text) { if (_debug != null) _debug.text = text; }

        /// <summary>Shows a prominent message (e.g. missing permission/model). Pass null or empty to hide.</summary>
        public void SetCenterMessage(string text, bool isError = false)
        {
            if (_centerMessage == null)
            {
                return;
            }
            bool show = !string.IsNullOrEmpty(text);
            _centerMessage.gameObject.SetActive(show);
            if (show)
            {
                _centerMessage.text = text;
                _centerMessage.color = isError ? UiFactory.HudRed : UiFactory.HudAmber;
            }
        }

        public void SetReticleLocked(bool locked)
        {
            if (_reticleDot != null)
            {
                _reticleDot.color = locked ? UiFactory.HudRed : UiFactory.HudCyan;
            }
        }

        /// <summary>
        /// Half arc length (m) of a cylinder of radius r that touches the HUD plane at distance d, such that its
        /// edge is seen from the eye at halfAngle (rad). For r = d this is just d * halfAngle.
        /// </summary>
        private static float ArcHalfWidth(float d, float r, float halfAngle)
        {
            float lo = 0f, hi = 0.5f * Mathf.PI;
            for (int i = 0; i < 30; i++)
            {
                float mid = 0.5f * (lo + hi);
                float seen = Mathf.Atan2(r * Mathf.Sin(mid), d - r * (1f - Mathf.Cos(mid)));
                if (seen < halfAngle) lo = mid; else hi = mid;
            }
            return r * 0.5f * (lo + hi);
        }

        private void SnapToHead()
        {
            if (_head == null || _canvas == null)
            {
                return;
            }
            var t = _canvas.transform;
            t.position = _head.position + _head.forward * Distance;
            t.rotation = Quaternion.LookRotation(t.position - _head.position, Vector3.up);
        }

        private void LateUpdate()
        {
            if (_head == null || _canvas == null)
            {
                return;
            }

            // Clock + FPS
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
            {
                float fps = 1f / dt;
                _fpsSmoothed = _fpsSmoothed <= 0f ? fps : Mathf.Lerp(_fpsSmoothed, fps, 0.1f);
            }
            _clock.text = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            _fps.text = string.Format(CultureInfo.InvariantCulture, "{0:0} FPS", _fpsSmoothed);

            if (HeadLocked)
            {
                return; // parented to the head in Init
            }

            // Lazy follow: only start moving when the head has turned away far enough.
            var t = _canvas.transform;
            Vector3 toHud = t.position - _head.position;
            float angle = Vector3.Angle(_head.forward, toHud);
            if (angle > RecenterAngle || Mathf.Abs(toHud.magnitude - Distance) > 0.3f)
            {
                _following = true;
            }
            if (_following)
            {
                Vector3 targetPos = _head.position + _head.forward * Distance;
                Quaternion targetRot = Quaternion.LookRotation(targetPos - _head.position, Vector3.up);
                float k = 1f - Mathf.Exp(-FollowSpeed * dt);
                t.position = Vector3.Lerp(t.position, targetPos, k);
                t.rotation = Quaternion.Slerp(t.rotation, targetRot, k);
                if (Vector3.Angle(_head.forward, t.position - _head.position) < 2f)
                {
                    _following = false;
                }
            }
        }
    }
}
