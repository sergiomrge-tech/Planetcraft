using Planetcraft.Core;
using UnityEngine;

namespace Planetcraft.World
{
    // Liga objetos (vegetação, clima, fauna) quando um estágio é alcançado.
    public class StageActivator : MonoBehaviour
    {
        [SerializeField] TerraformStage stage;
        [SerializeField] GameObject[] targets;

        void Start()
        {
            var tm = TerraformManager.Instance;
            if (tm == null) return;
            tm.TiChanged += OnTiChanged;
            OnTiChanged(tm.Ti);
        }

        void OnDestroy()
        {
            if (TerraformManager.Instance != null) TerraformManager.Instance.TiChanged -= OnTiChanged;
        }

        void OnTiChanged(double _)
        {
            bool on = TerraformManager.Instance.HasReached(stage);
            foreach (var t in targets)
                if (t != null && t.activeSelf != on) t.SetActive(on);
        }
    }
}
