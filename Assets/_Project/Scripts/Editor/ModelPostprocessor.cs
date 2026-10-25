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
        public override uint GetVersion() => 3;

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
            HDMaterial.ValidateMaterial(material);
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
