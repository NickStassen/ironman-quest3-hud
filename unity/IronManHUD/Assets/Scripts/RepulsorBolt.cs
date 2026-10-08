using System;
using UnityEngine;

namespace IronManHud
{
    /// <summary>A repulsor bolt: glowing core plus trail, flies in a straight line to its end point, then calls onImpact.</summary>
    public class RepulsorBolt : MonoBehaviour
    {
        private Vector3 _start;
        private Vector3 _direction;
        private float _distance;
        private float _travelled;
        private float _speed;
        private Transform _viewer;
        private SpriteRenderer _core;
        private Action _onImpact;

        public static RepulsorBolt Spawn(Vector3 from, Vector3 to, float speed, float charge, Color color, Transform viewer, Action onImpact)
        {
            var go = new GameObject("RepulsorBolt");
            go.transform.position = from;
            var bolt = go.AddComponent<RepulsorBolt>();
            bolt._start = from;
            Vector3 delta = to - from;
            bolt._distance = delta.magnitude;
            bolt._direction = bolt._distance > 1e-4f ? delta / bolt._distance : Vector3.forward;
            bolt._speed = Mathf.Max(1f, speed);
            bolt._viewer = viewer;
            bolt._onImpact = onImpact;

            bolt._core = RepulsorFx.CreateGlow("Core", go.transform, Color.Lerp(color, Color.white, 0.5f));
            bolt._core.transform.localScale = Vector3.one * Mathf.Lerp(0.07f, 0.16f, charge);

            var trail = go.AddComponent<TrailRenderer>();
            trail.material = RepulsorFx.LineMaterial;
            trail.time = 0.12f;
            trail.minVertexDistance = 0.02f;
            trail.numCapVertices = 2;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, Mathf.Lerp(0.03f, 0.07f, charge)), new Keyframe(1f, 0f));
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            return bolt;
        }

        private void Update()
        {
            _travelled += _speed * Time.deltaTime;
            if (_travelled >= _distance)
            {
                transform.position = _start + _direction * _distance;
                enabled = false;
                _core.enabled = false;
                _onImpact?.Invoke();
                Destroy(gameObject, 0.15f); // let the trail fade out
                return;
            }
            transform.position = _start + _direction * _travelled;
            if (_viewer != null)
            {
                Vector3 away = _core.transform.position - _viewer.position;
                if (away.sqrMagnitude > 1e-6f)
                {
                    _core.transform.rotation = Quaternion.LookRotation(away);
                }
            }
        }
    }
}
