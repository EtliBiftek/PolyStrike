using PolyStrike.Core;
using PolyStrike.Gameplay;
using PolyStrike.Player;
using UnityEngine;

namespace PolyStrike.Match
{
    [RequireComponent(typeof(MatchParticipant))]
    public sealed class BuyMenu : MonoBehaviour
    {
        private MatchParticipant participant;
        private HitscanWeapon weapon;
        private UtilityController utility;
        private bool open;
        private GUIStyle titleStyle;
        private GUIStyle moneyStyle;

        public bool IsOpen => open;

        private void Awake()
        {
            participant = GetComponent<MatchParticipant>();
            weapon = GetComponentInChildren<HitscanWeapon>();
            utility = GetComponent<UtilityController>();
        }

        private void Update()
        {
            var match = MatchRoundManager.Instance;
            var canBuy = match != null && match.BuyAllowed && participant.IsInBuyZone() && participant.IsAlive;

            if (open && (!canBuy || GameInput.EscapePressed))
            {
                SetOpen(false);
                return;
            }

            if (!GameInput.BuyPressed)
                return;

            if (open)
                SetOpen(false);
            else if (canBuy)
                SetOpen(true);
        }

        private void OnDisable()
        {
            if (open)
                SetOpen(false);
        }

        private void OnGUI()
        {
            if (!open)
                return;

            EnsureStyles();

            var width = Mathf.Min(720f, Screen.width - 80f);
            var height = Mathf.Min(600f, Screen.height - 80f);
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 24f, rect.y + 20f, rect.width - 48f, rect.height - 40f));

            GUILayout.BeginHorizontal();
            GUILayout.Label(Localization.Get("buy.title"), titleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(string.Format(Localization.Get("buy.money"), participant.Money), moneyStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(18f);
            GUILayout.Label(Localization.Get("buy.primary_section"));
            DrawBuyButton(
                participant.Team == MatchTeam.Terrorists ? "buy.t_rifle" : "buy.ct_rifle",
                participant.Team == MatchTeam.Terrorists ? MatchRules.TRiflePrice : MatchRules.CTRiflePrice,
                participant.BuyPrimaryRifle,
                weapon != null && weapon.HasPrimary);

            GUILayout.Space(14f);
            GUILayout.Label(Localization.Get("buy.equipment_section"));
            DrawBuyButton("buy.kevlar", MatchRules.KevlarPrice, participant.BuyKevlar, participant.Health.Armor >= 100f);

            var helmetPrice = participant.Health.Armor >= 100f && !participant.Health.HasHelmet ? 350 : MatchRules.HelmetBundlePrice;
            DrawBuyButton("buy.helmet", helmetPrice, participant.BuyHelmetBundle, participant.Health.Armor >= 100f && participant.Health.HasHelmet);

            if (participant.Team == MatchTeam.CounterTerrorists)
                DrawBuyButton("buy.defuse_kit", MatchRules.DefuseKitPrice, participant.BuyDefuseKit, participant.HasDefuseKit);

            GUILayout.Space(14f);
            GUILayout.Label(Localization.Get("buy.utility_section"));
            DrawGrenadeButton(GrenadeType.HighExplosive, "buy.he", MatchRules.HePrice);
            DrawGrenadeButton(GrenadeType.Flashbang, "buy.flash", MatchRules.FlashPrice);
            DrawGrenadeButton(GrenadeType.Smoke, "buy.smoke", MatchRules.SmokePrice);
            DrawGrenadeButton(GrenadeType.Molotov, "buy.molotov", MatchRules.MolotovPrice);

            GUILayout.FlexibleSpace();
            GUILayout.Label(Localization.Get("buy.close_hint"));
            GUILayout.EndArea();
        }

        private void DrawGrenadeButton(GrenadeType type, string localizationKey, int price)
        {
            var count = utility != null ? utility.GetCount(type) : 0;
            var name = Localization.Get(localizationKey);
            var label = count > 0
                ? string.Format(Localization.Get("buy.item_count_price"), name, count, price)
                : string.Format(Localization.Get("buy.item_price"), name, price);
            var available = utility != null && utility.CanBuy(type) && participant.Money >= price;

            GUI.enabled = available;
            if (GUILayout.Button(label, GUILayout.Height(38f)))
                participant.BuyGrenade(type);
            GUI.enabled = true;
        }

        private void DrawBuyButton(string localizationKey, int price, System.Func<bool> buy, bool owned)
        {
            var name = Localization.Get(localizationKey);
            var label = owned
                ? string.Format(Localization.Get("buy.item_owned"), name)
                : string.Format(Localization.Get("buy.item_price"), name, price);

            GUI.enabled = !owned && participant.Money >= price;
            if (GUILayout.Button(label, GUILayout.Height(38f)))
                buy();
            GUI.enabled = true;
        }

        private void SetOpen(bool value)
        {
            open = value;

            if (open)
            {
                weapon?.SetExternalInputBlocked(true);
                utility?.SetExternalInputBlocked(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            var match = MatchRoundManager.Instance;
            var roundLocked = match != null && (match.Phase == RoundPhase.FreezeTime || match.Phase == RoundPhase.RoundEnd || match.Phase == RoundPhase.HalfTime || match.Phase == RoundPhase.MatchEnd);
            weapon?.SetExternalInputBlocked(roundLocked);
            utility?.SetExternalInputBlocked(roundLocked);

            if (participant.IsAlive && match != null && match.Phase != RoundPhase.MatchEnd)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 26,
                    fontStyle = FontStyle.Bold
                };
            }

            if (moneyStyle == null)
            {
                moneyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    alignment = TextAnchor.MiddleRight
                };
            }
        }
    }
}
