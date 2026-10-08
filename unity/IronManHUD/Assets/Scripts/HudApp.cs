using System;
using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Meta.XR;
using Unity.InferenceEngine;
using UnityEngine;

namespace IronManHud
{
    /// <summary>
    /// Entry point. Needs only an OVRCameraRig (with OVRManager) in the scene; everything else is created at runtime:
    /// passthrough, HUD, Passthrough Camera Access, depth raycaster, YOLO detector, targets, repulsor, latency overlay.
    /// If the camera permission, the depth API or the model is missing, the HUD still runs and shows a message.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class HudApp : MonoBehaviour
    {
        [Header("Model (Phase 1)")]
        [Tooltip("YOLO model (.sentis or .onnx imported by Inference Engine). If empty, loads Resources/<ResourcesModelName>.")]
        public ModelAsset Model;
        [Tooltip("Optional class names, one per line. Defaults to the 80 COCO classes.")]
        public TextAsset Labels;
        public string ResourcesModelName = "yolo";
        [Tooltip("CPU is what Meta's sample uses. Press Y on the left controller to toggle CPU / GPUCompute at runtime.")]
        public BackendType Backend = BackendType.CPU;
        [Range(0.5f, 30f), Tooltip("Upper limit on how often inference starts (Hz).")]
        public float MaxDetectionHz = 8f;
        [Range(0f, 1f)] public float ScoreThreshold = 0.3f;
        [Range(0f, 1f)] public float IouThreshold = 0.5f;

        [Header("Camera (Passthrough Camera API)")]
        public PassthroughCameraAccess.CameraPositionType CameraPosition = PassthroughCameraAccess.CameraPositionType.Left;
        public Vector2Int CameraResolution = new Vector2Int(1280, 960);

        [Header("Debug")]
        public bool ShowDebugOverlay = true;
        public bool WriteLatencyCsv = true;

        private OVRCameraRig _rig;
        private Transform _head;
        private HudCanvas _hud;
        private TargetTracker _tracker;
        private RepulsorController _repulsor;
        private PassthroughCameraAccess _pca;
        private EnvironmentRaycastManager _raycaster;
        private YoloDetector _detector;
        private string[] _labels;
        private readonly LatencyStats _stats = new LatencyStats();
        private double _lastInferenceStart;
        private int _lastDetectionCount;
        private string _modelMessage;
        private string _cameraMessage;
        private string _depthMessage = "depth: waiting for scene permission";
        private bool _cameraPermissionRequested;
        private string _bootMessage;
        private float _nextDebugRefresh;

        [DllImport("OVRPlugin", CallingConvention = CallingConvention.Cdecl)]
        private static extern OVRPlugin.Result ovrp_GetNodePoseStateAtTime(double time, OVRPlugin.Node nodeId, out OVRPlugin.PoseStatef nodePoseState);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            // Lets a plain "Camera Rig + Passthrough" Building Blocks scene work without adding HudApp by hand.
            if (FindAnyObjectByType<HudApp>() == null && FindAnyObjectByType<OVRCameraRig>() != null)
            {
                new GameObject("IronManHUD (auto)").AddComponent<HudApp>();
            }
        }

        private void Start()
        {
            _labels = CocoLabels.Parse(Labels);
            SetupRigAndPassthrough();

            _hud = new GameObject("HUD").AddComponent<HudCanvas>();
            _hud.Init(_head);
            _hud.DebugVisible = ShowDebugOverlay;
            _hud.SetStatus("J.A.R.V.I.S. online. Booting...");

            _tracker = new GameObject("Targets").AddComponent<TargetTracker>();
            _tracker.Init(_head, _labels);

            var controllerAnchor = _rig != null ? _rig.rightControllerAnchor : null;
            _repulsor = new GameObject("Repulsor").AddComponent<RepulsorController>();
            _repulsor.Init(new TouchRepulsorInput(controllerAnchor), _tracker);

            if (WriteLatencyCsv)
            {
                _stats.OpenCsv();
            }

            StartCoroutine(BootSequence());
        }

