using UnityEngine;

namespace IronManHud
{
    /// <summary>
    /// Repulsor v0: hold the trigger for a bright beam from the controller, with haptics.
    /// A detected target the beam passes near is highlighted (red brackets).
    /// </summary>
    public class RepulsorController : MonoBehaviour
    {
        public float MaxBeamLength = 8f;
        public float HitRadius = 0.12f;
        public Color BeamColor = new Color(0.55f, 0.95f, 1f, 1f);

        private IRepulsorInput _input;
        private TargetTracker _tracker;
        private LineRenderer _beam;
        private Light _glow;
        private float _flash;

        public TargetTracker.Target LastHit { get; private set; }
        public bool IsFiring { get; private set; }
        public int ShotsFired { get; private set; }

        public void Init(IRepulsorInput input, TargetTracker tracker)
        {
            _input = input;
            _tracker = tracker;

            _beam = gameObject.AddComponent<LineRenderer>();
            _beam.positionCount = 2;
            _beam.useWorldSpace = true;
            _beam.numCapVertices = 4;
            _beam.widthCurve = new AnimationCurve(new Keyframe(0f, 0.035f), new Keyframe(1f, 0.012f));
            // "Sprites/Default" is in the default Always Included Shaders list and renders in Built-in and URP.
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _beam.material = new Material(shader);
            }
            _beam.startColor = BeamColor;
            _beam.endColor = new Color(BeamColor.r, BeamColor.g, BeamColor.b, 0.2f);
            _beam.enabled = false;

            var glowGo = new GameObject("RepulsorGlow");
            glowGo.transform.SetParent(transform, false);
            _glow = glowGo.AddComponent<Light>();
            _glow.type = LightType.Point;
            _glow.range = 1.2f;
            _glow.color = BeamColor;
            _glow.intensity = 0f;
        }

        private void Update()
        {
            if (_input == null)
            {
                return;
            }
            _input.Tick(Time.deltaTime);

            IsFiring = _input.IsTracked && _input.FireHeld;
            if (_input.IsTracked && _input.FirePressedThisFrame)
            {
                ShotsFired++;
                _flash = 1f;
                _input.Pulse(0.9f, 0.12f);
            }

            var pose = _input.AimPose;
            Vector3 origin = pose.position + pose.rotation * new Vector3(0f, 0f, 0.05f);
            Vector3 dir = pose.rotation * Vector3.forward;
            _glow.transform.position = origin;

            if (!IsFiring)
            {
                _beam.enabled = false;
                LastHit = null;
                _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 6f);
                _glow.intensity = _flash * 2f;
                return;
            }

            float length = MaxBeamLength;
            float along = MaxBeamLength;
            LastHit = _tracker != null ? _tracker.RaycastTargets(new Ray(origin, dir), HitRadius, out along) : null;
            if (LastHit != null)
            {
                length = along;
                _tracker.Highlight(LastHit, 0.5f);
                // Light continuous rumble while locked on a target.
                _input.Pulse(0.35f, 0.05f);
            }

            float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 25f, 0f);
            _beam.enabled = true;
            _beam.SetPosition(0, origin);
            _beam.SetPosition(1, origin + dir * length);
            _beam.widthMultiplier = (1f + _flash) * flicker;
            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 4f);
            _glow.intensity = 1.5f + _flash * 2f;
        }

        private void OnDisable()
        {
            if (_beam != null)
            {
                _beam.enabled = false;
            }
            _input?.Pulse(0f, 0f);
        }
    }
}
