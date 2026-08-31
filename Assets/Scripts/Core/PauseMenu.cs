using System.Globalization;
using PolyStrike.Networking;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PolyStrike.Core
{
    public sealed class PauseMenu : MonoBehaviour
    {
        private bool settingsOpen;
        private bool onlineSession;
        private GUIStyle titleStyle;

        public static bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindFirstObjectByType<PauseMenu>() != null)
                return;

            var root = new GameObject("Pause Menu");
            DontDestroyOnLoad(root);
            root.AddComponent<PauseMenu>();
        }

        private void Update()
        {
            if (AlphaStartMenu.IsOpen || DeveloperConsole.IsOpen)
                return;

            if (EscapePressed())
            {
                if (IsOpen && settingsOpen)
                {
                    settingsOpen = false;
                    return;
                }

                SetOpen(!IsOpen);
            }

            if (!IsOpen)
                return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            if (!IsOpen)
                return;

            EnsureStyles();
            var width = Mathf.Min(460f, Screen.width - 48f);
            var height = settingsOpen ? 430f : 340f;
            var rect = new Rect((Screen.width - width) * 0.5f, Mathf.Max(24f, (Screen.height - height) * 0.5f), width, height);
            GUI.Box(rect, GUIContent.none);

            GUILayout.BeginArea(new Rect(rect.x + 28f, rect.y + 24f, rect.width - 56f, rect.height - 48f));
            GUILayout.Label(settingsOpen ? Localization.Get("pause.settings") : Localization.Get("pause.title"), titleStyle);
            GUILayout.Space(22f);

            if (settingsOpen)
                DrawSettings();
            else
                DrawMainButtons();

            GUILayout.EndArea();
        }

        private void DrawMainButtons()
        {
            if (GUILayout.Button(Localization.Get("pause.resume"), GUILayout.Height(46f)))
                SetOpen(false);

            GUILayout.Space(8f);
            if (GUILayout.Button(Localization.Get("pause.settings"), GUILayout.Height(46f)))
                settingsOpen = true;

            GUILayout.Space(8f);
            if (GUILayout.Button(Localization.Get("pause.console"), GUILayout.Height(46f)))
            {
                SetOpen(false);
                DeveloperConsole.Execute("toggleconsole");
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Localization.Get("menu.exit"), GUILayout.Height(40f)))
                Application.Quit();
        }

        private void DrawSettings()
        {
            GUILayout.Label(string.Format(Localization.Get("pause.sensitivity"), CompetitiveCvars.Sensitivity.ToString("0.00", CultureInfo.InvariantCulture)));
            var sensitivity = GUILayout.HorizontalSlider(CompetitiveCvars.Sensitivity, 0.10f, 4f);
            if (!Mathf.Approximately(sensitivity, CompetitiveCvars.Sensitivity))
                CompetitiveCvars.SetSensitivity(sensitivity);

            GUILayout.Space(18f);
            GUILayout.Label(string.Format(Localization.Get("pause.volume"), Mathf.RoundToInt(CompetitiveCvars.Volume * 100f)));
            var volume = GUILayout.HorizontalSlider(CompetitiveCvars.Volume, 0f, 1f);
            if (!Mathf.Approximately(volume, CompetitiveCvars.Volume))
                CompetitiveCvars.SetVolume(volume);

            GUILayout.Space(18f);
            GUILayout.Label(string.Format(Localization.Get("pause.fps"), CompetitiveCvars.FpsMax <= 0 ? Localization.Get("pause.unlimited") : CompetitiveCvars.FpsMax.ToString(CultureInfo.InvariantCulture)));
            var fps = Mathf.RoundToInt(GUILayout.HorizontalSlider(CompetitiveCvars.FpsMax <= 0 ? 60 : CompetitiveCvars.FpsMax, 60f, 500f) / 10f) * 10;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Localization.Get("pause.unlimited"), GUILayout.Height(34f)))
                CompetitiveCvars.SetFpsMax(0);
            if (GUILayout.Button(fps.ToString(CultureInfo.InvariantCulture), GUILayout.Height(34f)))
                CompetitiveCvars.SetFpsMax(fps);
            GUILayout.EndHorizontal();

            GUILayout.Space(18f);
            var fullscreenLabel = Screen.fullScreen ? Localization.Get("pause.fullscreen_on") : Localization.Get("pause.fullscreen_off");
            if (GUILayout.Button(fullscreenLabel, GUILayout.Height(40f)))
                Screen.fullScreen = !Screen.fullScreen;

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Localization.Get("pause.back"), GUILayout.Height(40f)))
                settingsOpen = false;
        }

        private void SetOpen(bool open)
        {
            if (IsOpen == open)
                return;

            IsOpen = open;
            settingsOpen = false;
            onlineSession = HasClientConnection();

            if (open)
            {
                if (!onlineSession)
                    Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                if (!onlineSession)
                    Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private static bool HasClientConnection()
        {
            var world = ClientServerBootstrap.ClientWorld;
            if (world == null || !world.IsCreated)
                return false;

            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>());
            return !query.IsEmptyIgnoreFilter;
        }

        private void EnsureStyles()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 25,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?.escapeKey.wasPressedThisFrame == true;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
