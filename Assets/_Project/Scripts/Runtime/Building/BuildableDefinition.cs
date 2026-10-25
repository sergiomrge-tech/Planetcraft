using Planetcraft.Core;
using Planetcraft.Items;
using UnityEngine;

namespace Planetcraft.Building
{
    [CreateAssetMenu(menuName = "Planetcraft/Buildable")]
    public class BuildableDefinition : ScriptableObject
    {
        [Tooltip("Id estável usado no save.")]
        public string id;
        public string displayName;
        [TextArea] public string description;
        public GameObject prefab;
        public ItemAmount[] cost;
        public double requiredTi;

        [Header("Máquina")]
        [Min(0f)] public float powerGeneration;
        [Min(0f)] public float powerDemand;
        public TerraformRate[] terraformRates;
    }
}
