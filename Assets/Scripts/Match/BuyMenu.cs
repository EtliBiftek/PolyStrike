using System.Collections.Generic;
using PolyStrike.Core;
using PolyStrike.Gameplay;
using PolyStrike.Player;
using UnityEngine;

namespace PolyStrike.Match
{
    [RequireComponent(typeof(MatchParticipant))]
    public sealed class BuyMenu : MonoBehaviour
    {
        private enum PurchaseKind
        {
            Primary,
            Armor,
            DefuseKit,
            Grenade
        }

        private sealed class PurchaseRecord
        {
            public PurchaseKind Kind;
            public int Price;
            public GrenadeType GrenadeType;
            public int GrenadeCountBefore;
            public float ArmorBefore;
            public bool HelmetBefore;
            public float ArmorAfter;
            public bool HelmetAfter;
            public int DamageRevisionAfter;
        }

        private MatchParticipant participant;
        private HitscanWeapon weapon;
        private UtilityController utility;
        private C4Controller c4;
        private readonly List<PurchaseRecord> purchases = new List<PurchaseRecord>();
        private int observedRound = -1;
        private bool open;
        private GUIStyle titleStyle;
        private GUIStyle moneyStyle;

        public bool IsOpen => open;

        private void Awake()
        {
            participant = GetComponent<MatchParticipant>();
            weapon = GetComponentInChildren<HitscanWeapon>();
            utility = GetComponent<UtilityController>();
            c4 = GetComponent<C4Controller>();
        }

