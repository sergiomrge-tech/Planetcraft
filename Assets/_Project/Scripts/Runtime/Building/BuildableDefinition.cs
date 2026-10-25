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
        [Tooltip("Projeto obtido com microchip de destroço. Vazio = não precisa.")]
        public string blueprintId;
        [Tooltip("Só pode existir um no mundo (ex.: estaleiro).")]
        public bool unique;

        public bool IsUnlocked(double ti) => ti >= requiredTi && Blueprints.Has(blueprintId);

        [Header("Máquina")]
        [Min(0f)] public float powerGeneration;
        [Min(0f)] public float powerDemand;
        public TerraformRate[] terraformRates;
    }
}
