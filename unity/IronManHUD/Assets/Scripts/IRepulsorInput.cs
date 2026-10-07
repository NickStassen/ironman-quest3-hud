using UnityEngine;

namespace IronManHud
{
    /// <summary>
    /// Input source for the repulsor. Today: the right Touch controller (<see cref="TouchRepulsorInput"/>).
    /// Later: a nicklink BLE prop (Phase 4) can implement this too (buttons/LEDs over BLE, pose still from the
    /// Touch controller mounted in the shell). Nothing BLE-related is compiled in yet.
    /// </summary>
    public interface IRepulsorInput
    {
        string Name { get; }
        bool IsTracked { get; }
        /// <summary>World-space pose of the palm emitter; forward = beam direction.</summary>
        Pose AimPose { get; }
        bool FireHeld { get; }
        bool FirePressedThisFrame { get; }
        /// <summary>Haptic / feedback pulse. amplitude 0..1.</summary>
        void Pulse(float amplitude, float seconds);
        void Tick(float deltaTime);
    }
}
