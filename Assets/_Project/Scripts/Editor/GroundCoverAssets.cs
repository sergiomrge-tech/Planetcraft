using System.Collections.Generic;
using System.IO;
using System.Linq;
using Planetcraft.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using UnityEditor.Rendering.HighDefinition;

namespace Planetcraft.EditorTools
{
    // Cobertura viva do solo: capim em tufos (malha própria, instanciada pelo terreno) e flores/ervas escaneadas do
    // Poly Haven. O terreno desenha tudo por GPU instancing e a densidade vem do GroundReactor, ligada ao verde do solo.
    public static class GroundCoverAssets
    {
        const string Dir = "Assets/_Project/Generated/GroundCover";
        const string PolyHavenModels = "Assets/ThirdParty/PolyHaven/Models";
        const string FoliageProfile =
            "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipelineResources/FoliageDiffusionProfile.asset";

        // Estágios do verde: o capim seco abre caminho, depois vêm o capim alto, as ervas e as flores.
        static GroundCover.Layer Make(string name, Mesh mesh, Material[] mats, float perSqm, float appear, float patch, float cutoff,
            float distance, Vector2 width, Vector2 height, float fadeIn = 0.25f, float fadeOut = 2f) =>
            new()
            {
                name = name, mesh = mesh, materials = mats, perSqm = perSqm, appear = appear, patchSize = patch, cutoff = cutoff,
                distance = distance, width = width, height = height, fadeIn = fadeIn, fadeOut = fadeOut, seed = name.GetHashCode() & 0xffff,
            };

