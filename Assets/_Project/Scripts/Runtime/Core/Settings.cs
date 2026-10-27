using System;
using UnityEngine;

namespace Planetcraft.Core
{
    // Opções do jogador, salvas em PlayerPrefs. Carregadas uma vez e cacheadas (lidas a cada frame).
    public static class Settings
    {
        const string VolumeKey = "pc.volume", SensitivityKey = "pc.sensitivity", InvertKey = "pc.invertY",
            FovKey = "pc.fov", QualityKey = "pc.quality", LanguageKey = "pc.language";

        static bool loaded;
        static float volume, sensitivity, fov;
        static bool invertY;
        static int quality;
        static string language;

        public static event Action Changed;

        public static float MasterVolume { get { Load(); return volume; } set { volume = Mathf.Clamp01(value); Save(); } }
        public static float MouseSensitivity { get { Load(); return sensitivity; } set { sensitivity = Mathf.Clamp(value, 0.02f, 0.5f); Save(); } }
        public static bool InvertY { get { Load(); return invertY; } set { invertY = value; Save(); } }
        public static float FieldOfView { get { Load(); return fov; } set { fov = Mathf.Clamp(value, 55f, 100f); Save(); } }
        public static int QualityLevel { get { Load(); return quality; } set { quality = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1); Save(); } }
        /// <summary>"pt" ou "en" (padrão: idioma do sistema).</summary>
        public static string Language { get { Load(); return language; } set { language = value == "en" ? "en" : "pt"; Save(); } }

        // Tela cheia é aplicada só quando o jogador muda (não na inicialização, para respeitar -screen-fullscreen).
        public static bool Fullscreen
        {
            get => Screen.fullScreen;
            set => Screen.fullScreen = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            loaded = false;
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ApplyOnStart() => Apply();

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            volume = PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            sensitivity = PlayerPrefs.GetFloat(SensitivityKey, 0.12f);
            invertY = PlayerPrefs.GetInt(InvertKey, 0) == 1;
            fov = PlayerPrefs.GetFloat(FovKey, 70f);
            quality = PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel());
            language = PlayerPrefs.GetString(LanguageKey, Loc.SystemDefault());
        }

        static void Save()
        {
            PlayerPrefs.SetFloat(VolumeKey, volume);
            PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
            PlayerPrefs.SetInt(InvertKey, invertY ? 1 : 0);
            PlayerPrefs.SetFloat(FovKey, fov);
            PlayerPrefs.SetInt(QualityKey, quality);
            PlayerPrefs.SetString(LanguageKey, language);
            PlayerPrefs.Save();
            Apply();
        }

        public static void Apply()
        {
            Load();
            AudioListener.volume = volume;
            if (QualitySettings.GetQualityLevel() != quality) QualitySettings.SetQualityLevel(quality, true);
            Changed?.Invoke();
        }
    }

    // Tempo de jogo acumulado (pausa não conta), salvo no save.
    public static class PlayClock
    {
        public static double Seconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Seconds = 0;

        public static string Formatted
        {
            get
            {
                var t = System.TimeSpan.FromSeconds(Seconds);
                return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}min" : $"{t.Minutes}min {t.Seconds:00}s";
            }
        }
    }
}
