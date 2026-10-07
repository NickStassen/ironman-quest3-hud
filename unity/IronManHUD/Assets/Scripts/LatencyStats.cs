using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace IronManHud
{
    /// <summary>Rolling timing stats for one pipeline stage (milliseconds).</summary>
    public class RollingStat
    {
        private readonly float[] _samples;
        private int _count;
        private int _next;

        public RollingStat(int capacity = 120)
        {
            _samples = new float[capacity];
        }

        public float Last { get; private set; } = -1f;
        public int Count => _count;

        public void Add(float value)
        {
            Last = value;
            _samples[_next] = value;
            _next = (_next + 1) % _samples.Length;
            if (_count < _samples.Length)
            {
                _count++;
            }
        }

        /// <summary>Percentile in [0, 1]. Returns -1 when empty.</summary>
        public float Percentile(float p)
        {
            if (_count == 0)
            {
                return -1f;
            }
            var copy = new float[_count];
            Array.Copy(_samples, copy, _count);
            Array.Sort(copy);
            int idx = Mathf.Clamp(Mathf.RoundToInt(p * (_count - 1)), 0, _count - 1);
            return copy[idx];
        }

        public string Format()
        {
            if (_count == 0)
            {
                return "--";
            }
            return string.Format(CultureInfo.InvariantCulture, "{0,5:0.0} (p50 {1:0.0} / p95 {2:0.0})", Last, Percentile(0.5f), Percentile(0.95f));
        }
    }

    /// <summary>
    /// Collects per-detection-frame timings and writes them to a CSV in Application.persistentDataPath.
    /// Pull it with: adb pull /sdcard/Android/data/&lt;package&gt;/files/latency_*.csv
    /// </summary>
    public class LatencyStats : IDisposable
    {
        public readonly RollingStat FrameMs = new RollingStat(180);
        public readonly RollingStat PreprocessMs = new RollingStat();
        public readonly RollingStat InferenceMs = new RollingStat();
        public readonly RollingStat PostprocessMs = new RollingStat();
        public readonly RollingStat PipelineMs = new RollingStat();
        public readonly RollingStat CaptureAgeMs = new RollingStat();
        public readonly RollingStat DetectionHz = new RollingStat(30);

        private StreamWriter _writer;
        private double _lastDetectionTime = -1;
        private int _rowsSinceFlush;

        public string CsvPath { get; private set; }

        public void OpenCsv()
        {
            try
            {
                CsvPath = Path.Combine(Application.persistentDataPath,
                    "latency_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv");
                _writer = new StreamWriter(CsvPath, false, Encoding.UTF8);
                _writer.WriteLine("t_s,backend,detections,preprocess_ms,inference_ms,postprocess_ms,pipeline_ms,capture_age_ms,app_frame_ms");
                Debug.Log("[IronManHud] Latency CSV: " + CsvPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[IronManHud] Could not open latency CSV: " + e.Message);
                _writer = null;
            }
        }

        public void RecordFrame(float deltaMs) => FrameMs.Add(deltaMs);

        /// <param name="captureAgeMs">Negative when unknown.</param>
        public void RecordDetection(string backend, int detections, float preMs, float infMs, float postMs, float pipelineMs, float captureAgeMs)
        {
            PreprocessMs.Add(preMs);
            InferenceMs.Add(infMs);
            PostprocessMs.Add(postMs);
            PipelineMs.Add(pipelineMs);
            if (captureAgeMs >= 0f)
            {
                CaptureAgeMs.Add(captureAgeMs);
            }

            double now = Time.realtimeSinceStartupAsDouble;
            if (_lastDetectionTime > 0 && now > _lastDetectionTime)
            {
                DetectionHz.Add((float)(1.0 / (now - _lastDetectionTime)));
            }
            _lastDetectionTime = now;

            if (_writer == null)
            {
                return;
            }
            _writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0:0.000},{1},{2},{3:0.00},{4:0.00},{5:0.00},{6:0.00},{7:0.00},{8:0.00}",
                now, backend, detections, preMs, infMs, postMs, pipelineMs, captureAgeMs, FrameMs.Last));
            if (++_rowsSinceFlush >= 30)
            {
                _rowsSinceFlush = 0;
                _writer.Flush();
            }
        }

        public void Flush() => _writer?.Flush();

        public void Dispose()
        {
            if (_writer != null)
            {
                _writer.Flush();
                _writer.Dispose();
                _writer = null;
            }
        }
    }
}
