using UnityEngine;

namespace Planetcraft.Core
{
    // Sons gerados por código: o protótipo não depende de assets de áudio.
    public static class ProceduralAudio
    {
        const int SampleRate = 44100;

        // Ruído filtrado com crossfade nas pontas para loop sem emenda.
        public static AudioClip RainLoop(float seconds = 3f)
        {
            int n = (int)(SampleRate * seconds);
            int fade = SampleRate / 10;
            var raw = new float[n];
            var rng = new System.Random(1);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.3f * (white - lp);
                lp2 += 0.6f * (lp - lp2);
                raw[i] = lp2 * 0.9f;
            }

            var data = new float[n - fade];
            for (int i = 0; i < data.Length; i++) data[i] = raw[i];
            for (int i = 0; i < fade; i++)
            {
                float a = i / (float)fade;
                data[i] = raw[i] * a + raw[n - fade + i] * (1f - a);
            }
            return Create("RainLoop", data);
        }

        // Arpejo ascendente (dó-mi-sol-dó) para um novo estágio.
        public static AudioClip StageChime() =>
            Arpeggio("StageChime", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.12f, 2.2f, 2.5f, 0.16f);

        // Duas notas curtas para um novo projeto desbloqueado.
        public static AudioClip UnlockBlip() =>
            Arpeggio("UnlockBlip", new[] { 880f, 1318.5f }, 0.08f, 0.6f, 7f, 0.14f);

        // Passo em cascalho: ruído curto, abafado, com ataque rápido. `seed` dá variação.
        public static AudioClip Footstep(int seed)
        {
            int n = (int)(SampleRate * 0.14f);
            var data = new float[n];
            var rng = new System.Random(seed);
            float lp = 0f;
            float cutoff = 0.18f + (float)rng.NextDouble() * 0.1f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Min(1f, t * 400f) * Mathf.Exp(-t * 32f);
                lp += cutoff * ((float)(rng.NextDouble() * 2.0 - 1.0) - lp);
                data[i] = lp * env * 0.9f;
            }
            return Create("Footstep" + seed, data);
        }

        public static AudioClip Collect() =>
            Arpeggio("Collect", new[] { 660f, 990f }, 0.05f, 0.35f, 9f, 0.16f);

        public static AudioClip Craft() =>
            Arpeggio("Craft", new[] { 523.25f, 783.99f, 659.25f }, 0.06f, 0.45f, 8f, 0.13f);

        public static AudioClip Click() =>
            Arpeggio("Click", new[] { 1400f }, 0f, 0.05f, 60f, 0.12f);

        // Baque grave para construir; invertido (agudo→grave) para desmontar.
        public static AudioClip Thump(bool falling)
        {
            int n = (int)(SampleRate * 0.4f);
            var data = new float[n];
            var rng = new System.Random(3);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float k = t / 0.4f;
                float freq = falling ? Mathf.Lerp(420f, 140f, k) : Mathf.Lerp(150f, 70f, k);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float env = Mathf.Min(1f, t * 300f) * Mathf.Exp(-t * (falling ? 9f : 12f));
                data[i] = (Mathf.Sin(phase) * 0.6f + (float)(rng.NextDouble() * 2.0 - 1.0) * 0.12f) * env * 0.5f;
            }
            return Create(falling ? "Deconstruct" : "Build", data);
        }

        // Ronco grave em loop para a descida da nave-mãe.
        public static AudioClip Rumble(float seconds = 4f)
        {
            int n = (int)(SampleRate * seconds);
            var data = new float[n];
            var rng = new System.Random(9);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                lp += 0.02f * ((float)(rng.NextDouble() * 2.0 - 1.0) - lp);
                lp2 += 0.05f * (lp - lp2);
                // Frequências múltiplas de 1/seconds para o loop fechar sem emenda.
                data[i] = lp2 * 6f + Mathf.Sin(2f * Mathf.PI * 41f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.15f;
            }
            return Create("Rumble", data);
        }

        // Tiro de pulso: varredura aguda que cai, com um estalo de ruído no começo.
        public static AudioClip Zap(bool heavy = false)
        {
            float length = heavy ? 0.35f : 0.2f;
            int n = (int)(SampleRate * length);
            var data = new float[n];
            var rng = new System.Random(5);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float k = t / length;
                float freq = Mathf.Lerp(heavy ? 900f : 1900f, heavy ? 90f : 260f, Mathf.Sqrt(k));
                phase += 2f * Mathf.PI * freq / SampleRate;
                float env = Mathf.Min(1f, t * 600f) * Mathf.Exp(-t * (heavy ? 7f : 14f));
                data[i] = (Mathf.Sin(phase) * 0.55f + Mathf.Sign(Mathf.Sin(phase * 0.5f)) * 0.12f +
                           (float)(rng.NextDouble() * 2.0 - 1.0) * 0.18f * Mathf.Exp(-t * 60f)) * env * 0.6f;
            }
            return Create(heavy ? "ZapHeavy" : "Zap", data);
        }

        // Impacto seco e curto.
        public static AudioClip Hit()
        {
            int n = (int)(SampleRate * 0.15f);
            var data = new float[n];
            var rng = new System.Random(8);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                lp += 0.25f * ((float)(rng.NextDouble() * 2.0 - 1.0) - lp);
                data[i] = (lp * 0.8f + Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.5f) * Mathf.Exp(-t * 28f);
            }
            return Create("Hit", data);
        }

        // Sirene de alerta de dois tons.
        public static AudioClip Alarm()
        {
            int n = (int)(SampleRate * 1.6f);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float freq = (int)(t / 0.4f) % 2 == 0 ? 640f : 860f;
                phase += 2f * Mathf.PI * freq / SampleRate;
                float env = Mathf.Min(1f, t * 80f) * Mathf.Min(1f, (1.6f - t) * 40f);
                data[i] = (Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2f)) * 0.22f * env;
            }
            return Create("Alarm", data);
        }

        // Explosão: ruído grave com queda rápida.
        public static AudioClip Boom()
        {
            int n = (int)(SampleRate * 0.9f);
            var data = new float[n];
            var rng = new System.Random(12);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                lp += 0.08f * ((float)(rng.NextDouble() * 2.0 - 1.0) - lp);
                lp2 += 0.2f * (lp - lp2);
                data[i] = (lp2 * 5f + Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 35f, t) * t) * 0.6f) * Mathf.Exp(-t * 5f) * Mathf.Min(1f, t * 400f);
            }
            return Create("Boom", data);
        }

        static AudioClip Arpeggio(string name, float[] notes, float gap, float length, float decay, float volume)
        {
            int n = (int)(SampleRate * length);
            var data = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = (int)(k * gap * SampleRate);
                float w = 2f * Mathf.PI * notes[k];
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)SampleRate;
                    float env = Mathf.Exp(-t * decay) * Mathf.Min(1f, t * 200f);
                    data[i] += (Mathf.Sin(w * t) + 0.3f * Mathf.Sin(2f * w * t)) * env * volume;
                }
            }
            return Create(name, data);
        }

        static AudioClip Create(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
