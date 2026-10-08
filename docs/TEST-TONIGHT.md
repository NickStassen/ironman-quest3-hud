# Test checklist: first on-device run (30 to 45 min)

Have a tape measure, a phone (for notes and a slow-mo video), and 3 to 4 objects: a chair, a cup or bottle, a laptop, and optionally a person.

Write the numbers into `docs/MEASUREMENTS.md` (create it) or just a note, and commit them later.

## 0. Record the setup (2 min)
- [ ] Horizon OS version (Settings > System > About): ______
- [ ] Unity version: ______  Meta SDK: 85.0.0  Inference Engine: 2.2.1
- [ ] Model: yolov9sentis (Meta) / other: ______
- [ ] Camera resolution shown in status line (`CAM WxH`): ______

## 1. Boot and fallback (5 min)
- [ ] App launches into passthrough with the HUD frame, clock, FPS and status line.
- [ ] Permission prompts appeared, and both were allowed.
- [ ] HUD follows lazily when you turn your head (it shouldn't feel glued to your face).
- [ ] Fallback check (optional): build once without `yolo.sentis`. The HUD still runs and says "No YOLO model".
- Notes on comfort or readability: ______

## 2. Frame rate and inference timing (10 min)
Press **X** to show the timing overlay. Look at a cluttered desk for about 60 s per configuration.

| Config | App FPS | frame ms p50/p95 | infer ms p50/p95 | grab->place ms p50/p95 | capture age ms p50/p95 | det Hz |
|---|---|---|---|---|---|---|
| CPU, 8 Hz cap (default) | | | | | | |
| GPUCompute (press **Y**) | | | | | | |
| Best of the two, 15 Hz cap (Inspector: Max Detection Hz, needs a rebuild) | | | | | | |

- "capture age" = time from camera capture to the overlay update. If it shows `--`, the PCA timestamp isn't wall-clock on-device; use grab->place plus about 20 to 40 ms (Meta's quoted capture latency) and note that.
- [ ] Does it feel live or laggy? ______

## 3. Detection quality (5 min)
- [ ] Which classes show up reliably: chair / cup / bottle / laptop / tv / person / other: ______
- [ ] False positives seen: ______
- [ ] Track IDs (`#N`) stay the same on a static object for 30 s? ID switches per minute: ______
- [ ] Brackets flicker? (yes / a little / no) ______

## 4. Distance accuracy (10 min)
Stand still and put an object at measured distances from the headset front. Read the `x.xx m` label. The label measures from the centre-eye point, which sits a few cm behind the headset front, so expect a small constant offset and note it.

| Object | 0.5 m | 1.0 m | 2.0 m | 3.0 m |
|---|---|---|---|---|
| Chair | | | | |
| Bottle/cup | | | | |
| Laptop | | | | |

- [ ] Miss rate: how often the label shows `-- m` (no depth hit): ______
- [ ] `depth hit/miss` and status on the overlay when it misses: ______

## 5. World lock (5 min)
- [ ] Turn your head about 90 degrees and back at normal speed. How far do the brackets drift off the object (cm, by eye)? ______
- [ ] Walk 1 to 2 m to the side. Do brackets stay on the object? ______

## 6. Repulsor v0 (5 min)
- [ ] Right trigger shows a beam from the controller with a haptic kick on press.
- [ ] Aim at a detected object: brackets turn red, status shows `LOCK #N CLASS`, light continuous rumble.
- [ ] Lock success at 1 m over 10 tries: __/10. At 3 m: __/10.

## 7. Pull the log
```bash
adb pull /sdcard/Android/data/<package id>/files/ ./latency/
```
CSV columns: `t_s, backend, detections, preprocess_ms, inference_ms, postprocess_ms, pipeline_ms, capture_age_ms, app_frame_ms`.

## Where to look first if something breaks
1. Compile errors on first open: see docs/SETUP.md Troubleshooting (package versions).
2. Black screen instead of passthrough: OVRManager Passthrough Support + Project Setup Tool.
3. No boxes: status line `CAM --` (camera permission), `YOLO off` (model), `TARGETS 0` with `dets > 0` (depth or pose), and logcat.
