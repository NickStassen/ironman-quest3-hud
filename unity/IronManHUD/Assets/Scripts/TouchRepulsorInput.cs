using UnityEngine;

namespace IronManHud
{
    /// <summary>Right Touch (Plus) controller via OVRInput: index trigger fires, haptics through SetControllerVibration.</summary>
    public class TouchRepulsorInput : IRepulsorInput
    {
        private const OVRInput.Controller Hand = OVRInput.Controller.RTouch;
        private readonly Transform _controllerAnchor;
        private float _hapticTimeLeft;

        /// <param name="controllerAnchor">OVRCameraRig.rightControllerAnchor (world-space pose of the controller).</param>
        public TouchRepulsorInput(Transform controllerAnchor)
        {
            _controllerAnchor = controllerAnchor;
        }

        public string Name => "Touch (right)";

        public bool IsTracked => _controllerAnchor != null && OVRInput.IsControllerConnected(Hand);

        public Pose AimPose => _controllerAnchor != null
            ? new Pose(_controllerAnchor.position, _controllerAnchor.rotation)
            : new Pose(Vector3.zero, Quaternion.identity);

        public bool FireHeld => OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, Hand) > 0.55f;

        public bool FirePressedThisFrame => OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, Hand);

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
