using UnityEngine;

namespace Planetcraft.World
{
    // Céu, névoa e cor do sol vão de planeta morto para atmosfera respirável.
    public class SkyReactor : TerraformReactor
    {
        [SerializeField] Light sun;
        [SerializeField] Gradient skyTint = Gradient2(new Color(0.55f, 0.3f, 0.2f), new Color(0.45f, 0.55f, 0.75f));
        [SerializeField] AnimationCurve atmosphereThickness = AnimationCurve.Linear(0f, 0.25f, 1f, 1.1f);
        [SerializeField] Gradient fogColor = Gradient2(new Color(0.45f, 0.3f, 0.22f), new Color(0.65f, 0.75f, 0.85f));
        [SerializeField] AnimationCurve fogDensity = AnimationCurve.Linear(0f, 0.006f, 1f, 0.0015f);
        [SerializeField] Gradient sunColor = Gradient2(new Color(1f, 0.75f, 0.55f), new Color(1f, 0.96f, 0.9f));
        [Tooltip("Cor que a água reflete (global _PlanetSkyColor).")]
        [SerializeField] Gradient reflectionColor = Gradient2(new Color(0.45f, 0.32f, 0.26f), new Color(0.55f, 0.72f, 0.92f));

        static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
        static readonly int ThicknessId = Shader.PropertyToID("_AtmosphereThickness");
        static readonly int ReflectionId = Shader.PropertyToID("_PlanetSkyColor");

        Material skyInstance;

        protected override void Start()
        {
            // Copia o skybox para não alterar o asset durante o Play no editor.
            if (RenderSettings.skybox != null)
            {
                skyInstance = new Material(RenderSettings.skybox);
                RenderSettings.skybox = skyInstance;
            }
            base.Start();
        }

        protected override void Apply(float t)
        {
            if (skyInstance != null)
            {
                if (skyInstance.HasProperty(SkyTintId)) skyInstance.SetColor(SkyTintId, skyTint.Evaluate(t));
                if (skyInstance.HasProperty(ThicknessId)) skyInstance.SetFloat(ThicknessId, atmosphereThickness.Evaluate(t));
            }
            RenderSettings.fogColor = fogColor.Evaluate(t);
            RenderSettings.fogDensity = fogDensity.Evaluate(t);
            if (sun != null) sun.color = sunColor.Evaluate(t);
            Shader.SetGlobalColor(ReflectionId, reflectionColor.Evaluate(t));
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
