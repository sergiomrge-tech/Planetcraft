using System.Collections.Generic;
using Planetcraft.Building;
using Planetcraft.Combat;
using Planetcraft.Core;
using UnityEngine;

namespace Planetcraft.Defense
{
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
}
