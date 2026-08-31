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

            var root = new GameObject("Network Player Ghost");
            root.AddComponent<GhostAuthoringComponent>();
            root.AddComponent<NetworkPlayerGhostAuthoring>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerGhostPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        public static SceneAsset RebuildNetworkSubScene(GameObject playerGhostPrefab)
        {
            Directory.CreateDirectory("Assets/Scenes");

            var previousScene = EditorSceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene);

            var setupObject = new GameObject("Network Game Setup");
            var setup = setupObject.AddComponent<NetworkGameSetupAuthoring>();
            setup.PlayerGhostPrefab = playerGhostPrefab;

            if (!EditorSceneManager.SaveScene(scene, NetworkSubScenePath))
                throw new System.InvalidOperationException("Network subscene kaydedilemedi.");

            EditorSceneManager.CloseScene(scene, true);
            if (previousScene.IsValid() && previousScene.isLoaded)
                EditorSceneManager.SetActiveScene(previousScene);

            AssetDatabase.ImportAsset(NetworkSubScenePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(NetworkSubScenePath);
        }

        public static void AttachNetworkSubScene(SceneAsset sceneAsset)
        {
            var root = new GameObject("Network SubScene");
            var subScene = root.AddComponent<SubScene>();
            subScene.SceneAsset = sceneAsset;
            subScene.AutoLoadScene = true;
        }
    }
}
