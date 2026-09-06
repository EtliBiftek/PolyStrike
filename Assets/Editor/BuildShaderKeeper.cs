using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PolyStrike.Editor
{
    public static class BuildShaderKeeper
    {
        private const string ResourceDirectory = "Assets/Resources/BuildShaders";

        public static void EnsureRuntimeShaders()
        {
            Directory.CreateDirectory(ResourceDirectory);
            AssetDatabase.Refresh();

            EnsureMaterial("Standard", "RuntimeStandard.mat");
            EnsureMaterial("Unlit/Color", "RuntimeUnlit.mat");
            EnsureMaterial("Sprites/Default", "RuntimeSprites.mat");

            AssetDatabase.SaveAssets();
        }

        private static void EnsureMaterial(string shaderName, string fileName)
        {
            var shader = Shader.Find(shaderName);
            // Fallback chain for URP / HDRP / Built-in differences
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("HDRP/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Standard");

            if (shader == null)
                throw new InvalidOperationException($"Build için gerekli shader bulunamadı: {shaderName} (fallback zinciri de başarısız)");

            var path = $"{ResourceDirectory}/{fileName}";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = $"PolyStrike {shaderName} Build Reference"
                };
                AssetDatabase.CreateAsset(material, path);
                return;
            }

            if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
        }
    }
}
