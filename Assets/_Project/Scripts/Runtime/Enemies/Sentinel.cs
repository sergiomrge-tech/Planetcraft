using System.Collections.Generic;
using Planetcraft.Combat;
using Planetcraft.Core;
using Planetcraft.Items;
using Planetcraft.Player;
using UnityEngine;

namespace Planetcraft.Enemies
{
    // Sentinela do velho mundo: drone de guerra que ainda patrulha. Voa a meia altura, mantém distância e atira.
    // Guardas rondam as ruínas e só atacam o jogador e as defesas; invasores (ThreatDirector) vão atrás da base inteira.
    [RequireComponent(typeof(Health))]
    public class Sentinel : MonoBehaviour
    {
        [SerializeField] float speed = 6.5f;
        [SerializeField] float hover = 2.6f;
        [SerializeField] float aggroRange = 36f;
        [SerializeField] float attackRange = 22f;
        [SerializeField] float preferredDistance = 13f;
        [SerializeField] float damage = 7f;
        [SerializeField] float fireInterval = 1.35f;
        [SerializeField] float patrolRadius = 18f;
        [Tooltip("Invasor: caça qualquer peça da base, não só o jogador.")]
        public bool raider;

        static readonly List<Sentinel> all = new();
        public static IReadOnlyList<Sentinel> All => all;

        Health health;
        Transform ring, muzzle;
        Terrain terrain;
        Vector3 home;
        Health target;
        Health lastAttacker;
        float nextShot, nextScan, strafe, strafeUntil;
        float bobPhase;
        Vector3 patrolPoint;

        void Awake()
        {
            health = GetComponent<Health>();
            bobPhase = Random.value * 6.28f;
            health.Team = Team.Hostile;
            health.Died += OnDied;
            health.Damaged += (_, _, src) =>
            {
                if (src != null && src.TryGetComponent<Health>(out var h)) lastAttacker = h;
            };
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        void Start()
        {
            terrain = Terrain.activeTerrain;
            home = transform.position;
            ring = transform.Find("Spin_Y_Ring");
            muzzle = transform.Find("Fx_Muzzle");
            patrolPoint = home;
            strafe = Random.value < 0.5f ? -1f : 1f;
        }

        float GroundY(Vector3 p) => terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : p.y;

        void Update()
        {
            if (ring != null) ring.Rotate(Vector3.up, 280f * Time.deltaTime, Space.Self);
            if (Settings.ThreatScale <= 0f) return;

            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.4f;
                target = FindTarget();
            }

            Vector3 desired = transform.position;
            if (target != null)
            {
                var to = target.transform.position - transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                Vector3 dir = dist > 0.01f ? to / dist : Vector3.forward;
                if (Time.time >= strafeUntil)
                {
                    strafeUntil = Time.time + Random.Range(1.5f, 3.5f);
                    strafe = Random.value < 0.5f ? -1f : 1f;
                }
                float radial = dist > preferredDistance + 2f ? 1f : dist < preferredDistance - 3f ? -0.8f : 0f;
                desired += (dir * radial + Vector3.Cross(Vector3.up, dir) * strafe * 0.6f) * speed * Time.deltaTime;
                FaceTowards(dir);
                TryShoot(dist);
            }
            else
            {
                // Sem alvo: invasores avançam rumo à base; guardas patrulham em volta do ponto de origem.
                var goal = raider ? RaidGoal() : patrolPoint;
                var to = goal - transform.position;
                to.y = 0f;
                if (to.magnitude < 2f && !raider)
                    patrolPoint = home + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * patrolRadius;
                else
                {
                    desired += to.normalized * speed * 0.7f * Time.deltaTime;
                    FaceTowards(to.normalized);
                }
            }
            desired.y = Mathf.Lerp(transform.position.y, GroundY(desired) + hover + Mathf.Sin(Time.time * 1.7f + bobPhase) * 0.25f, 3f * Time.deltaTime);
            transform.position = desired;
        }

        void FaceTowards(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            // O canhão do modelo aponta para -Z do Unity: gira o corpo para o alvo com a "frente" invertida.
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(-dir, Vector3.up), 220f * Time.deltaTime);
        }

