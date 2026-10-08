using UnityEngine;

namespace IronManHud
{
    /// <summary>
    /// Right Touch (Plus) controller via OVRInput. Index trigger charges and fires, B switches trigger / palm mode,
    /// thumbstick click aligns the palm emitter (hold to reset). Haptics through SetControllerVibration.
    /// </summary>
    public class TouchRepulsorInput : IRepulsorInput
    {
        private const OVRInput.Controller Hand = OVRInput.Controller.RTouch;
        private readonly Transform _controllerAnchor;
        private readonly Transform _trackingSpace;
        private float _hapticTimeLeft;

        /// <param name="controllerAnchor">OVRCameraRig.rightControllerAnchor (world-space pose of the controller).</param>
        /// <param name="trackingSpace">OVRCameraRig.trackingSpace (OVRInput velocities are in this space).</param>
        public TouchRepulsorInput(Transform controllerAnchor, Transform trackingSpace)
        {
            _controllerAnchor = controllerAnchor;
            _trackingSpace = trackingSpace;
        }

        public string Name => "Touch (right)";

        public bool IsTracked => _controllerAnchor != null && OVRInput.IsControllerConnected(Hand);

        public Pose AimPose => _controllerAnchor != null
            ? new Pose(_controllerAnchor.position, _controllerAnchor.rotation)
            : new Pose(Vector3.zero, Quaternion.identity);

        public Vector3 Velocity
        {
            get
            {
                Vector3 local = OVRInput.GetLocalControllerVelocity(Hand);
                return _trackingSpace != null ? _trackingSpace.TransformDirection(local) : local;
            }
        }

        public bool FireHeld => OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, Hand) > 0.55f;

        public bool FirePressedThisFrame => OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, Hand);

        public bool CalibratePressedThisFrame => OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, Hand);

        public bool CalibrateHeld => OVRInput.Get(OVRInput.Button.PrimaryThumbstick, Hand);

        public bool ModeTogglePressedThisFrame => OVRInput.GetDown(OVRInput.Button.Two, Hand);

        public void Pulse(float amplitude, float seconds)
        {
            OVRInput.SetControllerVibration(1f, Mathf.Clamp01(amplitude), Hand);
            _hapticTimeLeft = Mathf.Max(_hapticTimeLeft, seconds);
        }

        public void Tick(float deltaTime)
        {
            if (_hapticTimeLeft > 0f)
            {
                _hapticTimeLeft -= deltaTime;
                if (_hapticTimeLeft <= 0f)
                {
                    OVRInput.SetControllerVibration(0f, 0f, Hand);
                }
            }
        }
    }
}
