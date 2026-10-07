using System;
using System.Collections;
using System.Collections.Generic;
using Unity.InferenceEngine;
using UnityEngine;

namespace IronManHud
{
    /// <summary>
    /// Runs a YOLO model with the Unity Inference Engine (formerly Sentis) on a passthrough camera texture.
    ///
    /// Supported model output layouts:
    ///  1. Meta sample format (3 outputs): boxes [N,4] as x1,y1,x2,y2 in input pixels, class ids [N] (int), scores [N].
    ///     This is the "yolov9sentis.sentis" file from oculus-samples/Unity-PassthroughCameraApiSamples.
    ///  2. Ultralytics raw export (1 output): [1, 4 + numClasses, N] with cx,cy,w,h in input pixels
    ///     (YOLOv8n / YOLO11n exported with `yolo export format=onnx opset=15 imgsz=640`).
    ///
    /// NMS runs on the CPU. The inference loop structure follows Meta's SentisInferenceRunManager (MIT).
    /// </summary>
    public class YoloDetector : IDisposable
    {
        public struct Result
        {
            public List<Detection> Detections;
            public Pose CameraPose;
            public float PreprocessMs;
            public float InferenceMs;
            public float PostprocessMs;
            /// <summary>Realtime (Time.realtimeSinceStartupAsDouble) when the frame was grabbed.</summary>
            public double GrabTime;
            /// <summary>Camera capture timestamp reported by PCA (UTC).</summary>
            public DateTime CaptureTimestamp;
        }

        public float ScoreThreshold = 0.3f;
        public float IouThreshold = 0.5f;
        public int MaxDetections = 20;

        private readonly Model _model;
        private Worker _worker;
        private Tensor<float> _input;
        private readonly bool _metaFormat;
        private readonly List<Detection> _candidates = new List<Detection>(256);
        private readonly List<Detection> _results = new List<Detection>(32);

        public Vector2Int InputSize { get; }
        public BackendType Backend { get; private set; }
        public bool IsBusy { get; private set; }
        public string FormatName => _metaFormat ? "meta-3out" : "ultralytics-1out";

        public YoloDetector(ModelAsset modelAsset, BackendType backend)
        {
            _model = ModelLoader.Load(modelAsset);
            var shape = _model.inputs[0].shape;
            int h = shape.rank >= 4 ? shape.Get(2) : -1;
            int w = shape.rank >= 4 ? shape.Get(3) : -1;
            // Dynamic dims come back as -1; fall back to the usual YOLO input size.
            InputSize = new Vector2Int(w > 0 ? w : 640, h > 0 ? h : 640);
            _metaFormat = _model.outputs.Count >= 3;
            _input = new Tensor<float>(new TensorShape(1, 3, InputSize.y, InputSize.x));
            CreateWorker(backend);
        }

        private void CreateWorker(BackendType backend)
        {
            _worker?.Dispose();
            Backend = backend;
            _worker = new Worker(_model, backend);
        }

        /// <summary>Switch backend between runs (e.g. CPU vs GPUCompute) to compare timings.</summary>
        public void SetBackend(BackendType backend)
        {
            if (IsBusy || backend == Backend)
            {
                return;
            }
            CreateWorker(backend);
        }

