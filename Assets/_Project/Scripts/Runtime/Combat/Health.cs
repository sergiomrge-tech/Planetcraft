using System;
using System.Collections.Generic;
using UnityEngine;

namespace Planetcraft.Combat
{
    public enum Team { Player, Base, Hostile }

    // Vida de qualquer coisa que pode ser atacada: jogador, peças da base, defesas e inimigos.
    public class Health : MonoBehaviour
    {
        static readonly List<Health> all = new();

        [SerializeField] float max = 100f;
        [SerializeField] Team team = Team.Base;
        [Tooltip("Fração do dano absorvida (0-0,9).")]
        [SerializeField, Range(0f, 0.9f)] float armor;
        [SerializeField] bool destroyOnDeath = true;

        float current;
        bool dead;

        public static IReadOnlyList<Health> All => all;
        public Team Team { get => team; set => team = value; }
        public float Max { get => max; set { max = value; current = Mathf.Min(current, max); } }
        public float Current => current;
        public float Normalized => max > 0f ? current / max : 0f;
        public bool IsDead => dead;
        public float LastDamageTime { get; private set; } = -100f;

        public event Action<Health, float, GameObject> Damaged;
        public event Action<Health> Died;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => all.Clear();

        void Awake() => current = max;
        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        // Devolve true se o golpe contou (alvo vivo). `source` é quem atacou, para a IA reagir.
        public bool Damage(float amount, GameObject source = null)
        {
            if (dead || amount <= 0f) return false;
            float dealt = amount * (1f - armor);
            current = Mathf.Max(0f, current - dealt);
            LastDamageTime = Time.time;
            Damaged?.Invoke(this, dealt, source);
            if (current <= 0f) Die();
            return true;
        }

        public void Heal(float amount)
        {
            if (dead) return;
            current = Mathf.Min(max, current + amount);
        }

        public void Restore()
        {
            dead = false;
            current = max;
        }

        void Die()
        {
            dead = true;
            Died?.Invoke(this);
            if (destroyOnDeath) Destroy(gameObject);
        }

        // Alvo hostil mais próximo de um ponto, dentro do alcance (torres e armas).
        public static Health NearestHostile(Vector3 from, float range)
        {
            Health best = null;
            float bestSq = range * range;
            foreach (var h in all)
            {
                if (h.team != Team.Hostile || h.dead) continue;
                float sq = (h.transform.position - from).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = h;
                }
            }
            return best;
        }
    }
}
