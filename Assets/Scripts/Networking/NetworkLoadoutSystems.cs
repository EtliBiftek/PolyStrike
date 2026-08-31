using Unity.Entities;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(NetworkServerCombatSystem))]
    public partial struct NetworkLoadoutSwitchSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (player, loadout, input) in
                     SystemAPI.Query<RefRW<NetworkPlayerState>, RefRW<NetworkLoadoutState>, RefRW<NetworkPlayerInput>>())
            {
                var requested = input.ValueRO.WeaponSlot;
                if (requested == 4)
                {
                    requested = ResolveUtilitySlot(player.ValueRO.ActiveWeapon, in loadout.ValueRO);
                    input.ValueRW.WeaponSlot = requested;
                }

                if (requested == 0 || requested == player.ValueRO.ActiveWeapon)
                    continue;

                if (!CanEquip(requested, in player.ValueRO, in loadout.ValueRO))
                {
                    input.ValueRW.WeaponSlot = 0;
                    continue;
                }

                StoreActiveAmmo(ref loadout.ValueRW, in player.ValueRO);
                player.ValueRW.ActiveWeapon = requested;
                LoadActiveAmmo(ref player.ValueRW, in loadout.ValueRO);
            }
        }

        private static byte ResolveUtilitySlot(byte activeWeapon, in NetworkLoadoutState loadout)
        {
            var start = activeWeapon switch
            {
                6 => 1,
                7 => 2,
                8 => 3,
                10 => 0,
                _ => 0
            };

            for (var offset = 0; offset < 4; offset++)
            {
                var index = (start + offset) % 4;
                var slot = index switch
                {
                    0 => (byte)6,
                    1 => (byte)7,
                    2 => (byte)8,
                    _ => (byte)10
                };

                if (CanEquipUtility(slot, in loadout))
                    return slot;
            }

            return 0;
        }

        private static bool CanEquip(byte slot, in NetworkPlayerState player, in NetworkLoadoutState loadout)
        {
            return slot switch
            {
                1 => loadout.PrimaryOwned != 0,
                2 => true,
                5 => (player.Flags & NetworkPlayerFlags.HasBomb) != 0,
                6 or 7 or 8 or 10 => CanEquipUtility(slot, in loadout),
                _ => false
            };
        }

        private static bool CanEquipUtility(byte slot, in NetworkLoadoutState loadout)
        {
            return slot switch
            {
                6 => loadout.HeGrenades > 0,
                7 => loadout.Flashbangs > 0,
                8 => loadout.SmokeGrenades > 0,
                10 => loadout.FireGrenades > 0,
                _ => false
            };
        }

        public static void StoreActiveAmmo(ref NetworkLoadoutState loadout, in NetworkPlayerState player)
        {
            if (player.ActiveWeapon == 1 && loadout.PrimaryOwned != 0)
            {
                loadout.PrimaryMagazine = player.MagazineAmmo;
                loadout.PrimaryReserve = player.ReserveAmmo;
            }
            else if (player.ActiveWeapon == 2)
            {
                loadout.PistolMagazine = player.MagazineAmmo;
                loadout.PistolReserve = player.ReserveAmmo;
            }
        }

        public static void LoadActiveAmmo(ref NetworkPlayerState player, in NetworkLoadoutState loadout)
        {
            if (player.ActiveWeapon == 1 && loadout.PrimaryOwned != 0)
            {
                player.MagazineAmmo = loadout.PrimaryMagazine;
                player.ReserveAmmo = loadout.PrimaryReserve;
            }
            else if (player.ActiveWeapon == 2)
            {
                player.MagazineAmmo = loadout.PistolMagazine;
                player.ReserveAmmo = loadout.PistolReserve;
            }
            else
            {
                player.MagazineAmmo = 0;
                player.ReserveAmmo = 0;
            }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(NetworkServerCombatSystem))]
    [UpdateBefore(typeof(NetworkServerMatchSystem))]
    public partial struct NetworkLoadoutAmmoCommitSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (player, loadout) in
                     SystemAPI.Query<RefRO<NetworkPlayerState>, RefRW<NetworkLoadoutState>>())
            {
                NetworkLoadoutSwitchSystem.StoreActiveAmmo(ref loadout.ValueRW, in player.ValueRO);
            }
        }
    }
}
