using UnityEngine;

namespace Planetcraft.Core
{
    // Efeitos sonoros curtos (procedurais) tocados por uma fonte 2D global que sobrevive à troca de cena.
    public static class Sfx
    {
        static AudioSource source;
        static AudioClip[] steps;
        static AudioClip collect, craft, click, build, deconstruct;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => source = null;

        static AudioSource Source
        {
            get
            {
                if (source != null) return source;
                var go = new GameObject("Sfx");
                Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                steps = new[] { ProceduralAudio.Footstep(1), ProceduralAudio.Footstep(2), ProceduralAudio.Footstep(3), ProceduralAudio.Footstep(4) };
                collect = ProceduralAudio.Collect();
                craft = ProceduralAudio.Craft();
                click = ProceduralAudio.Click();
                build = ProceduralAudio.Thump(false);
                deconstruct = ProceduralAudio.Thump(true);
                return source;
            }
        }

        public static void Step(float volume)
        {
            var s = Source;
            s.PlayOneShot(steps[Random.Range(0, steps.Length)], volume * Random.Range(0.8f, 1f) * Settings.EffectsVolume);
        }

        public static void Collect() => Source.PlayOneShot(collect, Settings.EffectsVolume);
        public static void Craft() => Source.PlayOneShot(craft, Settings.EffectsVolume);
        public static void Click() => Source.PlayOneShot(click, 0.6f * Settings.EffectsVolume);
        public static void Build() => Source.PlayOneShot(build, Settings.EffectsVolume);
        public static void Deconstruct() => Source.PlayOneShot(deconstruct, Settings.EffectsVolume);
    }
}
