using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PolyStrike.Gameplay;
using PolyStrike.Match;
using PolyStrike.Networking;
using PolyStrike.Player;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PolyStrike.Core
{
    public sealed class DeveloperConsole : MonoBehaviour
    {
        private delegate void ConsoleCommand(IReadOnlyList<string> args);

        private static DeveloperConsole instance;
        private readonly Dictionary<string, ConsoleCommand> commands = new Dictionary<string, ConsoleCommand>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<KeyCode, string> bindings = new Dictionary<KeyCode, string>();
        private readonly List<string> lines = new List<string>();
        private readonly List<string> history = new List<string>();

        private Vector2 scroll;
        private string input = string.Empty;
        private int historyIndex;
        private bool focusInput;
        private CursorLockMode previousLockMode;
        private bool previousCursorVisible;
        private string lastAddress = "127.0.0.1";
        private string pendingConnectAddress;
        private float pendingConnectAt;
        private GUIStyle outputStyle;
        private GUIStyle inputStyle;
        private GUIStyle hintStyle;

        public static bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (instance != null || FindFirstObjectByType<DeveloperConsole>() != null)
                return;

            var root = new GameObject("Developer Console");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<DeveloperConsole>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            RegisterCommands();
            Print(Localization.Get("console.ready"));
        }

        private void Update()
        {
            if (TogglePressed())
            {
                SetOpen(!IsOpen);
                return;
            }

            if (pendingConnectAddress != null && Time.unscaledTime >= pendingConnectAt && !HasClientConnection())
            {
                var address = pendingConnectAddress;
                pendingConnectAddress = null;
                ConnectTo(address);
            }

            if (!IsOpen)
            {
                RunBindings();
                return;
            }

            if (EscapePressed())
            {
                SetOpen(false);
                return;
            }

            if (EnterPressed())
            {
                SubmitInput();
                return;
            }

            if (UpPressed())
                RecallHistory(-1);
            else if (DownPressed())
                RecallHistory(1);
            else if (TabPressed())
                Autocomplete();
        }

        private void OnGUI()
        {
            if (!IsOpen)
                return;

            EnsureStyles();
            var height = Mathf.Min(Screen.height * 0.62f, 600f);
            GUI.Box(new Rect(0f, 0f, Screen.width, height), GUIContent.none);

            GUILayout.BeginArea(new Rect(14f, 12f, Screen.width - 28f, height - 24f));
            GUILayout.Label(Localization.Get("console.title"));

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            for (var i = 0; i < lines.Count; i++)
                GUILayout.Label(lines[i], outputStyle);
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUILayout.Label("]", GUILayout.Width(16f));
            GUI.SetNextControlName("ConsoleInput");
            input = GUILayout.TextField(input, inputStyle);
            GUILayout.EndHorizontal();
            GUILayout.Label(Localization.Get("console.hint"), hintStyle);
            GUILayout.EndArea();

            if (focusInput)
            {
                GUI.FocusControl("ConsoleInput");
                focusInput = false;
            }
        }

        public static void Execute(string commandLine)
        {
            instance?.ExecuteLine(commandLine, true);
        }

        private void RegisterCommands()
        {
            commands["help"] = _ => PrintCommandList();
            commands["cvarlist"] = _ => PrintCommandList();
            commands["find"] = FindCommands;
            commands["clear"] = _ => lines.Clear();
            commands["echo"] = args => Print(string.Join(" ", args));
            commands["toggleconsole"] = _ => SetOpen(!IsOpen);

            commands["sv_cheats"] = args => BoolCvar("sv_cheats", args, () => CompetitiveCvars.SvCheats, value => CompetitiveCvars.SvCheats = value);
            commands["mp_freezetime"] = args => FloatCvar("mp_freezetime", args, () => CompetitiveCvars.FreezeTime, value => CompetitiveCvars.FreezeTime = Mathf.Clamp(value, 0f, 60f));
            commands["mp_roundtime"] = args => FloatCvar("mp_roundtime", args, () => CompetitiveCvars.RoundTime / 60f, value => CompetitiveCvars.RoundTime = Mathf.Clamp(value * 60f, 15f, 3600f));
            commands["mp_buytime"] = args => FloatCvar("mp_buytime", args, () => CompetitiveCvars.BuyTime, value => CompetitiveCvars.BuyTime = Mathf.Clamp(value, 0f, 9999f));
            commands["mp_startmoney"] = args => IntCvar("mp_startmoney", args, () => CompetitiveCvars.StartMoney, value => CompetitiveCvars.StartMoney = Mathf.Clamp(value, 0, CompetitiveCvars.MaxMoney));
            commands["mp_maxmoney"] = args => IntCvar("mp_maxmoney", args, () => CompetitiveCvars.MaxMoney, value => CompetitiveCvars.MaxMoney = Mathf.Clamp(value, 0, 65535));
            commands["mp_buy_anywhere"] = args => IntCvar("mp_buy_anywhere", args, () => CompetitiveCvars.BuyAnywhere, value => CompetitiveCvars.BuyAnywhere = Mathf.Clamp(value, 0, 3));
            commands["mp_restartgame"] = RestartGame;
            commands["map"] = MapCommand;
            commands["changelevel"] = MapCommand;

            commands["bot_stop"] = args => BoolCvar("bot_stop", args, () => CompetitiveCvars.BotStop, value => CompetitiveCvars.BotStop = value);
            commands["bot_add"] = args => AddBot(args, null);
            commands["bot_add_t"] = args => AddBot(args, MatchTeam.Terrorists);
            commands["bot_add_ct"] = args => AddBot(args, MatchTeam.CounterTerrorists);
            commands["bot_kick"] = _ => Print(GameBootstrap.ConsoleKickBots().ToString(CultureInfo.InvariantCulture));
            commands["bot_place"] = _ => Print(GameBootstrap.ConsolePlaceBot() ? Localization.Get("console.ok") : Localization.Get("console.no_bot"));

            commands["noclip"] = _ => ToggleNoclip();
            commands["god"] = _ => ToggleGod();
            commands["kill"] = _ => KillLocalPlayer();
            commands["give"] = Give;

            commands["sensitivity"] = Sensitivity;
            commands["volume"] = Volume;
            commands["fps_max"] = FpsMax;
            commands["cl_showfps"] = ShowFps;

            commands["connect"] = Connect;
            commands["disconnect"] = _ => Disconnect();
            commands["retry"] = _ => Retry();
            commands["status"] = _ => PrintStatus();

            commands["bind"] = Bind;
            commands["unbind"] = Unbind;
            commands["unbindall"] = _ => bindings.Clear();
            commands["key_listboundkeys"] = _ => PrintBindings();
            commands["exec"] = Exec;

            commands["say"] = args => PrintChat(args, false);
            commands["say_team"] = args => PrintChat(args, true);
            commands["quit"] = _ => Application.Quit();
            commands["exit"] = _ => Application.Quit();
        }

        private void ExecuteLine(string commandLine, bool echo)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
                return;

            foreach (var statement in SplitStatements(commandLine))
            {
                var tokens = Tokenize(statement);
                if (tokens.Count == 0)
                    continue;

                if (echo)
                    Print("] " + statement.Trim());

                if (!commands.TryGetValue(tokens[0], out var command))
                {
                    Print(string.Format(Localization.Get("console.unknown"), tokens[0]));
                    continue;
                }

                var args = tokens.Count > 1 ? tokens.GetRange(1, tokens.Count - 1) : new List<string>();
                try
                {
                    command(args);
                }
                catch (Exception exception)
                {
                    Print(string.Format(Localization.Get("console.error"), exception.Message));
                }
            }
        }

        private void SubmitInput()
        {
            var command = input.Trim();
            input = string.Empty;
            focusInput = true;
            if (command.Length == 0)
                return;

            if (history.Count == 0 || !string.Equals(history[history.Count - 1], command, StringComparison.Ordinal))
                history.Add(command);
            if (history.Count > 64)
                history.RemoveAt(0);

            historyIndex = history.Count;
            ExecuteLine(command, true);
        }

        private void SetOpen(bool open)
        {
            if (IsOpen == open)
                return;

            IsOpen = open;
            if (open)
            {
                previousLockMode = Cursor.lockState;
                previousCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                focusInput = true;
            }
            else
            {
                Cursor.lockState = previousLockMode == CursorLockMode.None ? CursorLockMode.Locked : previousLockMode;
                Cursor.visible = previousCursorVisible;
            }
        }

        private void RestartGame(IReadOnlyList<string> args)
        {
            var delay = 0f;
            if (args.Count > 0 && !TryFloat(args[0], out delay))
            {
                PrintUsage("mp_restartgame <seconds>");
                return;
            }

            delay = Mathf.Clamp(delay, 0f, 60f);
            MatchRoundManager.Instance?.RequestRestart(delay);
            if (HasLocalServerWorld())
                NetworkConsoleBridge.RequestRestart(delay);
            Print(string.Format(Localization.Get("console.restart"), delay));
        }

        private void MapCommand(IReadOnlyList<string> args)
        {
            if (args.Count == 0)
            {
                PrintUsage("map de_sandline");
                return;
            }

            var map = args[0].Trim().ToLowerInvariant();
            if (map != "de_sandline" && map != "sandline")
            {
                Print(string.Format(Localization.Get("console.map_unknown"), args[0]));
                return;
            }

            MatchRoundManager.Instance?.RequestRestart(0f);
            if (HasLocalServerWorld())
                NetworkConsoleBridge.RequestRestart(0f);
            Print("de_sandline");
        }

        private void AddBot(IReadOnlyList<string> args, MatchTeam? forcedTeam)
        {
            var team = forcedTeam ?? ChooseSmallerTeam();
            var participant = GameBootstrap.ConsoleAddBot(team);
            Print(participant != null ? Localization.Get("console.ok") : Localization.Get("console.team_full"));
        }

        private static MatchTeam ChooseSmallerTeam()
        {
            var t = 0;
            var ct = 0;
            var all = MatchParticipant.All;
            for (var i = 0; i < all.Count; i++)
            {
                if (all[i] == null || all[i].IsLocalPlayer)
                    continue;
                if (all[i].Team == MatchTeam.Terrorists) t++; else ct++;
            }
            return t <= ct ? MatchTeam.Terrorists : MatchTeam.CounterTerrorists;
        }

        private void ToggleNoclip()
        {
            if (!RequireCheats())
                return;

            var player = MatchRoundManager.Instance?.GetLocalPlayer();
            if (player == null)
            {
                Print(Localization.Get("console.offline_only"));
                return;
            }

            var noclip = player.GetComponent<DeveloperNoclip>() ?? player.gameObject.AddComponent<DeveloperNoclip>();
            noclip.Toggle();
            Print("noclip = " + (noclip.Enabled ? "1" : "0"));
        }

        private void ToggleGod()
        {
            if (!RequireCheats())
                return;

            var player = MatchRoundManager.Instance?.GetLocalPlayer();
            if (player == null)
            {
                Print(Localization.Get("console.offline_only"));
                return;
            }

            player.Health.SetGodMode(!player.Health.GodMode);
            Print("god = " + (player.Health.GodMode ? "1" : "0"));
        }

        private void KillLocalPlayer()
        {
            var player = MatchRoundManager.Instance?.GetLocalPlayer();
            if (player == null)
            {
                Print(Localization.Get("console.offline_only"));
                return;
            }
            player.Health.TakeDamage(10000f);
        }

        private void Give(IReadOnlyList<string> args)
        {
            if (!RequireCheats())
                return;
            if (args.Count == 0)
            {
                PrintUsage("give weapon_ak47");
                return;
            }

            var player = MatchRoundManager.Instance?.GetLocalPlayer();
            if (player == null)
            {
                Print(Localization.Get("console.offline_only"));
                return;
            }

            var item = args[0].ToLowerInvariant();
            var success = item switch
            {
                "weapon_ak47" => GivePrimary(player),
                "weapon_m4a1" => GivePrimary(player),
                "weapon_m4a1_silencer" => GivePrimary(player),
                "weapon_hegrenade" => GiveGrenade(player, GrenadeType.HighExplosive),
                "weapon_flashbang" => GiveGrenade(player, GrenadeType.Flashbang),
                "weapon_smokegrenade" => GiveGrenade(player, GrenadeType.Smoke),
                "weapon_molotov" => GiveGrenade(player, GrenadeType.Molotov),
                "weapon_incgrenade" => GiveGrenade(player, GrenadeType.Molotov),
                _ => false
            };

            Print(success ? Localization.Get("console.ok") : string.Format(Localization.Get("console.give_unknown"), args[0]));
        }

        private static bool GivePrimary(MatchParticipant player)
        {
            var weapon = player.GetComponentInChildren<HitscanWeapon>();
            if (weapon == null)
                return false;
            weapon.BuyPrimary();
            return true;
        }

        private static bool GiveGrenade(MatchParticipant player, GrenadeType type)
        {
            var utility = player.GetComponent<UtilityController>();
            return utility != null && utility.AddGrenade(type);
        }

        private void Sensitivity(IReadOnlyList<string> args)
        {
            if (args.Count == 0)
            {
                Print("sensitivity = " + CompetitiveCvars.Sensitivity.ToString("0.###", CultureInfo.InvariantCulture));
                return;
            }
            if (!TryFloat(args[0], out var value))
            {
                PrintUsage("sensitivity <value>");
                return;
            }
            CompetitiveCvars.SetSensitivity(value);
            Print("sensitivity = " + CompetitiveCvars.Sensitivity.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private void Volume(IReadOnlyList<string> args)
        {
            if (args.Count == 0)
            {
                Print("volume = " + CompetitiveCvars.Volume.ToString("0.###", CultureInfo.InvariantCulture));
                return;
            }
            if (!TryFloat(args[0], out var value))
            {
                PrintUsage("volume <0-1>");
                return;
            }
            CompetitiveCvars.SetVolume(value);
            Print("volume = " + CompetitiveCvars.Volume.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private void FpsMax(IReadOnlyList<string> args)
        {
            if (args.Count == 0)
            {
                Print("fps_max = " + CompetitiveCvars.FpsMax);
                return;
            }
            if (!int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                PrintUsage("fps_max <0-1000>");
                return;
            }
            CompetitiveCvars.SetFpsMax(value);
            Print("fps_max = " + CompetitiveCvars.FpsMax);
        }

        private void ShowFps(IReadOnlyList<string> args)
        {
            if (args.Count == 0)
            {
                Print("cl_showfps = " + CompetitiveCvars.ShowFps);
                return;
            }
            if (!int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                PrintUsage("cl_showfps <0-3>");
                return;
            }
            CompetitiveCvars.SetShowFps(value);
            Print("cl_showfps = " + CompetitiveCvars.ShowFps);
        }

        private void Connect(IReadOnlyList<string> args)
        {
            if (args.Count == 0)
            {
                PrintUsage("connect <ip>");
                return;
            }
            ConnectTo(args[0]);
        }

        private void ConnectTo(string host)
        {
            var clientWorld = ClientServerBootstrap.ClientWorld;
            if (!IsUsable(clientWorld))
            {
                Print(Localization.Get("network.status.world_error"));
                return;
            }

            NetworkEndpoint endpoint;
            try
            {
                endpoint = NetworkEndpoint.Parse(host.Trim(), PolyStrikeNetcodeBootstrap.DefaultGamePort);
            }
            catch
            {
                Print(Localization.Get("network.status.invalid_address"));
                return;
            }

            if (!endpoint.IsValid)
            {
                Print(Localization.Get("network.status.invalid_address"));
                return;
            }

            lastAddress = host.Trim();
            var entity = clientWorld.EntityManager.CreateEntity(typeof(NetworkStreamRequestConnect));
            clientWorld.EntityManager.SetComponentData(entity, new NetworkStreamRequestConnect { Endpoint = endpoint });
            Print(string.Format(Localization.Get("console.connecting"), lastAddress));
        }

        private void Disconnect()
        {
            var clientWorld = ClientServerBootstrap.ClientWorld;
            if (!IsUsable(clientWorld))
                return;

            var manager = clientWorld.EntityManager;
            var query = manager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>());
            using var connections = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < connections.Length; i++)
            {
                if (!manager.HasComponent<NetworkStreamRequestDisconnect>(connections[i]))
                    manager.AddComponent<NetworkStreamRequestDisconnect>(connections[i]);
            }
            Print(Localization.Get("console.disconnected"));
        }

        private void Retry()
        {
            Disconnect();
            pendingConnectAddress = lastAddress;
            pendingConnectAt = Time.unscaledTime + 0.25f;
        }

        private void PrintStatus()
        {
            var clientWorld = ClientServerBootstrap.ClientWorld;
            if (IsUsable(clientWorld))
            {
                var manager = clientWorld.EntityManager;
                var query = manager.CreateEntityQuery(ComponentType.ReadOnly<NetworkPlayerState>());
                using var players = query.ToComponentDataArray<NetworkPlayerState>(Allocator.Temp);
                if (players.Length > 0)
                {
                    Print(string.Format(Localization.Get("console.status_players"), players.Length));
                    for (var i = 0; i < players.Length; i++)
                    {
                        var team = players[i].Team == 0 ? "T" : "CT";
                        Print($"#{i + 1} {players[i].PlayerName}  {team}  {players[i].PingMs}ms");
                    }
                    return;
                }
            }

            var all = MatchParticipant.All;
            Print(string.Format(Localization.Get("console.status_players"), all.Count));
            for (var i = 0; i < all.Count; i++)
            {
                var player = all[i];
                if (player != null)
                    Print($"#{i + 1} {player.name}  {(player.Team == MatchTeam.Terrorists ? "T" : "CT")}");
            }
        }

        private void Bind(IReadOnlyList<string> args)
        {
            if (args.Count == 0)
            {
                PrintBindings();
                return;
            }
            if (!TryParseKey(args[0], out var key))
            {
                Print(string.Format(Localization.Get("console.bad_key"), args[0]));
                return;
            }
            if (args.Count == 1)
            {
                Print(bindings.TryGetValue(key, out var existing) ? $"{args[0]} = {existing}" : Localization.Get("console.not_bound"));
                return;
            }
            bindings[key] = string.Join(" ", args.Skip(1));
            Print($"bind {args[0]} \"{bindings[key]}\"");
        }

        private void Unbind(IReadOnlyList<string> args)
        {
            if (args.Count == 0 || !TryParseKey(args[0], out var key))
            {
                PrintUsage("unbind <key>");
                return;
            }
            bindings.Remove(key);
        }

        private void PrintBindings()
        {
            foreach (var pair in bindings.OrderBy(pair => pair.Key.ToString()))
                Print($"bind {pair.Key} \"{pair.Value}\"");
        }

        private void Exec(IReadOnlyList<string> args)
        {
            if (args.Count == 0)
            {
                PrintUsage("exec autoexec.cfg");
                return;
            }

            var fileName = Path.GetFileName(args[0]);
            if (!fileName.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase))
                fileName += ".cfg";

            var path = Path.Combine(Application.streamingAssetsPath, "Config", fileName);
            if (!File.Exists(path))
            {
                Print(string.Format(Localization.Get("console.exec_missing"), fileName));
                return;
            }

            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                    continue;
                ExecuteLine(line, false);
            }
            Print(string.Format(Localization.Get("console.exec_done"), fileName));
        }

        private void FindCommands(IReadOnlyList<string> args)
        {
            if (args.Count == 0)
            {
                PrintUsage("find <text>");
                return;
            }
            var needle = args[0];
            foreach (var name in commands.Keys.Where(name => name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(name => name))
                Print(name);
        }

        private void PrintCommandList()
        {
            Print(Localization.Get("console.command_list"));
            Print(string.Join("  ", commands.Keys.OrderBy(name => name)));
        }

        private void PrintChat(IReadOnlyList<string> args, bool teamOnly)
        {
            if (args.Count == 0)
                return;
            var prefix = teamOnly ? "(TEAM) " : string.Empty;
            Print(prefix + NetworkConnectionMenu.LocalPlayerName + ": " + string.Join(" ", args));
        }

        private void BoolCvar(string name, IReadOnlyList<string> args, Func<bool> getter, Action<bool> setter)
        {
            if (args.Count > 0)
            {
                if (!TryBool(args[0], out var value))
                {
                    PrintUsage(name + " <0/1>");
                    return;
                }
                setter(value);
            }
            Print(name + " = " + (getter() ? "1" : "0"));
        }

        private void FloatCvar(string name, IReadOnlyList<string> args, Func<float> getter, Action<float> setter)
        {
            if (args.Count > 0)
            {
                if (!TryFloat(args[0], out var value))
                {
                    PrintUsage(name + " <value>");
                    return;
                }
                setter(value);
            }
            Print(name + " = " + getter().ToString("0.###", CultureInfo.InvariantCulture));
        }

        private void IntCvar(string name, IReadOnlyList<string> args, Func<int> getter, Action<int> setter)
        {
            if (args.Count > 0)
            {
                if (!int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                {
                    PrintUsage(name + " <value>");
                    return;
                }
                setter(value);
            }
            Print(name + " = " + getter());
        }

        private bool RequireCheats()
        {
            if (CompetitiveCvars.SvCheats)
                return true;
            Print(Localization.Get("console.cheats_required"));
            return false;
        }

        private void RecallHistory(int direction)
        {
            if (history.Count == 0)
                return;
            historyIndex = Mathf.Clamp(historyIndex + direction, 0, history.Count);
            input = historyIndex >= history.Count ? string.Empty : history[historyIndex];
            focusInput = true;
        }

        private void Autocomplete()
        {
            var prefix = input.Trim();
            if (prefix.Contains(' '))
                return;
            var matches = commands.Keys.Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).OrderBy(name => name).ToArray();
            if (matches.Length == 1)
            {
                input = matches[0] + " ";
                focusInput = true;
            }
            else if (matches.Length > 1)
            {
                Print(string.Join("  ", matches));
            }
        }

        private void RunBindings()
        {
            if (bindings.Count == 0)
                return;
            foreach (var pair in bindings.ToArray())
            {
                if (Input.GetKeyDown(pair.Key))
                    ExecuteLine(pair.Value, false);
            }
        }

        private void Print(string text)
        {
            if (text == null)
                return;
            lines.Add(text);
            if (lines.Count > 220)
                lines.RemoveRange(0, lines.Count - 220);
            scroll.y = float.MaxValue;
        }

        private void PrintUsage(string usage)
        {
            Print(string.Format(Localization.Get("console.usage"), usage));
        }

        private void EnsureStyles()
        {
            outputStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 13 };
            inputStyle ??= new GUIStyle(GUI.skin.textField) { fontSize = 14 };
            hintStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 11 };
        }

        private static List<string> SplitStatements(string inputLine)
        {
            var result = new List<string>();
            var start = 0;
            var quoted = false;
            for (var i = 0; i < inputLine.Length; i++)
            {
                if (inputLine[i] == '"') quoted = !quoted;
                if (inputLine[i] != ';' || quoted) continue;
                result.Add(inputLine.Substring(start, i - start));
                start = i + 1;
            }
            result.Add(inputLine.Substring(start));
            return result;
        }

        private static List<string> Tokenize(string statement)
        {
            var tokens = new List<string>();
            var current = new System.Text.StringBuilder();
            var quoted = false;
            for (var i = 0; i < statement.Length; i++)
            {
                var character = statement[i];
                if (character == '"')
                {
                    quoted = !quoted;
                    continue;
                }
                if (char.IsWhiteSpace(character) && !quoted)
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }
                    continue;
                }
                current.Append(character);
            }
            if (current.Length > 0)
                tokens.Add(current.ToString());
            return tokens;
        }

        private static bool TryFloat(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryBool(string text, out bool value)
        {
            if (text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }
            if (text == "0" || text.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }
            value = false;
            return false;
        }

        private static bool TryParseKey(string text, out KeyCode key)
        {
            var normalized = text.Trim().ToLowerInvariant();
            var alias = normalized switch
            {
                "mouse1" => "Mouse0",
                "mouse2" => "Mouse1",
                "mouse3" => "Mouse2",
                "mwheelup" => "None",
                "mwheeldown" => "None",
                "space" => "Space",
                "ctrl" => "LeftControl",
                "shift" => "LeftShift",
                "alt" => "LeftAlt",
                "enter" => "Return",
                "esc" => "Escape",
                "del" => "Delete",
                "ins" => "Insert",
                _ => text
            };
            return Enum.TryParse(alias, true, out key) && key != KeyCode.None;
        }

        private static bool HasLocalServerWorld()
        {
            var world = ClientServerBootstrap.ServerWorld;
            return IsUsable(world);
        }

        private static bool HasClientConnection()
        {
            var world = ClientServerBootstrap.ClientWorld;
            if (!IsUsable(world))
                return false;
            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>());
            return !query.IsEmptyIgnoreFilter;
        }

        private static bool IsUsable(World world)
        {
            return world != null && world.IsCreated;
        }

        private static bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?.backquoteKey.wasPressedThisFrame == true;
#else
            return Input.GetKeyDown(KeyCode.BackQuote);
#endif
        }

        private static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?.escapeKey.wasPressedThisFrame == true;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private static bool EnterPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?.enterKey.wasPressedThisFrame == true || Keyboard.current?.numpadEnterKey.wasPressedThisFrame == true;
#else
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
        }

        private static bool UpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?.upArrowKey.wasPressedThisFrame == true;
#else
            return Input.GetKeyDown(KeyCode.UpArrow);
#endif
        }

        private static bool DownPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?.downArrowKey.wasPressedThisFrame == true;
#else
            return Input.GetKeyDown(KeyCode.DownArrow);
#endif
        }

        private static bool TabPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?.tabKey.wasPressedThisFrame == true;
#else
            return Input.GetKeyDown(KeyCode.Tab);
#endif
        }
    }
}
