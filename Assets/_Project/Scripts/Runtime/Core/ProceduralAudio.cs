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
