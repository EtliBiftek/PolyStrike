using UnityEngine;

namespace PolyStrike.Core
{
    public static class CompetitiveCvars
    {
        private const string SensitivityKey = "polystrike.sensitivity";
        private const string VolumeKey = "polystrike.volume";
        private const string FpsMaxKey = "polystrike.fps_max";
        private const string ShowFpsKey = "polystrike.cl_showfps";

        public static bool SvCheats { get; set; }
        public static bool BotStop { get; set; }
        public static int BuyAnywhere { get; set; }

        public static float FreezeTime { get; set; } = 15f;
        public static float BuyTime { get; set; } = 20f;
        public static float RoundTime { get; set; } = 115.2f;
        public static int StartMoney { get; set; } = 800;
        public static int MaxMoney { get; set; } = 16000;

        public static float Sensitivity { get; private set; } = 1f;
        public static float Volume { get; private set; } = 1f;
        public static int FpsMax { get; private set; }
        public static int ShowFps { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadClientSettings()
        {
            Sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), 0.05f, 8f);
            Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            FpsMax = Mathf.Clamp(PlayerPrefs.GetInt(FpsMaxKey, 0), 0, 1000);
            ShowFps = Mathf.Clamp(PlayerPrefs.GetInt(ShowFpsKey, 0), 0, 3);
            ApplyClientSettings();
        }

        public static void SetSensitivity(float value)
        {
            Sensitivity = Mathf.Clamp(value, 0.05f, 8f);
            PlayerPrefs.SetFloat(SensitivityKey, Sensitivity);
            PlayerPrefs.Save();
        }

        public static void SetVolume(float value)
        {
            Volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, Volume);
            PlayerPrefs.Save();
            AudioListener.volume = Volume;
        }

        public static void SetFpsMax(int value)
        {
            FpsMax = Mathf.Clamp(value, 0, 1000);
            PlayerPrefs.SetInt(FpsMaxKey, FpsMax);
            PlayerPrefs.Save();
            Application.targetFrameRate = FpsMax <= 0 ? -1 : FpsMax;
        }

        public static void SetShowFps(int value)
        {
            ShowFps = Mathf.Clamp(value, 0, 3);
            PlayerPrefs.SetInt(ShowFpsKey, ShowFps);
            PlayerPrefs.Save();
        }

        public static void ResetServerDefaults()
        {
            SvCheats = false;
            BotStop = false;
            BuyAnywhere = 0;
            FreezeTime = 15f;
            BuyTime = 20f;
            RoundTime = 115.2f;
            StartMoney = 800;
            MaxMoney = 16000;
        }

        private static void ApplyClientSettings()
        {
            AudioListener.volume = Volume;
            Application.targetFrameRate = FpsMax <= 0 ? -1 : FpsMax;
        }
    }
}
