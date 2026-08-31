using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

namespace PolyStrike.Editor
{
    public static class BuildScript
    {
        public static void BuildWindows()
        {
            const string scenePath = "Assets/Scenes/Bootstrap.unity";
            const string outputDirectory = "build/Windows";
            const string executablePath = outputDirectory + "/PolyStrike.exe";

            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory(outputDirectory);
            BuildShaderKeeper.EnsureRuntimeShaders();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Additive scene generation needs a real scene path in batchmode.
            if (!EditorSceneManager.SaveScene(scene, scenePath))
                throw new InvalidOperationException("Build sahnesi ilk kez kaydedilemedi.");

            var playerGhostPrefab = NetworkContentBuilder.EnsurePlayerGhostPrefab();
            if (playerGhostPrefab == null)
                throw new InvalidOperationException("Network player ghost prefab oluşturulamadı.");

            var networkSubScene = NetworkContentBuilder.RebuildNetworkSubScene(playerGhostPrefab);
            if (networkSubScene == null)
                throw new InvalidOperationException("Network subscene oluşturulamadı.");

            NetworkContentBuilder.AttachNetworkSubScene(networkSubScene);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
                throw new InvalidOperationException("Build sahnesi kaydedilemedi.");

            AssetDatabase.SaveAssets();

            var options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report == null)
                throw new InvalidOperationException("Unity build raporu oluşturulamadı.");

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Windows build başarısız: {report.summary.result}");
        }
    }
}
