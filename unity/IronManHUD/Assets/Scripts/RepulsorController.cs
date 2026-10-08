using System.Globalization;
using UnityEngine;

namespace IronManHud
{
    /// <summary>
    /// Repulsor v1 (Phase 3). Hold to charge (whine, palm glow, rising haptics), release to fire a bolt at the locked
    /// target, or straight ahead to the first real surface the depth raycast finds. Impact: flash, shockwave, sparks,
    /// sound, a haptic kick and a hit reaction on the target's brackets.
    ///
    /// Fire modes (B toggles):
    ///  - Trigger: hold the index trigger to charge, release to fire.
    ///  - PalmGesture, for the open-hand palm-emitter prop: charges while the arm is out in front with the palm pointing
    ///    roughly where you look, and fires on a forward thrust of the hand. The trigger still works in this mode.
    ///
    /// The emitter direction relative to the controller is calibrated at runtime: point the palm (or the controller)
    /// where you look and click the right thumbstick; hold the click to reset. Saved between runs.
    /// </summary>
    public class RepulsorController : MonoBehaviour
    {
        public enum FireMode { Trigger, PalmGesture }

        [Header("Emitter")]
        [Tooltip("Emitter position in controller space (m). Default: 5 cm ahead of a hand-held controller.")]
        public Vector3 EmitterLocalPosition = new Vector3(0f, 0f, 0.05f);
        public FireMode Mode = FireMode.Trigger;

        [Header("Charge and blast")]
        public float ChargeSeconds = 0.8f;
        [Tooltip("A quick tap still fires a small bolt at this charge.")]
        public float MinCharge = 0.2f;
        public float CooldownSeconds = 0.3f;
        public float BoltSpeed = 14f;
        public float MaxRange = 8f;
        [Tooltip("A target within this angle of the emitter axis (plus its own angular radius) is locked, degrees.")]
        public float LockConeDeg = 6f;

        [Header("Palm gesture mode")]
        [Tooltip("Charges while the palm points within this angle of the view direction (degrees).")]
        public float GestureAimConeDeg = 35f;
        [Tooltip("...and the emitter is at least this far in front of the eyes (m), i.e. the arm is raised and out.")]
        public float GestureMinReach = 0.4f;
        [Tooltip("Forward hand speed along the palm axis that fires (m/s).")]
        public float ThrustSpeed = 1.2f;

        public Color BeamColor = new Color(0.55f, 0.95f, 1f, 1f);

        private const string CalibrationKey = "IronManHud.EmitterRotation";
        private const float ResetHoldSeconds = 1.5f;

        private IRepulsorInput _input;
        private TargetTracker _tracker;
        private Transform _head;
        private Quaternion _emitterLocalRotation = Quaternion.identity;
        private LineRenderer _guide;
        private SpriteRenderer _palmGlow;
        private AudioSource _whine;
        private bool _charging;
        private bool _chargedByTrigger;
        private float _charge;
        private float _cooldown;
        private float _flash;
        private float _calibrateHeldFor;
        private string _notice;
        private float _noticeUntil;

        public TargetTracker.Target LockedTarget { get; private set; }
        public Pose EmitterPose { get; private set; }
        public bool IsCharging => _charging;
        public float Charge => _charge;
        public int ShotsFired { get; private set; }
        public int Hits { get; private set; }
        /// <summary>Short-lived message for the HUD (calibration, mode switch), or null.</summary>
        public string Notice => Time.time < _noticeUntil ? _notice : null;

        public void Init(IRepulsorInput input, TargetTracker tracker, Transform head)
        {
            _input = input;
            _tracker = tracker;
            _head = head;
            LoadCalibration();

            _guide = gameObject.AddComponent<LineRenderer>();
            _guide.positionCount = 2;
            _guide.useWorldSpace = true;
            _guide.numCapVertices = 2;
            _guide.widthCurve = new AnimationCurve(new Keyframe(0f, 0.006f), new Keyframe(1f, 0.002f));
            _guide.material = RepulsorFx.LineMaterial;
            _guide.enabled = false;

            _palmGlow = RepulsorFx.CreateGlow("PalmGlow", transform, BeamColor);
            _palmGlow.enabled = false;

            _whine = gameObject.AddComponent<AudioSource>();
            _whine.clip = RepulsorAudio.Whine;
            _whine.loop = true;
            _whine.playOnAwake = false;
            _whine.spatialBlend = 1f;
            _whine.minDistance = 0.3f;
            _whine.volume = 0f;
        }

