using PolyStrike.Networking;
using UnityEngine;

namespace PolyStrike.Core
{
    public sealed class AlphaStartMenu : MonoBehaviour
    {
        private enum MenuPage
        {
            Home,
            Play,
            Settings
        }

        private static AlphaStartMenu instance;

        private NetworkConnectionMenu networkMenu;
        private bool choiceMade;
        private MenuPage page;

        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle buttonStyle;
        private GUIStyle secondaryButtonStyle;
        private GUIStyle labelStyle;
        private GUIStyle footerStyle;

        private Texture2D buttonTexture;
        private Texture2D buttonHoverTexture;
        private Texture2D secondaryTexture;
        private Texture2D secondaryHoverTexture;

        public static bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            instance = null;
            IsOpen = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureExists()
        {
            if (instance != null)
                return;

            var root = new GameObject("PolyStrike Main Menu");
            instance = root.AddComponent<AlphaStartMenu>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            Open();
        }

        private void Open()
        {
            choiceMade = false;
            page = MenuPage.Home;
            IsOpen = true;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            if (choiceMade)
                return;

            networkMenu ??= FindFirstObjectByType<NetworkConnectionMenu>();
            if (networkMenu != null && networkMenu.enabled)
                networkMenu.enabled = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            if (choiceMade)
                return;

            GUI.depth = -10000;
            EnsureStyles();

            DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.028f, 0.036f, 0.050f, 0.985f));

            var panelWidth = Mathf.Clamp(Screen.width * 0.38f, 430f, 620f);
            DrawRect(new Rect(0f, 0f, panelWidth, Screen.height), new Color(0.050f, 0.064f, 0.082f, 1f));
            DrawRect(new Rect(panelWidth - 4f, 0f, 4f, Screen.height), new Color(0.94f, 0.39f, 0.08f, 1f));

            var contentWidth = Mathf.Min(430f, panelWidth - 96f);
            var top = Mathf.Max(54f, Screen.height * 0.12f);
            GUILayout.BeginArea(new Rect(56f, top, contentWidth, Mathf.Max(300f, Screen.height - top - 46f)));

            GUILayout.Label(Localization.Get("start.title"), titleStyle);
            GUILayout.Space(4f);
            GUILayout.Label(Localization.Get("start.subtitle"), subtitleStyle);
            GUILayout.Space(42f);

            switch (page)
            {
                case MenuPage.Home:
                    DrawHome();
                    break;
                case MenuPage.Play:
                    DrawPlay();
                    break;
                case MenuPage.Settings:
                    DrawSettings();
                    break;
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(Localization.Get("start.console_hint"), footerStyle);
            GUILayout.EndArea();
        }

        private void DrawHome()
        {
            if (GUILayout.Button(Localization.Get("menu.play"), buttonStyle, GUILayout.Height(58f)))
                page = MenuPage.Play;

            GUILayout.Space(10f);
            if (GUILayout.Button(Localization.Get("menu.settings"), secondaryButtonStyle, GUILayout.Height(50f)))
                page = MenuPage.Settings;

            GUILayout.Space(10f);
            if (GUILayout.Button(Localization.Get("menu.exit"), secondaryButtonStyle, GUILayout.Height(50f)))
                Application.Quit();
        }

        private void DrawPlay()
        {
            if (GUILayout.Button(Localization.Get("start.offline"), buttonStyle, GUILayout.Height(58f)))
                StartOffline();

            GUILayout.Space(10f);
            if (GUILayout.Button(Localization.Get("start.online"), secondaryButtonStyle, GUILayout.Height(52f)))
                OpenOnlineMenu();

            GUILayout.Space(26f);
            if (GUILayout.Button(Localization.Get("pause.back"), secondaryButtonStyle, GUILayout.Height(44f)))
                page = MenuPage.Home;
        }

        private void DrawSettings()
        {
            var volumePercent = Mathf.RoundToInt(AudioListener.volume * 100f);
            GUILayout.Label(string.Format(Localization.Get("pause.volume"), volumePercent), labelStyle);
            AudioListener.volume = GUILayout.HorizontalSlider(AudioListener.volume, 0f, 1f, GUILayout.Height(28f));

            GUILayout.Space(18f);
            var fullscreenKey = Screen.fullScreen ? "pause.fullscreen_on" : "pause.fullscreen_off";
            if (GUILayout.Button(Localization.Get(fullscreenKey), secondaryButtonStyle, GUILayout.Height(48f)))
                Screen.fullScreen = !Screen.fullScreen;

            GUILayout.Space(10f);
            if (GUILayout.Button(GetFpsLabel(), secondaryButtonStyle, GUILayout.Height(48f)))
                CycleFpsLimit();

            GUILayout.Space(26f);
            if (GUILayout.Button(Localization.Get("pause.back"), secondaryButtonStyle, GUILayout.Height(44f)))
                page = MenuPage.Home;
        }

        private static string GetFpsLabel()
        {
            var value = Application.targetFrameRate <= 0
                ? Localization.Get("pause.unlimited")
                : Application.targetFrameRate.ToString();
            return string.Format(Localization.Get("pause.fps"), value);
        }

        private static void CycleFpsLimit()
        {
            if (Application.targetFrameRate <= 0)
                Application.targetFrameRate = 144;
            else if (Application.targetFrameRate < 240)
                Application.targetFrameRate = 240;
            else if (Application.targetFrameRate < 360)
                Application.targetFrameRate = 360;
            else
                Application.targetFrameRate = -1;
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

        private void OnDestroy()
        {
            if (instance != this)
                return;

            instance = null;
            IsOpen = false;
            if (!choiceMade)
                Time.timeScale = 1f;

            DestroyTexture(buttonTexture);
            DestroyTexture(buttonHoverTexture);
            DestroyTexture(secondaryTexture);
            DestroyTexture(secondaryHoverTexture);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            buttonTexture = MakeTexture(new Color(0.93f, 0.35f, 0.055f, 1f));
            buttonHoverTexture = MakeTexture(new Color(1f, 0.43f, 0.085f, 1f));
            secondaryTexture = MakeTexture(new Color(0.095f, 0.116f, 0.145f, 1f));
            secondaryHoverTexture = MakeTexture(new Color(0.135f, 0.162f, 0.198f, 1f));

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 46,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = Color.white;

            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            subtitleStyle.normal.textColor = new Color(0.72f, 0.77f, 0.84f);

            labelStyle = new GUIStyle(subtitleStyle)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = new Color(0.91f, 0.93f, 0.96f);

            footerStyle = new GUIStyle(subtitleStyle)
            {
                fontSize = 12
            };
            footerStyle.normal.textColor = new Color(0.52f, 0.58f, 0.65f);

            buttonStyle = BuildButtonStyle(buttonTexture, buttonHoverTexture, 19);
            secondaryButtonStyle = BuildButtonStyle(secondaryTexture, secondaryHoverTexture, 17);
        }

        private static GUIStyle BuildButtonStyle(Texture2D normal, Texture2D hover, int fontSize)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(20, 16, 0, 0),
                border = new RectOffset(0, 0, 0, 0)
            };

            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = hover;
            style.focused.background = normal;
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.focused.textColor = Color.white;
            return style;
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }

        private static void DrawRect(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture != null)
                Destroy(texture);
        }
    }
}
