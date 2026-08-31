namespace PolyStrike.Networking
{
    public static class NetworkConsoleBridge
    {
        private static bool restartRequested;
        private static float restartDelay;

        public static void RequestRestart(float delaySeconds)
        {
            restartDelay = UnityEngine.Mathf.Max(0f, delaySeconds);
            restartRequested = true;
        }

        public static bool TryConsumeRestart(out float delaySeconds)
        {
            delaySeconds = restartDelay;
            if (!restartRequested)
                return false;

            restartRequested = false;
            restartDelay = 0f;
            return true;
        }
    }
}
