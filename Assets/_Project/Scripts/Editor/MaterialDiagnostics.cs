using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Planetcraft.EditorTools
{
    // Diagnóstico: lista os materiais embutidos de um modelo com textura, mapeamento e cor (log do batchmode).
    public static class MaterialDiagnostics
    {
        [MenuItem("Planetcraft/Diagnóstico de materiais")]
        public static void Run()
        {
            foreach (var path in new[] { "Assets/_Project/Models/Machines/heater.fbx", "Assets/_Project/Models/Machines/base_module.fbx" })
            {
                foreach (var m in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    var tex = m.HasProperty("_BaseColorMap") ? m.GetTexture("_BaseColorMap") : null;
                    Debug.Log($"[MatDiag] {path} {m.name}: shader={m.shader.name} map={(tex != null ? tex.name : "-")} " +
                              $"uvBase={m.GetFloat("_UVBase")} cor={m.GetColor("_BaseColor")} " +
                              $"kw={string.Join(",", m.shaderKeywords)}");
                }
            }
        }
    }
}
