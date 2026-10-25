using Planetcraft.Core;
using UnityEngine;

namespace Planetcraft.World
{
    // Nuvens crescem com o Ti; a partir do estágio de chuva, alterna períodos secos e chuvosos.
    // A chuva engrossa as nuvens, escurece o sol e toca o som de chuva.
    public class WeatherController : MonoBehaviour
    {
        [Header("Nuvens")]
        [SerializeField] Renderer cloudLayer;
        [SerializeField] double cloudTiStart = 2_000;
        [SerializeField] double cloudTiEnd = 3_000_000;
        [SerializeField] AnimationCurve coverageByProgress = AnimationCurve.EaseInOut(0f, 0f, 1f, 0.55f);
        [SerializeField, Range(0f, 1f)] float rainCoverageBoost = 0.35f;
        [SerializeField] float coverageChangeSpeed = 0.05f;

        [Header("Chuva")]
        [SerializeField] TerraformStage rainStage;
        [SerializeField] ParticleSystem rain;
        [SerializeField] AudioSource rainAudio;
        [SerializeField] Transform followTarget;
        [SerializeField] float rainHeight = 25f;
        [SerializeField] float maxRainRate = 2500f;
        [SerializeField, Range(0f, 1f)] float maxRainVolume = 0.5f;
        [SerializeField] Vector2 rainDuration = new(40f, 90f);
        [SerializeField] Vector2 dryDuration = new(60f, 180f);
        [SerializeField] float intensityChangeSpeed = 0.15f;

        [Header("Luz")]
        [SerializeField] Light sun;
        [SerializeField, Range(0f, 1f)] float rainSunDimming = 0.45f;

        static readonly int CoverageId = Shader.PropertyToID("_Coverage");

        Material cloudMaterial;
        ParticleSystem.EmissionModule emission;
        float baseSunIntensity;
        bool raining;
        float stateTimer;
        float intensity;
        float coverage;

        public float RainIntensity => intensity;

        void Start()
        {
            var tm = TerraformManager.Instance;
            if (tm == null)
            {
                enabled = false;
                return;
            }
            tm.StageReached += OnStageReached;

            if (cloudLayer != null) cloudMaterial = cloudLayer.material;
            if (sun != null) baseSunIntensity = sun.intensity;
            if (rain != null)
            {
                emission = rain.emission;
                emission.rateOverTime = 0f;
                rain.Play();
            }
            if (rainAudio != null)
            {
                rainAudio.clip = ProceduralAudio.RainLoop();
                rainAudio.loop = true;
                rainAudio.volume = 0f;
                rainAudio.Play();
            }

            // O primeiro ciclo seco é mais curto para a chuva aparecer logo.
            stateTimer = Random.Range(dryDuration.x, dryDuration.y) * 0.3f;
            coverage = BaseCoverage(tm.Ti);
            ApplyCoverage();
        }

        void OnDestroy()
        {
            if (TerraformManager.Instance != null) TerraformManager.Instance.StageReached -= OnStageReached;
        }

        void OnStageReached(TerraformStage stage)
        {
            if (stage == rainStage) StartRain();
        }

        public void ForceRain()
        {
            StartRain();
            intensity = 1f;
        }

        void StartRain()
        {
            raining = true;
            stateTimer = Random.Range(rainDuration.x, rainDuration.y);
        }

        void Update()
        {
            var tm = TerraformManager.Instance;
            if (tm == null) return;

            if (rainStage != null && !tm.HasReached(rainStage))
            {
                raining = false;
            }
            else
            {
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    if (raining)
                    {
                        raining = false;
                        stateTimer = Random.Range(dryDuration.x, dryDuration.y);
                    }
                    else
                    {
                        StartRain();
                    }
                }
            }

            intensity = Mathf.MoveTowards(intensity, raining ? 1f : 0f, intensityChangeSpeed * Time.deltaTime);

            float target = Mathf.Clamp01(BaseCoverage(tm.Ti) + intensity * rainCoverageBoost);
            // Saltos grandes (carregar save) aplicam na hora; o resto transiciona suave.
            coverage = Mathf.Abs(target - coverage) > 0.25f
                ? target
                : Mathf.MoveTowards(coverage, target, coverageChangeSpeed * Time.deltaTime);
            ApplyCoverage();

            if (rain != null)
            {
                emission.rateOverTime = intensity * maxRainRate;
                if (followTarget != null) rain.transform.position = followTarget.position + Vector3.up * rainHeight;
            }
            if (rainAudio != null) rainAudio.volume = intensity * maxRainVolume;
            if (sun != null) sun.intensity = baseSunIntensity * (1f - intensity * rainSunDimming);
        }

        float BaseCoverage(double ti) =>
            coverageByProgress.Evaluate(TerraformReactor.LogProgress(ti, cloudTiStart, cloudTiEnd));

        void ApplyCoverage()
        {
            if (cloudMaterial != null) cloudMaterial.SetFloat(CoverageId, coverage);
        }
    }
}
