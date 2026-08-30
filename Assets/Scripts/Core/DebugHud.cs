using PolyStrike.Gameplay;
using UnityEngine;

namespace PolyStrike.Core
{
    public sealed class DebugHud : MonoBehaviour
    {
        private Health health;
        private HitscanWeapon weapon;
        private GUIStyle textStyle;
        private GUIStyle crosshairStyle;

        private void Awake()
        {
            health = GetComponent<Health>();
            weapon = GetComponentInChildren<HitscanWeapon>();
        }

        private void OnGUI()
        {
            EnsureStyles();

            GUI.Label(new Rect(20f, 20f, 600f, 30f), Localization.Get("prototype.title"), textStyle);
            GUI.Label(new Rect(20f, 48f, 800f, 30f), Localization.Get("prototype.controls"), textStyle);

            if (health != null)
            {
                var healthText = string.Format(Localization.Get("hud.health"), Mathf.CeilToInt(health.Current));
                GUI.Label(new Rect(20f, Screen.height - 55f, 250f, 30f), healthText, textStyle);
            }

            if (weapon != null)
            {
                var ammoText = string.Format(Localization.Get("hud.ammo"), weapon.AmmoInMagazine, weapon.ReserveAmmo);
                GUI.Label(new Rect(Screen.width - 240f, Screen.height - 55f, 220f, 30f), ammoText, textStyle);

                if (weapon.IsReloading)
                    GUI.Label(new Rect(Screen.width - 240f, Screen.height - 85f, 220f, 30f), Localization.Get("hud.reloading"), textStyle);
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
