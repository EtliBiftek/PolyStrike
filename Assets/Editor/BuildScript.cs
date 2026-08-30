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

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
                throw new InvalidOperationException("Build sahnesi kaydedilemedi.");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Windows build başarısız: {report.summary.result}");
        }
    }
}
