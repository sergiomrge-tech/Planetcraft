using System.Collections.Generic;
using Planetcraft.Building;
using Planetcraft.Combat;
using Planetcraft.Core;
using Planetcraft.Player;
using Planetcraft.Power;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Planetcraft.Enemies
{
    // Ritmo das ameaças: invasões de drones (com aviso e tempo para se preparar) e tempestades de poeira.
    // Crescem com a energia gerada e com o Ti, e respeitam o nível de ameaça das opções (0 desliga tudo).
    public class ThreatDirector : MonoBehaviour
    {
        [SerializeField] GameObject sentinelPrefab;
        [SerializeField] Volume atmosphere;
        [SerializeField] float firstRaidAfter = 420f;
        [SerializeField] Vector2 raidInterval = new(520f, 900f);
        [SerializeField] float warningSeconds = 45f;
        [SerializeField] Vector2 stormInterval = new(700f, 1300f);
        [SerializeField] float stormDuration = 80f;

        public static ThreatDirector Instance { get; private set; }

        // Radiação extra do ambiente durante a tempestade de poeira (somada pelo PlayerCombat).
        public static float StormIntensity { get; private set; }

        float raidTimer, stormTimer;
        float warningLeft;
        int pendingCount;
        float pendingAngle;
        float stormLeft;
        bool eligible;
        Fog fog;
        float baseFogDistance;
        Color baseFogColor;
        float eligibleTime;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            StormIntensity = 0f;
        }

        void Start()
        {
            raidTimer = firstRaidAfter;
            stormTimer = Random.Range(stormInterval.x, stormInterval.y) * 0.6f;
            if (atmosphere != null && atmosphere.profile.TryGet(out fog))
            {
                baseFogDistance = fog.meanFreePath.value;
                baseFogColor = fog.albedo.value;
            }
            BaseGrid.Breached += OnBreach;
        }

        void OnEnable()
        {
            if (Instance == null) Instance = this;
        }

        void OnBreach()
        {
            if (UI.GameHud.Instance != null) UI.GameHud.Instance.ShowToast(Loc.T("Brecha na base: o ar está escapando!"), 4f);
            Sfx.Alarm();
        }

        void Update()
        {
            float scale = Settings.ThreatScale;
            var tm = TerraformManager.Instance;
            if (tm == null) return;
            if (scale <= 0f)
            {
                warningLeft = 0f;
                StormIntensity = Mathf.MoveTowards(StormIntensity, 0f, Time.deltaTime);
                ApplyStorm();
                return;
            }

            // Só começa quando o jogador já tem o que defender.
            eligible = tm.Ti >= 10_000 && (BaseGrid.HasDatum || PowerGrid.Generation > 0f);
            float dt = Time.deltaTime;
            if (eligible)
            {
                eligibleTime += dt;
                if (warningLeft > 0f)
                {
                    warningLeft -= dt;
                    if (warningLeft <= 0f) LaunchRaid();
                }
                else
                {
                    raidTimer -= dt;
                    if (raidTimer <= 0f) Warn(RaidSize(tm.Ti, scale));
                }
                stormTimer -= dt;
                if (stormTimer <= 0f && stormLeft <= 0f && tm.Ti >= 8_000) StartStorm();
            }

            if (stormLeft > 0f)
            {
                stormLeft -= dt;
                StormIntensity = Mathf.MoveTowards(StormIntensity, 1f, dt * 0.3f);
                if (stormLeft <= 0f && UI.GameHud.Instance != null) UI.GameHud.Instance.ShowToast(Loc.T("A tempestade de poeira passou"), 3f);
            }
            else StormIntensity = Mathf.MoveTowards(StormIntensity, 0f, dt * 0.2f);
            ApplyStorm();
        }

        static int RaidSize(double ti, float scale)
        {
            int n = 2 + Mathf.Max(0, (int)(Mathf.Log10((float)ti) - 4f)) + (int)(PowerGrid.Generation / 30f);
            return Mathf.Clamp(Mathf.RoundToInt(n * scale), 1, 12);
        }

        // Anuncia a invasão: direção, quantidade e uma contagem para o jogador se preparar.
        public void Warn(int count, float seconds = -1f)
        {
            pendingCount = count;
            pendingAngle = Random.value * 360f;
            warningLeft = seconds > 0f ? seconds : warningSeconds;
            string dir = Compass(pendingAngle);
            if (UI.GameHud.Instance != null)
                UI.GameHud.Instance.ShowToast($"{Loc.T("Sinal hostil detectado")} ({dir}): {count} {Loc.T("drones chegam em")} {Mathf.RoundToInt(warningLeft)} s", 7f);
            Sfx.Alarm();
        }

        static string Compass(float angle)
        {
            string[] names = { "L", "NL", "N", "NO", "O", "SO", "S", "SL" };
            return Loc.T(names[Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / 45f) % 8]);
        }

        void LaunchRaid()
        {
            raidTimer = Random.Range(raidInterval.x, raidInterval.y) / Mathf.Max(0.5f, Settings.ThreatScale);
            if (sentinelPrefab == null) return;
            var centre = BaseCentre();
            for (int i = 0; i < pendingCount; i++)
            {
                float a = (pendingAngle + Random.Range(-18f, 18f)) * Mathf.Deg2Rad;
                var p = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(70f, 90f);
                var terrain = Terrain.activeTerrain;
                p.y = (terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : centre.y) + 6f;
                var go = Instantiate(sentinelPrefab, p, Quaternion.identity);
                go.name = "Sentinela invasora";
                if (go.TryGetComponent<Sentinel>(out var s)) s.raider = true;
            }
        }

        public void SpawnNow(int count) => Warn(count, 2f);

        static Vector3 BaseCentre()
        {
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var h in Health.All)
                if (h.Team == Team.Base)
                {
                    sum += h.transform.position;
                    n++;
                }
            if (n > 0) return sum / n;
            return PlayerCombat.Instance != null ? PlayerCombat.Instance.transform.position : Vector3.zero;
        }

        void StartStorm()
        {
            stormLeft = stormDuration * Random.Range(0.8f, 1.3f);
            stormTimer = Random.Range(stormInterval.x, stormInterval.y);
            if (UI.GameHud.Instance != null)
                UI.GameHud.Instance.ShowToast(Loc.T("Tempestade de poeira! Abrigue-se numa base selada."), 5f);
            Sfx.Alarm();
        }

        void ApplyStorm()
        {
            if (fog == null) return;
            float k = StormIntensity;
            fog.meanFreePath.Override(Mathf.Lerp(baseFogDistance, 22f, k));
            fog.albedo.Override(Color.Lerp(baseFogColor, new Color(0.62f, 0.42f, 0.24f), k));
        }
    }
}
