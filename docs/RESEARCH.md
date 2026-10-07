# Iron Man-style MR HUD on Meta Quest 3: Research Brief

_Web research as of Oct 6, 2026. Unmarked figures come from Meta or NVIDIA docs. [community] means a forum, blog or README figure, and [snippet] means a source seen only in search results. Anything not found is stated as unknown, not invented._

## TL;DR
The head-mounted Jetson Nano + OV5647 is probably unnecessary for v1. Meta's Passthrough Camera API (PCA) is now public and allowed in Store apps.
- It gives the headset's own RGB frames with intrinsics, a world-space camera pose and a timestamp per frame.
- So there is no external-camera calibration and no Jetson-to-Quest clock sync, which were the hardest parts of the original plan.
- Meta also ships an official sample that already goes camera → YOLO → depth raycast → 3D marker.

Recommended stack:
- Unity 6 + Meta XR Core SDK + MRUK (Mixed Reality Utility Kit).
- PCA for frames, with tiny on-device YOLO first, then offload to the RTX 5080 when bigger models or NvDCF tracking are wanted.
- `EnvironmentRaycastManager` for distance.
- A Touch Plus controller inside the printed repulsor for pose and trigger.
- nicklink over BLE (nRF52840) for LEDs and extras.

## Recommended architecture
1. **Quest app (Unity 6, Meta XR Core + MRUK):** passthrough, PCA frames, HUD rendering, raycasts, and the repulsor game logic.
2. **Detection, in two stages:**
   - **Stage A:** on-device YOLO using Meta's MultiObjectDetection sample (YOLOv9t uint8, about 2.3 MB, 80 COCO classes) via Unity Inference Engine (formerly Sentis).
   - **Stage B:** stream PCA frames over WebRTC to the RTX 5080, which runs YOLO with DeepStream 8.0+ and NvDCF. Version 8.0+ is needed for Blackwell; forum threads say 7.1 doesn't support RTX 50-series. Per-frame tracks come back over UDP.
   - Starting point for Stage B: `danieloquelis/Unity-QuestVisionStream`.
3. **Distance:**
   - Box centre → `PassthroughCameraAccess.ViewportPointToRay(uv)`, using the camera pose at frame time, not the current head pose.
   - Then `EnvironmentRaycastManager.Raycast` → hit point, and distance = |hit − head|.
   - Use a 3×3 grid of rays inside the box and take the median. That's an engineering suggestion, not Meta guidance.
4. **HUD:** overlays locked to world space, so head motion since capture cancels out. Effects: target lock, labels, distance readout.
5. **Repulsor:**
   - A Touch Plus controller mounted in the 3D print gives solid pose, trigger and haptics for free. Leave its IR LED areas uncovered.
   - nicklink (STM32F103 + LSM6DSV) adds an nRF52840 for BLE GATT notifications. It drives LEDs, sound and extra buttons, and can optionally send orientation.
6. **Measure first:** build a latency harness before any polish. The numbers that decide the Iron Man feel are unpublished: on-device YOLO FPS, offload round trip, and depth accuracy.

## 1) Engine choice
| | Unity + Meta XR SDK / MRUK | Unreal 5 + Meta XR plugin | Native OpenXR (C/C++) | WebXR (Quest Browser) |
|---|---|---|---|---|
| Passthrough | Passthrough Building Block | Yes | `XR_FB_passthrough` | `immersive-ar` (no pixel access) |
| Scene / room mesh | MRUK rooms, labels, EffectMesh | MRUK for Unreal | `XR_META_spatial_entity_mesh` | Plane/mesh detection, anchors |
| Hands | Interaction SDK | OpenXR Hand Tracking | Yes | WebXR Hand Input (25 joints) |
| Depth | Depth API + `EnvironmentRaycastManager` | `StartEnvironmentDepth` | `XR_META_environment_depth` | `depth-sensing`. Hit-test uses depth from Browser 40.4. |
| Camera (PCA) | Full: `PassthroughCameraAccess`, 5 official samples incl. YOLO | `UMRUKPassthroughCameraAccess` (Horizon OS v83+). No official sample found. | Android Camera2 + Meta vendor tags (not an OpenXR extension) | `getUserMedia` only. Static intrinsics, no per-frame pose. The WebXR camera-access spec isn't supported [forum]. |