        private void Update()
        {
            var match = MatchRoundManager.Instance;
            if (match != null && match.RoundNumber != observedRound)
            {
                observedRound = match.RoundNumber;
                purchases.Clear();
            }

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
            var height = Mathf.Min(650f, Screen.height - 80f);
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
            DrawPrimaryButton();

            GUILayout.Space(14f);
            GUILayout.Label(Localization.Get("buy.equipment_section"));
            DrawKevlarButton();
            DrawHelmetButton();

            if (participant.Team == MatchTeam.CounterTerrorists)
                DrawDefuseKitButton();

            GUILayout.Space(14f);
            GUILayout.Label(Localization.Get("buy.utility_section"));
            DrawGrenadeButton(GrenadeType.HighExplosive, "buy.he", MatchRules.HePrice);
            DrawGrenadeButton(GrenadeType.Flashbang, "buy.flash", MatchRules.FlashPrice);
            DrawGrenadeButton(GrenadeType.Smoke, "buy.smoke", MatchRules.SmokePrice);

            var fireKey = participant.Team == MatchTeam.Terrorists ? "buy.molotov" : "buy.incendiary";
            var firePrice = participant.Team == MatchTeam.Terrorists ? MatchRules.MolotovPrice : MatchRules.IncendiaryPrice;
            DrawGrenadeButton(GrenadeType.Molotov, fireKey, firePrice);

            GUILayout.Space(14f);
            if (TryGetRefundablePurchase(out var refundable, out var recordIndex))
            {
                if (GUILayout.Button(string.Format(Localization.Get("buy.refund"), refundable.Price), GUILayout.Height(38f)))
                    RefundPurchase(refundable, recordIndex);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(Localization.Get("buy.close_hint"));
            GUILayout.EndArea();
        }

        private void DrawPrimaryButton()
        {
            var key = participant.Team == MatchTeam.Terrorists ? "buy.t_rifle" : "buy.ct_rifle";
            var price = participant.Team == MatchTeam.Terrorists ? MatchRules.TRiflePrice : MatchRules.CTRiflePrice;
            var name = Localization.Get(key);
            var owned = weapon != null && weapon.HasPrimary;
            var label = owned
                ? string.Format(Localization.Get("buy.item_owned"), name)
                : string.Format(Localization.Get("buy.item_price"), name, price);

            GUI.enabled = !owned && participant.Money >= price;
            if (GUILayout.Button(label, GUILayout.Height(38f)) && participant.BuyPrimaryRifle())
            {
                purchases.Add(new PurchaseRecord
                {
                    Kind = PurchaseKind.Primary,
                    Price = price
                });
            }
            GUI.enabled = true;
        }

        private void DrawKevlarButton()
        {
            var owned = participant.Health.Armor >= 100f;
            var name = Localization.Get("buy.kevlar");
            var label = owned
                ? string.Format(Localization.Get("buy.item_owned"), name)
                : string.Format(Localization.Get("buy.item_price"), name, MatchRules.KevlarPrice);

            GUI.enabled = !owned && participant.Money >= MatchRules.KevlarPrice;
            if (GUILayout.Button(label, GUILayout.Height(38f)))
                TryBuyArmor(false, MatchRules.KevlarPrice);
            GUI.enabled = true;
        }

        private void DrawHelmetButton()
        {
            var owned = participant.Health.Armor >= 100f && participant.Health.HasHelmet;
            var price = participant.Health.Armor >= 100f && !participant.Health.HasHelmet ? 350 : MatchRules.HelmetBundlePrice;
            var name = Localization.Get("buy.helmet");
            var label = owned
                ? string.Format(Localization.Get("buy.item_owned"), name)
                : string.Format(Localization.Get("buy.item_price"), name, price);

            GUI.enabled = !owned && participant.Money >= price;
            if (GUILayout.Button(label, GUILayout.Height(38f)))
                TryBuyArmor(true, price);
            GUI.enabled = true;
        }

        private void TryBuyArmor(bool helmet, int price)
        {
            var beforeArmor = participant.Health.Armor;
            var beforeHelmet = participant.Health.HasHelmet;
            var purchased = helmet ? participant.BuyHelmetBundle() : participant.BuyKevlar();
            if (!purchased)
                return;

            purchases.Add(new PurchaseRecord
            {
                Kind = PurchaseKind.Armor,
                Price = price,
                ArmorBefore = beforeArmor,
                HelmetBefore = beforeHelmet,
                ArmorAfter = participant.Health.Armor,
                HelmetAfter = participant.Health.HasHelmet,
                DamageRevisionAfter = participant.Health.DamageRevision
            });
        }

        private void DrawDefuseKitButton()
        {
            var name = Localization.Get("buy.defuse_kit");
            var label = participant.HasDefuseKit
                ? string.Format(Localization.Get("buy.item_owned"), name)
                : string.Format(Localization.Get("buy.item_price"), name, MatchRules.DefuseKitPrice);

            GUI.enabled = !participant.HasDefuseKit && participant.Money >= MatchRules.DefuseKitPrice;
            if (GUILayout.Button(label, GUILayout.Height(38f)) && participant.BuyDefuseKit())
            {
                purchases.Add(new PurchaseRecord
                {
                    Kind = PurchaseKind.DefuseKit,
                    Price = MatchRules.DefuseKitPrice
                });
            }
            GUI.enabled = true;
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
            if (GUILayout.Button(label, GUILayout.Height(38f)) && participant.BuyGrenade(type))
            {
                purchases.Add(new PurchaseRecord
                {
                    Kind = PurchaseKind.Grenade,
                    Price = price,
                    GrenadeType = type,
                    GrenadeCountBefore = count
                });
            }
            GUI.enabled = true;
        }

        private bool TryGetRefundablePurchase(out PurchaseRecord record, out int index)
        {
            for (var i = purchases.Count - 1; i >= 0; i--)
            {
                if (!CanRefund(purchases[i]))
                    continue;

                record = purchases[i];
                index = i;
                return true;
            }

            record = null;
            index = -1;
            return false;
        }

        private bool CanRefund(PurchaseRecord record)
        {
            switch (record.Kind)
            {
                case PurchaseKind.Primary:
                    return weapon != null && weapon.CanRefundPrimary();

                case PurchaseKind.Armor:
                    return participant.Health.DamageRevision == record.DamageRevisionAfter &&
                           Mathf.Approximately(participant.Health.Armor, record.ArmorAfter) &&
                           participant.Health.HasHelmet == record.HelmetAfter;

                case PurchaseKind.DefuseKit:
                    return participant.HasDefuseKit;

                case PurchaseKind.Grenade:
                    return utility != null && utility.GetCount(record.GrenadeType) > record.GrenadeCountBefore;

                default:
                    return false;
            }
        }

        private void RefundPurchase(PurchaseRecord record, int index)
        {
            var refunded = false;

            switch (record.Kind)
            {
                case PurchaseKind.Primary:
                    refunded = weapon != null && weapon.RefundPrimary();
                    break;

                case PurchaseKind.Armor:
                    if (CanRefund(record))
                    {
                        participant.Health.SetEquipment(record.ArmorBefore, record.HelmetBefore);
                        refunded = true;
                    }
                    break;

                case PurchaseKind.DefuseKit:
                    if (participant.HasDefuseKit)
                    {
                        participant.SetDefuseKit(false);
                        refunded = true;
                    }
                    break;

                case PurchaseKind.Grenade:
                    refunded = utility != null && utility.RefundGrenade(record.GrenadeType, record.GrenadeCountBefore);
                    break;
            }

            if (!refunded)
                return;

            participant.AddMoney(record.Price);
            purchases.RemoveAt(index);
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
            var bombEquipped = c4 != null && c4.IsBombEquipped;
            weapon?.SetExternalInputBlocked(roundLocked || bombEquipped);
            utility?.SetExternalInputBlocked(roundLocked || bombEquipped);

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
