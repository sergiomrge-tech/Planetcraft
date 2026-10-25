using Planetcraft.Core;
using Planetcraft.Items;
using Planetcraft.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Planetcraft.Building
{
    // Mirar numa construção + X desmonta e devolve o custo ao inventário.
    public class Deconstructor : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] InventoryComponent inventory;
        [SerializeField] BuildPlacer placer;
        [SerializeField] float range = 6f;

        public string Prompt { get; private set; }

        void Update()
        {
            Prompt = null;
            if (UIState.MenuOpen || (placer != null && placer.Selected != null)) return;

            var t = cam.transform;
            if (!Physics.Raycast(t.position, t.forward, out var hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            var placed = hit.collider.GetComponentInParent<PlacedObject>();
            if (placed == null || placed.Definition == null) return;

            string blocked = BlockReason(placed);
            Prompt = blocked ?? $"[X] Desmontar {placed.Definition.displayName}";
            var kb = Keyboard.current;
            if (blocked == null && kb != null && kb.xKey.wasPressedThisFrame) Deconstruct(placed);
        }

        string BlockReason(PlacedObject placed)
        {
            if (placed.TryGetComponent<Shipyard>(out _) && ShipProgress.Count > 0)
                return "A nave-mãe está em montagem neste estaleiro";
            if (placed.TryGetComponent<InventoryComponent>(out var storage) && !storage.Inventory.IsEmpty)
                return "Esvazie o baú antes de desmontar";
            if (placed.TryGetComponent<PressurizedZone>(out _) && placed.TryGetComponent<Collider>(out var zone)
                && zone.bounds.Contains(transform.position + Vector3.up))
                return "Saia da base para desmontar";
            var cost = placed.Definition.cost;
            if (cost != null)
                foreach (var c in cost)
                    if (c.item != null && inventory.Inventory.SpaceFor(c.item) < c.amount) return "Inventário cheio";
            return null;
        }

        void Deconstruct(PlacedObject placed)
        {
            var cost = placed.Definition.cost;
            if (cost != null)
                foreach (var c in cost) inventory.Inventory.TryAdd(c.item, c.amount);
            Sfx.Deconstruct();
            Destroy(placed.gameObject);
        }
    }
}