**Pick Unity 6 + Meta XR Core + MRUK.**
- The C# is glue code; the heavy CV stays in Python/C++ on the desktop.
- PCA frames can be previewed in the Unity Editor over Meta Horizon Link v2.1+.
- Unity 6000.0.38f1+ is required. Meta deprecated the Oculus XR Plugin in favour of Unity OpenXR.
- Native OpenXR fits a C++ background, but you'd build your own renderer and UI, so it's the steepest path.
- WebXR is the fastest to prototype, but has no per-frame camera pose.

**Gotcha:** Meta's SDK version numbers jumped (one community repo pins 205.0.0), so older v7x/v8x tutorials won't match.

## 2) Passthrough Camera API (PCA)
- **Status:** experimental in March 2025, then Public and Store-allowed (with review) from SDK/OS v76.
- **Requirements:** Quest 3/3S and Horizon OS v74+ (Camera2 needs v76+).
- **Permissions:** `horizonos.permission.HEADSET_CAMERA` or `android.permission.CAMERA`.
- **Specs (Meta docs, Apr 2026):** 60 Hz, 20–40 ms capture latency, up to 1280×1280 on v83+, YUV420, about 1–2% GPU and about 45 MB per stream. The experimental-era figures were 1280×960 at 30 fps and 40–60 ms (UploadVR, Mar 2025; QuestCameraKit README).
- **Field of view:** smaller than the passthrough view. Even 1280×1280 doesn't cover the full view, so detections exist only in the central part of the HUD.
- **Per-frame metadata:** intrinsics, `GetCameraPose()` at the frame timestamp, `ViewportPointToRay`, `WorldToViewportPoint`, `Timestamp`, and both cameras.
- **On-device inference:** Unity Inference Engine on Quest doesn't use the NPU or any hardware acceleration and runs on the main thread. Meta recommends layer-by-layer inference, async readback and the smallest model; its small YOLO beat the medium one. No reliable Quest YOLO FPS figure is published, so measure it.
- **Can it replace the Jetson? Yes for v1.** You get pose and timestamp in the same world frame, no extra weight, heat or battery, and Store eligibility.
- **Reasons to keep an external camera:** a wider field of view, a special sensor (thermal, global shutter), or running fully off the headset's compute.

## 3) Depth and distance to detected objects
- **Unity:** `ViewportPointToRay` → MRUK `EnvironmentRaycastManager.Raycast`. From MRUK v81 no Depth API component is needed in the scene. Needs the `com.oculus.permission.USE_SCENE` permission.
  - Rays outside the depth camera's frustum return `HitPointOutsideOfCameraFrustum` or `NoHit`.
