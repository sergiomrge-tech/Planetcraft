using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Planetcraft.EditorTools
{
    // Importação dos FBX gerados por tools/gen_models.py:
    // - colisores de malha só em construções, minérios e props (precisam de raycast/colisão);
    // - materiais viram HDRP/Lit com a cor do Blender; "glow_*" ficam emissivos e "glass_*" translúcidos.
    public class ModelPostprocessor : AssetPostprocessor
    {
        const string ModelRoot = "Assets/_Project/Models/";

        // Luminância dos emissivos (nits): visível de dia com exposição automática, sem estourar.
        public const float GlowNits = 4000f;

        // Depois do pré-processador de materiais do próprio HDRP.
        public override int GetPostprocessOrder() => 100;

        // Incrementar quando mudar a conversão: força o Unity a reimportar os modelos.
        public override uint GetVersion() => 6;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelRoot)) return;
            var importer = (ModelImporter)assetImporter;
            importer.addCollider = assetPath.Contains("/Machines/") || assetPath.Contains("/Ore/") || assetPath.Contains("/Props/");
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }

        void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] clips)
        {
            if (!assetPath.StartsWith(ModelRoot)) return;

            var color = Color.white;
            if (description.TryGetProperty("DiffuseColor", out Vector4 diffuse))
                color = new Color(diffuse.x, diffuse.y, diffuse.z, 1f);
            // O Blender guarda cores lineares; propriedades de cor do material são em gama.
            color = color.gamma;

            string name = description.materialName ?? material.name;
            ConfigureLit(material, color, name.StartsWith("glass_") ? 0.9f : 0.3f);
            if (name.StartsWith("glow_")) MakeEmissive(material, color, GlowNits);
            else if (name.StartsWith("glass_")) MakeTransparent(material, 0.35f);
            else ApplySurface(material, name, color);
            HDMaterial.ValidateMaterial(material);
        }

        const string SurfaceDir = "Assets/ThirdParty/PolyHaven/Surfaces/";

        // Textura PBR por família, escolhida pelo nome do material, em projeção triplanar no espaço do objeto
        // (os modelos gerados não têm UV, e a textura acompanha peças que se movem). A cor do Blender tinge o albedo.
        void ApplySurface(Material material, string name, Color color)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("solar") || n.Contains("water")) return;
            // Cristais: sem textura, polidos e translúcidos no brilho (luz do sol atravessa as facetas).
            if (n.StartsWith("crystal"))
            {
                material.SetFloat("_Smoothness", 0.93f);
                material.SetFloat("_Metallic", 0.15f);
                return;
            }
            string family = n.Contains("concrete") ? "concrete"
                : n.Contains("rock") || n.Contains("stone") ? "rock"
                : n.Contains("rust") || n.Contains("scorch") ? "rust" : "panel";
            var albedo = Surface<Texture2D>(family, "albedo.png");
            if (albedo == null) return;
            bool metal = n.Contains("steel") || n.Contains("pipe") || n.Contains("metal") || n.Contains("gold") ||
                         n.Contains("chrome") || n.StartsWith("ti");

            // O albedo cinza claro (~0.78) escurece a cor; compensa para manter o tom escolhido no Blender.
            material.SetColor("_BaseColor", family == "rust" ? Color.Lerp(Color.white, color, 0.35f) : new Color(color.r * 1.25f, color.g * 1.25f, color.b * 1.25f, 1f));
            material.SetTexture("_BaseColorMap", albedo);
            material.SetTexture("_NormalMap", Surface<Texture2D>(family, "normal.png"));
            material.SetFloat("_NormalScale", 1.2f);
            material.SetTexture("_MaskMap", Surface<Texture2D>(family, "mask.png"));
            material.SetFloat("_MetallicRemapMin", 0f);
            material.SetFloat("_MetallicRemapMax", metal ? 1f : 0f);
            material.SetFloat("_SmoothnessRemapMin", 0.05f);
            material.SetFloat("_SmoothnessRemapMax", metal ? 0.8f : 0.6f);
            material.SetFloat("_UVBase", 5f); // triplanar
            material.SetFloat("_ObjectSpaceUVMapping", 1f);
            material.SetFloat("_TexWorldScale", family == "concrete" ? 0.5f : 0.9f);
        }

        T Surface<T>(string family, string file) where T : Object
        {
            string path = SurfaceDir + family + "/" + file;
            context.DependsOnSourceAsset(path);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        public static void ConfigureLit(Material material, Color color, float smoothness, float metallic = 0f)
        {
            material.shader = Shader.Find("HDRP/Lit");
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
        }

        public static void MakeEmissive(Material material, Color color, float nits)
        {
            HDMaterial.SetUseEmissiveIntensity(material, true);
            HDMaterial.SetEmissiveColor(material, color);
            HDMaterial.SetEmissiveIntensity(material, nits, EmissiveIntensityUnit.Nits);
        }

        public static void MakeTransparent(Material material, float alpha)
        {
            var c = material.GetColor("_BaseColor");
            c.a = alpha;
            material.SetColor("_BaseColor", c);
            HDMaterial.SetSurfaceType(material, true);
        }
    }
}
