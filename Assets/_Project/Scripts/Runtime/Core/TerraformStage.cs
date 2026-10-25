using UnityEngine;

namespace Planetcraft.Core
{
    [CreateAssetMenu(menuName = "Planetcraft/Terraform Stage")]
    public class TerraformStage : ScriptableObject
    {
        public string displayName;
        public double requiredTi;
        [TextArea] public string description;
    }
}
