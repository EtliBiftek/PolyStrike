using PolyStrike.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace PolyStrike.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkBuyMenu : MonoBehaviour
    {
        private bool open;
        private Entity localPlayer = Entity.Null;
        private NetworkPlayerState playerState;
        private NetworkMatchSnapshot matchState;

        private void Update()
        {
            RefreshLocalState();

            if (localPlayer == Entity.Null)
            {
                open = false;
                return;
            }

            if (GameInput.BuyPressed)
            {
                if (open)
                    Close();
                else if (CanOpen())
                    Open();
            }

            if (open && !CanOpen())
                Close();
        }

        private void OnGUI()
        {
            if (!open || localPlayer == Entity.Null)
                return;

            const float width = 560f;
            const float height = 520f;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(rect, string.Empty);

            GUILayout.BeginArea(new Rect(rect.x + 24f, rect.y + 20f, rect.width - 48f, rect.height - 40f));
            GUILayout.Label(Localization.Get("buy.title"));
            GUILayout.Label(Localization.Get("buy.money").Replace("{0}", playerState.Money.ToString()));
            GUILayout.Space(12f);

            Section("buy.primary_section");
            var rifleKey = playerState.Team == 0 ? "buy.t_rifle" : "buy.ct_rifle";
            var riflePrice = playerState.Team == 0 ? NetworkMatchRules.TRiflePrice : NetworkMatchRules.CTRiflePrice;
            BuyButton(rifleKey, riflePrice, NetworkPurchaseItem.Rifle);

            GUILayout.Space(10f);
            Section("buy.equipment_section");
            BuyButton("buy.kevlar", NetworkMatchRules.KevlarPrice, NetworkPurchaseItem.Kevlar);
            var helmetPrice = playerState.Armor > 0 ? 350 : NetworkMatchRules.HelmetBundlePrice;
            BuyButton("buy.helmet", helmetPrice, NetworkPurchaseItem.HelmetBundle);
            if (playerState.Team == 1)
                BuyButton("buy.defuse_kit", NetworkMatchRules.DefuseKitPrice, NetworkPurchaseItem.DefuseKit);

            GUILayout.Space(10f);
            Section("buy.utility_section");
            BuyButton("buy.he", NetworkMatchRules.HePrice, NetworkPurchaseItem.HeGrenade);
            BuyButton("buy.flash", NetworkMatchRules.FlashPrice, NetworkPurchaseItem.Flashbang);
            BuyButton("buy.smoke", NetworkMatchRules.SmokePrice, NetworkPurchaseItem.SmokeGrenade);
            BuyButton(
                playerState.Team == 0 ? "buy.molotov" : "buy.incendiary",
                playerState.Team == 0 ? NetworkMatchRules.MolotovPrice : NetworkMatchRules.IncendiaryPrice,
                NetworkPurchaseItem.FireGrenade);

            GUILayout.FlexibleSpace();
            GUILayout.Label(Localization.Get("buy.close_hint"));
            GUILayout.EndArea();
        }

        private void BuyButton(string nameKey, int price, NetworkPurchaseItem item)
        {
            var label = Localization.Get("buy.item_price")
                .Replace("{0}", Localization.Get(nameKey))
                .Replace("{1}", price.ToString());

            GUI.enabled = playerState.Money >= price;
            if (GUILayout.Button(label, GUILayout.Height(32f)))
                SendPurchase(item);
            GUI.enabled = true;
        }

        private static void Section(string key)
        {
            GUILayout.Label(Localization.Get(key));
        }

        private bool CanOpen()
        {
            if (matchState.BuyTimeRemaining <= 0f ||
                (matchState.Phase != NetworkMatchPhase.FreezeTime && matchState.Phase != NetworkMatchPhase.Live))
                return false;

            return playerState.Team == 0 ? playerState.Position.z <= -20f : playerState.Position.z >= 20f;
        }

        private void Open()
        {
            open = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Close()
        {
            open = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void RefreshLocalState()
        {
            localPlayer = Entity.Null;
            var world = ClientServerBootstrap.ClientWorld;
            if (world == null || !world.IsCreated)
                return;

            var entityManager = world.EntityManager;
            var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<NetworkPlayerState>(),
                ComponentType.ReadOnly<NetworkMatchSnapshot>());
            var entities = query.ToEntityArray(Allocator.Temp);
            if (entities.Length > 0)
            {
                localPlayer = entities[0];
                playerState = entityManager.GetComponentData<NetworkPlayerState>(localPlayer);
                matchState = entityManager.GetComponentData<NetworkMatchSnapshot>(localPlayer);
            }

            entities.Dispose();
            query.Dispose();
        }

        private static void SendPurchase(NetworkPurchaseItem item)
        {
            var world = ClientServerBootstrap.ClientWorld;
            if (world == null || !world.IsCreated)
                return;

            var entityManager = world.EntityManager;
            var connectionQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NetworkId>(),
                ComponentType.ReadOnly<NetworkStreamConnection>());
            var connections = connectionQuery.ToEntityArray(Allocator.Temp);
            if (connections.Length > 0)
            {
                var rpc = entityManager.CreateEntity();
                entityManager.AddComponentData(rpc, new NetworkPurchaseRequest { Item = item });
                entityManager.AddComponentData(rpc, new SendRpcCommandRequest { TargetConnection = connections[0] });
            }

            connections.Dispose();
            connectionQuery.Dispose();
        }
    }
}