        private void Update()
        {
            if (_input == null)
            {
                return;
            }
            float dt = Time.deltaTime;
            _input.Tick(dt);
            HandleModeAndCalibration(dt);

            Pose controller = _input.AimPose;
            Vector3 origin = controller.position + controller.rotation * EmitterLocalPosition;
            Quaternion rotation = controller.rotation * _emitterLocalRotation;
            Vector3 forward = rotation * Vector3.forward;
            EmitterPose = new Pose(origin, rotation);
            transform.SetPositionAndRotation(origin, rotation);

            bool tracked = _input.IsTracked;
            LockedTarget = tracked && _tracker != null ? _tracker.FindLockTarget(new Ray(origin, forward), LockConeDeg, MaxRange) : null;
            if (_tracker != null)
            {
                _tracker.LockedTarget = LockedTarget;
            }

            if (_cooldown > 0f)
            {
                _cooldown -= dt;
            }

            bool triggerHeld = tracked && _input.FireHeld;
            bool palmReady = Mode == FireMode.PalmGesture && tracked && _head != null
                && Vector3.Angle(forward, _head.forward) < GestureAimConeDeg
                && Vector3.Dot(origin - _head.position, _head.forward) > GestureMinReach;

            if (_cooldown <= 0f && (triggerHeld || palmReady))
            {
                if (!_charging)
                {
                    _charging = true;
                    _charge = 0f;
                }
                _chargedByTrigger |= triggerHeld;
                _charge = Mathf.Min(1f, _charge + dt / ChargeSeconds);
                _input.Pulse(0.08f + 0.4f * _charge, 0.05f);
            }

            if (_charging)
            {
                if (_chargedByTrigger)
                {
                    if (!triggerHeld)
                    {
                        Fire(origin, forward);
                    }
                }
                else if (Vector3.Dot(_input.Velocity, forward) > ThrustSpeed && _charge >= MinCharge)
                {
                    Fire(origin, forward);
                }
                else if (!palmReady)
                {
                    // Palm dropped without a thrust: let the charge bleed off.
                    _charge -= dt * 2f;
                    if (_charge <= 0f)
                    {
                        ResetCharge();
                    }
                }
            }

            UpdateVisuals(dt, origin, forward);
        }

        private void Fire(Vector3 origin, Vector3 forward)
        {
            float charge = Mathf.Max(_charge, MinCharge);
            ResetCharge();
            _cooldown = CooldownSeconds;
            _flash = 1f;
            ShotsFired++;

            var target = LockedTarget;
            Vector3 end;
            bool surface = false;
            if (target != null)
            {
                end = target.Position;
            }
            else if (_tracker != null && _tracker.TryRaycastEnvironment(new Ray(origin, forward), MaxRange, out Vector3 hit))
            {
                end = hit;
                surface = true;
            }
            else
            {
                end = origin + forward * MaxRange;
            }

            RepulsorAudio.Play(RepulsorAudio.Blast, origin, 0.5f + 0.5f * charge);
            _input.Pulse(1f, 0.08f + 0.12f * charge);
            RepulsorBolt.Spawn(origin, end, BoltSpeed, charge, BeamColor, _head, () => OnImpact(target, end, surface, charge));
        }

        private void OnImpact(TargetTracker.Target target, Vector3 point, bool surface, float charge)
        {
            bool hitTarget = target != null && _tracker != null && _tracker.Contains(target);
            if (!hitTarget && !surface)
            {
                return; // flew out to max range
            }
            RepulsorFx.Impact(point, _head, BeamColor, charge);
            RepulsorAudio.Play(RepulsorAudio.Impact, point, 0.4f + 0.6f * charge);
            if (hitTarget)
            {
                _tracker.RegisterHit(target);
                Hits++;
                _input.Pulse(0.7f, 0.06f);
            }
        }

