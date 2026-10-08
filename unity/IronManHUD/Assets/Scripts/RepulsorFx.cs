using UnityEngine;

namespace IronManHud
{
    /// <summary>
    /// Code-built repulsor effects (no assets): glow sprites, the impact flash, shockwave ring and sparks.
    /// Everything uses "Sprites/Default", which is in the default Always Included Shaders list.
    /// </summary>
    public static class RepulsorFx
    {
        private static Material _lineMaterial;
        private static Material _glowMaterial;
        private static Sprite _glowSprite;

        /// <summary>Plain material for lines and trails (vertex colors).</summary>
        public static Material LineMaterial
        {
            get
            {
                if (_lineMaterial == null)
                {
                    _lineMaterial = new Material(Shader.Find("Sprites/Default"));
                }
                return _lineMaterial;
            }
        }

        /// <summary>Soft round glow, for particles.</summary>
        public static Material GlowMaterial
        {
            get
            {
                if (_glowMaterial == null)
                {
                    _glowMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = GlowSprite.texture };
                }
                return _glowMaterial;
            }
        }

        /// <summary>1 m wide radial glow sprite.</summary>
        public static Sprite GlowSprite
        {
            get
            {
                if (_glowSprite == null)
                {
                    const int size = 64;
                    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                    var px = new Color32[size * size];
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dx = (x + 0.5f) / size * 2f - 1f;
                            float dy = (y + 0.5f) / size * 2f - 1f;
                            float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                            float a = Mathf.Pow(1f - r, 2.2f);
                            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                        }
                    }
                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    _glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                }
                return _glowSprite;
            }
        }

        public static SpriteRenderer CreateGlow(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = GlowSprite;
            sprite.color = color;
            return sprite;
        }

        /// <summary>Flash, shockwave ring and sparks at an impact point. charge (0..1) scales everything.</summary>
        public static void Impact(Vector3 point, Transform viewer, Color color, float charge)
        {
            float k = Mathf.Lerp(0.5f, 1.2f, charge);

            var flash = CreateGlow("ImpactFlash", null, Color.Lerp(color, Color.white, 0.6f));
            flash.transform.position = point;
            FxFade.Attach(flash.gameObject, viewer, 0.35f, 0.15f * k, 0.6f * k);

            var ring = new GameObject("Shockwave");
            ring.transform.position = point;
            var line = ring.AddComponent<LineRenderer>();
            const int segments = 48;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
            }
            line.widthMultiplier = 0.012f * k;
            line.material = LineMaterial;
            line.startColor = line.endColor = color;
            FxFade.Attach(ring, viewer, 0.45f, 0.05f, 0.5f * k);

            Sparks(point, color, charge);
        }

        private static void Sparks(Vector3 point, Color color, float charge)
        {
            var go = new GameObject("Sparks");
            go.transform.position = point;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f + 2.5f * charge);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.04f);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
            main.gravityModifier = 0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 96;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(20f + 40f * charge)) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.4f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = GlowMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
            Object.Destroy(go, 1.2f);
        }
    }
}
