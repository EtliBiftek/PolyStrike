using System.Collections.Generic;
using PolyStrike.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace PolyStrike.Networking
{
    public struct NetworkKillFeedRpc : IRpcCommand
    {
        public FixedString128Bytes KillerName;
        public FixedString128Bytes VictimName;
        public byte KillerTeam;
        public byte VictimTeam;
        public byte Weapon;
        public byte Flags;
    }

    public static class NetworkKillFeedFlags
    {
        public const byte Headshot = 1 << 0;
        public const byte TeamKill = 1 << 1;
        public const byte Suicide = 1 << 2;
        public const byte Environment = 1 << 3;
    }

    public static class NetworkKillFeedServer
    {
        public static void Broadcast(
            ref SystemState state,
            in NetworkPlayerState killer,
            in NetworkPlayerState victim,
            byte weapon,
            bool headshot,
            bool suicide = false)
        {
            var flags = (byte)0;
            if (headshot)
                flags |= NetworkKillFeedFlags.Headshot;
            if (killer.Team == victim.Team && !suicide)
                flags |= NetworkKillFeedFlags.TeamKill;
            if (suicide)
                flags |= NetworkKillFeedFlags.Suicide;

            Send(ref state, new NetworkKillFeedRpc
            {
                KillerName = killer.PlayerName,
                VictimName = victim.PlayerName,
                KillerTeam = killer.Team,
                VictimTeam = victim.Team,
                Weapon = weapon,
                Flags = flags
            });
        }

        public static void BroadcastEnvironment(ref SystemState state, in NetworkPlayerState victim, byte weapon)
        {
            Send(ref state, new NetworkKillFeedRpc
            {
                KillerName = default,
                VictimName = victim.PlayerName,
                KillerTeam = byte.MaxValue,
                VictimTeam = victim.Team,
                Weapon = weapon,
                Flags = NetworkKillFeedFlags.Environment
            });
        }

        private static void Send(ref SystemState state, in NetworkKillFeedRpc rpc)
        {
            var rpcEntity = state.EntityManager.CreateEntity(typeof(NetworkKillFeedRpc), typeof(SendRpcCommandRequest));
            state.EntityManager.SetComponentData(rpcEntity, rpc);
            state.EntityManager.SetComponentData(rpcEntity, new SendRpcCommandRequest
            {
                TargetConnection = Entity.Null
            });
        }
    }

    [DisallowMultipleComponent]
    public sealed class NetworkKillFeedOverlay : MonoBehaviour
    {
        private const float EntryLifetime = 6f;
        private const int MaximumVisibleEntries = 6;

        private readonly List<Entry> entries = new List<Entry>(MaximumVisibleEntries);

        private sealed class Entry
        {
            public string Killer;
            public string Victim;
            public byte KillerTeam;
            public byte VictimTeam;
            public byte Weapon;
            public byte Flags;
            public float ExpiresAt;
        }

        private void Update()
        {
            ConsumeEvents();
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime >= entries[i].ExpiresAt)
                    entries.RemoveAt(i);
            }
        }

        private void ConsumeEvents()
        {
            var world = ClientServerBootstrap.ClientWorld;
            if (world == null || !world.IsCreated)
                return;

            var entityManager = world.EntityManager;
            var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NetworkKillFeedRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            var entities = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var rpc = entityManager.GetComponentData<NetworkKillFeedRpc>(entities[i]);
                entries.Add(new Entry
                {
                    Killer = rpc.KillerName.ToString(),
                    Victim = rpc.VictimName.ToString(),
                    KillerTeam = rpc.KillerTeam,
                    VictimTeam = rpc.VictimTeam,
                    Weapon = rpc.Weapon,
                    Flags = rpc.Flags,
                    ExpiresAt = Time.unscaledTime + EntryLifetime
                });
                entityManager.DestroyEntity(entities[i]);
            }

            while (entries.Count > MaximumVisibleEntries)
                entries.RemoveAt(0);

            entities.Dispose();
            query.Dispose();
        }

        private void OnGUI()
        {
            if (entries.Count == 0)
                return;

            var rowStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 15,
                padding = new RectOffset(8, 8, 3, 3)
            };
            rowStyle.normal.textColor = Color.white;

            var y = 52f;
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                var weapon = Localization.Get(GetWeaponKey(entry.Weapon, entry.KillerTeam));
                var headshot = (entry.Flags & NetworkKillFeedFlags.Headshot) != 0
                    ? "  " + Localization.Get("killfeed.headshot")
                    : string.Empty;
                var teamKill = (entry.Flags & NetworkKillFeedFlags.TeamKill) != 0
                    ? "  " + Localization.Get("killfeed.teamkill")
                    : string.Empty;
                var suicide = (entry.Flags & NetworkKillFeedFlags.Suicide) != 0
                    ? "  " + Localization.Get("killfeed.suicide")
                    : string.Empty;
                var environment = (entry.Flags & NetworkKillFeedFlags.Environment) != 0;

                var text = environment
                    ? $"{weapon}   {entry.Victim}"
                    : $"{entry.Killer}   {weapon}{headshot}{teamKill}{suicide}   {entry.Victim}";
                var width = Mathf.Clamp(rowStyle.CalcSize(new GUIContent(text)).x + 20f, 260f, 560f);
                GUI.Box(new Rect(Screen.width - width - 22f, y, width, 28f), text, rowStyle);
                y += 31f;
            }
        }

        private static string GetWeaponKey(byte weapon, byte team)
        {
            return weapon switch
            {
                1 => team == 0 ? "weapon.t_rifle" : "weapon.ct_rifle",
                2 => team == 0 ? "weapon.t_pistol" : "weapon.ct_pistol",
                6 => "grenade.he",
                10 => "grenade.molotov",
                11 => "killfeed.c4",
                _ => "killfeed.unknown_weapon"
            };
        }
    }
}
