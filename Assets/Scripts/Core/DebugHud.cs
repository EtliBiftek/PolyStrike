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
        private UtilityController utility;
        private GUIStyle textStyle;
        private GUIStyle crosshairStyle;

        private void Awake()
        {
            health = GetComponent<Health>();
            movement = GetComponent<PlayerMovement>();
            utility = GetComponent<UtilityController>();
            weapon = GetComponentInChildren<HitscanWeapon>();
        }

        private void OnGUI()
        {
            EnsureStyles();

            GUI.Label(new Rect(20f, 20f, 600f, 30f), Localization.Get("prototype.title"), textStyle);
            GUI.Label(new Rect(20f, 48f, 1400f, 30f), Localization.Get("prototype.controls"), textStyle);

            if (movement != null)
            {
                var speedText = string.Format(Localization.Get("hud.speed"), Mathf.RoundToInt(movement.SpeedSourceUnits));
                GUI.Label(new Rect(20f, 78f, 300f, 30f), speedText, textStyle);

                var tagText = string.Format(Localization.Get("hud.velocity_modifier"), movement.VelocityModifier);
                GUI.Label(new Rect(20f, 106f, 300f, 30f), tagText, textStyle);
            }

            if (health != null)
            {
                var healthText = string.Format(Localization.Get("hud.health"), Mathf.CeilToInt(health.Current));
                GUI.Label(new Rect(20f, Screen.height - 55f, 220f, 30f), healthText, textStyle);

                var armorText = string.Format(Localization.Get("hud.armor"), Mathf.CeilToInt(health.Armor));
                GUI.Label(new Rect(180f, Screen.height - 55f, 220f, 30f), armorText, textStyle);
            }

            if (utility != null && utility.IsEquipped)
                DrawUtility();
            else if (weapon != null)
                DrawWeapon();

            GUI.Label(new Rect(Screen.width * 0.5f - 15f, Screen.height * 0.5f - 18f, 30f, 36f), "+", crosshairStyle);
        }

        private void DrawWeapon()
        {
            GUI.Label(new Rect(Screen.width - 300f, Screen.height - 115f, 280f, 30f), weapon.DisplayName, textStyle);

            var accuracyText = string.Format(Localization.Get("hud.inaccuracy"), weapon.CurrentInaccuracy);
            GUI.Label(new Rect(Screen.width - 300f, Screen.height - 85f, 280f, 30f), accuracyText, textStyle);

            var ammoText = string.Format(Localization.Get("hud.ammo"), weapon.AmmoInMagazine, weapon.ReserveAmmo);
            GUI.Label(new Rect(Screen.width - 300f, Screen.height - 55f, 280f, 30f), ammoText, textStyle);

            if (weapon.IsReloading)
                GUI.Label(new Rect(Screen.width - 300f, Screen.height - 145f, 280f, 30f), Localization.Get("hud.reloading"), textStyle);
            else if (weapon.IsDeploying)
                GUI.Label(new Rect(Screen.width - 300f, Screen.height - 145f, 280f, 30f), Localization.Get("hud.deploying"), textStyle);
        }

        private void DrawUtility()
        {
            var grenadeName = Localization.Get(GetGrenadeKey(utility.SelectedType));
            var utilityText = string.Format(Localization.Get("hud.utility"), grenadeName, utility.SelectedCount);
            GUI.Label(new Rect(Screen.width - 330f, Screen.height - 85f, 310f, 30f), utilityText, textStyle);

            if (!utility.IsPrimed)
                return;

            var throwName = Localization.Get(GetThrowKey(utility.ThrowStrength));
            var throwText = string.Format(Localization.Get("hud.utility_throw"), throwName);
            GUI.Label(new Rect(Screen.width - 330f, Screen.height - 55f, 310f, 30f), throwText, textStyle);
        }

        private static string GetGrenadeKey(GrenadeType type)
        {
            switch (type)
            {
                case GrenadeType.HighExplosive:
                    return "grenade.he";
                case GrenadeType.Flashbang:
                    return "grenade.flash";
                case GrenadeType.Smoke:
                    return "grenade.smoke";
                case GrenadeType.Molotov:
                    return "grenade.molotov";
                default:
                    return "grenade.he";
            }
        }

        private static string GetThrowKey(float strength)
        {
            if (strength <= 0.1f)
                return "grenade.throw.short";
            if (strength < 0.9f)
                return "grenade.throw.medium";
            return "grenade.throw.full";
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
