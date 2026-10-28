using UnityEngine;

namespace Planetcraft.World
{
    // Pinta o solo pelos alphamaps: rocha nas encostas íngremes (sempre), solo queimado no resto e o
    // verde se espalhando — começa nas partes baixas e planas (beira da água) e sobe conforme o Ti cresce.
    public class GroundReactor : TerraformReactor
    {
        [SerializeField] Terrain terrain;
        [SerializeField] int deadLayer = 0;
        [SerializeField] int rockLayer = 1;
        [SerializeField] int greenLayer = 2;
        [Tooltip("Inclinação (graus) em que a rocha começa / domina.")]
        [SerializeField] Vector2 rockSlope = new(26f, 38f);
        [SerializeField, Range(0.01f, 0.5f)] float edgeSoftness = 0.08f;
        [Tooltip("Reaplica os alphamaps só quando o progresso muda pelo menos isso.")]
        [SerializeField, Range(0.001f, 0.1f)] float applyStep = 0.01f;

        TerrainData data;
        float[,] mask;
        float[,] rock;
        float[,,] maps;
        float[,] greenMap;
        float lastApplied = -1f;

        protected override void Start()
        {
            if (terrain == null || terrain.terrainData == null ||
                terrain.terrainData.alphamapLayers <= Mathf.Max(greenLayer, rockLayer, deadLayer))
            {
                enabled = false;
                return;
            }

            // Trabalha numa cópia para não alterar o asset (no editor isso ficaria salvo).
            data = Instantiate(terrain.terrainData);
            terrain.terrainData = data;
            if (terrain.TryGetComponent<TerrainCollider>(out var col)) col.terrainData = data;

            BuildMasks();
            base.Start();
        }

        // mask: valor baixo = fica verde primeiro (baixo, plano, com ruído para bordas orgânicas).
        // rock: peso da rocha pela inclinação, com ruído para a transição não ficar em faixas.
        void BuildMasks()
        {
            int res = data.alphamapResolution;
            mask = new float[res, res];
            rock = new float[res, res];
            maps = new float[res, res, data.alphamapLayers];
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float u = x / (float)(res - 1), v = y / (float)(res - 1);
                float h = data.GetInterpolatedHeight(u, v) / data.size.y;
                float slope = data.GetSteepness(u, v);
                float noise = Mathf.PerlinNoise(u * 14f + 3.1f, v * 14f + 8.7f);
                mask[y, x] = Mathf.Clamp01(h * 1.5f + slope / 90f * 0.9f + (noise - 0.5f) * 0.3f);
                float jitter = (Mathf.PerlinNoise(u * 40f + 1.7f, v * 40f + 5.3f) - 0.5f) * 8f;
                rock[y, x] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rockSlope.x, rockSlope.y, slope + jitter));
            }
        }

        // Progresso (0-1) já aplicado ao solo e um contador que muda a cada reaplicação: a cobertura de capim
        // (GroundCover) regenera seus blocos quando o contador muda.
        public float Progress => Mathf.Max(0f, lastApplied);
        public int Version { get; private set; }

        // Peso do verde (0-1) num ponto do mundo, o mesmo que pinta a textura do solo.
        public float GreenAt(float worldX, float worldZ)
        {
            if (greenMap == null || data == null) return 0f;
            int res = greenMap.GetLength(0);
            var origin = terrain.transform.position;
            int x = Mathf.Clamp(Mathf.RoundToInt((worldX - origin.x) / data.size.x * (res - 1)), 0, res - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt((worldZ - origin.z) / data.size.z * (res - 1)), 0, res - 1);
            return greenMap[y, x];
        }

        protected override void Apply(float t)
        {
            if (data == null) return;
            bool edge = t <= 0f || t >= 1f;
            if (!edge && Mathf.Abs(t - lastApplied) < applyStep) return;
            if (Mathf.Approximately(t, lastApplied)) return;
            lastApplied = t;

            int res = data.alphamapResolution;
            int layers = data.alphamapLayers;
            if (greenMap == null) greenMap = new float[res, res];
            float threshold = t * 1.15f;
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float r = rock[y, x];
                float green = t <= 0f ? 0f : Mathf.Clamp01((threshold - mask[y, x]) / edgeSoftness) * (1f - r);
                for (int l = 0; l < layers; l++) maps[y, x, l] = 0f;
                maps[y, x, rockLayer] = r;
                maps[y, x, greenLayer] = green;
                maps[y, x, deadLayer] = Mathf.Max(0f, 1f - r - green);
                greenMap[y, x] = green;
            }
            data.SetAlphamaps(0, 0, maps);
            Version++;
        }
    }
}
