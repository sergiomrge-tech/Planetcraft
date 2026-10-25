using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Planetcraft.EditorTools
{
    public static class BuildScript
    {
        const string OutputDir = "Builds/Windows";

        [MenuItem("Planetcraft/Build Windows")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory(OutputDir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/Title.unity", "Assets/_Project/Scenes/Prototype.unity" },
                locationPathName = OutputDir + "/Planetcraft.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Planetcraft] Build {report.summary.result}: {report.summary.outputPath} ({report.summary.totalSize / (1024 * 1024)} MB)");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
