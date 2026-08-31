using PolyStrike.Networking;
using UnityEngine;

namespace PolyStrike.Core
{
    public sealed class AlphaStartMenu : MonoBehaviour
    {
        private NetworkConnectionMenu networkMenu;
        private bool initialized;
        private bool choiceMade;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;

        public static bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindFirstObjectByType<AlphaStartMenu>() != null)
                return;

            var root = new GameObject("PolyStrike Start Menu");
            DontDestroyOnLoad(root);
            root.AddComponent<AlphaStartMenu>();
        }

        private void Awake()
        {
            IsOpen = true;
            Time.timeScale = 0f;
        }

        private void OnDestroy()
        {
            if (IsOpen)
            {
                IsOpen = false;
                Time.timeScale = 1f;
            }
        }

        private void Update()
        {
            if (choiceMade)
                return;

            networkMenu ??= FindFirstObjectByType<NetworkConnectionMenu>();
            if (networkMenu != null && !initialized)
            {
                networkMenu.enabled = false;
                initialized = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            if (choiceMade)
                return;

            EnsureStyles();
            var width = Mathf.Min(500f, Screen.width - 48f);
            var height = 360f;
            var rect = new Rect((Screen.width - width) * 0.5f, Mathf.Max(24f, (Screen.height - height) * 0.5f), width, height);
            GUI.Box(rect, GUIContent.none);

            GUILayout.BeginArea(new Rect(rect.x + 32f, rect.y + 28f, rect.width - 64f, rect.height - 56f));
            GUILayout.Label(Localization.Get("start.title"), titleStyle);
            GUILayout.Label(Localization.Get("start.subtitle"), subtitleStyle);
            GUILayout.Space(28f);

            if (GUILayout.Button(Localization.Get("start.offline"), GUILayout.Height(52f)))
                StartOffline();

            GUILayout.Space(8f);
            if (GUILayout.Button(Localization.Get("start.online"), GUILayout.Height(52f)))
                OpenOnlineMenu();

            GUILayout.FlexibleSpace();
            GUILayout.Label(Localization.Get("start.console_hint"), subtitleStyle);
            GUILayout.Space(8f);
            if (GUILayout.Button(Localization.Get("menu.exit"), GUILayout.Height(38f)))
                Application.Quit();
            GUILayout.EndArea();
        }

        private void StartOffline()
        {
            choiceMade = true;
            IsOpen = false;
            Time.timeScale = 1f;
            if (networkMenu != null)
                networkMenu.enabled = false;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OpenOnlineMenu()
        {
            choiceMade = true;
            IsOpen = false;
            Time.timeScale = 1f;
            networkMenu ??= FindFirstObjectByType<NetworkConnectionMenu>();
            if (networkMenu != null)
                networkMenu.enabled = true;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void EnsureStyles()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            subtitleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
        }
    }
}
