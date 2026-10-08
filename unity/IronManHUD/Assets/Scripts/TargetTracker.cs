using System;
using System.Collections.Generic;
using System.Globalization;
using Meta.XR;
using UnityEngine;
using UnityEngine.UI;

namespace IronManHud
{
    /// <summary>
    /// Phase 2: turns 2D detections into world-locked 3D targets.
    ///  - Ray through the PCA intrinsics using the camera pose at capture time (PassthroughCameraAccess.ViewportPointToRay).
    ///  - Depth from MRUK EnvironmentRaycastManager: median of 5 rays inside the box.
    ///  - Association: nearest neighbour by angle from the camera (same class), with a timeout.
    ///  - Smoothing: exponential on world position and size.
    /// </summary>
    public class TargetTracker : MonoBehaviour
    {
        public class Target
        {
            public int Id;
            public int ClassId;
            public string Label;
            public float Score;
            public Vector3 Position;
            public Vector2 SizeM;
            public bool HasDepth;
            public int Hits;
            public float LastSeen;
            public float HighlightUntil;
            public int MatchedBatch;
            public TargetMarker Marker;
        }

        [Tooltip("Max angle between a detection ray and an existing target (degrees) to treat them as the same object.")]
        public float AssociationAngleDeg = 8f;
        [Tooltip("Seconds without a detection before a target is removed.")]
        public float TimeoutSeconds = 1.2f;
        [Tooltip("Detections needed before a target is shown (reduces flicker).")]
        public int MinHitsToShow = 2;
        [Range(0f, 1f), Tooltip("Smoothing factor per detection (1 = no smoothing).")]
        public float PositionAlpha = 0.35f;
        [Tooltip("Distance used when the depth raycast misses (m). Shown as '--'.")]
        public float FallbackDistance = 2f;

        private readonly List<Target> _targets = new List<Target>();
        private readonly List<float> _depthSamples = new List<float>(5);
        private PassthroughCameraAccess _camera;
        private EnvironmentRaycastManager _raycaster;
        private Transform _head;
        private string[] _labels;
        private int _nextId = 1;
        private int _batch;

        public IReadOnlyList<Target> Targets => _targets;
        public int LastDepthHits { get; private set; }
        public int LastDepthMisses { get; private set; }
        public string LastDepthStatus { get; private set; } = "--";

        public void Init(Transform head, string[] labels)
        {
            _head = head;
            _labels = labels;
        }

        public void SetSources(PassthroughCameraAccess cameraAccess, EnvironmentRaycastManager raycaster)
        {
            _camera = cameraAccess;
            _raycaster = raycaster;
        }

        public void Ingest(List<Detection> detections, Vector2Int inputSize, Pose cameraPose)
        {
            if (_camera == null || !_camera.IsPlaying || detections == null)
            {
                return;
            }
            LastDepthHits = 0;
            LastDepthMisses = 0;
            float now = Time.time;
            _batch++;

            foreach (var det in detections)
            {
                // Model input pixels (y down) -> PCA viewport (0..1, y up). Matches Meta's sample.
                float u = (det.X1 + det.X2) * 0.5f / inputSize.x;
                float v = 1f - (det.Y1 + det.Y2) * 0.5f / inputSize.y;
                float halfW = det.Width * 0.5f / inputSize.x;
                float halfH = det.Height * 0.5f / inputSize.y;

                Ray centerRay = _camera.ViewportPointToRay(new Vector2(u, v), cameraPose);
                bool hasDepth = TryMedianDepth(cameraPose, u, v, halfW, halfH, out float depth);
                if (hasDepth) LastDepthHits++; else LastDepthMisses++;

                // Find a target of the same class within the association cone (angle seen from the camera).
                Target match = null;
                float bestAngle = AssociationAngleDeg;
                foreach (var t in _targets)
                {
                    // One detection per target per batch, so two nearby objects of the same class don't merge.
                    if (t.ClassId != det.ClassId || t.MatchedBatch == _batch)
                    {
                        continue;
                    }
                    float angle = Vector3.Angle(centerRay.direction, t.Position - cameraPose.position);
                    if (angle < bestAngle)
                    {
                        bestAngle = angle;
                        match = t;
                    }
                }

                if (!hasDepth)
                {
                    // Keep the previous depth of a matched target; otherwise use the fallback distance.
                    depth = match != null && match.HasDepth
                        ? Vector3.Distance(cameraPose.position, match.Position)
                        : FallbackDistance;
                }

                Vector3 worldPos = centerRay.GetPoint(depth);
                Vector2 sizeM = AngularSizeToMetres(cameraPose, u, v, halfW, halfH, depth);

                if (match == null)
                {
                    match = new Target
                    {
                        Id = _nextId++,
                        ClassId = det.ClassId,
                        Label = CocoLabels.Get(_labels, det.ClassId),
                        Position = worldPos,
                        SizeM = sizeM,
                    };
                    _targets.Add(match);
                }
                else if (hasDepth && !match.HasDepth)
                {
                    // First real depth: jump there instead of easing in from the fallback distance.
                    match.Position = worldPos;
                    match.SizeM = sizeM;
                }
                else
                {
                    float a = (hasDepth || !match.HasDepth) ? PositionAlpha : PositionAlpha * 0.5f;
                    match.Position = Vector3.Lerp(match.Position, worldPos, a);
                    match.SizeM = Vector2.Lerp(match.SizeM, sizeM, a);
                }
                match.MatchedBatch = _batch;
                match.HasDepth = hasDepth || match.HasDepth;
                match.Score = det.Score;
                match.Hits++;
                match.LastSeen = now;
            }
        }

