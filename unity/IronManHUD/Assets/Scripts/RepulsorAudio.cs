using UnityEngine;

namespace IronManHud
{
    /// <summary>Repulsor sounds synthesized at startup (no audio assets): a loopable charge whine, the blast and the impact.</summary>
    public static class RepulsorAudio
    {
        private static AudioClip _whine;
        private static AudioClip _blast;
        private static AudioClip _impact;

        private static int Rate => AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;

        /// <summary>1 s loop; every partial has a whole number of cycles per second so it loops without a click.</summary>
        public static AudioClip Whine
        {
            get
            {
                if (_whine == null)
                {
                    _whine = Make("RepulsorWhine", 1f, (t, rng) =>
                        0.20f * Sin(440f, t) + 0.10f * Sin(880f, t) + 0.05f * Sin(1320f, t)
                        + 0.06f * Sin(6f, t) * Sin(660f, t));
                }
                return _whine;
            }
        }

        /// <summary>Noise burst with a falling cutoff plus a pitch-dropping sine: a short "thoom".</summary>
        public static AudioClip Blast
        {
            get
            {
                if (_blast == null)
                {
                    float lowpass = 0f;
                    float phase = 0f;
                    int rate = Rate;
                    _blast = Make("RepulsorBlast", 0.7f, (t, rng) =>
                    {
                        float cutoff = Mathf.Lerp(0.05f, 0.6f, Mathf.Exp(-t * 9f));
                        lowpass += ((float)rng.NextDouble() * 2f - 1f - lowpass) * cutoff;
                        float freq = 55f + 520f * Mathf.Exp(-t * 14f);
                        phase += 2f * Mathf.PI * freq / rate;
                        float attack = Mathf.Clamp01(t / 0.004f);
                        return attack * (0.7f * lowpass * Mathf.Exp(-t * 6f) + 0.55f * Mathf.Sin(phase) * Mathf.Exp(-t * 5f));
                    });
                }
                return _blast;
            }
        }

        /// <summary>Low thump plus a short crackle.</summary>
        public static AudioClip Impact
        {
            get
            {
                if (_impact == null)
                {
                    float phase = 0f;
                    int rate = Rate;
                    _impact = Make("RepulsorImpact", 0.5f, (t, rng) =>
                    {
                        float freq = 42f + 110f * Mathf.Exp(-t * 22f);
                        phase += 2f * Mathf.PI * freq / rate;
                        float crackle = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 28f);
                        float attack = Mathf.Clamp01(t / 0.002f);
                        return attack * (0.8f * Mathf.Sin(phase) * Mathf.Exp(-t * 9f) + 0.45f * crackle);
                    });
                }
                return _impact;
            }
        }

        /// <summary>One-shot 3D sound at a world position.</summary>
        public static void Play(AudioClip clip, Vector3 position, float volume)
        {
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, position, Mathf.Clamp01(volume));
            }
        }

        private static float Sin(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);

        private static AudioClip Make(string name, float seconds, System.Func<float, System.Random, float> sample)
        {
            int rate = Rate;
            int n = Mathf.RoundToInt(seconds * rate);
            var data = new float[n];
            var rng = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                data[i] = Mathf.Clamp(sample((float)i / rate, rng), -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
