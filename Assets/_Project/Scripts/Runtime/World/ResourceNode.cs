using System.Collections.Generic;
using Planetcraft.Core;
using Planetcraft.Items;
using Planetcraft.Player;
using UnityEngine;

namespace Planetcraft.World
{
    // Rocha/minério coletável uma única vez. O id fixo permite salvar quais já foram coletados.
    public class ResourceNode : MonoBehaviour, IInteractable
    {
        static readonly HashSet<string> collectedIds = new();
        static readonly List<ResourceNode> active = new();

        [SerializeField] string nodeId;
        [SerializeField] ItemDefinition item;
        [SerializeField, Min(1)] int amount = 1;

        public static IEnumerable<string> CollectedIds => collectedIds;

        public string Prompt => item != null ? $"[E] {Loc.T("Coletar")} {Loc.T(item.displayName)}" : "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            collectedIds.Clear();
            active.Clear();
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        public void Interact(GameObject interactor)
        {
            if (!interactor.TryGetComponent<InventoryComponent>(out var inv)) return;
            if (!inv.Inventory.TryAdd(item, amount)) return;
            if (!string.IsNullOrEmpty(nodeId)) collectedIds.Add(nodeId);
            Sfx.Collect();
            Destroy(gameObject);
        }

        public static void RestoreCollected(IEnumerable<string> ids)
        {
            collectedIds.Clear();
            if (ids != null) collectedIds.UnionWith(ids);
            foreach (var n in active.ToArray())
                if (collectedIds.Contains(n.nodeId)) Destroy(n.gameObject);
        }

#if UNITY_EDITOR
        public void EditorSetup(string id, ItemDefinition itemDef, int count)
        {
            nodeId = id;
            item = itemDef;
            amount = count;
        }
#endif
    }
}