        /// <summary>
        /// Blocking warm-up run. The first inference compiles kernels and can stall the main thread for a while,
        /// so do it once at startup behind a "warming up" message.
        /// </summary>
        public void WarmUp()
        {
            var temp = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            try
            {
                TextureConverter.ToTensor(temp, _input, new TextureTransform());
                _worker.Schedule(_input);
                for (int i = 0; i < _model.outputs.Count; i++)
                {
                    _worker.PeekOutput(i)?.CompleteAllPendingOperations();
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(temp);
            }
        }

        /// <summary>Coroutine: grabs the texture, runs the model, waits for async readback, then NMS. Calls onDone with the result.</summary>
        public IEnumerator Run(Texture source, Pose cameraPose, DateTime captureTimestamp, Action<Result> onDone)
        {
            if (IsBusy || source == null)
            {
                yield break;
            }
            IsBusy = true;
            var result = new Result
            {
                CameraPose = cameraPose,
                CaptureTimestamp = captureTimestamp,
                GrabTime = Time.realtimeSinceStartupAsDouble,
            };

            double t0 = Time.realtimeSinceStartupAsDouble;
            TextureConverter.ToTensor(source, _input, new TextureTransform());
            _worker.Schedule(_input);
            double t1 = Time.realtimeSinceStartupAsDouble;
            result.PreprocessMs = (float)((t1 - t0) * 1000.0);

            Tensor<float> boxes = null;
            Tensor<int> classIds = null;
            Tensor<float> scores = null;
            Tensor<float> raw = null;
            try
            {
                if (_metaFormat)
                {
                    var boxesAwaiter = (_worker.PeekOutput(0) as Tensor<float>).ReadbackAndCloneAsync().GetAwaiter();
                    while (!boxesAwaiter.IsCompleted) yield return null;
                    boxes = boxesAwaiter.GetResult();

                    var idsAwaiter = (_worker.PeekOutput(1) as Tensor<int>).ReadbackAndCloneAsync().GetAwaiter();
                    while (!idsAwaiter.IsCompleted) yield return null;
                    classIds = idsAwaiter.GetResult();

                    var scoresAwaiter = (_worker.PeekOutput(2) as Tensor<float>).ReadbackAndCloneAsync().GetAwaiter();
                    while (!scoresAwaiter.IsCompleted) yield return null;
                    scores = scoresAwaiter.GetResult();
                }
                else
                {
                    var rawAwaiter = (_worker.PeekOutput(0) as Tensor<float>).ReadbackAndCloneAsync().GetAwaiter();
                    while (!rawAwaiter.IsCompleted) yield return null;
                    raw = rawAwaiter.GetResult();
                }

                double t2 = Time.realtimeSinceStartupAsDouble;
                result.InferenceMs = (float)((t2 - t1) * 1000.0);

                _candidates.Clear();
                if (_metaFormat)
                {
                    DecodeMeta(boxes, classIds, scores);
                }
                else
                {
                    DecodeUltralytics(raw);
                }
                Nms();
                result.Detections = new List<Detection>(_results);
                result.PostprocessMs = (float)((Time.realtimeSinceStartupAsDouble - t2) * 1000.0);
            }
            finally
            {
                boxes?.Dispose();
                classIds?.Dispose();
                scores?.Dispose();
                raw?.Dispose();
                IsBusy = false;
            }

            onDone?.Invoke(result);
        }

        private void DecodeMeta(Tensor<float> boxes, Tensor<int> classIds, Tensor<float> scores)
        {
            if (boxes == null || classIds == null || scores == null || boxes.shape.rank < 2)
            {
                return;
            }
            int n = Mathf.Min(boxes.shape[0], Mathf.Min(classIds.shape.length, scores.shape.length));
            if (n <= 0)
            {
                return;
            }
            var b = boxes.DownloadToArray();
            var c = classIds.DownloadToArray();
            var s = scores.DownloadToArray();
            for (int i = 0; i < n; i++)
            {
                if (s[i] < ScoreThreshold)
                {
                    continue;
                }
                _candidates.Add(new Detection
                {
                    ClassId = c[i],
                    Score = s[i],
                    X1 = b[i * 4 + 0],
                    Y1 = b[i * 4 + 1],
                    X2 = b[i * 4 + 2],
                    Y2 = b[i * 4 + 3],
                });
            }
        }

        private void DecodeUltralytics(Tensor<float> raw)
        {
            if (raw == null || raw.shape.rank != 3)
            {
                Debug.LogWarning("[IronManHud] Unexpected YOLO output rank; expected [1, 4+C, N].");
                return;
            }
            int channels = raw.shape[1];
            int n = raw.shape[2];
            int numClasses = channels - 4;
            if (numClasses <= 0)
            {
                return;
            }
            var data = raw.DownloadToArray();
            for (int i = 0; i < n; i++)
            {
                int best = -1;
                float bestScore = ScoreThreshold;
                for (int k = 0; k < numClasses; k++)
                {
                    float v = data[(4 + k) * n + i];
                    if (v > bestScore)
                    {
                        bestScore = v;
                        best = k;
                    }
                }
                if (best < 0)
                {
                    continue;
                }
                float cx = data[0 * n + i];
                float cy = data[1 * n + i];
                float w = data[2 * n + i];
                float h = data[3 * n + i];
                _candidates.Add(new Detection
                {
                    ClassId = best,
                    Score = bestScore,
                    X1 = cx - w * 0.5f,
                    Y1 = cy - h * 0.5f,
                    X2 = cx + w * 0.5f,
                    Y2 = cy + h * 0.5f,
                });
            }
        }

        /// <summary>Greedy class-aware non-max suppression on the CPU.</summary>
        private void Nms()
        {
            _results.Clear();
            _candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
            for (int i = 0; i < _candidates.Count && _results.Count < MaxDetections; i++)
            {
                var cand = _candidates[i];
                bool keep = true;
                for (int j = 0; j < _results.Count; j++)
                {
                    if (_results[j].ClassId == cand.ClassId && Detection.IoU(_results[j], cand) > IouThreshold)
                    {
                        keep = false;
                        break;
                    }
                }
                if (keep)
                {
                    _results.Add(cand);
                }
            }
        }

        public void Dispose()
        {
            if (_worker != null)
            {
                try
                {
                    for (int i = 0; i < _model.outputs.Count; i++)
                    {
                        _worker.PeekOutput(i)?.CompleteAllPendingOperations();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[IronManHud] Worker cleanup: " + e.Message);
                }
                _worker.Dispose();
                _worker = null;
            }
            _input?.Dispose();
            _input = null;
        }
    }
}
