using UnityEngine;

namespace PolyStrike.Core
{
    public sealed class ConsoleFpsOverlay : MonoBehaviour
    {
        private float smoothedDelta = 1f / 60f;
        private GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindFirstObjectByType<ConsoleFpsOverlay>() != null)
                return;
            var root = new GameObject("FPS Overlay");
            DontDestroyOnLoad(root);
            root.AddComponent<ConsoleFpsOverlay>();
        }

        private void Update()
        {
            smoothedDelta = Mathf.Lerp(smoothedDelta, Mathf.Max(Time.unscaledDeltaTime, 0.00001f), 0.08f);
        }

        private void OnGUI()
        {
            if (CompetitiveCvars.ShowFps <= 0)
                return;

            style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft
            };

            var fps = Mathf.RoundToInt(1f / smoothedDelta);
            var text = CompetitiveCvars.ShowFps >= 2
                ? $"{fps} fps  {smoothedDelta * 1000f:0.00} ms  de_sandline"
                : $"{fps} fps";
            GUI.Label(new Rect(8f, 6f, 360f, 24f), text, style);
        }
    }
}
