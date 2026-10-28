using System.Collections.Generic;
using Planetcraft.Building;
using Planetcraft.Combat;
using Planetcraft.Core;
using UnityEngine;

namespace Planetcraft.Defense
{
    // Torre automática: gira a cabeça para o inimigo mais próximo e atira enquanto tem energia.
    [RequireComponent(typeof(Machine))]
    public class Turret : MonoBehaviour
    {
        [SerializeField] float range = 26f;
        [SerializeField] float damage = 14f;
        [SerializeField] float interval = 0.6f;
        [SerializeField] bool heavy;
        [SerializeField] Color tracer = new(0.5f, 0.95f, 1f, 0.9f);

        Machine machine;
        Transform head, muzzle;
        float nextShot;
        Health target;
        float nextScan;

        void Awake() => machine = GetComponent<Machine>();

        void Start()
        {
            head = transform.Find("Aim_Y_Head") ?? FindDeep(transform, "Aim_Y_Head");
            muzzle = FindDeep(transform, "Fx_Muzzle");
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        void Update()
        {
            if (head == null || !machine.IsRunning) return;
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.25f;
                target = Health.NearestHostile(transform.position + Vector3.up, range);
            }
            if (target == null || target.IsDead) return;

            var aim = target.transform.position + Vector3.up * 0.4f;
            var from = muzzle != null ? muzzle.position : head.position;
            var dir = (aim - from).normalized;
            // O cano do modelo aponta para -Z do Unity.
            head.rotation = Quaternion.RotateTowards(head.rotation, Quaternion.LookRotation(-dir, Vector3.up), 300f * Time.deltaTime);
            if (Time.time < nextShot || Vector3.Angle(-head.forward, dir) > 6f) return;
            nextShot = Time.time + interval;
            var end = CombatFx.Hitscan(from, dir, range + 6f, damage, Team.Base, gameObject, out _);
            CombatFx.Tracer(from, end, tracer, heavy ? 0.07f : 0.04f, 0.08f);
            Sfx.ShotAt(from, heavy);
        }
    }

    // Refletor: liga com energia e afasta os lobos da área iluminada.
    [RequireComponent(typeof(Machine))]
    public class Floodlight : MonoBehaviour
    {
        static readonly List<Floodlight> all = new();

        [SerializeField] Light[] lights;
        Machine machine;

        public bool Lit => machine != null && machine.IsRunning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => all.Clear();

        void Awake() => machine = GetComponent<Machine>();
        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        void Update()
        {
            if (lights == null) return;
            bool on = Lit;
            foreach (var l in lights)
                if (l != null && l.enabled != on) l.enabled = on;
        }

        public static bool LitNear(Vector3 point, float radius)
        {
            foreach (var f in all)
                if (f.Lit && (f.transform.position - point).sqrMagnitude < radius * radius) return true;
            return false;
        }
    }

    // Cerca eletrificada: dá choque em hostis que a atravessam enquanto houver energia.
    [RequireComponent(typeof(Machine))]
    public class ShockFence : MonoBehaviour
    {
        [SerializeField] float damagePerSecond = 28f;
        Machine machine;
        float nextSpark;

        void Awake() => machine = GetComponent<Machine>();

        void OnTriggerStay(Collider other)
        {
            if (!machine.IsRunning) return;
            var h = other.GetComponentInParent<Health>();
            if (h == null || h.Team != Team.Hostile || h.IsDead) return;
            h.Damage(damagePerSecond * Time.deltaTime, gameObject);
            if (Time.time >= nextSpark)
            {
                nextSpark = Time.time + 0.25f;
                CombatFx.Sparks(other.ClosestPoint(transform.position), Vector3.up, new Color(0.5f, 0.9f, 1f), 8);
            }
        }
    }
}
