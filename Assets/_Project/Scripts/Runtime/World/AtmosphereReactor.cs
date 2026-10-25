using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Planetcraft.World
{
    // Atmosfera da Terra pós-guerra, dirigida pelo Ti (HDRP):
    // início = ar rarefeito e cheio de cinza (céu escuro, poeira marrom, neblina densa);
    // fim = atmosfera terrestre normal (céu azul, ar limpo, horizonte distante).
    public class AtmosphereReactor : TerraformReactor
    {
        [SerializeField] Volume volume;
        [SerializeField] Light sun;
        [Tooltip("Fração da densidade de ar terrestre ao longo do progresso.")]
        [SerializeField] AnimationCurve airDensity = AnimationCurve.EaseInOut(0f, 0.08f, 1f, 1f);
        [Tooltip("Densidade de aerossóis (cinza/poeira) ao longo do progresso.")]
        [SerializeField] AnimationCurve aerosolDensity = AnimationCurve.EaseInOut(0f, 0.75f, 1f, 0.03f);
        [SerializeField] Gradient aerosolTint = Gradient2(new Color(0.42f, 0.33f, 0.26f), new Color(0.9f, 0.9f, 0.9f));
        [Tooltip("Distância média de visibilidade da neblina (m).")]
        [SerializeField] AnimationCurve fogMeanFreePath = AnimationCurve.EaseInOut(0f, 90f, 1f, 900f);
        [SerializeField] Gradient fogAlbedo = Gradient2(new Color(0.55f, 0.45f, 0.38f), new Color(0.85f, 0.9f, 1f));
        [SerializeField] Gradient sunColor = Gradient2(new Color(1f, 0.72f, 0.5f), new Color(1f, 0.97f, 0.92f));

        PhysicallyBasedSky sky;
        Fog fog;
        float baseAirR, baseAirG, baseAirB;

        protected override void Start()
        {
            if (volume != null)
            {
                var profile = volume.profile; // instância própria em runtime
                if (profile.TryGet(out sky))
                {
                    baseAirR = sky.airDensityR.value;
                    baseAirG = sky.airDensityG.value;
                    baseAirB = sky.airDensityB.value;
                    sky.airDensityR.overrideState = sky.airDensityG.overrideState = sky.airDensityB.overrideState = true;
                    sky.aerosolDensity.overrideState = sky.aerosolTint.overrideState = true;
                }
                if (profile.TryGet(out fog))
                    fog.meanFreePath.overrideState = fog.albedo.overrideState = true;
            }
            base.Start();
        }

        protected override void Apply(float t)
        {
            if (sky != null)
            {
                float air = airDensity.Evaluate(t);
                sky.airDensityR.value = baseAirR * air;
                sky.airDensityG.value = baseAirG * air;
                sky.airDensityB.value = baseAirB * air;
                sky.aerosolDensity.value = aerosolDensity.Evaluate(t);
                sky.aerosolTint.value = aerosolTint.Evaluate(t);
            }
            if (fog != null)
            {
                fog.meanFreePath.value = fogMeanFreePath.Evaluate(t);
                fog.albedo.value = fogAlbedo.Evaluate(t);
            }
            if (sun != null) sun.color = sunColor.Evaluate(t);
        }

        static Gradient Gradient2(Color from, Color to)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }
}