        private void SetupRigAndPassthrough()
        {
            _rig = FindAnyObjectByType<OVRCameraRig>();
            if (_rig != null && _rig.centerEyeAnchor != null)
            {
                _head = _rig.centerEyeAnchor;
            }
            else if (Camera.main != null)
            {
                _head = Camera.main.transform;
                Debug.LogWarning("[IronManHud] No OVRCameraRig found; using Camera.main. Add the Camera Rig building block.");
            }
            else
            {
                _head = new GameObject("FallbackHead").AddComponent<Camera>().transform;
                Debug.LogError("[IronManHud] No camera in scene.");
            }

            if (OVRManager.instance != null)
            {
                OVRManager.instance.isInsightPassthroughEnabled = true;
            }
            if (FindAnyObjectByType<OVRPassthroughLayer>() == null)
            {
                // Same approach as Meta's PCA samples: an underlay passthrough layer created at runtime.
                var ptGo = new GameObject(nameof(OVRPassthroughLayer));
                ptGo.AddComponent<OVRPassthroughLayer>();
            }

            // Passthrough shows wherever the eye buffer is transparent.
            var cam = _head.GetComponent<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }
        }

        private IEnumerator BootSequence()
        {
            // 1. Permissions (camera + scene/depth). The OS dialog appears once; later runs remember the answer.
            OVRPermissionsRequester.Request(new[]
            {
                OVRPermissionsRequester.Permission.Scene,
                OVRPermissionsRequester.Permission.PassthroughCameraAccess
            });
            _cameraPermissionRequested = true;

            // 2. Model (independent of the camera so a missing model never blocks the HUD).
            LoadModel();

            // 3. Camera.
            if (!PassthroughCameraAccess.IsSupported)
            {
                _cameraMessage = "Passthrough Camera API not supported here (needs Quest 3/3S on Horizon OS v74+).";
            }
            else
            {
                var pcaGo = new GameObject("PassthroughCameraAccess");
                pcaGo.SetActive(false); // set fields before OnEnable runs
                _pca = pcaGo.AddComponent<PassthroughCameraAccess>();
                _pca.CameraPosition = CameraPosition;
                _pca.RequestedResolution = CameraResolution;
                pcaGo.SetActive(true);
                _tracker.SetSources(_pca, null);
            }

            // 4. Depth raycaster, once the scene permission is granted.
            StartCoroutine(EnableDepthWhenPermitted());

            // 5. Warm up the model once (blocks the main thread briefly on first run).
            if (_detector != null)
            {
                _bootMessage = "Warming up detector...";
                yield return null;
                yield return null;
                try
                {
                    _detector.WarmUp();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    _modelMessage = "Model warm-up failed: " + e.Message;
                    _detector.Dispose();
                    _detector = null;
                }
                _bootMessage = null;
            }

            // 6. Main loop.
            while (true)
            {
                TryStartInference();
                yield return null;
            }
        }

        private void LoadModel()
        {
            var asset = Model != null ? Model : Resources.Load<ModelAsset>(ResourcesModelName);
            if (asset == null)
            {
                _modelMessage = "No YOLO model. Run tools/get-model.sh (or see docs/SETUP.md), then rebuild. HUD + repulsor still work.";
                return;
            }
            try
            {
                _detector = new YoloDetector(asset, Backend)
                {
                    ScoreThreshold = ScoreThreshold,
                    IouThreshold = IouThreshold,
                };
                Debug.Log($"[IronManHud] Model loaded: {asset.name}, input {_detector.InputSize}, format {_detector.FormatName}, backend {Backend}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _modelMessage = "Model failed to load: " + e.Message;
                _detector = null;
            }
        }

        private IEnumerator EnableDepthWhenPermitted()
        {
            while (!OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.Scene))
            {
                yield return new WaitForSeconds(0.5f);
            }
            if (!EnvironmentRaycastManager.IsSupported)
            {
                _depthMessage = "depth: EnvironmentRaycastManager not supported (distances show --)";
                yield break;
            }
            _raycaster = new GameObject("EnvironmentRaycastManager").AddComponent<EnvironmentRaycastManager>();
            _tracker.SetSources(_pca, _raycaster);
            _depthMessage = "depth: on";
        }

        private void TryStartInference()
        {
            if (_detector == null || _detector.IsBusy || _pca == null || !_pca.isActiveAndEnabled || !_pca.IsPlaying)
            {
                return;
            }
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - _lastInferenceStart < 1.0 / Mathf.Max(0.5f, MaxDetectionHz))
            {
                return;
            }

            // Same guard as Meta's sample: while head tracking is lost, GetCameraPose() is built from an identity
            // head pose and would place targets near the origin.
            if (!ovrp_GetNodePoseStateAtTime(OVRPlugin.GetTimeInSeconds(), OVRPlugin.Node.Head, out _).IsSuccess())
            {
                return;
            }
            Pose cameraPose = _pca.GetCameraPose();
            if (cameraPose.rotation.x == 0f && cameraPose.rotation.y == 0f && cameraPose.rotation.z == 0f && cameraPose.rotation.w == 0f)
            {
                return; // pose not available yet
            }
            Texture tex = _pca.GetTexture();
            if (tex == null)
            {
                return;
            }
            _lastInferenceStart = now;
            StartCoroutine(_detector.Run(tex, cameraPose, _pca.Timestamp, OnDetections));
        }

        private void OnDetections(YoloDetector.Result result)
        {
            _lastDetectionCount = result.Detections != null ? result.Detections.Count : 0;
            _tracker.Ingest(result.Detections, _detector.InputSize, result.CameraPose);

            double now = Time.realtimeSinceStartupAsDouble;
            float pipelineMs = (float)((now - result.GrabTime) * 1000.0);

            // Age of the camera frame when its overlay is applied. PCA Timestamp is built from a Unix-epoch
            // microsecond value; if that clock isn't wall-clock time on-device, the value is discarded (-1).
            float captureAgeMs = -1f;
            if (result.CaptureTimestamp != default)
            {
                double age = (DateTime.UtcNow - result.CaptureTimestamp).TotalMilliseconds;
                if (age > 0 && age < 2000)
                {
                    captureAgeMs = (float)age;
                }
            }
            _stats.RecordDetection(_detector.Backend.ToString(), _lastDetectionCount, result.PreprocessMs, result.InferenceMs,
                result.PostprocessMs, pipelineMs, captureAgeMs);
        }

        private void Update()
        {
            _stats.RecordFrame(Time.unscaledDeltaTime * 1000f);

            // Left controller: X toggles the debug overlay, Y toggles CPU / GPUCompute.
            if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch) && _hud != null)
            {
                _hud.DebugVisible = !_hud.DebugVisible;
            }
            if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch) && _detector != null && !_detector.IsBusy)
            {
                var next = _detector.Backend == BackendType.CPU ? BackendType.GPUCompute : BackendType.CPU;
                try
                {
                    _detector.SetBackend(next);
                    _stats.ResetDetectionStats(); // don't mix CPU and GPU samples in the on-screen percentiles
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        private void LateUpdate()
        {
            if (_hud == null)
            {
                return;
            }
            UpdateCameraMessage();

            // Prominent message: first problem wins.
            string problem = _bootMessage ?? _cameraMessage ?? _modelMessage;
            _hud.SetCenterMessage(problem, _bootMessage == null && problem != null && _cameraMessage != null);

            bool locked = _repulsor != null && _repulsor.LastHit != null;
            _hud.SetReticleLocked(locked);

            string cam = _pca != null && _pca.IsPlaying ? $"CAM {_pca.CurrentResolution.x}x{_pca.CurrentResolution.y}" : "CAM --";
            string model = _detector != null ? $"YOLO {_detector.Backend}" : "YOLO off";
            int targets = _tracker != null ? _tracker.Targets.Count : 0;
            string lockText = locked ? $"  |  LOCK #{_repulsor.LastHit.Id} {_repulsor.LastHit.Label.ToUpperInvariant()}" : "";
            _hud.SetStatus($"{cam}  |  {model}  |  {(_raycaster != null ? "DEPTH on" : "DEPTH --")}  |  TARGETS {targets}{lockText}");

            if (_hud.DebugVisible && Time.unscaledTime >= _nextDebugRefresh)
            {
                // 5 Hz is plenty to read, and keeps the overlay from skewing the frame times it reports.
                _nextDebugRefresh = Time.unscaledTime + 0.2f;
                _hud.SetDebug(BuildDebugText());
            }
        }

        private void UpdateCameraMessage()
        {
            if (_pca == null)
            {
                return; // unsupported message already set
            }
            if (_cameraPermissionRequested && !OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.PassthroughCameraAccess))
            {
                _cameraMessage = "Camera permission not granted. Allow it in the headset prompt, or Settings > Apps > Permissions. Detection is off.";
            }
            else if (!_pca.enabled)
            {
                _cameraMessage = "Passthrough camera failed to start (see adb logcat). Detection is off.";
            }
            else if (!_pca.IsPlaying)
            {
                _cameraMessage = Time.timeSinceLevelLoad > 8f ? "Waiting for passthrough camera frames..." : null;
            }
            else
            {
                _cameraMessage = null;
            }
        }

        private readonly StringBuilder _sb = new StringBuilder(512);

        private string BuildDebugText()
        {
            _sb.Clear();
            var ci = CultureInfo.InvariantCulture;
            _sb.AppendFormat(ci, "frame ms     {0}\n", _stats.FrameMs.Format());
            _sb.AppendFormat(ci, "preproc ms   {0}\n", _stats.PreprocessMs.Format());
            _sb.AppendFormat(ci, "infer ms     {0}\n", _stats.InferenceMs.Format());
            _sb.AppendFormat(ci, "nms ms       {0}\n", _stats.PostprocessMs.Format());
            _sb.AppendFormat(ci, "grab->place  {0}\n", _stats.PipelineMs.Format());
            _sb.AppendFormat(ci, "capture age  {0}\n", _stats.CaptureAgeMs.Format());
            _sb.AppendFormat(ci, "det Hz {0:0.0}  dets {1}  depth hit/miss {2}/{3} ({4})\n",
                Mathf.Max(0f, _stats.DetectionHz.Percentile(0.5f)), _lastDetectionCount,
                _tracker != null ? _tracker.LastDepthHits : 0, _tracker != null ? _tracker.LastDepthMisses : 0,
                _tracker != null ? _tracker.LastDepthStatus : "--");
            _sb.Append(_depthMessage);
            if (_detector != null)
            {
                _sb.AppendFormat(ci, "  |  model {0} {1}x{2}", _detector.FormatName, _detector.InputSize.x, _detector.InputSize.y);
            }
            _sb.Append("\n[X] debug  [Y] CPU/GPU  [R trigger] repulsor");
            return _sb.ToString();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _stats.Flush();
            }
        }

        private void OnDestroy()
        {
            _detector?.Dispose();
            _detector = null;
            _stats.Dispose();
        }
    }
}
