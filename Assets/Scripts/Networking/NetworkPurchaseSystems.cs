using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct NetworkPurchaseServerSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (receive, purchase, rpcEntity) in
                     SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<NetworkPurchaseRequest>>()
                         .WithEntityAccess())
            {
                var connection = receive.ValueRO.SourceConnection;
                if (!SystemAPI.HasComponent<NetworkPlayerConnection>(connection))
                {
                    commandBuffer.DestroyEntity(rpcEntity);
                    continue;
                }

                var link = SystemAPI.GetComponent<NetworkPlayerConnection>(connection);
                if (!state.EntityManager.Exists(link.Player) ||
                    !state.EntityManager.HasComponent<NetworkMatchSnapshot>(link.Player))
                {
                    commandBuffer.DestroyEntity(rpcEntity);
                    continue;
                }

                var snapshot = state.EntityManager.GetComponentData<NetworkMatchSnapshot>(link.Player);
                var player = state.EntityManager.GetComponentData<NetworkPlayerState>(link.Player);
                var loadout = state.EntityManager.GetComponentData<NetworkLoadoutState>(link.Player);

                if (CanBuy(in player, in snapshot) && TryPurchase(purchase.ValueRO.Item, ref player, ref loadout))
                {
                    if (player.ActiveWeapon == 1 || player.ActiveWeapon == 2)
                        NetworkLoadoutSwitchSystem.LoadActiveAmmo(ref player, in loadout);
                    state.EntityManager.SetComponentData(link.Player, player);
                    state.EntityManager.SetComponentData(link.Player, loadout);
                }

                commandBuffer.DestroyEntity(rpcEntity);
            }

            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();
        }

        private static bool CanBuy(in NetworkPlayerState player, in NetworkMatchSnapshot snapshot)
        {
            if ((player.Flags & NetworkPlayerFlags.Alive) == 0 || snapshot.BuyTimeRemaining <= 0f)
                return false;

            if (snapshot.Phase != NetworkMatchPhase.FreezeTime && snapshot.Phase != NetworkMatchPhase.Live)
                return false;

            // Sandline spawn buy zones are deliberately compact. Once a player commits into the map,
            // the remaining few seconds of buy time cannot be abused from mid.
            return player.Team == 0 ? player.Position.z <= -20f : player.Position.z >= 20f;
        }

        private static bool TryPurchase(
            NetworkPurchaseItem item,
            ref NetworkPlayerState player,
            ref NetworkLoadoutState loadout)
        {
            switch (item)
            {
                case NetworkPurchaseItem.Rifle:
                {
                    if (loadout.PrimaryOwned != 0)
                        return false;
                    var price = player.Team == 0 ? NetworkMatchRules.TRiflePrice : NetworkMatchRules.CTRiflePrice;
                    if (!Spend(ref player, price))
                        return false;
                    loadout.PrimaryOwned = 1;
                    loadout.PrimaryMagazine = 30;
                    loadout.PrimaryReserve = 90;
                    return true;
                }

                case NetworkPurchaseItem.Kevlar:
                    if (player.Armor >= 100 || !Spend(ref player, NetworkMatchRules.KevlarPrice))
                        return false;
                    player.Armor = 100;
                    return true;

                case NetworkPurchaseItem.HelmetBundle:
                {
                    if ((player.Flags & NetworkPlayerFlags.Helmet) != 0 && player.Armor >= 100)
                        return false;
                    var price = player.Armor > 0 ? 350 : NetworkMatchRules.HelmetBundlePrice;
                    if (!Spend(ref player, price))
                        return false;
                    player.Armor = 100;
                    player.Flags |= NetworkPlayerFlags.Helmet;
                    return true;
                }

                case NetworkPurchaseItem.DefuseKit:
                    if (player.Team != 1 || (player.Flags & NetworkPlayerFlags.DefuseKit) != 0 ||
                        !Spend(ref player, NetworkMatchRules.DefuseKitPrice))
                        return false;
                    player.Flags |= NetworkPlayerFlags.DefuseKit;
                    return true;

                case NetworkPurchaseItem.HeGrenade:
                    if (loadout.HeGrenades >= 1 || TotalGrenades(in loadout) >= 4 ||
                        !Spend(ref player, NetworkMatchRules.HePrice))
                        return false;
                    loadout.HeGrenades++;
                    return true;

                case NetworkPurchaseItem.Flashbang:
                    if (loadout.Flashbangs >= 2 || TotalGrenades(in loadout) >= 4 ||
                        !Spend(ref player, NetworkMatchRules.FlashPrice))
                        return false;
                    loadout.Flashbangs++;
                    return true;

                case NetworkPurchaseItem.SmokeGrenade:
                    if (loadout.SmokeGrenades >= 1 || TotalGrenades(in loadout) >= 4 ||
                        !Spend(ref player, NetworkMatchRules.SmokePrice))
                        return false;
                    loadout.SmokeGrenades++;
                    return true;

                case NetworkPurchaseItem.FireGrenade:
                {
                    if (loadout.FireGrenades >= 1 || TotalGrenades(in loadout) >= 4)
                        return false;
                    var price = player.Team == 0 ? NetworkMatchRules.MolotovPrice : NetworkMatchRules.IncendiaryPrice;
                    if (!Spend(ref player, price))
                        return false;
                    loadout.FireGrenades++;
                    return true;
                }

                default:
                    return false;
            }
        }

        private static bool Spend(ref NetworkPlayerState player, int amount)
        {
            if (player.Money < amount)
                return false;
            player.Money = (ushort)(player.Money - amount);
            return true;
        }

        private static int TotalGrenades(in NetworkLoadoutState loadout)
        {
            return loadout.HeGrenades + loadout.Flashbangs + loadout.SmokeGrenades + loadout.FireGrenades;
        }
    }
}
