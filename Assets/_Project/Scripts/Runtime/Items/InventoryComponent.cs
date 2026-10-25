using UnityEngine;

namespace Planetcraft.Items
{
    public class InventoryComponent : MonoBehaviour
    {
        [SerializeField, Min(1)] int capacity = 24;
        [SerializeField] ItemAmount[] startingItems;

        public Inventory Inventory { get; private set; }

        void Awake()
        {
            Inventory = new Inventory(capacity);
            if (startingItems == null) return;
            foreach (var s in startingItems) Inventory.TryAdd(s.item, s.amount);
        }
    }
}