        private void ResetCharge()
        {
            _charging = false;
            _chargedByTrigger = false;
            _charge = 0f;
        }

        private void UpdateVisuals(float dt, Vector3 origin, Vector3 forward)
        {
            // Palm glow: grows while charging, flares on fire.
            float glow = _charging ? 0.03f + 0.09f * _charge : 0f;
            glow += 0.14f * _flash;
            _palmGlow.enabled = glow > 0.001f;
            if (_palmGlow.enabled)
            {
                _palmGlow.transform.localScale = Vector3.one * glow;
                if (_head != null)
                {
                    _palmGlow.transform.rotation = Quaternion.LookRotation(_palmGlow.transform.position - _head.position);
                }
            }
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 6f);

            // Faint aim guide while charging, red when locked.
            _guide.enabled = _charging;
            if (_charging)
            {
                Vector3 end = LockedTarget != null ? LockedTarget.Position : origin + forward * Mathf.Min(3f, MaxRange);
                var c = LockedTarget != null ? UiFactory.HudRed : BeamColor;
                c.a = 0.15f + 0.35f * _charge;
                _guide.startColor = c;
                _guide.endColor = new Color(c.r, c.g, c.b, 0f);
                _guide.SetPosition(0, origin);
                _guide.SetPosition(1, end);
            }

            // Charge whine: pitch and volume follow the charge.
            if (_charging)
            {
                if (!_whine.isPlaying)
                {
                    _whine.Play();
                }
                _whine.pitch = 0.7f + 1.3f * _charge;
                _whine.volume = 0.15f + 0.45f * _charge;
            }
            else if (_whine.isPlaying)
            {
                _whine.volume = Mathf.MoveTowards(_whine.volume, 0f, dt * 4f);
                if (_whine.volume <= 0f)
                {
                    _whine.Stop();
                }
            }
        }

        private void HandleModeAndCalibration(float dt)
        {
            if (_input.ModeTogglePressedThisFrame)
            {
                Mode = Mode == FireMode.Trigger ? FireMode.PalmGesture : FireMode.Trigger;
                ResetCharge();
                ShowNotice(Mode == FireMode.Trigger
                    ? "Repulsor: TRIGGER mode. Hold to charge, release to fire."
                    : "Repulsor: PALM mode. Raise your arm, palm where you look, to charge. Push forward to fire.");
            }

            if (_input.CalibratePressedThisFrame && _head != null && _input.IsTracked)
            {
                // Emitter forward := current view direction, expressed in controller space.
                _emitterLocalRotation = Quaternion.Inverse(_input.AimPose.rotation) * Quaternion.LookRotation(_head.forward, _head.up);
                SaveCalibration();
                ShowNotice("Repulsor aligned to your view. Hold the stick click to reset.");
            }

            if (_input.CalibrateHeld)
            {
                float before = _calibrateHeldFor;
                _calibrateHeldFor += dt;
                if (before < ResetHoldSeconds && _calibrateHeldFor >= ResetHoldSeconds)
                {
                    _emitterLocalRotation = Quaternion.identity;
                    SaveCalibration();
                    ShowNotice("Repulsor alignment reset to the controller's forward.");
                }
            }
            else
            {
                _calibrateHeldFor = 0f;
            }
        }

        private void ShowNotice(string text)
        {
            _notice = text;
            _noticeUntil = Time.time + 3f;
        }

        private void LoadCalibration()
        {
            var parts = PlayerPrefs.GetString(CalibrationKey, "").Split(',');
            if (parts.Length == 4
                && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)
                && float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float w))
            {
                _emitterLocalRotation = new Quaternion(x, y, z, w).normalized;
            }
        }

        private void SaveCalibration()
        {
            var q = _emitterLocalRotation;
            PlayerPrefs.SetString(CalibrationKey, string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", q.x, q.y, q.z, q.w));
            PlayerPrefs.Save();
        }

        private void OnDisable()
        {
            if (_guide != null)
            {
                _guide.enabled = false;
            }
            _input?.Pulse(0f, 0f);
        }
    }
}