- **Native:** `xrAcquireEnvironmentDepthImageMETA`.
- **WebXR:** `getDepthInMeters(x, y)`.
- **Limits:**
  - Minimum range about 0.2 m (Meta).
  - About 5 m max [UploadVR].
  - Depth texture about 320×320 [repo contributor's GitHub comment].
  - Meta publishes no accuracy figure.
- **Scene mesh vs depth:** the MRUK scene mesh is static and misses people and pets. Live depth handles dynamic objects.

## 4) Extrinsic calibration of an external camera (only if keeping the Jetson)
**Best method: calibrate against the Quest's PCA camera with a ChArUco board.**
- Rigidly mount both cameras, then capture the board from both at the same moment.
- Solve with `cv2.stereoCalibrate` or paired `solvePnP`, then chain with PCA's camera-to-head extrinsics.
- No headset motion needed. Validate by projecting the tracked controller position into the OV5647 image.

**Alternatives:**
- Hand-eye calibration (`cv2.calibrateHandEye`, AX=XB): Quest head poses plus a static board. Needs at least 3 poses (more is better) and clock sync.
- A controller as a known 3D target with `solvePnP`, the way LIV and Meta's deprecated MRC tool work.

**Prior work:** TakashiYoshinaga/QuestArUcoMarkerTracking, arXiv 2604.22118 (camera-to-mocap calibration on Quest 3), and LIV's calibration guide.

**Risks:** mount flex (recalibrate if the strap moves), OV5647 rolling shutter, Wi-Fi clock-sync accuracy, and head-pose buffering on the Quest (the Jetson has no access to Quest poses).

## 5) Low-latency transport (Jetson/desktop → Quest)
**Use UDP unicast:**
- A per-frame tracks packet: capture timestamp, frame number, all active tracks with normalized bboxes. JSON first, FlatBuffers/protobuf later.
- A 50–100 Hz Quest → sender heartbeat for an NTP-style clock offset (keep the lowest-round-trip samples) and keep-alive.

**Fallbacks and what to skip:**
- **TCP** if UDP arrives in bursts. The hand-tracking-streamer author saw Quest UDP batching that `WIFI_MODE_FULL_LOW_LATENCY` didn't fix (that was traffic leaving the Quest; traffic arriving at it is untested).
- **WebRTC** only if you also stream video. `com.unity.webrtc` is active and used by QuestCameraKit and QuestVisionStream; it needs a `wss://` signalling server.
- **Skip ROS 2 / ROS-TCP-Connector:** the Nano's Ubuntu 18.04 ROS 2 distros are EOL, ROS-TCP-Connector was last pushed May 2024, and ros2-for-unity doesn't officially support Android.
- **Skip** MQTT for real-time traffic (broker hop) and Unity Netcode (Unity-to-Unity only).

**Latency data:**
- No rigorous published PC→Quest 3 UDP figure.
- Quest2ROS measured 63–106 ms end to end, mean 82 ms, over TCP.
- Ping is typically about 3–5 ms with 100–400 ms spikes [forum].
- Quest 3 passthrough is about 39 ms [Road to VR].

**Pitfalls:**
- Wi-Fi power save: hold the low-latency Wi-Fi lock (foreground and screen on only).
- Android drops multicast without a `MulticastLock`, so use unicast and keep broadcast for discovery.
- Router: disable AP isolation and PMF, and open the UDP port in the firewall.
- Dedicated 5/6 GHz SSID, PC wired. Disabling Energy Efficient Ethernet fixed 100 ms spikes for one user [forum].
- The Nano devkit has no Wi-Fi; it needs an M.2 Key E card (e.g. Intel 8265).

**Time alignment (main risk):**
- Stamp each message with the capture time.
  - On Jetson, use the Argus `ICaptureMetadata` sensor timestamp. `nvarguscamerasrc` buffer PTS is pipeline-relative, and DeepStream `attach-sys-ts` stamps arrival at the mux, not capture.
- Estimate the clock offset.
- Look up head pose at capture time with `OVRPlugin.GetNodePoseStateAtTime` or OpenXR `xrLocateSpace` with a past time.
- Apply the extrinsic, raycast, and draw world-locked.
- On the Nano, the camera alone is about 96–98 ms glass-to-glass before inference [RidgeRun].

## 6) Custom IMU prop pose into the Quest
**Easiest: put a Touch Plus controller inside the print.** Its tracking fuses IR LEDs, IMU and hand tracking. It weighs about 126 g with battery [wiki]. Meta's dynamic object tracker only handles keyboards [snippet].

**Radio for nicklink (the STM32F103 has none):**
- **nRF52840 (BLE 5 with 2 Mbps mode, USB): recommended.** On high connection priority, AOSP uses 11.25–15 ms intervals with zero peripheral latency; verify on Horizon OS.
- **ESP32-C3:** 2.4 GHz Wi-Fi 4 + BLE 5. Its UDP is routed through the AP to the 5/6 GHz Quest. It's the Wi-Fi alternative.
- **HC-05:** Bluetooth Classic SPP, not BLE. Unity support on Quest is unproven. Avoid it.

**Quest BLE caveats:**
- Connect with `connectGatt(..., TRANSPORT_LE)` (needed on Quest 2).
- Store apps should use the Companion Device API; scan/connect permissions need review. Sideloading is unaffected.
- Unity plugin: Velorexe/Unity-Android-Bluetooth-Low-Energy. Quest 3 example: horusknox/BLE-for-the-meta-quest3.

**Controller-free option:** fuse Quest hand-tracking position with LSM6DSV orientation.
- The LSM6DSV's built-in SFLP fusion outputs a game-rotation quaternion, gravity and gyro bias into its FIFO at 15–480 Hz (default 120).
- With no magnetometer, yaw drifts. Re-align yaw to the wrist pose whenever hand tracking is confident.
- Hand-tracking latency is about 31 ms passthrough-to-virtual [Road to VR].
- Hands gripping a prop likely track worse. That's an inference; no source found.

## 7) Open-source to borrow from
| Repo | What it offers | Last push |
|---|---|---|
| [oculus-samples/Unity-PassthroughCameraApiSamples](https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples) | Official PCA samples: CameraToWorld, MultiObjectDetection (on-device YOLO) | Oct 6, 2026 |
| [xrdevrob/QuestCameraKit](https://github.com/xrdevrob/QuestCameraKit) | YOLO, QR, WebRTC streaming, shaders, image LLM | Sep 23, 2026 |
| [danieloquelis/Unity-QuestVisionStream](https://github.com/danieloquelis/Unity-QuestVisionStream) | PCA → WebRTC → Python YOLO/Florence2 → detections back. Closest to desktop offload. Beta. | Aug 25, 2025 |
| [wengmister/hand-tracking-streamer](https://github.com/wengmister/hand-tracking-streamer) | Quest hand/wrist pose over UDP or TCP (see issue #4) | Jun 7, 2026 |
| [TakashiYoshinaga/QuestArUcoMarkerTracking](https://github.com/TakashiYoshinaga/QuestArUcoMarkerTracking) | ArUco on PCA (needs paid OpenCV for Unity) | n/a |
| [lukasmoro/cameraaccess-metaquest](https://github.com/lukasmoro/cameraaccess-metaquest) | Older approach: cast → OBS → Python YOLO → TCP → Unity | Aug 12, 2024 |
| [emreabd5/XR-Object-Quest3](https://github.com/emreabd5/XR-Object-Quest3) | Older approach: screen-capture YOLO → Unity raycast | Jul 26, 2024 |
| [xSmoking/Hololens_IronMan](https://github.com/xSmoking/Hololens_IronMan) | HoloLens Iron Man HUD with Jarvis voice commands. HUD visual ideas. | Feb 13, 2019 |
| [vkchamp09/edith-vision](https://github.com/vkchamp09/edith-vision) | Desktop Python/OpenCV YOLOv8 "Iron Man HUD" (no AR) | Aug 19, 2025 |
| [plentifulprops3d/wireless_repulsor_glove](https://github.com/plentifulprops3d/wireless_repulsor_glove) | ESP32-S3 repulsor glove sketch + audio | Nov 30, 2024 |
| [Velorexe/Unity-Android-Bluetooth-Low-Energy](https://github.com/Velorexe/Unity-Android-Bluetooth-Low-Energy) | BLE for Unity on Android/Quest | May 19, 2026 |
| [horusknox/BLE-for-the-meta-quest3](https://github.com/horusknox/BLE-for-the-meta-quest3) | Quest 3 BLE example | Apr 21, 2024 |
| [marcoslucianops/DeepStream-Yolo](https://github.com/marcoslucianops/DeepStream-Yolo) | YOLO for DeepStream 5.1–8.0 | Jan 25, 2026 |

## 8) Jetson Nano limits vs alternatives
- **Jetson Nano:**
  - JetPack 4.6.6 (L4T 32.7.6, Nov 2024) is the final JetPack 4 release and it's EOL.
  - DeepStream 6.0.1 (CUDA 10.2, TensorRT 8.2, Ubuntu 18.04) is the Nano's ceiling.
  - The devkit is EOL; the module is available to Jan 2027.
  - YOLOv4-tiny runs about 20–25 fps (repo README; jkjung measured 25.5 fps for the 416 FP16 model). Power is 5–10 W, the devkit is about 141 g, and it has no onboard Wi-Fi.
- **Jetson Orin Nano Super:**
  - 67 sparse / 33 dense INT8 TOPS, 8 GB, 7–25 W.
  - About $399 after NVIDIA's July 2026 price hike (was $249); often out of stock.
  - JetPack 6.x, DeepStream 7.1+. DeepStream 8.0+ dropped the bundled YOLOv4-tiny sample, but DeepStream-Yolo still covers it.
  - CSI is 22-pin, so the custom OV5647 Nano driver won't carry over. About 176 g.
- **Desktop RTX 5080:** DeepStream 8.0+ (and 9.x) supports Blackwell. It's the strongest compute and adds no head weight. The camera source would be PCA over WebRTC.
- **On-headset:** Meta's MultiObjectDetection sample. No published Quest 3 FPS figure.
- **Head mount:** the Quest 3 is 515 g. Any Jetson rig adds weight, 5–25 W of heat, a battery and cables, plus calibration and clock sync.

## Alternative directions
- **A. All on-device (Quest only):** PCA, on-device YOLO and a controller-in-prop.
  - Pros: simplest, untethered, Store-able.
  - Cons: small models, unmeasured FPS, limited field of view.
  - Best as the first milestone.
- **B. Quest camera + desktop offload (recommended target):** PCA → WebRTC → RTX 5080 DeepStream 8+/NvDCF → UDP tracks.
  - Pros: reuses DeepStream and tracking skills with big models.
  - Cons: needs Wi-Fi, an unmeasured round trip (likely tens to 100+ ms), and a `wss://` signalling server.
- **C. Original plan, upgraded (external camera):** an Orin Nano Super (or the Nano as-is) with a camera on the headset; `events.c` changed to send per-frame tracks over UDP.
  - Pros: wider field of view or a special sensor.
  - Cons: ChArUco calibration, clock sync, weight, heat and a battery. It's the hardest path; worth it only for a sensor PCA can't provide.

## Changes needed if deepstream-house-tracker stays as a sender
- Today `src/events.c` prints only appeared/lost/summary JSON lines to stdout or a file. There are no per-frame bbox updates, so the HUD couldn't follow moving objects.
- `ts` is wall-clock time from `g_get_real_time()` in the OSD probe, so it's late. Use the Argus `ICaptureMetadata` sensor timestamp.
- Bboxes are pixels in the 1280×720 streammux output. Normalize them.
- Add a UDP unicast sender with per-frame tracks.

## Key risks
1. End-to-end latency for offloading and on-device detection FPS are unpublished, so measure first.
2. PCA's field of view is narrower than passthrough.
3. Depth accuracy and resolution are limited (about 320×320, 0.2 to ~5 m).
4. Quest Wi-Fi UDP can arrive in bursts.
5. Store review for camera and BLE permissions; sideloading avoids it.
6. Jetson Nano EOL and the July 2026 Jetson price hike.

## Sources
### Meta: Unity / PCA / depth
- https://developers.meta.com/horizon/documentation/unity/unity-pca-overview/
- https://developers.meta.com/horizon/documentation/unity/unity-pca-documentation/
- https://developers.meta.com/horizon/documentation/unity/unity-pca-migration-from-webcamtexture/
- https://developers.meta.com/horizon/documentation/unity/unity-pca-sentis/
- https://developers.meta.com/horizon/documentation/unity/unity-sample-camera-object-detection/
- https://developers.meta.com/horizon/documentation/unity/unity-sample-camera-to-world/
- https://developers.meta.com/horizon/documentation/unity/unity-mr-utility-kit-environment-raycast/
- https://developers.meta.com/horizon/reference/mruk/v85/class_meta_x_r_environment_raycast_manager/
- https://developers.meta.com/horizon/documentation/unity/unity-depthapi-overview/
- https://github.com/oculus-samples/Unity-DepthAPI/issues/80
- https://developers.meta.com/horizon/blog/new-era-mixed-reality-passthrough-camera-api-machine-learning-computer-vision/
### Meta: Unreal / native / Android / WebXR
- https://developers.meta.com/horizon/documentation/unreal/unreal-openxr/
- https://developers.meta.com/horizon/reference/unreal/v85/class_u_m_r_u_k_passthrough_camera_access/
- https://developers.meta.com/horizon/documentation/native/android/mobile-depth/
- https://developers.meta.com/horizon/documentation/native/android/pca-native-overview/
- https://developers.meta.com/horizon/documentation/native/android/pca-native-documentation/
- https://developers.meta.com/horizon/documentation/android-apps/passthrough-camera-samples/
- https://github.com/meta-quest/Meta-OpenXR-SDK
- https://developers.meta.com/horizon/documentation/web/webxr-mixed-reality/
- https://developers.meta.com/horizon/documentation/web/webxr-hands/
- https://developers.meta.com/horizon/documentation/iwsdk/guides/11-scene-understanding/
- https://developers.meta.com/horizon/documentation/iwsdk/guides/13-camera-access/
- https://developers.meta.com/horizon/documentation/iwsdk/guides/14-environment-raycast/
- https://developers.meta.com/horizon/documentation/iwsdk/guides/15-depth-occlusion/
- https://developers.meta.com/horizon/release-notes/web
- https://communityforums.atmeta.com/discussions/Questions_Discussions/request-webxr-raw-camera-access-camera-access-feature-in-quest-browser/1367463
- https://beta.developers.meta.com/horizon/resources/permissions-review-required/ [snippet]
- https://latest.developers.meta.com/horizon/documentation/native/android/mobile-dynamic-object-tracker/ [snippet]
### Press / measurements
- https://www.uploadvr.com/quest-passthrough-camera-api-experimental-out-now/
- https://mixed-news.com/en/meta-quest-passthough-camera-api-public-release/
- https://www.uploadvr.com/quest-browser-depth-api-webxr-hit-testing-instant-placement/
- https://www.uploadvr.com/meta-explains-quest-3-controller-tracking/
- https://roadtovr.com/apple-vision-pro-meta-quest-3-hand-tracking-latency-comparison/
- https://mcwelle.com/preprints/welle_q2r.pdf
- https://developer.ridgerun.com/wiki/index.php/Jetson_glass_to_glass_latency
- https://www.cnx-software.com/2026/07/22/nvidia-increases-the-price-of-jetson-modules-and-devkits-by-up-to-101/ [snippet]
- https://vrarwiki.com/wiki/Meta_Quest_Touch_Plus_Controllers [snippet]
### Calibration
- https://docs.opencv.org/4.x/d9/d0c/group__calib3d.html
- https://arxiv.org/abs/2604.22118
- https://liv-creators.mintlify.app/liv-steam-app-on-pcvr/guides/mixed-reality/camera-calibration
- https://developers.meta.com/horizon/documentation/native/pc/dg-mrc/
### Transport / networking
- https://github.com/wengmister/hand-tracking-streamer/issues/4
- https://developer.android.com/reference/android/net/wifi/WifiManager.MulticastLock
- https://github.com/alvr-org/ALVR/wiki/Troubleshooting
- https://forums.developer.nvidia.com/t/nvarguscamerasrc-timestamping/199563
- https://docs.nvidia.com/metropolis/deepstream/9.1/text/DS_NTP_Timestamp.html [snippet]
- https://registry.khronos.org/OpenXR/specs/1.1/man/html/xrLocateSpace.html [snippet]
- https://github.com/Unity-Technologies/ROS-TCP-Connector/releases/tag/v0.7.0
- https://github.com/RobotecAI/ros2-for-unity
- https://www.ros.org/reps/rep-2000.html
### Prop / radios
- https://documentation.espressif.com/esp32-c3_datasheet_en.pdf [snippet]
- https://docs.nordicsemi.com/bundle/ps_nrf52840/page/keyfeatures_html5.html [snippet]
- https://android.googlesource.com/platform/packages/modules/Bluetooth/+/refs/heads/main/android/app/res/values/config.xml
- https://github.com/Velorexe/Unity-Android-Bluetooth-Low-Energy/issues/31
- https://communityforums.atmeta.com/discussions/dev-general/quest-3-disconnects-from-classical-bluetooth-with-esp32-after-3-seconds/1203903
- https://github.com/bentalebahmed/BlueUnity
- https://datasheet.lcsc.com/datasheet/pdf/4f5f08105ffd7c7cc4cb39b339912d04.pdf?productCode=C41785564 (ST LSM6DSV DS13476 mirror)
### Jetson / DeepStream
- https://forums.developer.nvidia.com/t/announcing-end-of-life-for-nvidia-jetpack-4-with-the-release-of-jetpack-4-6-6/314409
- https://developer.nvidia.com/embedded/lifecycle
- https://developer.nvidia.com/embedded/learn/get-started-jetson-nano-devkit
- https://github.com/jkjung-avt/tensorrt_demos
- https://docs.nvidia.com/metropolis/deepstream/8.0/text/DS_Release_notes.html
- https://docs.nvidia.com/metropolis/deepstream/9.1/text/DS_Release_notes.html
- https://forums.developer.nvidia.com/t/does-geforce-rtx50-series-support-deepstream-apps/341282
- https://docs.nvidia.com/jetson/orin-nano-devkit/user-guide/latest/howto.html
- https://github.com/NickStassen/deepstream-house-tracker