        Vector3 RaidGoal()
        {
            var inBase = Building.BaseGrid.HasDatum ? new Vector3(0f, Building.BaseGrid.DatumY, 0f) : Vector3.zero;
            Health best = null;
            float bestSq = float.MaxValue;
            foreach (var h in Health.All)
            {
                if (h.Team != Team.Base || h.IsDead) continue;
                float sq = (h.transform.position - transform.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = h; }
            }
            if (best != null) return best.transform.position;
            return PlayerCombat.Instance != null ? PlayerCombat.Instance.transform.position : inBase;
        }

        Health FindTarget()
        {
            Health best = null;
            float bestScore = float.MaxValue;
            float range = aggroRange;
            foreach (var h in Health.All)
            {
                if (h.IsDead || h.Team == Team.Hostile) continue;
                bool isPlayer = h.Team == Team.Player;
                if (!isPlayer && !raider && !IsDefense(h)) continue;
                float d = (h.transform.position - transform.position).magnitude;
                if (d > (raider && !isPlayer ? range * 3f : range)) continue;
                // Prefere defesas e o jogador a paredes: assim uma torre "puxa" o fogo inimigo.
                float score = d * (isPlayer ? 0.8f : IsDefense(h) ? 0.9f : 1.2f);
                if (score < bestScore) { bestScore = score; best = h; }
            }
            return best;
        }

        static bool IsDefense(Health h) => h.TryGetComponent<Building.BasePiece>(out var p) && p.Kind == Building.PieceKind.Defense;

        void TryShoot(float dist)
        {
            if (Time.time < nextShot || dist > attackRange || target == null) return;
            nextShot = Time.time + fireInterval * Random.Range(0.85f, 1.25f);
            var origin = muzzle != null ? muzzle.position : transform.position;
            var aim = target.transform.position + Vector3.up * 1.0f;
            var dir = (aim - origin).normalized;
            dir += Random.insideUnitSphere * 0.035f;
            var end = CombatFx.Hitscan(origin, dir.normalized, attackRange + 8f, damage * Settings.ThreatScale, Team.Hostile, gameObject, out _);
            CombatFx.Tracer(origin, end, new Color(1f, 0.3f, 0.2f, 0.9f), 0.05f, 0.1f);
            Sfx.ShotAt(origin);
        }

        void OnDied(Health h)
        {
            Sfx.BoomAt(transform.position);
            CombatFx.Explosion(transform.position, 1f);
            Loot.Drop(transform.position, lastAttacker != null && lastAttacker.Team == Team.Player, 0.9f);
        }
    }

    // Recompensa de inimigos abatidos: entra direto no inventário do jogador.
    public static class Loot
    {
        static Dictionary<string, ItemDefinition> items;

        static ItemDefinition Find(string id)
        {
            if (items == null)
            {
                items = new Dictionary<string, ItemDefinition>();
                foreach (var i in Resources.FindObjectsOfTypeAll<ItemDefinition>()) items[i.id] = i;
            }
            return items.TryGetValue(id, out var item) ? item : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => items = null;

        public static void Drop(Vector3 position, bool byPlayer, float richness)
        {
            if (!byPlayer) return;
            var inv = Object.FindAnyObjectByType<InventoryComponent>();
            if (inv == null) return;
            var gained = new List<string>();
            void Give(string id, int amount)
            {
                var item = Find(id);
                if (item != null && amount > 0 && inv.Inventory.TryAdd(item, amount)) gained.Add($"{amount} {Loc.T(item.displayName)}");
            }
            Give("iron", Random.Range(1, 3));
            if (Random.value < 0.6f * richness) Give("silicon", 1);
            if (Random.value < 0.25f * richness) Give("titanium", 1);
            if (Random.value < 0.08f * richness) Give("alloy", 1);
            if (gained.Count > 0 && UI.GameHud.Instance != null)
                UI.GameHud.Instance.ShowToast($"{Loc.T("Sucata recuperada")}: {string.Join(", ", gained)}", 2.5f);
        }
    }
}
