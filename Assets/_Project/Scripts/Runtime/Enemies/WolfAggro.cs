using Planetcraft.Combat;
using Planetcraft.Core;
using Planetcraft.Player;
using Planetcraft.World;
using UnityEngine;

namespace Planetcraft.Enemies
{
    // Lobo: pastava em paz, agora persegue e morde quem chega perto (jogador e construções). Foge ferido e é afastado
    // por refletores acesos. Com a ameaça desligada nas opções volta a ser só um animal.
    [RequireComponent(typeof(Health), typeof(AnimalWander))]
    public class WolfAggro : MonoBehaviour
    {
        [SerializeField] float aggroRange = 22f;
        [SerializeField] float leashRange = 50f;
        [SerializeField] float runSpeed = 7f;
        [SerializeField] float biteDamage = 9f;
        [SerializeField] float biteInterval = 1.1f;
        [SerializeField] Animator animator;

        static readonly int MovingId = Animator.StringToHash("Moving");

        Health health;
        AnimalWander wander;
        Terrain terrain;
        Health target;
        bool chasing;
        float nextBite, nextScan;

        void Awake()
        {
            health = GetComponent<Health>();
            health.Team = Team.Hostile;
            wander = GetComponent<AnimalWander>();
            health.Died += _ => Loot.Drop(transform.position, true, 0.5f);
        }

        void Start() => terrain = Terrain.activeTerrain;

        void Update()
        {
            if (Settings.ThreatScale <= 0f || terrain == null)
            {
                Stop();
                return;
            }
            bool wounded = health.Normalized < 0.3f;
            bool scared = Defense.Floodlight.LitNear(transform.position, 18f);

            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.5f;
                target = wounded || scared ? null : FindTarget();
            }
            if (target == null || target.IsDead)
            {
                Stop();
                return;
            }

            var to = target.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist > leashRange)
            {
                target = null;
                return;
            }
            chasing = true;
            wander.enabled = false;
            if (animator != null) animator.SetBool(MovingId, dist > 2f);
            if (dist > 1.8f)
            {
                var dir = to / dist;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 420f * Time.deltaTime);
                var p = transform.position + transform.forward * (runSpeed * Time.deltaTime);
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
                transform.position = p;
            }
            else if (Time.time >= nextBite)
            {
                nextBite = Time.time + biteInterval;
                target.Damage(biteDamage * Settings.ThreatScale, gameObject);
                Sfx.Hit();
            }
        }

        void Stop()
        {
            if (!chasing) return;
            chasing = false;
            wander.enabled = true;
        }

        Health FindTarget()
        {
            Health best = null;
            float bestD = aggroRange;
            foreach (var h in Health.All)
            {
                if (h.IsDead || h.Team == Team.Hostile) continue;
                float range = h.Team == Team.Player ? aggroRange : 12f;
                float d = Vector3.Distance(h.transform.position, transform.position);
                if (d < range && d < bestD)
                {
                    bestD = d;
                    best = h;
                }
            }
            return best;
        }
    }
}
