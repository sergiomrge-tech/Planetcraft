using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Planetcraft.World
{
    // Cobertura viva do solo: capim, ervas e flores desenhados por GPU instancing ao redor da câmera. Cada bloco
    // (16 m) gera suas instâncias de forma determinística; a densidade segue o verde pintado pelo GroundReactor,
    // então o mato cresce onde o solo reverdece e o chão fica forrado de vegetação no mundo vivo.
    public class GroundCover : MonoBehaviour
    {
        [Serializable]
        public class Layer
        {
            public string name;
            public Mesh mesh;
            public Material[] materials;
            [Tooltip("Instâncias por m² quando a camada está plena.")]
            public float perSqm = 1f;
            [Tooltip("Progresso (0-1) em que a camada começa a aparecer e quanto leva para ficar plena.")]
            public float appear;
            public float fadeIn = 0.25f;
            [Tooltip("Progresso em que a camada começa a sumir (capim seco cede ao verde); 2 = nunca.")]
            public float fadeOut = 2f;
            [Tooltip("Tamanho das manchas (m) e limiar do ruído: valores altos = canteiros esparsos.")]
            public float patchSize = 30f;
            public float cutoff;
            public Vector2 width = Vector2.one;
            public Vector2 height = Vector2.one;
            [Tooltip("Distância (m) em que a camada some; as instâncias rareiam gradualmente antes disso.")]
            public float distance = 50f;
            public int seed;
        }

        [SerializeField] Terrain terrain;
        [SerializeField] GroundReactor ground;
        [SerializeField] Layer[] layers = new Layer[0];
        [SerializeField, Min(8f)] float chunkSize = 16f;
        [Tooltip("Altura mínima (m): abaixo fica a água final.")]
        [SerializeField] float minHeight = 17f;
        [Tooltip("Blocos novos gerados por quadro (o primeiro quadro gera tudo ao redor da câmera).")]
        [SerializeField, Min(1)] int chunksPerFrame = 3;

        // Escala global de densidade (opção gráfica futura).
        public static float DensityScale = 1f;

        class Chunk
        {
            public Vector3 center;
            public int version = -1;
            public Matrix4x4[][] mats;
            public int[] counts;
        }

        readonly Dictionary<long, Chunk> chunks = new();
        readonly List<long> evict = new();
        readonly List<(Chunk chunk, float dist)> visible = new();
        readonly Plane[] planes = new Plane[6];
        Matrix4x4[][] batch;
        Matrix4x4[] draw = new Matrix4x4[1023];
        RenderParams[][] renderParams;
        bool warm;
        float maxDistance;

        void OnEnable()
        {
            if (terrain == null) terrain = GetComponent<Terrain>();
            if (ground == null) ground = GetComponent<GroundReactor>();
            maxDistance = 0f;
            foreach (var l in layers) maxDistance = Mathf.Max(maxDistance, l.distance);
            batch = new Matrix4x4[layers.Length][];
            renderParams = new RenderParams[layers.Length][];
            var bounds = new Bounds(terrain.transform.position + terrain.terrainData.size * 0.5f, terrain.terrainData.size + Vector3.one * 50f);
            for (int k = 0; k < layers.Length; k++)
            {
                batch[k] = new Matrix4x4[2048];
                var mats = layers[k].materials;
                renderParams[k] = new RenderParams[mats.Length];
                for (int m = 0; m < mats.Length; m++)
                    renderParams[k][m] = new RenderParams(mats[m])
                    {
                        shadowCastingMode = ShadowCastingMode.Off,
                        receiveShadows = true,
                        motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
                        worldBounds = bounds,
                    };
            }
        }

        void OnDisable() => chunks.Clear();

        static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

        void LateUpdate()
        {
            if (terrain == null || ground == null || layers.Length == 0) return;
            var cam = Camera.main;
            if (cam == null) return;
            float t = ground.Progress;
            if (t <= 0f) return;

            var camPos = cam.transform.position;
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            int range = Mathf.CeilToInt((maxDistance + chunkSize) / chunkSize);
            int ccx = Mathf.FloorToInt(camPos.x / chunkSize), ccz = Mathf.FloorToInt(camPos.z / chunkSize);

            visible.Clear();
            int budget = warm ? chunksPerFrame : int.MaxValue;
            for (int dz = -range; dz <= range; dz++)
            for (int dx = -range; dx <= range; dx++)
            {
                int cx = ccx + dx, cz = ccz + dz;
                var center = new Vector3((cx + 0.5f) * chunkSize, camPos.y, (cz + 0.5f) * chunkSize);
                float dist = new Vector2(center.x - camPos.x, center.z - camPos.z).magnitude - chunkSize * 0.7f;
                if (dist > maxDistance) continue;
                var bounds = new Bounds(new Vector3(center.x, terrain.transform.position.y + 20f, center.z), new Vector3(chunkSize, 60f, chunkSize));
                if (dist > chunkSize && !GeometryUtility.TestPlanesAABB(planes, bounds)) continue;

                long key = Key(cx, cz);
                if (!chunks.TryGetValue(key, out var chunk)) chunks[key] = chunk = new Chunk { center = center };
                if (chunk.version != ground.Version && budget > 0)
                {
                    Build(chunk, cx, cz, t);
                    budget--;
                }
                if (chunk.mats != null) visible.Add((chunk, Mathf.Max(0f, dist)));
            }
            warm = true;

            // Libera blocos longe da câmera.
            evict.Clear();
            float keep = maxDistance + chunkSize * 3f;
            foreach (var kv in chunks)
                if (new Vector2(kv.Value.center.x - camPos.x, kv.Value.center.z - camPos.z).magnitude > keep) evict.Add(kv.Key);
            foreach (var k in evict) chunks.Remove(k);

            for (int k = 0; k < layers.Length; k++) Draw(k);
        }

        void Draw(int k)
        {
            var layer = layers[k];
            if (layer.mesh == null) return;
            var buf = batch[k];
            int n = 0;
            foreach (var (chunk, dist) in visible)
            {
                if (dist >= layer.distance) continue;
                int count = chunk.counts[k];
                // Rareia com a distância: os primeiros N da lista (ordem aleatória) ficam.
                float f = Mathf.Clamp01((layer.distance - dist) / (layer.distance * 0.45f));
                count = Mathf.CeilToInt(count * f);
                if (count <= 0) continue;
                if (n + count > buf.Length) Array.Resize(ref buf, Mathf.Max(buf.Length * 2, n + count));
                Array.Copy(chunk.mats[k], 0, buf, n, count);
                n += count;
            }
            batch[k] = buf;
            for (int start = 0; start < n; start += 1023)
            {
                int c = Mathf.Min(1023, n - start);
                for (int s = 0; s < layer.mesh.subMeshCount; s++)
                {
                    var rp = renderParams[k][Mathf.Min(s, renderParams[k].Length - 1)];
                    Graphics.RenderMeshInstanced(rp, layer.mesh, s, buf, c, start);
                }
            }
        }

        void Build(Chunk chunk, int cx, int cz, float t)
        {
            chunk.version = ground.Version;
            chunk.mats ??= new Matrix4x4[layers.Length][];
            chunk.counts ??= new int[layers.Length];
            float originY = terrain.transform.position.y;
            float area = chunkSize * chunkSize;
            for (int k = 0; k < layers.Length; k++)
            {
                var layer = layers[k];
                float grow = Mathf.Clamp01((t - layer.appear) / Mathf.Max(0.01f, layer.fadeIn));
                if (layer.fadeOut < 2f) grow *= 1f - Mathf.SmoothStep(0f, 1f, (t - layer.fadeOut) / 0.25f);
                int candidates = Mathf.CeilToInt(area * layer.perSqm * DensityScale);
                var list = chunk.mats[k];
                if (list == null || list.Length < candidates) list = chunk.mats[k] = new Matrix4x4[candidates];
                int n = 0;
                if (grow > 0f)
                {
                    // Semente por bloco e camada: o mesmo bloco sempre sorteia as mesmas posições.
                    var rng = new System.Random(cx * 73856093 ^ cz * 19349663 ^ (layer.seed + k) * 83492791);
                    float scale = terrain.terrainData.size.x / Mathf.Max(1f, layer.patchSize);
                    for (int i = 0; i < candidates; i++)
                    {
                        float x = (cx + (float)rng.NextDouble()) * chunkSize;
                        float z = (cz + (float)rng.NextDouble()) * chunkSize;
                        float rank = (float)rng.NextDouble();
                        float yaw = (float)rng.NextDouble() * 360f;
                        float sw = Mathf.Lerp(layer.width.x, layer.width.y, (float)rng.NextDouble());
                        float sh = Mathf.Lerp(layer.height.x, layer.height.y, (float)rng.NextDouble());
                        float green = ground.GreenAt(x, z);
                        if (green <= 0f) continue;
                        float u = x / terrain.terrainData.size.x, v = z / terrain.terrainData.size.z;
                        float noise = Mathf.PerlinNoise(u * scale + 31.7f * (k + 1), v * scale + 31.7f * (k + 1)) * 0.75f +
                                      Mathf.PerlinNoise(u * scale * 3.1f + 31.7f * (k + 1), v * scale * 3.1f + 31.7f * (k + 1)) * 0.25f;
                        float patch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(layer.cutoff, layer.cutoff + 0.25f, noise));
                        if (rank >= patch * green * grow) continue;
                        float y = terrain.SampleHeight(new Vector3(x, 0f, z)) + originY;
                        if (y < minHeight) continue;
                        list[n++] = Matrix4x4.TRS(new Vector3(x, y - 0.02f, z), Quaternion.Euler(0f, yaw, 0f), new Vector3(sw, sh, sw));
                    }
                }
                chunk.counts[k] = n;
            }
        }
    }
}