        public static void Configure(Terrain terrain, GroundReactor ground)
        {
            Directory.CreateDirectory(Dir);
            var grassFresh = GrassMaterial("grass_fresh", new Color(0.78f, 1.0f, 0.62f));
            var grassDry = GrassMaterial("grass_dry", new Color(1.0f, 0.88f, 0.5f));

            var layers = new List<GroundCover.Layer>
            {
                // O chão forrado: capim curto por toda parte, tufos altos em manchas e um pouco de capim seco no começo.
                Make("grass_short", GrassMesh("grass_short", 28, 0.34f, 0.22f, 0.45f, 0.02f, 11), new[] { grassFresh },
                     3.2f, 0.02f, 45f, 0.0f, 42f, new(1.1f, 1.6f), new(0.8f, 1.2f), 0.2f),
                Make("grass_tall", GrassMesh("grass_tall", 22, 0.34f, 0.55f, 0.95f, 0.02f, 23), new[] { grassFresh },
                     1.3f, 0.15f, 20f, 0.25f, 38f, new(1.0f, 1.5f), new(0.8f, 1.3f)),
                Make("grass_dry", GrassMesh("grass_dry", 16, 0.34f, 0.45f, 0.8f, 0.02f, 37), new[] { grassDry },
                     1.2f, 0.0f, 24f, 0.40f, 38f, new(1.0f, 1.5f), new(0.8f, 1.2f), 0.1f, 0.65f),
            };
            // Ervas e flores escaneadas: cada família ocupa manchas próprias, então surgem canteiros de uma cor.
            AddScanned(layers, "dandelion_01", Vector2.one, 0.9f, 0.30f, 14f, 0.50f, 32f);
            AddScanned(layers, "periwinkle_plant", Vector2.one, 0.8f, 0.40f, 12f, 0.52f, 32f);
            AddScanned(layers, "flower_gazania", Vector2.one, 0.7f, 0.50f, 10f, 0.52f, 32f);
            AddScanned(layers, "flower_ursinia_a", Vector2.one, 0.9f, 0.45f, 13f, 0.50f, 32f);
            AddScanned(layers, "flower_ursinia_c", Vector2.one, 0.7f, 0.65f, 8f, 0.55f, 32f);
            AddScanned(layers, "flower_empodium_a", Vector2.one, 0.7f, 0.55f, 9f, 0.52f, 32f);
            AddScanned(layers, "flower_empodium_c", Vector2.one, 0.6f, 0.70f, 8f, 0.55f, 32f);
            AddScanned(layers, "shrub_sorrel_01_a", Vector2.one, 1.0f, 0.35f, 15f, 0.45f, 34f);
            AddScanned(layers, "shrub_sorrel_01_d", Vector2.one, 0.7f, 0.50f, 11f, 0.52f, 34f);
            AddScanned(layers, "weed_plant_02_a", Vector2.one, 0.9f, 0.25f, 16f, 0.45f, 34f);
            // Mato mais alto: samambaias e arbustinhos entre o capim.
            AddScanned(layers, "fern_02_a", new(0.5f, 0.9f), 0.25f, 0.15f, 18f, 0.40f, 55f);
            AddScanned(layers, "fern_02_c", new(0.5f, 0.9f), 0.2f, 0.30f, 14f, 0.45f, 55f);
            AddScanned(layers, "shrub_03_b", new(0.35f, 0.6f), 0.12f, 0.45f, 22f, 0.5f, 60f);
            AddScanned(layers, "shrub_04_a", new(0.35f, 0.6f), 0.1f, 0.55f, 22f, 0.5f, 60f);

            terrain.terrainData.detailPrototypes = new DetailPrototype[0];
            EditorUtility.SetDirty(terrain.terrainData);

            var cover = terrain.gameObject.AddComponent<GroundCover>();
            var so = new SerializedObject(cover);
            so.FindProperty("terrain").objectReferenceValue = terrain;
            so.FindProperty("ground").objectReferenceValue = ground;
            var arr = so.FindProperty("layers");
            arr.arraySize = layers.Count;
            for (int i = 0; i < layers.Count; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                var l = layers[i];
                e.FindPropertyRelative("name").stringValue = l.name;
                e.FindPropertyRelative("mesh").objectReferenceValue = l.mesh;
                var mats = e.FindPropertyRelative("materials");
                mats.arraySize = l.materials.Length;
                for (int m = 0; m < l.materials.Length; m++) mats.GetArrayElementAtIndex(m).objectReferenceValue = l.materials[m];
                e.FindPropertyRelative("perSqm").floatValue = l.perSqm;
                e.FindPropertyRelative("appear").floatValue = l.appear;
                e.FindPropertyRelative("fadeIn").floatValue = l.fadeIn;
                e.FindPropertyRelative("fadeOut").floatValue = l.fadeOut;
                e.FindPropertyRelative("patchSize").floatValue = l.patchSize;
                e.FindPropertyRelative("cutoff").floatValue = l.cutoff;
                e.FindPropertyRelative("width").vector2Value = l.width;
                e.FindPropertyRelative("height").vector2Value = l.height;
                e.FindPropertyRelative("distance").floatValue = l.distance;
                e.FindPropertyRelative("seed").intValue = l.seed;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Planetcraft] Cobertura do solo: {layers.Count} camadas ({string.Join(", ", layers.Select(l => l.name))}).");
        }

        static void AddScanned(List<GroundCover.Layer> layers, string id, Vector2 scale, float perSqm, float appear, float patch, float cutoff, float distance)
        {
            string fbx = $"{PolyHavenModels}/{id}/{id}.fbx";
            var meshes = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().ToArray();
            // O LOD1 já é raleado; usa-se ele quando existe, senão o LOD0 (variantes pequenas só têm este).
            var mesh = meshes.FirstOrDefault(m => m.name.EndsWith("_LOD1") && m.vertexCount > 0) ??
                       meshes.FirstOrDefault(m => m.name.EndsWith("_LOD0") && m.vertexCount > 0);
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (mesh == null || src == null)
            {
                Debug.LogWarning($"[Planetcraft] Sem malha para cobertura do solo: {id}");
                return;
            }
            var renderer = src.GetComponentsInChildren<MeshRenderer>()
                .FirstOrDefault(r => r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh == mesh);
            var mats = (renderer != null ? renderer.sharedMaterials : src.GetComponentInChildren<MeshRenderer>().sharedMaterials)
                .Where(m => m != null).ToArray();
            foreach (var m in mats.Where(m => !m.enableInstancing))
            {
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
            }
            layers.Add(Make(id, mesh, mats, perSqm, appear, patch, cutoff, distance, new Vector2(0.85f, 1.25f) * scale.x, new Vector2(0.85f, 1.3f) * scale.y));
        }

