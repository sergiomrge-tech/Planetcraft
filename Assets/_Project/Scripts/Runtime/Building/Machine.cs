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
        AudioSource hum;

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
            UpdateHum();
        }

        // Máquinas que consomem energia zumbem (som 3D) enquanto funcionam; geradores e a base ficam em silêncio.
        void UpdateHum()
        {
            bool should = IsRunning && PowerDemand > 0f;
            if (should && hum == null)
            {
                var clip = Resources.Load<AudioClip>("Audio/Ambience/hum_loop");
                if (clip == null) return;
                hum = gameObject.AddComponent<AudioSource>();
                hum.clip = clip;
                hum.loop = true;
                hum.spatialBlend = 1f;
                hum.minDistance = 2.5f;
                hum.maxDistance = 22f;
                hum.rolloffMode = AudioRolloffMode.Linear;
                hum.pitch = Random.Range(0.85f, 1.2f);
                hum.time = Random.value * clip.length;
            }
            if (hum == null) return;
            hum.volume = 0.22f * Settings.EffectsVolume;
            if (should && !hum.isPlaying) hum.Play();
            else if (!should && hum.isPlaying) hum.Pause();
        }
    }
}
