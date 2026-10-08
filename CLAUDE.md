# CLAUDE.md: agent briefing for ironman-quest3-hud

Read this first, then `README.md`, `docs/SETUP.md`, and `docs/TEST-TONIGHT.md`. `docs/PLAN.md` has the full phased plan and `docs/RESEARCH.md` has the background research.

## What this is
A mixed-reality Iron Man-style HUD for the Meta Quest 3, built in Unity. It runs YOLO on frames from the headset's passthrough camera, draws world-locked target brackets with distance labels, and lets a Touch controller act as a "repulsor" that fires at targets.

## Current state (read carefully)
- Phases 0 to 2 plus repulsor v0 are written in `unity/IronManHUD/Assets/Scripts/`, but **they have never been opened in a real Unity Editor or run on a headset.** They were only checked against hand-written API stubs, so expect compile errors on the first open. Fixing those is job #1.
- Pinned versions: Unity **6000.0.84f1** (bumped from 66f2, which doesn't launch on Ubuntu 26.04) with the built-in render pipeline, Meta XR Core SDK and MRUK **85.0.0**, and Unity Inference Engine **2.2.1** (namespace `Unity.InferenceEngine`, not `Unity.Sentis`). Don't bump versions to make errors go away unless a pinned version is actually broken. If you do bump one, update `docs/SETUP.md`.
- Model: run `tools/get-model.sh` to download Meta's YOLOv9-t into `Assets/Resources/yolo.sentis`. It's git-ignored, so never commit it.
- Scene: built in code by the menu item **IronManHUD > Create Demo Scene** (`Assets/Scripts/Editor/IronManHudSceneBuilder.cs`). Components are added at runtime by `HudApp`. If the scene is wrong, fix the builder instead of hand-editing a scene file.
- nicklink hardware isn't available yet. Repulsor input goes through `IRepulsorInput`, and only `TouchRepulsorInput` exists. Leave the BLE path stubbed.

## First session goals, in order
1. Open the project, resolve packages, and fix compile errors with the smallest correct changes. Keep the existing architecture.
2. Run the Project Setup Tool (Fix All). Confirm the passthrough setup and that `Assets/Plugins/Android/AndroidManifest.xml` still declares `horizonos.permission.HEADSET_CAMERA` after Meta's tools run.
3. Build And Run to the Quest 3. Work through `docs/TEST-TONIGHT.md` with the user and record the numbers in `docs/MEASUREMENTS.md`.
4. Fix what the on-device test shows. The likely trouble spots are listed under "Where to look first" in TEST-TONIGHT.md and in the SETUP.md Troubleshooting table.

## How to debug on device
- `adb devices`, then `adb logcat -s Unity PassthroughCameraAccess` (add other tags as needed).
- The on-headset status line reports `CAM`, `YOLO`, `DEPTH`, and `TARGETS`. Ask the user what it says. They're wearing the headset and can read it to you.
- The latency CSV is written to the app's files directory on the headset (see step 7 of TEST-TONIGHT.md).
- Controls: right trigger = beam, X = timing overlay, Y = switch between CPU and GPU inference.

## Repo rules
- **This repo is public.** Never add personal details: no names of employers or jobs, nothing about the owner's family, relationships, events, health, money, home location, or schedule. Keep docs strictly technical.
- The default branch is **master** (not main). Use small, focused commits with conventional prefixes (`fix(unity):`, `feat(hud):`, `docs:`). Never force-push or rewrite history.
- Don't commit generated Unity folders (`Library/`, `Temp/`, `Builds/`, `UserSettings/`), APKs, or models. `unity/IronManHUD/.gitignore` already covers these.
- Cross-link related repos with real links: [nicklink](https://github.com/NickStassen/nicklink), [deepstream-house-tracker](https://github.com/NickStassen/deepstream-house-tracker), and [jetson-nano-ov5647](https://github.com/NickStassen/jetson-nano-ov5647).
- When something is verified on device, update the Status section of `README.md` so it says what has actually been tested.
