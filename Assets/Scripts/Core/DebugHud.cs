using PolyStrike.Gameplay;
using PolyStrike.Match;
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
        private MatchParticipant participant;
        private C4Controller c4;
        private BuyMenu buyMenu;
        private GUIStyle textStyle;
        private GUIStyle centerStyle;
        private GUIStyle crosshairStyle;

        private void Awake()
        {
            health = GetComponent<Health>();
            movement = GetComponent<PlayerMovement>();
            utility = GetComponent<UtilityController>();
            participant = GetComponent<MatchParticipant>();
            c4 = GetComponent<C4Controller>();
            buyMenu = GetComponent<BuyMenu>();
            weapon = GetComponentInChildren<HitscanWeapon>();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawMatchState();

            if (movement != null)
            {
                GUI.Label(new Rect(20f, 20f, 260f, 28f), string.Format(Localization.Get("hud.speed"), Mathf.RoundToInt(movement.SpeedSourceUnits)), textStyle);
                GUI.Label(new Rect(20f, 47f, 260f, 28f), string.Format(Localization.Get("hud.velocity_modifier"), movement.VelocityModifier), textStyle);
            }

            if (participant != null)
                GUI.Label(new Rect(20f, Screen.height - 88f, 260f, 30f), string.Format(Localization.Get("hud.money"), participant.Money), textStyle);

            if (health != null)
            {
                GUI.Label(new Rect(20f, Screen.height - 55f, 170f, 30f), string.Format(Localization.Get("hud.health"), Mathf.CeilToInt(health.Current)), textStyle);
                GUI.Label(new Rect(175f, Screen.height - 55f, 170f, 30f), string.Format(Localization.Get("hud.armor"), Mathf.CeilToInt(health.Armor)), textStyle);
            }

            if (utility != null && utility.IsEquipped)
                DrawUtility();
            else if (weapon != null)
                DrawWeapon();

            DrawC4Interaction();

            if (buyMenu == null || !buyMenu.IsOpen)
                GUI.Label(new Rect(Screen.width * 0.5f - 15f, Screen.height * 0.5f - 18f, 30f, 36f), "+", crosshairStyle);
        }

        private void DrawMatchState()
        {
            var match = MatchRoundManager.Instance;
            if (match == null)
                return;

            var score = string.Format(Localization.Get("hud.score"), match.TerroristScore, match.CounterTerroristScore);
            GUI.Label(new Rect(Screen.width * 0.5f - 170f, 16f, 340f, 34f), score, centerStyle);

            var phase = Localization.Get(GetPhaseKey(match.Phase));
            var timer = FormatTime(match.TimeRemaining);
            var round = string.Format(Localization.Get("hud.round"), match.RoundNumber, MatchRules.RegulationRounds);
            GUI.Label(new Rect(Screen.width * 0.5f - 260f, 49f, 520f, 30f), $"{round}  |  {phase}  |  {timer}", centerStyle);

            if (match.Phase == RoundPhase.PostPlant)
            {
                var bomb = string.Format(Localization.Get("hud.bomb_timer"), match.TimeRemaining);
                GUI.Label(new Rect(Screen.width * 0.5f - 220f, 79f, 440f, 30f), bomb, centerStyle);
            }

            if (match.Phase == RoundPhase.RoundEnd && match.LastRoundWinner.HasValue && match.LastRoundEndReason.HasValue)
            {
                var winner = Localization.Get(match.LastRoundWinner == MatchTeam.Terrorists ? "team.t" : "team.ct");
                var reason = Localization.Get(GetEndReasonKey(match.LastRoundEndReason.Value));
                GUI.Label(new Rect(Screen.width * 0.5f - 300f, 110f, 600f, 34f), string.Format(Localization.Get("hud.round_winner"), winner, reason), centerStyle);
            }
            else if (match.Phase == RoundPhase.MatchEnd)
            {
                var result = match.MatchDrawn
                    ? Localization.Get("match.draw")
                    : string.Format(Localization.Get("match.winner"), Localization.Get(match.MatchWinner == MatchTeam.Terrorists ? "team.t" : "team.ct"));
                GUI.Label(new Rect(Screen.width * 0.5f - 300f, 110f, 600f, 34f), result, centerStyle);
            }
        }

        private void DrawC4Interaction()
        {
            if (c4 == null || !c4.IsInteracting)
                return;

            var key = c4.IsPlanting ? "hud.planting" : "hud.defusing";
            var percent = Mathf.RoundToInt(c4.InteractionProgress * 100f);
            GUI.Label(new Rect(Screen.width * 0.5f - 180f, Screen.height * 0.68f, 360f, 32f), string.Format(Localization.Get(key), percent), centerStyle);
        }

        private void DrawWeapon()
        {
            GUI.Label(new Rect(Screen.width - 320f, Screen.height - 115f, 300f, 30f), weapon.DisplayName, textStyle);
            GUI.Label(new Rect(Screen.width - 320f, Screen.height - 85f, 300f, 30f), string.Format(Localization.Get("hud.inaccuracy"), weapon.CurrentInaccuracy), textStyle);
            GUI.Label(new Rect(Screen.width - 320f, Screen.height - 55f, 300f, 30f), string.Format(Localization.Get("hud.ammo"), weapon.AmmoInMagazine, weapon.ReserveAmmo), textStyle);

            if (weapon.IsReloading)
                GUI.Label(new Rect(Screen.width - 320f, Screen.height - 145f, 300f, 30f), Localization.Get("hud.reloading"), textStyle);
            else if (weapon.IsDeploying)
                GUI.Label(new Rect(Screen.width - 320f, Screen.height - 145f, 300f, 30f), Localization.Get("hud.deploying"), textStyle);
        }

        private void DrawUtility()
        {
            var grenadeName = Localization.Get(GetGrenadeKey(utility.SelectedType));
            GUI.Label(new Rect(Screen.width - 340f, Screen.height - 85f, 320f, 30f), string.Format(Localization.Get("hud.utility"), grenadeName, utility.SelectedCount), textStyle);

            if (!utility.IsPrimed)
                return;

            var throwName = Localization.Get(GetThrowKey(utility.ThrowStrength));
            GUI.Label(new Rect(Screen.width - 340f, Screen.height - 55f, 320f, 30f), string.Format(Localization.Get("hud.utility_throw"), throwName), textStyle);
        }

        private static string FormatTime(float seconds)
        {
            var value = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{value / 60:00}:{value % 60:00}";
        }

        private static string GetPhaseKey(RoundPhase phase)
        {
            return phase switch
            {
                RoundPhase.FreezeTime => "match.phase.freeze",
                RoundPhase.Live => "match.phase.live",
                RoundPhase.PostPlant => "match.phase.postplant",
                RoundPhase.RoundEnd => "match.phase.round_end",
                RoundPhase.HalfTime => "match.phase.halftime",
                RoundPhase.MatchEnd => "match.phase.match_end",
                _ => "match.phase.live"
            };
        }

        private static string GetEndReasonKey(RoundEndReason reason)
        {
            return reason switch
            {
                RoundEndReason.Elimination => "match.reason.elimination",
                RoundEndReason.TimeExpired => "match.reason.time",
                RoundEndReason.BombExploded => "match.reason.explosion",
                RoundEndReason.BombDefused => "match.reason.defuse",
                _ => "match.reason.elimination"
            };
        }

        private static string GetGrenadeKey(GrenadeType type)
        {
            return type switch
            {
                GrenadeType.HighExplosive => "grenade.he",
                GrenadeType.Flashbang => "grenade.flash",
                GrenadeType.Smoke => "grenade.smoke",
                GrenadeType.Molotov => "grenade.molotov",
                _ => "grenade.he"
            };
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
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
                textStyle.normal.textColor = Color.white;
            }

            if (centerStyle == null)
            {
                centerStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 20,
                    fontStyle = FontStyle.Bold
                };
                centerStyle.normal.textColor = Color.white;
            }

            if (crosshairStyle == null)
            {
                crosshairStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 26
                };
                crosshairStyle.normal.textColor = Color.white;
            }
        }
    }
}
