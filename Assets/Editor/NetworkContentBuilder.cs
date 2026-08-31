using System.IO;
using PolyStrike.Networking;
using Unity.NetCode;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PolyStrike.Editor
{
    public static class NetworkContentBuilder
    {
        public const string PlayerGhostPath = "Assets/Prefabs/NetworkPlayerGhost.prefab";
        public const string NetworkSubScenePath = "Assets/Scenes/NetworkGameSubScene.unity";

        public static GameObject EnsurePlayerGhostPrefab()
        {
            Directory.CreateDirectory("Assets/Prefabs");

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerGhostPath);
            if (existing != null)
                return existing;

            GameObject root = null;
            try
            {
                root = new GameObject("Network Player Ghost");
                root.AddComponent<GhostAuthoringComponent>();
                root.AddComponent<NetworkPlayerGhostAuthoring>();

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerGhostPath);
                if (prefab == null)
                    throw new System.InvalidOperationException("Network player ghost prefab kaydedilemedi.");

                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                if (root != null)
                    Object.DestroyImmediate(root);
            }
        }

        public static SceneAsset RebuildNetworkSubScene(GameObject playerGhostPrefab)
        {
            if (playerGhostPrefab == null)
                throw new System.ArgumentNullException(nameof(playerGhostPrefab));

            Directory.CreateDirectory("Assets/Scenes");

            var previousScene = EditorSceneManager.GetActiveScene();
            if (!previousScene.IsValid() || !previousScene.isLoaded || string.IsNullOrEmpty(previousScene.path))
                throw new System.InvalidOperationException("Network subscene oluşturulmadan önce aktif sahne kaydedilmiş olmalı.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                if (!scene.IsValid() || !scene.isLoaded)
                    throw new System.InvalidOperationException("Network subscene oluşturulamadı.");

                if (!EditorSceneManager.SetActiveScene(scene))
                    throw new System.InvalidOperationException("Network subscene aktif sahne yapılamadı.");

                var setupObject = new GameObject("Network Game Setup");
                var setup = setupObject.AddComponent<NetworkGameSetupAuthoring>();
                setup.PlayerGhostPrefab = playerGhostPrefab;

                if (!EditorSceneManager.SaveScene(scene, NetworkSubScenePath))
                    throw new System.InvalidOperationException("Network subscene kaydedilemedi.");
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);

                if (previousScene.IsValid() && previousScene.isLoaded)
                    EditorSceneManager.SetActiveScene(previousScene);
            }

            AssetDatabase.ImportAsset(NetworkSubScenePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(NetworkSubScenePath);
            if (sceneAsset == null)
                throw new System.InvalidOperationException("Kaydedilen network subscene tekrar yüklenemedi.");

            return sceneAsset;
        }

        public static void AttachNetworkSubScene(SceneAsset sceneAsset)
        {
            if (sceneAsset == null)
                throw new System.ArgumentNullException(nameof(sceneAsset));

            var root = new GameObject("Network SubScene");
            var subScene = root.AddComponent<SubScene>();
            subScene.SceneAsset = sceneAsset;
            subScene.AutoLoadScene = true;
        }
    }
}
