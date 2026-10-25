using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

namespace Planetcraft.EditorTools
{
    // Importação dos FBX gerados por tools/gen_models.py:
    // - colisores de malha só em construções e minérios (precisam de raycast/colisão);
    // - materiais viram URP/Lit com a cor do Blender; "glow_*" ficam emissivos e "glass_*" translúcidos.
    public class ModelPostprocessor : AssetPostprocessor
    {
        const string ModelRoot = "Assets/_Project/Models/";

        // Depois do pré-processador de materiais do próprio URP.
        public override int GetPostprocessOrder() => 100;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelRoot)) return;
            var importer = (ModelImporter)assetImporter;
            importer.addCollider = assetPath.Contains("/Machines/") || assetPath.Contains("/Ore/");
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

            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.25f);
            material.SetFloat("_Metallic", 0f);

            string name = description.materialName ?? material.name;
            if (name.StartsWith("glow_"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2.5f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else if (name.StartsWith("glass_"))
            {
                color.a = 0.35f;
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", 0.9f);
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
        }
    }
}
