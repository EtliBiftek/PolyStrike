using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PolyStrike.Core
{
    public static class Localization
    {
        private static readonly Dictionary<string, string> Entries = new(StringComparer.OrdinalIgnoreCase);

        public static string CurrentLanguage { get; private set; } = "tr";
        public static event Action LanguageChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadDefaultLanguage()
        {
            Load("tr");
        }

        public static bool Load(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                return false;

            var path = Path.Combine(Application.streamingAssetsPath, "Languages", $"{languageCode}.txt");
            if (!File.Exists(path))
            {
                Debug.LogError($"Dil dosyası bulunamadı: {path}");
                return false;
            }

            var nextEntries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                var separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;

                var key = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim().Replace("\\n", "\n");

                if (key.Length > 0)
                    nextEntries[key] = value;
            }

            Entries.Clear();
            foreach (var pair in nextEntries)
                Entries[pair.Key] = pair.Value;

            CurrentLanguage = languageCode;
            LanguageChanged?.Invoke();
            return true;
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            return Entries.TryGetValue(key, out var value) ? value : $"[{key}]";
        }

        public static string[] GetAvailableLanguages()
        {
            var directory = Path.Combine(Application.streamingAssetsPath, "Languages");
            if (!Directory.Exists(directory))
                return Array.Empty<string>();

            var files = Directory.GetFiles(directory, "*.txt");
            var languages = new string[files.Length];

            for (var i = 0; i < files.Length; i++)
                languages[i] = Path.GetFileNameWithoutExtension(files[i]);

            return languages;
        }
    }
}
