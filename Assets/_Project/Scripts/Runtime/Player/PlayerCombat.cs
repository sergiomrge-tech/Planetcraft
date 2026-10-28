using System;
using Planetcraft.Building;
using Planetcraft.Combat;
using Planetcraft.Core;
using UnityEngine;

namespace Planetcraft.Player
{
    // Vida do jogador, arma de pulso e os perigos do ambiente (radiação fora de abrigo). A arma usa uma célula de energia
    // que recarrega sozinha; sem construção selecionada, o botão de construir atira.
    [RequireComponent(typeof(SurvivalStats))]
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] float maxHealth = 100f;
        [SerializeField] float maxEnergy = 100f;
        [SerializeField] float shotCost = 7f;
        [SerializeField] float shotDamage = 24f;
        [SerializeField] float fireInterval = 0.22f;
        [SerializeField] float range = 80f;

        Camera cam;
        BuildPlacer placer;
        SurvivalStats survival;
        float energy;
        float nextShot;
        float rechargeAt;
        float radiationTimer;

        public static PlayerCombat Instance { get; private set; }

        public Health Health { get; private set; }
        public float EnergyNormalized => energy / maxEnergy;

        /// <summary>Fração da vida perdida num golpe (para o clarão vermelho da tela).</summary>
        public static event Action<float> Hurt;
        public static event Action Hit;

        // Quão contaminado está o ar lá fora (1 no mundo morto, 0 quando o Ti passa de ~2 milhões).
        public static float Radiation
        {
            get
            {
                var tm = TerraformManager.Instance;
                float baseRadiation = tm == null ? 0f : Mathf.Clamp01(1f - Mathf.Log10((float)tm.Ti + 1f) / 6.3f);
                return Mathf.Max(baseRadiation, Planetcraft.Enemies.ThreatDirector.StormIntensity * 0.6f);
            }
        }

        void Awake()
        {
            Instance = this;
            survival = GetComponent<SurvivalStats>();
            Health = gameObject.AddComponent<Health>();
            Health.Max = maxHealth;
            Health.Restore();
            Health.Team = Team.Player;
            Health.Damaged += OnDamaged;
            Health.Died += _ => OnDied();
            energy = maxEnergy;
        }

        void Start()
        {
            cam = GetComponentInChildren<Camera>();
            placer = FindAnyObjectByType<BuildPlacer>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Hurt = null;
            Hit = null;
        }

        void OnDamaged(Health h, float amount, GameObject source)
        {
            Hurt?.Invoke(amount / maxHealth);
            if (amount > 1.5f) Sfx.Hit();
        }

        void OnDied()
        {
            Health.Restore();
            energy = maxEnergy;
            survival.ForceDie();
            if (Planetcraft.UI.GameHud.Instance != null)
                Planetcraft.UI.GameHud.Instance.ShowToast(Loc.T("Você apagou e acordou na base"), 4f);
        }

        void Update()
        {
            if (cam == null || UIState.MenuOpen || Time.timeScale <= 0f) return;
            float dt = Time.deltaTime;

            bool sheltered = survival.InPressurizedZone;
            float scale = Settings.ThreatScale;

            // Radiação lá fora corrói a vida aos poucos; dentro de uma base pressurizada o corpo se recupera.
            if (!sheltered && scale > 0f)
            {
                radiationTimer += dt;
                if (radiationTimer >= 1f)
                {
                    radiationTimer = 0f;
                    float dose = Radiation * 0.9f * scale;
                    if (dose > 0.05f) Health.Damage(dose);
                }
            }
            if (Time.time - Health.LastDamageTime > 6f && (sheltered || Radiation < 0.05f))
                Health.Heal((sheltered ? 3f : 1f) * dt);

            if (Time.time >= rechargeAt) energy = Mathf.Min(maxEnergy, energy + 16f * dt);
            if (placer != null && placer.Selected != null) return;
            if (GameInput.Place.IsPressed() && Time.time >= nextShot && energy >= shotCost) Fire();
        }

        void Fire()
        {
            nextShot = Time.time + fireInterval;
            energy -= shotCost;
            rechargeAt = Time.time + 0.7f;
            var t = cam.transform;
            var dir = (t.forward + t.right * UnityEngine.Random.Range(-0.004f, 0.004f) + t.up * UnityEngine.Random.Range(-0.004f, 0.004f)).normalized;
            var muzzle = t.position + t.forward * 0.6f + t.right * 0.17f - t.up * 0.14f;
            var end = CombatFx.Hitscan(t.position, dir, range, shotDamage, Team.Player, gameObject, out var hit);
            CombatFx.Tracer(muzzle, end, new Color(0.5f, 0.95f, 1f, 0.9f), 0.035f, 0.07f);
            Sfx.ShotAt(muzzle);
            if (hit != null) Hit?.Invoke();
        }
    }
}
