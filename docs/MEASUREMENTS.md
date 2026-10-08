# Measurements

Numbers from on-device runs. Method and checklist: [TEST-TONIGHT.md](TEST-TONIGHT.md).

## Run 1: first on-device run (Oct 7, 2026)

### Setup
- Quest 3, Horizon OS v207 (`ro.vros.build.version` 207, system UX 207.0.0.297), Android 14 / API 34.
- Unity 6000.0.84f1, built-in RP, OpenXR. Built headless on Linux (`IronManHudBatch`).
- Meta XR Core SDK and MRUK 85.0.0 (Core carries the Linux-Editor compile fix from `tools/patch-meta-sdk-linux.sh`; runtime code unchanged). Inference Engine 2.2.1.
- Model: Meta `yolov9sentis.sentis` (pinned commit), 640x640 input, 3 outputs, NMS on CPU.
- Camera: PCA left camera, 1280x960 (status line `CAM 1280x960`).
- Backend: CPU. Max Detection Hz 8 (not reached; the pipeline limits it).

### Timing (CPU backend, 960 detection frames over about 5 minutes, indoor room)

| Metric | p50 | p95 |
|---|---|---|
| App frame (ms) | 13.8 | 14.6 |
| Preprocess (ms) | 45.5 | 46.6 |
| Inference (ms) | 176.8 | 190.9 |
| NMS (ms) | 0.0 | 0.0 |
| Grab -> overlay placed (ms) | 222.3 | 236.7 |
| Capture -> overlay, capture age (ms) | 256.1 | 274.4 |

- Detection rate: 4.5 Hz (median interval). Detections per frame: p50 2.
- App frame rate held 72 FPS while detecting: inference doesn't block rendering.
- PCA `Timestamp` is wall-clock on device, so capture age is a real measurement (not the `--` fallback).
- Preprocess is wall-clock and includes the few frames spent waiting for the input readback on the CPU backend; it isn't main-thread time. Inference is quantised to whole frames because completion is polled once per frame.

### Observations (60 s headset capture)
- Classes seen: tv, laptop, person, couch, chair, bottle. Distance labels from 0.36 m to 1.96 m; depth hits on most frames.
- Repulsor v0 works: the beam turns targets red and the status shows `LOCK #N TV`.
- The HUD covered only about 40 degrees and lagged behind head turns (lazy follow). Changed to head-locked, then to a 70-degree visor curve (1.6 m radius). The README captures used the flat 64-degree head-locked version.
- One monitor sometimes gets both a `TV` and a `LAPTOP` label (NMS is per class), and occasionally two `TV` tracks.
- A hand close to the headset is detected as `person` at about 0.36 m.

### Not measured yet
- GPUCompute backend (press Y), distance accuracy against a tape measure, world-lock drift on head turns, ID switches per minute, lock success rate.

## Run 2: repulsor v1 (Oct 7, 2026)

Same setup as run 1, plus the head-locked 70-degree curved HUD and repulsor v1. 28 s headset capture, trigger mode.

- Charge, lock, bolt, impact and hit reaction all work on device. Charge whine, guide line (red when locked), shockwave ring, sparks and `NEUTRALIZED` / `HIT xN` show as designed; HITS went from 2 to 6 in the clip.
- App frame time held 13.4 to 15.2 ms (72 FPS) with bolts, particles and the debug overlay on.
- Timing unchanged from run 1 (infer p50 about 190 ms, capture age p50 about 265 ms while the effects ran).
- False positives: `bird` (32 to 69%) and `kite` (39 to 45%) on a blank wall and ceiling corner at about 3 m. ScoreThreshold raised from 0.30 to 0.40.
- The status line wrapped when locked; it is now two lines (repulsor, then system).
- Palm-gesture mode and emitter calibration not yet tried on device.
