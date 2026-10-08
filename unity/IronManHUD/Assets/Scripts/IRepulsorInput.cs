using UnityEngine;

namespace IronManHud
{
    /// <summary>
    /// Input source for the repulsor. Today: the right Touch (Plus) controller (<see cref="TouchRepulsorInput"/>), held or
    /// strapped to the back of the hand in the palm-emitter prop. Later: a nicklink BLE prop (Phase 4) can implement this
    /// too (buttons/LEDs over BLE, pose still from the Touch controller in the shell). Nothing BLE-related is compiled in yet.
    /// </summary>
    public interface IRepulsorInput
    {
        string Name { get; }
        bool IsTracked { get; }
        /// <summary>World-space pose of the tracked controller. The repulsor applies its calibrated emitter offset on top.</summary>
        Pose AimPose { get; }
        /// <summary>World-space linear velocity of the controller (m/s), for the palm-thrust gesture.</summary>
        Vector3 Velocity { get; }
        bool FireHeld { get; }
        bool FirePressedThisFrame { get; }
        /// <summary>Align the emitter with the view direction (point the palm where you look, then press).</summary>
        bool CalibratePressedThisFrame { get; }
        /// <summary>Held for a while, resets the emitter alignment to the default.</summary>
        bool CalibrateHeld { get; }
        /// <summary>Switch between trigger and palm-gesture firing.</summary>
        bool ModeTogglePressedThisFrame { get; }
        /// <summary>Haptic / feedback pulse. amplitude 0..1.</summary>
        void Pulse(float amplitude, float seconds);
        void Tick(float deltaTime);
    }
}