        private bool TryMedianDepth(Pose cameraPose, float u, float v, float halfW, float halfH, out float depth)
        {
            depth = 0f;
            _depthSamples.Clear();
            if (_raycaster == null || !_raycaster.isActiveAndEnabled || !EnvironmentRaycastManager.IsSupported)
            {
                LastDepthStatus = "raycaster off";
                return false;
            }
            // Centre plus 4 points at 25% of the box towards each side.
            for (int i = 0; i < 5; i++)
            {
                float du = 0f, dv = 0f;
                switch (i)
                {
                    case 1: du = -0.5f * halfW; break;
                    case 2: du = 0.5f * halfW; break;
                    case 3: dv = -0.5f * halfH; break;
                    case 4: dv = 0.5f * halfH; break;
                }
                Ray ray = _camera.ViewportPointToRay(new Vector2(u + du, v + dv), cameraPose);
                if (_raycaster.Raycast(ray, out EnvironmentRaycastHit hit, 10f))
                {
                    // Depth along the ray from the camera.
                    _depthSamples.Add(Vector3.Dot(hit.point - ray.origin, ray.direction.normalized));
                    LastDepthStatus = "hit";
                }
                else
                {
                    LastDepthStatus = hit.status.ToString();
                }
            }
            if (_depthSamples.Count == 0)
            {
                return false;
            }
            _depthSamples.Sort();
            depth = _depthSamples[_depthSamples.Count / 2];
            return depth > 0.05f;
        }

        private Vector2 AngularSizeToMetres(Pose cameraPose, float u, float v, float halfW, float halfH, float depth)
        {
            Vector3 left = _camera.ViewportPointToRay(new Vector2(u - halfW, v), cameraPose).direction.normalized;
            Vector3 right = _camera.ViewportPointToRay(new Vector2(u + halfW, v), cameraPose).direction.normalized;
            Vector3 bottom = _camera.ViewportPointToRay(new Vector2(u, v - halfH), cameraPose).direction.normalized;
            Vector3 top = _camera.ViewportPointToRay(new Vector2(u, v + halfH), cameraPose).direction.normalized;
            float w = 2f * depth * Mathf.Tan(0.5f * Vector3.Angle(left, right) * Mathf.Deg2Rad);
            float h = 2f * depth * Mathf.Tan(0.5f * Vector3.Angle(bottom, top) * Mathf.Deg2Rad);
            return new Vector2(Mathf.Clamp(w, 0.05f, 5f), Mathf.Clamp(h, 0.05f, 5f));
        }

        /// <summary>Marks a target as hit by the repulsor.</summary>
        public void Highlight(Target target, float seconds)
        {
            if (target != null)
            {
                target.HighlightUntil = Time.time + seconds;
            }
        }

        /// <summary>Closest visible target the ray passes near (within max(radius, half the target size)).</summary>
        public Target RaycastTargets(Ray ray, float minRadius, out float distanceAlongRay)
        {
            Target best = null;
            distanceAlongRay = float.MaxValue;
            Vector3 dir = ray.direction.normalized;
            foreach (var t in _targets)
            {
                if (t.Hits < MinHitsToShow)
                {
                    continue;
                }
                Vector3 toTarget = t.Position - ray.origin;
                float along = Vector3.Dot(toTarget, dir);
                if (along <= 0f)
                {
                    continue;
                }
                float perp = (toTarget - dir * along).magnitude;
                float radius = Mathf.Max(minRadius, 0.5f * Mathf.Min(t.SizeM.x, t.SizeM.y));
                if (perp <= radius && along < distanceAlongRay)
                {
                    distanceAlongRay = along;
                    best = t;
                }
            }
            return best;
        }

        private void LateUpdate()
        {
            float now = Time.time;
            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                var t = _targets[i];
                if (now - t.LastSeen > TimeoutSeconds)
                {
                    if (t.Marker != null)
                    {
                        Destroy(t.Marker.gameObject);
                    }
                    _targets.RemoveAt(i);
                    continue;
                }

                bool visible = t.Hits >= MinHitsToShow;
                if (visible && t.Marker == null)
                {
                    t.Marker = TargetMarker.Create(transform);
                }
                if (t.Marker != null)
                {
                    t.Marker.gameObject.SetActive(visible);
                    if (visible && _head != null)
                    {
                        float dist = Vector3.Distance(_head.position, t.Position);
                        string distText = t.HasDepth ? dist.ToString("0.00", CultureInfo.InvariantCulture) + " m" : "-- m";
                        float age = now - t.LastSeen;
                        t.Marker.UpdateMarker(t.Position, t.SizeM, _head.position,
                            string.Format(CultureInfo.InvariantCulture, "#{0} {1} {2:0}%  {3}", t.Id, t.Label.ToUpperInvariant(), t.Score * 100f, distText),
                            now < t.HighlightUntil, 1f - Mathf.Clamp01(age / TimeoutSeconds) * 0.6f);
                    }
                }
            }
        }

        public void ClearAll()
        {
            foreach (var t in _targets)
            {
                if (t.Marker != null)
                {
                    Destroy(t.Marker.gameObject);
                }
            }
            _targets.Clear();
        }
    }
}
