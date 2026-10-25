using Planetcraft.Core;
using Planetcraft.Power;
using UnityEngine;

namespace Planetcraft.Building
{
    // Gera/consome energia e contribui para o Ti com as taxas da BuildableDefinition.
    [RequireComponent(typeof(PlacedObject))]
    public class Machine : MonoBehaviour, ITerraformContributor, IPowerNode
    {
        [Tooltip("Opcional: objeto ligado só quando a máquina está funcionando (luz, partículas).")]
        [SerializeField] GameObject runningIndicator;

        PlacedObject placed;

        BuildableDefinition Def => placed != null ? placed.Definition : null;

        public float PowerGeneration => Def != null ? Def.powerGeneration : 0f;
        public float PowerDemand => Def != null ? Def.powerDemand : 0f;
        public bool IsRunning => PowerDemand <= 0f || PowerGrid.IsPowered;

        void Awake() => placed = GetComponent<PlacedObject>();

        void OnEnable()
        {
            TerraformManager.Register(this);
            PowerGrid.Changed += RefreshIndicator;
            PowerGrid.Register(this);
            RefreshIndicator();
        }

        void OnDisable()
        {
            TerraformManager.Unregister(this);
            PowerGrid.Changed -= RefreshIndicator;
            PowerGrid.Unregister(this);
        }

        public void AccumulateRates(double[] rates)
        {
            var def = Def;
            if (def == null || def.terraformRates == null || !IsRunning) return;
            foreach (var r in def.terraformRates) rates[(int)r.parameter] += r.perSecond;
        }

        void RefreshIndicator()
        {
            if (runningIndicator != null) runningIndicator.SetActive(IsRunning);
        }
    }
}
