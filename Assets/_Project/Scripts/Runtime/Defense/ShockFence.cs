using System.Collections.Generic;
using Planetcraft.Building;
using Planetcraft.Combat;
using Planetcraft.Core;
using UnityEngine;

namespace Planetcraft.Defense
{
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
