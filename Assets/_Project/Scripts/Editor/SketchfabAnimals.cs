using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Planetcraft.EditorTools
{
    // Animais do Sketchfab (CC BY, ver CREDITS.md) em Assets/ThirdParty/Sketchfab/<id>/: rig genérico com
    // todas as animações em loop.
    public class SketchfabPostprocessor : AssetPostprocessor
    {
        public const string Root = "Assets/ThirdParty/Sketchfab/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips) clip.loopTime = true;
            importer.clipAnimations = clips;
        }
    }

    public static class SketchfabAnimals
    {
        static readonly Dictionary<string, RuntimeAnimatorController> Controllers = new();

        public static string ModelPath(string id)
        {
            string dir = SketchfabPostprocessor.Root + id;
            if (!Directory.Exists(dir)) return null;
            return Directory.GetFiles(dir, "*.fbx", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/'))
                .OrderBy(p => p.Length).FirstOrDefault();
        }

        public static GameObject Model(string id)
        {
            string path = ModelPath(id);
            return path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
        }

        // Controlador com dois estados: parado e andando (bool "Moving", usado pelo AnimalWander).
        // `moveSpeed` acelera ou freia o clipe de movimento (o lobo só tem corrida: tocada devagar vira trote).
        public static RuntimeAnimatorController Controller(string id, float moveSpeed)
        {
            if (Controllers.TryGetValue(id, out var cached)) return cached;
            string modelPath = ModelPath(id);
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToList();
            if (clips.Count == 0)
            {
                Debug.LogWarning($"[Sketchfab] {id}: nenhum clipe de animação em {modelPath}");
                return null;
            }
            AnimationClip Find(params string[] words) =>
                words.Select(w => clips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains(w))).FirstOrDefault(c => c != null);
            var idle = Find("idle", "stand", "breath") ?? clips[0];
            var move = Find("walk", "trot", "run") ?? clips[0];
            Debug.Log($"[Sketchfab] {id}: clipes [{string.Join(", ", clips.Select(c => c.name))}] parado={idle.name} andando={move.name}");

            string path = $"{SketchfabPostprocessor.Root}{id}/{id}.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            var sm = controller.layers[0].stateMachine;
            var idleState = sm.AddState("Parado");
            idleState.motion = idle;
            var moveState = sm.AddState("Andando");
            moveState.motion = move;
            moveState.speed = moveSpeed;
            sm.defaultState = idleState;
            var toMove = idleState.AddTransition(moveState);
            toMove.AddCondition(AnimatorConditionMode.If, 0, "Moving");
            toMove.hasExitTime = false;
            toMove.duration = 0.2f;
            var toIdle = moveState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Moving");
            toIdle.hasExitTime = false;
            toIdle.duration = 0.3f;
            AssetDatabase.SaveAssets();
            Controllers[id] = controller;
            return controller;
        }

        public static void ClearCache() => Controllers.Clear();

        // Escala o modelo para a altura desejada e apoia as patas no chão (os modelos do Sketchfab vêm em escalas
        // arbitrárias). Usa os limites da malha na pose de repouso.
        public static void FitToHeight(GameObject model, float height)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            if (b.size.y < 1e-4f) return;
            float k = height / b.size.y;
            model.transform.localScale *= k;
            float floor = (b.min.y - model.transform.position.y) * k;
            model.transform.localPosition -= Vector3.up * floor;
        }
    }
}
