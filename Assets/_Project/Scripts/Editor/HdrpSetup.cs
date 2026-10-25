using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Planetcraft.EditorTools
{
    // Configura o projeto para HDRP por código (sem o assistente): asset do pipeline com os recursos que o jogo usa,
    // configurações globais e os perfis de volume das cenas. Pode rodar de novo; só cria o que falta.
    public static class HdrpSetup
    {
        const string Dir = "Assets/_Project/Settings";
        const string PipelinePath = Dir + "/HDRP_ReturnToEarth.asset";
        const string AtmospherePath = Dir + "/Atmosphere.asset";
        const string TitlePath = Dir + "/TitleSpace.asset";

        [MenuItem("Planetcraft/Configurar HDRP")]
        public static void Configure()
        {
            System.IO.Directory.CreateDirectory(Dir);
            AssetDatabase.Refresh();

            var pipeline = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            EnableFeatures(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = null; // todos os níveis usam o pipeline padrão
            }
            QualitySettings.SetQualityLevel(current, false);

            EnsureGlobalSettings();
            AssetDatabase.SaveAssets();

            // Num projeto recém-clonado os FBX podem ter sido importados antes dos shaders do HDRP existirem
            // (Shader.Find devolve null e o material fica rosa). Reimporta os modelos com o pipeline já ativo.
            if (Shader.Find("HDRP/Lit") == null)
                Debug.LogError("[Planetcraft] Shader HDRP/Lit não encontrado; os modelos ficarão sem material.");
            AssetDatabase.ImportAsset("Assets/_Project/Models",
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[Planetcraft] HDRP configurado: " + PipelinePath);
        }

        static void EnableFeatures(HDRenderPipelineAsset pipeline)
        {
            var so = new SerializedObject(pipeline);
            foreach (var feature in new[]
                     {
                         "supportWater", "supportVolumetricClouds", "supportVolumetrics", "supportSSR", "supportSSAO",
                         "supportSSGI", "supportMotionVectors", "supportTerrainHole",
                     })
            {
                var p = so.FindProperty("m_RenderPipelineSettings." + feature);
                if (p != null) p.boolValue = true;
                else Debug.LogWarning("[Planetcraft] Recurso HDRP não encontrado: " + feature);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }

        // HDRenderPipelineGlobalSettings.Ensure é interno; é o mesmo que o assistente do HDRP chama.
        static void EnsureGlobalSettings()
        {
            var type = typeof(HDRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.HighDefinition.HDRenderPipelineGlobalSettings");
            var ensure = type?.GetMethod("Ensure", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (ensure == null)
            {
                Debug.LogWarning("[Planetcraft] Não achei HDRenderPipelineGlobalSettings.Ensure; abra o HDRP Wizard.");
                return;
            }
            ensure.Invoke(null, new object[] { true });
        }

        // Perfil da Terra: céu físico, nuvens volumétricas, neblina volumétrica e pós-processamento.
        // Os valores animados pelo Ti ficam no AtmosphereReactor e no WeatherController.
        public static VolumeProfile AtmosphereProfile()
        {
            var profile = LoadOrCreate(AtmospherePath, out bool created);
            if (!created) return profile;

            var env = Add<VisualEnvironment>(profile);
            env.skyType.Override((int)SkyType.PhysicallyBased);
            env.skyAmbientMode.Override(SkyAmbientMode.Dynamic);

            var sky = Add<PhysicallyBasedSky>(profile);
            sky.airDensityR.overrideState = sky.airDensityG.overrideState = sky.airDensityB.overrideState = true;
            sky.aerosolDensity.overrideState = sky.aerosolTint.overrideState = true;

            var clouds = Add<VolumetricClouds>(profile);
            clouds.enable.Override(false);
            clouds.cloudPreset = VolumetricClouds.CloudPresets.Custom;
            clouds.densityMultiplier.Override(0f);

            var fog = Add<Fog>(profile);
            fog.enabled.Override(true);
            fog.enableVolumetricFog.Override(true);
            fog.meanFreePath.Override(120f);
            fog.maximumHeight.Override(220f);
            fog.albedo.Override(new Color(0.55f, 0.45f, 0.38f));

            var exposure = Add<Exposure>(profile);
            exposure.mode.Override(ExposureMode.Automatic);
            exposure.limitMin.Override(9f);
            exposure.limitMax.Override(15f);

            Add<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
            Add<Bloom>(profile).intensity.Override(0.15f);
            Add<ScreenSpaceAmbientOcclusion>(profile).intensity.Override(0.8f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        // Perfil do título: espaço preto (a câmera limpa com preto), exposição fixa de luz do sol e bloom.
        public static VolumeProfile TitleProfile()
        {
            var profile = LoadOrCreate(TitlePath, out bool created);
            if (!created) return profile;
            Add<VisualEnvironment>(profile).skyType.Override(0);
            var exposure = Add<Exposure>(profile);
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(13.5f);
            Add<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
            Add<Bloom>(profile).intensity.Override(0.3f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static VolumeProfile LoadOrCreate(string path, out bool created)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            created = profile == null;
            if (created)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            return profile;
        }

        // Componentes de volume precisam ser sub-assets do perfil para serem salvos.
        static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
