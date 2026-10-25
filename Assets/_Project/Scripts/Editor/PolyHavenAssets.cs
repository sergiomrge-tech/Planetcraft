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
            if (assetPath.Contains("_diff") && assetPath.EndsWith(".png")) importer.alphaIsTransparency = true;
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
                var names = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Material>().Select(m => m.name).Distinct().ToList();
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

        static Material BuildMaterial(string dir, string name)
        {
            string path = $"{dir}/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(Shader.Find("HDRP/Lit"));

            var diff = FindTexture(dir, name, "diff");
            var normal = FindTexture(dir, name, "nor_gl");
            bool cutout = diff != null && AssetDatabase.GetAssetPath(diff).EndsWith(".png");

            // Folhas levemente puxadas para o verde vivo: os scans ficam escuros e pardos sob o céu do HDRP.
            mat.SetColor("_BaseColor", cutout ? new Color(1f, 1.1f, 0.85f) : Color.white);
            if (diff != null) mat.SetTexture("_BaseColorMap", diff);
            if (normal != null) mat.SetTexture("_NormalMap", normal);
            mat.SetFloat("_Smoothness", cutout ? 0.35f : 0.22f);
            mat.SetFloat("_Metallic", 0f);
            if (cutout)
            {
                // Folhas: recorte pela transparência e dupla face (cards vistos dos dois lados).
                HDMaterial.SetAlphaClipping(mat, true);
                mat.SetFloat("_AlphaCutoff", 0.45f);
                mat.SetFloat("_DoubleSidedEnable", 1f);
            }
            HDMaterial.ValidateMaterial(mat);
            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
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
