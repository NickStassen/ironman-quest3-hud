using UnityEngine;

namespace IronManHud
{
    /// <summary>Grows, fades out and faces the viewer, then destroys itself (flash and shockwave).</summary>
    public class FxFade : MonoBehaviour
    {
        private Transform _viewer;
        private float _lifetime;
        private float _startScale;
        private float _endScale;
        private float _age;
        private SpriteRenderer _sprite;
        private LineRenderer _line;
        private Color _color;

        public static FxFade Attach(GameObject go, Transform viewer, float lifetime, float startScale, float endScale)
        {
            var fx = go.AddComponent<FxFade>();
            fx._viewer = viewer;
            fx._lifetime = lifetime;
            fx._startScale = startScale;
            fx._endScale = endScale;
            fx._sprite = go.GetComponent<SpriteRenderer>();
            fx._line = go.GetComponent<LineRenderer>();
            fx._color = fx._sprite != null ? fx._sprite.color : fx._line != null ? fx._line.startColor : Color.white;
            fx.Apply(0f);
            return fx;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _lifetime);
            Apply(t);
            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private void Apply(float t)
        {
            float ease = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.one * Mathf.Lerp(_startScale, _endScale, ease);
            if (_viewer != null)
            {
                Vector3 away = transform.position - _viewer.position;
                if (away.sqrMagnitude > 1e-6f)
                {
                    transform.rotation = Quaternion.LookRotation(away);
                }
            }
            var c = _color;
            c.a *= 1f - t;
            if (_sprite != null)
            {
                _sprite.color = c;
            }
            if (_line != null)
            {
                _line.startColor = _line.endColor = c;
            }
        }
    }
}
