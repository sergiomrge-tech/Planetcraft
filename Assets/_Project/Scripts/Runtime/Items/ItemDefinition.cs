using UnityEngine;

namespace Planetcraft.Items
{
    [CreateAssetMenu(menuName = "Planetcraft/Item")]
    public class ItemDefinition : ScriptableObject
    {
        [Tooltip("Id estável usado no save. Não mude depois de publicar.")]
        public string id;
        public string displayName;
        public Sprite icon;
        [Min(1)] public int maxStack = 1;

        [Header("Consumível")]
        public float restoreOxygen;
        public float restoreWater;
        public float restoreFood;

        public bool IsConsumable => restoreOxygen > 0f || restoreWater > 0f || restoreFood > 0f;
    }

    [System.Serializable]
    public struct ItemAmount
    {
        public ItemDefinition item;
        [Min(1)] public int amount;

        public ItemAmount(ItemDefinition item, int amount)
        {
            this.item = item;
            this.amount = amount;
        }
    }
}
