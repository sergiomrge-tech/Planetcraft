using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Planetcraft.EditorTools
{
    // Importação dos assets do Poly Haven otimizados por tools/optimize_polyhaven.py.
    public class PolyHavenPostprocessor : AssetPostprocessor
    {
        public const string Root = "Assets/ThirdParty/PolyHaven/";

        void OnPreprocessTexture()
        {
            // Imagens da Terra (tools/fetch_nasa.py): resolução cheia e nuvens com transparência.
            if (assetPath.StartsWith("Assets/ThirdParty/NASA/"))
            {
                var nasa = (TextureImporter)assetImporter;
                nasa.maxTextureSize = 4096;
                nasa.alphaIsTransparency = assetPath.EndsWith(".png");
                return;
            }
            if (!assetPath.StartsWith(Root)) return;
            var importer = (TextureImporter)assetImporter;
            if (assetPath.Contains("_nor_gl")) importer.textureType = TextureImporterType.NormalMap;
            if (assetPath.EndsWith("_mask.png")) importer.sRGBTexture = false; // máscara do HDRP: dados lineares
            if (assetPath.Contains("_diff") && assetPath.EndsWith(".png")) importer.alphaIsTransparency = true;
            // Superfícies das máquinas (tools/fetch_surfaces.py): normal e máscara do HDRP são dados lineares.
            if (assetPath.EndsWith("/normal.png")) importer.textureType = TextureImporterType.NormalMap;
            if (assetPath.EndsWith("/mask.png")) importer.sRGBTexture = false;
            importer.maxTextureSize = 2048;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root + "Models")) return;
            var importer = (ModelImporter)assetImporter;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            // Rochas, troncos e tocos bloqueiam o jogador; folhagem não.
            importer.addCollider = assetPath.Contains("boulder") || assetPath.Contains("trunk") || assetPath.Contains("stump");
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }

        // O LODGroup automático corta o último nível cedo demais (árvores somem a poucas dezenas de metros).
        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(Root + "Models")) return;
            var group = root.GetComponent<LODGroup>();
            if (group == null) return;
            var lods = group.GetLODs();
            if (lods.Length != 2) return;
            lods[0].screenRelativeTransitionHeight = 0.18f;
            lods[1].screenRelativeTransitionHeight = 0.006f;
            group.SetLODs(lods);
            group.RecalculateBounds();
        }
    }

    // Cria materiais HDRP/Lit para cada material dos FBX (texturas por convenção de nome) e remapeia o importador.
    public static class PolyHavenMaterials
    {
        [MenuItem("Planetcraft/Materiais Poly Haven")]
        public static void Setup()
        {
            string modelsDir = PolyHavenPostprocessor.Root + "Models";
            if (!Directory.Exists(modelsDir)) return;
            foreach (var dir in Directory.GetDirectories(modelsDir).Select(d => d.Replace('\\', '/')))
            {
                string id = Path.GetFileName(dir);
                string fbx = $"{dir}/{id}.fbx";
                if (AssetImporter.GetAtPath(fbx) is not ModelImporter importer) continue;
                Directory.CreateDirectory($"{dir}/Materials");
                // Depois do primeiro remapeamento o FBX não expõe mais os materiais embutidos: junta os já remapeados.
                var names = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Material>().Select(m => m.name)
                    .Concat(importer.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).Select(k => k.name))
                    .Distinct().ToList();
                foreach (var name in names)
                {
                    var mat = BuildMaterial(dir, name);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
                }
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Planetcraft] Materiais Poly Haven configurados.");
        }

        const string FoliageProfile =
            "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipelineResources/FoliageDiffusionProfile.asset";

        static Material BuildMaterial(string dir, string name)
        {
            string path = $"{dir}/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created)
            {
                mat = new Material(Shader.Find("HDRP/Lit"));
                // O perfil de difusão (folhas) grava referências no asset: ele precisa existir antes.
                AssetDatabase.CreateAsset(mat, path);
            }

            var diff = FindTexture(dir, name, "diff");
            var normal = FindTexture(dir, name, "nor_gl");
            var maskPath = $"{dir}/textures/{name}_mask.png";
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            // Folhagem recortada: textura com alfa E nome de planta (peças de máquinas também trazem canal alfa cheio).
            string lower = name.ToLowerInvariant();
            bool foliage = lower.Contains("leaves") || lower.Contains("leaf") || lower.Contains("branches") ||
                           lower.Contains("shrub") || lower.Contains("fern") || lower.Contains("flower") ||
                           lower.Contains("dandelion") || lower.Contains("periwinkle") || lower.Contains("weed");
            bool cutout = foliage && diff != null && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(diff)) is TextureImporter ti &&
                          ti.DoesSourceTextureHaveAlpha();

            // Folhas levemente puxadas para o verde vivo: os scans ficam escuros e pardos sob o céu do HDRP.
            mat.SetColor("_BaseColor", cutout ? new Color(1f, 1.1f, 0.85f) : Color.white);
            if (diff != null) mat.SetTexture("_BaseColorMap", diff);
            if (normal != null) mat.SetTexture("_NormalMap", normal);
            mat.SetFloat("_Smoothness", cutout ? 0.35f : 0.22f);
            mat.SetFloat("_Metallic", 0f);
            // Metal, oclusão e lisura reais do scan (máscara empacotada por tools/optimize_polyhaven.py).
            mat.SetTexture("_MaskMap", mask);
            if (mask != null)
            {
                mat.SetFloat("_MetallicRemapMin", 0f);
                mat.SetFloat("_MetallicRemapMax", 1f);
                mat.SetFloat("_SmoothnessRemapMin", 0f);
                mat.SetFloat("_SmoothnessRemapMax", 1f);
                mat.SetFloat("_AORemapMin", 0f);
                mat.SetFloat("_AORemapMax", 1f);
            }
            if (cutout)
            {
                // Folhas: recorte pela transparência e dupla face (cards vistos dos dois lados).
                HDMaterial.SetAlphaClipping(mat, true);
                mat.SetFloat("_AlphaCutoff", 0.45f);
                mat.SetFloat("_DoubleSidedEnable", 1f);
                // Translúcidas com o perfil Foliage do HDRP: a luz atravessa as folhas e a copa contra o sol
                // fica verde em vez de uma silhueta preta.
                mat.SetMaterialType(MaterialId.LitTranslucent);
                HDMaterial.SetDiffusionProfile(mat, AssetDatabase.LoadAssetAtPath<DiffusionProfileSettings>(FoliageProfile));
                mat.SetFloat("_Thickness", 0.25f);
            }
            else
            {
                HDMaterial.SetAlphaClipping(mat, false);
                mat.SetFloat("_DoubleSidedEnable", 0f);
                mat.SetMaterialType(MaterialId.LitStandard);
            }
            if (name.EndsWith("_glass"))
            {
                // Vidro dos instrumentos: translúcido e liso.
                mat.SetColor("_BaseColor", new Color(0.7f, 0.85f, 0.9f, 0.3f));
                mat.SetTexture("_BaseColorMap", null);
                mat.SetTexture("_MaskMap", null);
                mat.SetFloat("_Smoothness", 0.95f);
                HDMaterial.SetSurfaceType(mat, true);
            }
            HDMaterial.ValidateMaterial(mat);
            Debug.Log($"[PolyHaven] {name}: diff={(diff != null ? diff.name : "-")} normal={(normal != null)} " +
                      $"recorte={cutout} tipo={mat.GetMaterialType()}");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D FindTexture(string dir, string material, string kind)
        {
            foreach (var ext in new[] { ".png", ".jpg", ".exr" })
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/textures/{material}_{kind}{ext}");
                if (tex != null) return tex;
            }
            return null;
        }
    }
}