        // Tufo: lâminas finas e afiladas, curvadas para fora, com 4 níveis de altura (curva suave com 5 triângulos cada).
        static Mesh GrassMesh(string name, int blades, float radius, float minH, float maxH, float baseWidth, int seed)
        {
            var rng = new System.Random(seed);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            const int rows = 3;
            for (int b = 0; b < blades; b++)
            {
                float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float dist = radius * Mathf.Sqrt((float)rng.NextDouble());
                var origin = new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                float h = Mathf.Lerp(minH, maxH, (float)rng.NextDouble());
                // Mais alto no centro do tufo: forma de moita, sem cair em um disco achatado.
                h *= Mathf.Lerp(1f, 0.55f, dist / radius);
                float face = (float)(rng.NextDouble() * Mathf.PI * 2f);
                var side = new Vector3(Mathf.Cos(face), 0f, Mathf.Sin(face));
                var outward = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                var lean = Vector3.Lerp(outward, new Vector3(-side.z, 0f, side.x), (float)rng.NextDouble() * 0.6f) *
                           (0.25f + (float)rng.NextDouble() * 0.45f);
                int first = verts.Count;
                for (int r = 0; r <= rows; r++)
                {
                    float f = r / (float)rows;
                    // Curva parabólica: a lâmina sobe reta e tomba perto da ponta.
                    var center = origin + lean * (f * f * h) + Vector3.up * (f * h * (1f - 0.18f * f));
                    float w = baseWidth * (1f - f * f * 0.92f);
                    var normal = (Vector3.up * 0.65f + Vector3.Cross(side, Vector3.up).normalized * 0.35f + outward * 0.2f).normalized;
                    if (r < rows)
                    {
                        verts.Add(center - side * w); norms.Add(normal); uvs.Add(new Vector2(0f, f));
                        verts.Add(center + side * w); norms.Add(normal); uvs.Add(new Vector2(1f, f));
                    }
                    else
                    {
                        verts.Add(center); norms.Add(normal); uvs.Add(new Vector2(0.5f, 1f));
                    }
                }
                for (int r = 0; r < rows - 1; r++)
                {
                    int i = first + r * 2;
                    tris.AddRange(new[] { i, i + 2, i + 1, i + 1, i + 2, i + 3 });
                }
                int top = first + (rows - 1) * 2;
                tris.AddRange(new[] { top, top + 2, top + 1 });
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            string path = $"{Dir}/{name}.mesh.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Texture2D BladeTexture()
        {
            string path = $"{Dir}/blade.png";
            if (File.Exists(path)) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            // Base escura, miolo claro e ponta amarelada: dá profundidade ao tufo mesmo de longe.
            var tex = new Texture2D(8, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 8; x++)
            {
                float v = y / 31f;
                float vein = 1f - Mathf.Abs(x - 3.5f) / 3.5f;
                var c = Color.Lerp(new Color(0.07f, 0.17f, 0.04f), new Color(0.36f, 0.58f, 0.13f), Mathf.Pow(v, 0.7f));
                c = Color.Lerp(c, new Color(0.62f, 0.72f, 0.24f), Mathf.Clamp01((v - 0.7f) * 2.5f) * 0.6f);
                c *= 0.88f + vein * 0.2f;
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material GrassMaterial(string name, Color tint)
        {
            string path = $"{Dir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("HDRP/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseColorMap", BladeTexture());
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Smoothness", 0.28f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_DoubleSidedEnable", 1f);
            HDMaterial.SetAlphaClipping(mat, false);
            mat.SetMaterialType(MaterialId.LitStandard);
            mat.enableInstancing = true;
            HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
