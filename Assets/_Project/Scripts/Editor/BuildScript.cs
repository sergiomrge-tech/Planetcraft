using System.IO;
using System.Linq;
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
                // Mesma lista que o gerador grava (título, jogo, epílogo).
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = OutputDir + "/ReturnToEarth.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Planetcraft] Build {report.summary.result}: {report.summary.outputPath} ({report.summary.totalSize / (1024 * 1024)} MB)");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
