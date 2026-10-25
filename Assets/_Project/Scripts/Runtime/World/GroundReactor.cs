using UnityEngine;

namespace Planetcraft.World
{
    // Faz o verde se espalhar pelo terreno: começa nas partes baixas e planas (beira da água)
    // e sobe as encostas conforme o Ti cresce. Pinta a camada `greenLayer` nos alphamaps.
    public class GroundReactor : TerraformReactor
    {
        [SerializeField] Terrain terrain;
        [SerializeField] int greenLayer = 1;
        [SerializeField, Range(0.01f, 0.5f)] float edgeSoftness = 0.08f;
        [Tooltip("Reaplica os alphamaps só quando o progresso muda pelo menos isso.")]
        [SerializeField, Range(0.001f, 0.1f)] float applyStep = 0.01f;

        TerrainData data;
        float[,] mask;
        float[,,] maps;
        float lastApplied = -1f;

        protected override void Start()
        {
            if (terrain == null || terrain.terrainData == null || terrain.terrainData.alphamapLayers <= greenLayer)
            {
                enabled = false;
                return;
            }

            // Trabalha numa cópia para não alterar o asset (no editor isso ficaria salvo).
            data = Instantiate(terrain.terrainData);
            terrain.terrainData = data;
            if (terrain.TryGetComponent<TerrainCollider>(out var col)) col.terrainData = data;

            BuildMask();
            base.Start();
        }

        // Valor baixo = fica verde primeiro: altura baixa, pouca inclinação, com ruído para bordas orgânicas.
        void BuildMask()
        {
            int res = data.alphamapResolution;
            mask = new float[res, res];
            maps = new float[res, res, data.alphamapLayers];
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float u = x / (float)(res - 1), v = y / (float)(res - 1);
                float h = data.GetInterpolatedHeight(u, v) / data.size.y;
                float steep = data.GetSteepness(u, v) / 90f;
                float noise = Mathf.PerlinNoise(u * 14f + 3.1f, v * 14f + 8.7f);
                mask[y, x] = Mathf.Clamp01(h * 1.5f + steep * 0.9f + (noise - 0.5f) * 0.3f);
            }
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
            float threshold = t * 1.15f;
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float green = t <= 0f ? 0f : Mathf.Clamp01((threshold - mask[y, x]) / edgeSoftness);
                for (int l = 0; l < layers; l++) maps[y, x, l] = 0f;
                maps[y, x, 0] = 1f - green;
                maps[y, x, greenLayer] = green;
            }
            data.SetAlphamaps(0, 0, maps);
        }
    }
}
