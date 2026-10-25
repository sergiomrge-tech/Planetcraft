using System;
using System.Collections.Generic;

namespace Planetcraft.Items
{
    // Inventário de slots. Cada slot guarda um tipo de item até o maxStack dele.
    public class Inventory
    {
        public struct Slot
        {
            public ItemDefinition item;
            public int amount;
            public bool IsEmpty => item == null || amount <= 0;
        }

        readonly Slot[] slots;

        public event Action Changed;

        public Inventory(int capacity) => slots = new Slot[capacity];

        public int Capacity => slots.Length;
        public Slot this[int index] => slots[index];

        public int Count(ItemDefinition item)
        {
            int n = 0;
            foreach (var s in slots)
                if (!s.IsEmpty && s.item == item) n += s.amount;
            return n;
        }

        public bool Has(IEnumerable<ItemAmount> costs)
        {
            if (costs == null) return true;
            foreach (var c in costs)
                if (Count(c.item) < c.amount) return false;
            return true;
        }

        public int SpaceFor(ItemDefinition item)
        {
            int space = 0;
            foreach (var s in slots)
            {
                if (s.IsEmpty) space += item.maxStack;
                else if (s.item == item) space += item.maxStack - s.amount;
            }
            return space;
        }

        public bool TryAdd(ItemDefinition item, int amount = 1)
        {
            if (item == null || amount <= 0 || SpaceFor(item) < amount) return false;
            for (int i = 0; i < slots.Length && amount > 0; i++)
                if (!slots[i].IsEmpty && slots[i].item == item) amount = Fill(i, item, amount);
            for (int i = 0; i < slots.Length && amount > 0; i++)
                if (slots[i].IsEmpty) amount = Fill(i, item, amount);
            Changed?.Invoke();
            return true;
        }

        public bool TryRemove(ItemDefinition item, int amount = 1)
        {
            if (item == null || Count(item) < amount) return false;
            for (int i = slots.Length - 1; i >= 0 && amount > 0; i--)
            {
                if (slots[i].IsEmpty || slots[i].item != item) continue;
                int taken = Math.Min(slots[i].amount, amount);
                amount -= taken;
                int left = slots[i].amount - taken;
                slots[i] = left > 0 ? new Slot { item = item, amount = left } : default;
            }
            Changed?.Invoke();
            return true;
        }

        public bool TryRemoveAll(IReadOnlyList<ItemAmount> costs)
        {
            if (!Has(costs)) return false;
            if (costs != null)
                foreach (var c in costs) TryRemove(c.item, c.amount);
            return true;
        }

        public void SetSlot(int index, ItemDefinition item, int amount)
        {
            slots[index] = item != null && amount > 0 ? new Slot { item = item, amount = amount } : default;
            Changed?.Invoke();
        }

        public void Clear()
        {
            Array.Clear(slots, 0, slots.Length);
            Changed?.Invoke();
        }

        int Fill(int index, ItemDefinition item, int amount)
        {
            int current = slots[index].IsEmpty ? 0 : slots[index].amount;
            int moved = Math.Min(item.maxStack - current, amount);
            slots[index] = new Slot { item = item, amount = current + moved };
            return amount - moved;
        }
    }
}
