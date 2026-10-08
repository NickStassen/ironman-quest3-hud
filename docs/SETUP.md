# Setup: build the HUD to a Quest 3

_Versions checked Oct 7, 2026 against Meta's official [Unity-PassthroughCameraApiSamples](https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples) (main branch) and the Unity package registry._

## What's pinned and why

| Package | Version | Source |
|---|---|---|
| Unity Editor | **6000.0.84f1** (Unity 6.0 LTS) | Latest 6000.0 LTS patch. Meta's PCA samples use 6000.0.66f2, but 66f2 doesn't launch on Ubuntu 26.04 (missing `libxml2.so.2`, [Unity issue 24671](https://issuetracker.unity.com/issues/24671/editor-fails-to-launch-with-a-missing-libxml2so2-error-when-starting-on-ubuntu-2604-lts), fixed from 6000.0.76f1). Meta Core/MRUK 85 and Inference Engine 2.2.1 accept any 6000.0. |
| `com.meta.xr.mrutilitykit` (MRUK) | **85.0.0** | Unity registry (packages.unity.com). Meta's PCA samples pin this exact version. |
| `com.meta.xr.sdk.core` | **85.0.0** | Unity registry. Dependency of MRUK 85.0.0. |
| `com.unity.ai.inference` (Inference Engine, ex-Sentis) | **2.2.1** | Unity registry. Same as the samples, so Meta's `yolov9sentis.sentis` loads. |
| `com.unity.xr.openxr` | **1.15.1** | Unity registry (same as samples). |
| `com.unity.xr.management` | **4.5.4** | Unity registry (same as samples). |

Notes:
- Meta's packages are now on the **standard Unity registry**, so no scoped registry or Asset Store step is needed. The older `npm.developer.oculus.com` scoped registry still exists but isn't used here.
- Newer versions exist (Meta 207.0.0 as of Sep 24, 2026; Inference Engine 2.6.1). They're untested with this code; upgrade later in a branch.
- The project uses Unity **OpenXR** (Meta deprecated the Oculus XR Plugin). It uses the **built-in render pipeline**, the same as Meta's PCA samples. URP is optional (see the end).
- Passthrough Camera API (PCA): **Quest 3 / 3S on Horizon OS v74 or later** (Meta docs). It has been public and Store-allowed since v76. The MRUK code checks for v74+. Update the headset to the latest OS anyway.
- Camera frames come from MRUK's `PassthroughCameraAccess` component. That is the current pattern in Meta's samples, which replaced the older `WebCamTexture` approach ([Meta migration guide](https://developers.meta.com/horizon/documentation/unity/unity-pca-migration-from-webcamtexture/)).

## 1. Install Unity (about 20 min, mostly download)

1. Install [Unity Hub](https://unity.com/download). On Ubuntu/Debian, from Unity's apt repo:
   ```bash
   sudo install -d /etc/apt/keyrings
   curl -fsSL https://hub.unity3d.com/linux/keys/public | sudo gpg --dearmor -o /etc/apt/keyrings/unityhub.gpg
   printf 'Types: deb\nURIs: https://hub.unity3d.com/linux/repos/deb\nSuites: stable\nComponents: main\nSigned-By: /etc/apt/keyrings/unityhub.gpg\n' | sudo tee /etc/apt/sources.list.d/unityhub.sources
   sudo apt update && sudo apt install unityhub
   ```
2. Sign in to Hub and add a free Personal license (Hub > Preferences > Licenses). Personal licenses can't be activated from the command line.
3. Install **6000.0.84f1** from the deep link `unityhub://6000.0.84f1/78ab6fc243d5`, or Hub > Installs > Install Editor > Archive.
4. Tick **Android Build Support** with **OpenJDK** and **Android SDK & NDK Tools**. Or from the command line:
   ```bash
   unityhub --headless install --version 6000.0.84f1 --changeset 78ab6fc243d5 --module android android-sdk-ndk-tools android-open-jdk
   ```
   This bundles JDK 17, the Android SDK/NDK and adb. Nothing else is needed system-wide.

### Linux notes
- **Run `./tools/patch-meta-sdk-linux.sh` once before opening the project.** Meta XR Core SDK 85.0.0 doesn't compile in the Linux Editor (`Installer.cs(88,42): error CS0103: The name 'downloadedInstallerPath' does not exist`). The script embeds 85.0.0 into `Packages/` (git-ignored) and applies the fix Meta shipped in 207.0.0. Windows and macOS don't need it.
- Meta officially supports only Windows and macOS for development. On Linux the project compiles and builds for Android, but Meta Horizon Link, the XR Simulator and Meta Quest Developer Hub aren't available. Play mode in the Editor does nothing, so every test is a **Build And Run** to the headset.
- Meta's own adb helpers (OVR Build APK & Run, and similar) look for `adb.exe` and silently do nothing on Linux. Unity's **Build And Run** works.
- In XR Plug-in Management, enable OpenXR on the **Android** tab only. Leave the Standalone (Linux) tab off; its OpenXR plugin is reported to crash the Linux Editor in Play mode.
- If Hub crashes at launch with "No usable sandbox", start it with `unityhub --no-sandbox`.

## 2. Get the model (1 min)

From the repo root:

```bash
./tools/get-model.sh
```

This downloads Meta's YOLOv9-t model (about 2.3 MB, 80 COCO classes, already in Inference Engine format) to `unity/IronManHUD/Assets/Resources/yolo.sentis`. It is git-ignored.

On Windows without bash, download [yolov9sentis.sentis](https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples/raw/main/Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model/yolov9sentis.sentis) by hand and save it as `unity/IronManHUD/Assets/Resources/yolo.sentis`.

Other models: an Ultralytics YOLOv8n or YOLO11n ONNX file also works (`yolo export model=yolo11n.pt format=onnx opset=15 imgsz=640`, about 10 MB). Drop it in `Assets/` and drag it onto **HudApp > Model**. The detector auto-detects the layout: 3 outputs is Meta format, 1 output `[1,84,N]` is Ultralytics format.

## 3. Open the project (5 to 15 min first time)

1. Unity Hub > Add > Add project from disk, and pick `unity/IronManHUD`.
2. Open it. Unity will generate `Library/` and the default ProjectSettings, then resolve the packages. The first import takes a while.
3. If you get a "Safe Mode" prompt, there are compile errors. Check the Console and see Troubleshooting.

## 4. Meta XR setup (5 min)

1. **File > Build Profiles** (or Build Settings) > **Android** > **Switch Platform**.
2. **Edit > Project Settings > XR Plug-in Management > Android tab**: tick **OpenXR**, then under OpenXR enable the **Meta XR** feature group. Do NOT tick "Oculus" (deprecated plugin).
3. **IronManHUD > Create Demo Scene** (menu added by this project). Do this **before** Fix All. It creates `Assets/Scenes/IronManHUD.unity` with the OVRCameraRig (passthrough enabled), a passthrough underlay and the `HudApp` component, assigns `Resources/yolo.sentis` if present, and adds the scene to the build list. It also sets Meta's project config: Passthrough Support Required, **Passthrough Camera Access** on, Scene Support Required, anchors on, targets Quest 3/3S. Without that, Meta's manifest regeneration (some Fix All rules, Meta > Tools > Update AndroidManifest) deletes `horizonos.permission.HEADSET_CAMERA` from our manifest, because the camera component is only created at runtime.
4. **Meta > Tools > Project Setup Tool**: select the Android tab, click **Fix All**, then **Apply All**. Repeat until nothing is red. This sets the minimum API level, ARM64/IL2CPP, Vulkan, color space, GameActivity, the OpenXR features and so on.
5. Check: select **OVRCameraRig** and confirm **OVRManager > Quest Features > General** shows Passthrough Support **Required**, Scene Support **Required** and **Enable Passthrough Camera Access** ticked. Then confirm `Assets/Plugins/Android/AndroidManifest.xml` still has `horizonos.permission.HEADSET_CAMERA`. If it's gone, re-add it (`git checkout` the file) and tick the box.
6. Optional: Player Settings > Company/Product name and package id, e.g. `com.nickstassen.ironmanhud`.
7. Commit the generated settings so a fresh clone doesn't start from Unity's defaults: `ProjectSettings/*.asset`, `Packages/packages-lock.json`, `Assets/XR/`, `Assets/Oculus/OculusProjectConfig.asset` and `Assets/Scenes/`.

Manual alternative to step 3: new empty scene, delete Main Camera, then add the **Camera Rig** and **Passthrough** Building Blocks (Meta > Tools > Building Blocks), and an empty GameObject with **HudApp**. `HudApp` also auto-creates itself in any scene that has an OVRCameraRig.

### Command-line alternative (handy on Linux)
Steps 1 to 4 and the build can also run headless with `Assets/Scripts/Editor/IronManHudBatch.cs`. Close the Editor first; an open Editor locks the project.
```bash
UNITY=~/Unity/Hub/Editor/6000.0.84f1/Editor/Unity
PROJ=unity/IronManHUD
# OpenXR loader on Android, Create Demo Scene, Fix All. No -quit: it exits by itself when the fixes are done.
"$UNITY" -batchmode -nographics -projectPath $PROJ -buildTarget Android -executeMethod IronManHud.EditorTools.IronManHudBatch.Setup -logFile setup.log
# Build to unity/IronManHUD/Builds/IronManHUD.apk
"$UNITY" -batchmode -nographics -quit -projectPath $PROJ -buildTarget Android -executeMethod IronManHud.EditorTools.IronManHudBatch.BuildApk -logFile build.log
adb install -r $PROJ/Builds/IronManHUD.apk
adb shell am start -n com.DefaultCompany.IronManHUD/com.unity3d.player.UnityPlayerGameActivity
```
On Linux, Fix All logs an `ArgumentException: Invalid path` from the Meta XR Simulator download task. The Simulator doesn't exist for Linux; ignore it.

## 5. Headset (first time only)

1. Phone app (Meta Horizon) > Devices > your Quest 3 > Developer Mode: **on** (needs a developer org at developers.meta.com).
2. Reboot the headset. Settings > System > Software update: install the latest. Note the OS version.
3. Plug in a **USB-C data cable**. In the headset, accept **Allow USB debugging** (tick "Always allow").
4. Check: `adb devices` should list the headset as `device`. Unity bundles adb at `<Unity>/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb`.
5. On Linux, USB access needs a udev rule for Meta's vendor id `2833`. `sudo apt install android-udev-rules` covers the Quest 3. Don't also install the distro `adb` package: its older adb server fights with Unity's bundled one. Put Unity's adb on your PATH instead, for example `ln -s ~/Unity/Hub/Editor/6000.0.84f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb ~/.local/bin/adb`.

## 6. Build and run

1. File > Build Profiles > Android > **Run Device**: pick the Quest > **Build And Run**. The first build takes 5 to 15 minutes.
2. In the headset, two permission prompts appear: **camera** (headset cameras) and **spatial data**. Allow both.
3. You should see passthrough with the cyan HUD frame, a clock, FPS and the status line. Point at a chair, cup, laptop, bottle or person.

Controls:
- **Right trigger**: repulsor beam plus haptics. A target the beam crosses turns red and shows LOCK.
- **X** (left): toggle the timing overlay.
- **Y** (left): switch inference CPU / GPUCompute.

Logs:
```bash
adb logcat -s Unity | grep -E "IronManHud|PassthroughCamera|EnvironmentRaycast|InferenceEngine"
adb shell ls /sdcard/Android/data/<package id>/files/
adb pull /sdcard/Android/data/<package id>/files/ ./latency/
```

## Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| Black background instead of passthrough | OVRManager > Passthrough Support not enabled, or `isInsightPassthroughEnabled` off. Re-run IronManHUD > Create Demo Scene and the Project Setup Tool. |
| HUD says "Camera permission not granted" | Headset: Settings > Apps > Installed > IronManHUD > Permissions > allow camera. Or `adb shell pm grant <package> horizonos.permission.HEADSET_CAMERA`. |
| "Passthrough Camera API not supported" | Horizon OS older than v74 or not a Quest 3/3S. Update the OS. |
| "Passthrough camera failed to start" | Check logcat for `PassthroughCameraAccess`. Make sure `Assets/Plugins/Android/AndroidManifest.xml` is in the build (it declares `horizonos.permission.HEADSET_CAMERA`). If Meta's tools regenerated the manifest, re-add that permission. |
| "No YOLO model" message | Run `tools/get-model.sh`, or assign a model on HudApp. Rebuild. |
| Distances always `--` | Spatial-data (scene) permission denied, or depth not ready yet (it takes a few frames). The status shows `DEPTH --` until `EnvironmentRaycastManager` is enabled. In Editor over Link (Windows only), enable Link's "Spatial Data over Meta Quest Link" beta. |
| Compile errors mentioning `PassthroughCameraAccess` or `EnvironmentRaycastManager` | Package versions drifted. Check Packages/manifest.json still says 85.0.0 for both Meta packages. |
| Compile errors mentioning `Unity.InferenceEngine` | Inference Engine must be 2.x (namespace `Unity.InferenceEngine`). With Sentis 1.x/2.1 the namespace is `Unity.Sentis`. |
| `TextureTransform.SetDimensions is obsolete` warning | Not used here; harmless if you see it from other code. |
| Big stall at startup | Expected: the first inference compiles the model ("Warming up detector..."). |
| Low FPS while detecting | Lower `HudApp > Max Detection Hz` (default 8), try Y to swap CPU/GPU, or request a smaller camera resolution (e.g. 640x480). |
| Boxes offset from objects | Expected to a degree; PCA FOV is narrower than passthrough. Write down how far off and at what distance. |
| Unity Editor won't start: `libxml2.so.2: cannot open shared object file` | Ubuntu 26.04 with a 6000.0 Editor older than 76f1. Use the pinned 6000.0.84f1. |
| IL2CPP build fails with `clang++: No such file or directory` (Linux) | Broken symlinks in the bundled NDK. List them with `find <Unity>/Editor/Data/PlaybackEngines/AndroidPlayer/NDK/toolchains/llvm/prebuilt/linux-x86_64/bin -xtype l` and re-link with `ln -sf`. |
| Model output errors in logcat | The model layout isn't Meta 3-output or Ultralytics `[1,84,N]`. Use the Meta model first. |

## Optional: URP

The code uses only uGUI and `Sprites/Default`, which render in both Built-in and URP. To switch: install **Universal RP** from the Package Manager, create a URP Asset (Create > Rendering > URP Asset (with Universal Renderer)), assign it in Project Settings > Graphics and Quality, then run the Project Setup Tool again. Not needed tonight.

## Credits

- Camera, inference and raycast patterns follow Meta's [Unity-PassthroughCameraApiSamples](https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples) (MIT), especially `SentisInferenceRunManager`, `SentisInferenceUiManager` and `EnvironmentRayCastSampleManager`. The AndroidManifest is adapted from the same repo.
- The default model is Meta's `yolov9sentis.sentis` from that repo (downloaded, not redistributed here).
