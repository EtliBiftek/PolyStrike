using PolyStrike.Gameplay;
using PolyStrike.Player;
using UnityEngine;

namespace PolyStrike.Core
{
    public sealed class DebugHud : MonoBehaviour
    {
        private Health health;
        private HitscanWeapon weapon;
        private PlayerMovement movement;
        private GUIStyle textStyle;
        private GUIStyle crosshairStyle;

        private void Awake()
        {
            health = GetComponent<Health>();
            movement = GetComponent<PlayerMovement>();
            weapon = GetComponentInChildren<HitscanWeapon>();
        }

        private void OnGUI()
        {
            EnsureStyles();

            GUI.Label(new Rect(20f, 20f, 600f, 30f), Localization.Get("prototype.title"), textStyle);
            GUI.Label(new Rect(20f, 48f, 1000f, 30f), Localization.Get("prototype.controls"), textStyle);

            if (movement != null)
            {
                var speedText = string.Format(Localization.Get("hud.speed"), Mathf.RoundToInt(movement.SpeedSourceUnits));
                GUI.Label(new Rect(20f, 78f, 300f, 30f), speedText, textStyle);
            }

            if (health != null)
            {
                var healthText = string.Format(Localization.Get("hud.health"), Mathf.CeilToInt(health.Current));
                GUI.Label(new Rect(20f, Screen.height - 55f, 250f, 30f), healthText, textStyle);
            }

            if (weapon != null)
            {
                GUI.Label(new Rect(Screen.width - 300f, Screen.height - 115f, 280f, 30f), weapon.DisplayName, textStyle);

                var accuracyText = string.Format(Localization.Get("hud.inaccuracy"), weapon.CurrentInaccuracy);
                GUI.Label(new Rect(Screen.width - 300f, Screen.height - 85f, 280f, 30f), accuracyText, textStyle);

                var ammoText = string.Format(Localization.Get("hud.ammo"), weapon.AmmoInMagazine, weapon.ReserveAmmo);
                GUI.Label(new Rect(Screen.width - 300f, Screen.height - 55f, 280f, 30f), ammoText, textStyle);

                if (weapon.IsReloading)
                    GUI.Label(new Rect(Screen.width - 300f, Screen.height - 145f, 280f, 30f), Localization.Get("hud.reloading"), textStyle);
            }

            GUI.Label(new Rect(Screen.width * 0.5f - 15f, Screen.height * 0.5f - 18f, 30f, 36f), "+", crosshairStyle);
        }

        private void EnsureStyles()
        {
            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    normal = { textColor = Color.white }
                };
            }

            if (crosshairStyle == null)
            {
                crosshairStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 26,
                    normal = { textColor = Color.white }
                };
            }
        }
    }
}
