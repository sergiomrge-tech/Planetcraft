using System.Collections.Generic;
using Planetcraft.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Planetcraft.Core
{
    // Trilha e ambiência do jogo, sobrevivem à troca de cena. Os áudios são originais, sintetizados por
    // tools/gen_audio.py e carregados de Resources/Audio:
    //  - música: título e Terra morta (music_dead), primeiros brotos (music_hope), Terra viva (music_alive e epílogo),
    //    com cruzamento de 6 s entre as faixas;
    //  - vento: forte na Terra morta, some aos poucos e fica mais baixo dentro do Módulo de Base;
    //  - cantos de ave 3D ao redor do jogador a partir da Flora, mais frequentes com a floresta e a fauna.
    public class GameAudio : MonoBehaviour
    {
        const float CrossfadeSeconds = 6f;
        const int HopeStage = 5, AliveStage = 9, BirdsStage = 8;

        AudioSource musicA, musicB, wind;
        string currentTrack;
        float windLevel;
        float nextBird;
        AudioClip[] birds;
        SurvivalStats survival;

        static GameAudio instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("GameAudio");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameAudio>();
        }

        void Awake()
        {
            musicA = NewSource("MusicA", true);
            musicB = NewSource("MusicB", true);
            wind = NewSource("Wind", true);
            wind.clip = Resources.Load<AudioClip>("Audio/Ambience/wind_loop");
            birds = new AudioClip[6];
            for (int i = 0; i < birds.Length; i++) birds[i] = Resources.Load<AudioClip>($"Audio/Ambience/bird_{i + 1:00}");
            SceneManager.sceneLoaded += (_, _) => survival = null;
        }

        AudioSource NewSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        // Faixa da trilha para a cena e o estágio atuais (null = manter a que está tocando).
        string DesiredTrack()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (scene == Planetcraft.Save.Scenes.Title) return "music_dead";
            if (scene == Planetcraft.Save.Scenes.Epilogue) return "music_alive";
            var tm = TerraformManager.Instance;
            if (tm == null) return null;
            int stage = tm.CurrentStageIndex;
            return stage >= AliveStage ? "music_alive" : stage >= HopeStage ? "music_hope" : "music_dead";
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateMusic(dt);
            UpdateWind(dt);
            UpdateBirds();
        }

        void UpdateMusic(float dt)
        {
            string want = DesiredTrack();
            if (want != null && want != currentTrack)
            {
                var clip = Resources.Load<AudioClip>($"Audio/Music/{want}");
                if (clip != null)
                {
                    // A faixa nova entra na fonte que estiver livre; a antiga sai no mesmo cruzamento.
                    (musicA, musicB) = (musicB, musicA);
                    musicA.clip = clip;
                    musicA.time = Random.value * clip.length * 0.5f;
                    musicA.Play();
                    currentTrack = want;
                }
            }
            float target = Settings.MusicVolume * 0.7f;
            float step = target / CrossfadeSeconds * dt;
            musicA.volume = Mathf.MoveTowards(musicA.volume, target, step);
            musicB.volume = Mathf.MoveTowards(musicB.volume, 0f, step);
            if (musicB.volume <= 0f && musicB.isPlaying) musicB.Stop();
        }

        void UpdateWind(float dt)
        {
            var tm = TerraformManager.Instance;
            bool inGame = SceneManager.GetActiveScene().name == Planetcraft.Save.Scenes.Game && tm != null;
            float strength = 0f;
            if (inGame)
            {
                int stage = tm.CurrentStageIndex;
                strength = stage < HopeStage ? 1f : stage < AliveStage ? 0.6f : 0.35f;
                if (survival == null) survival = FindAnyObjectByType<SurvivalStats>();
                if (survival != null && survival.InPressurizedZone) strength *= 0.25f; // abafado dentro da base
            }
            windLevel = Mathf.MoveTowards(windLevel, strength, dt * 0.25f);
            wind.volume = windLevel * 0.32f * Settings.EffectsVolume;
            if (windLevel > 0.01f && !wind.isPlaying && wind.clip != null) wind.Play();
            else if (windLevel <= 0.01f && wind.isPlaying) wind.Stop();
        }

        void UpdateBirds()
        {
            var tm = TerraformManager.Instance;
            if (tm == null || SceneManager.GetActiveScene().name != Planetcraft.Save.Scenes.Game || tm.CurrentStageIndex < BirdsStage) return;
            if (Time.unscaledTime < nextBird || Camera.main == null || birds[0] == null) return;
            float life = Mathf.InverseLerp(BirdsStage, 11, tm.CurrentStageIndex);
            nextBird = Time.unscaledTime + Mathf.Lerp(14f, 5f, life) * Random.Range(0.6f, 1.5f);
            var offset = Random.insideUnitCircle.normalized * Random.Range(16f, 45f);
            var pos = Camera.main.transform.position + new Vector3(offset.x, Random.Range(4f, 9f), offset.y);
            var go = new GameObject("Ave");
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.clip = birds[Random.Range(0, birds.Length)];
            s.spatialBlend = 1f;
            s.minDistance = 6f;
            s.maxDistance = 80f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.pitch = Random.Range(0.9f, 1.25f);
            s.volume = 0.55f * Settings.EffectsVolume;
            s.Play();
            Destroy(go, s.clip.length / s.pitch + 0.5f);
        }
    }
}
