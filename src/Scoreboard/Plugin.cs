using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Scoreboard
{
    [BepInPlugin(PluginId, "Scoreboard", PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginId = "se.junge.valheim.scoreboard";
        public const string PluginVersion = "0.1.0";
        private const int Protocol = 1;
        private const string Hello = "Scoreboard.Hello.v1";
        private const string Report = "Scoreboard.Report.v1";
        private const string Ack = "Scoreboard.Ack.v1";
        private const string Request = "Scoreboard.Request.v1";
        private const string Board = "Scoreboard.Board.v1";
        private const float StorageRetrySeconds = 15;

        internal static Plugin Instance;
        private Harmony harmony;
        private ConfigEntry<KeyCode> toggle;
        private ZNet network;
        private Database database;
        private ClientJournal journal;
        private string databasePath;
        private string journalPath;
        private long world;
        private long character;
        private bool pluginFailed;
        private bool serverUnavailable;
        private bool clientUnavailable;
        private bool journalDirty;
        private bool handshakeReceived;
        private Counters pending = new Counters();
        private long pendingCharacter;
        private float nextTick;
        private float lastTick;
        private float nextDatabaseSave;
        private float nextJournalSave;
        private float nextStorageLog;
        private float lastBoard;
        private float nextHello;
        private int helloAttempts;
        private readonly HashSet<ZRpc> registered = new HashSet<ZRpc>();
        private readonly HashSet<ZRpc> negotiated = new HashSet<ZRpc>();
        private readonly Dictionary<ZRpc, string> connectionStreams = new Dictionary<ZRpc, string>();
        private readonly Dictionary<ZRpc, float> lastRequests = new Dictionary<ZRpc, float>();
        private readonly Dictionary<ZRpc, float> lastReports = new Dictionary<ZRpc, float>();
        private readonly Dictionary<long, float> reporting = new Dictionary<long, float>();
        private readonly List<BoardRow> rows = new List<BoardRow>();
        private bool visible;
        private int sort = 3;
        private int page;
        private string status = "Waiting for the Scoreboard server...";
        private readonly string[] columns = { "Player", "Online", "Time", "Kills", "Bosses", "Deaths" };

        private void Awake()
        {
            Instance = this;
            toggle = Config.Bind("Panel", "ToggleKey", KeyCode.F8, "Show or hide Scoreboard.");
            harmony = new Harmony(PluginId);
            harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo($"Scoreboard {PluginVersion} loaded. Full statistics require Scoreboard on the server and clients.");
        }

        private void Update()
        {
            if (network != ZNet.instance)
            {
                ResetSession();
                network = ZNet.instance;
                lastTick = Time.realtimeSinceStartup;
            }
            if (!network) return;
            UpdateKeys();
            if (pluginFailed || Time.realtimeSinceStartup < nextTick) return;

            try
            {
                var now = Time.realtimeSinceStartup;
                var elapsed = Math.Max(0, now - lastTick);
                nextTick = now + 1;
                lastTick = now;
                RegisterPeers();

                if (network.IsServer()) UpdateServer(now, elapsed);
                else UpdateClientHandshake(now);

                if (journal != null && !clientUnavailable)
                {
                    if (journalDirty && now >= nextJournalSave) TrySaveJournal(now);
                    if (!journalDirty) SendOrApplyJournal(now);
                }

                if (visible)
                {
                    if (network.IsServer() && database != null) SetRows(BuildRows());
                    else if (journal != null && handshakeReceived) network.GetServerRPC()?.Invoke(Request, page);
                }

                if (database != null && now >= nextDatabaseSave) TrySaveDatabase(now);
            }
            catch (Exception error)
            {
                pluginFailed = true;
                status = "Scoreboard stopped after an unexpected error. Check the BepInEx log.";
                Logger.LogError(error);
            }
        }

        private void UpdateServer(float now, float elapsed)
        {
            if (serverUnavailable || !TryEnsureDatabase()) return;
            var online = OnlinePlayers();
            foreach (var entry in online)
            {
                var row = database.Touch(entry.Key, entry.Value);
                if (row == null) continue;
                row.Seconds = Math.Min(Database.MaximumSeconds, row.Seconds + elapsed);
                row.LastSeenUtc = DateTime.UtcNow.Ticks;
            }
            foreach (var id in reporting.Keys.Where(id => !online.ContainsKey(id)).ToArray()) reporting.Remove(id);
            if (!network.IsDedicated() && Player.m_localPlayer && !clientUnavailable) TryEnsureJournal(world);
        }

        private void UpdateClientHandshake(float now)
        {
            if (clientUnavailable) return;
            if (handshakeReceived)
            {
                if (journal == null) TryEnsureJournal(world);
                return;
            }
            if (now < nextHello || helloAttempts >= 12 || network.GetServerRPC() == null) return;
            network.GetServerRPC().Invoke(Hello, Protocol, 0L);
            helloAttempts++;
            nextHello = now + 5;
            if (helloAttempts == 12) status = "No Scoreboard server response. Reopen the panel to retry.";
        }

        private void SendOrApplyJournal(float now)
        {
            if (network.IsServer())
            {
                if (database == null || Player.m_localPlayer == null) return;
                if (database.Touch(character, Player.m_localPlayer.GetPlayerName()) == null) return;
                if (database.Apply(character, journal.Stream, journal.Totals)) reporting[character] = now;
                status = "Connected to the local Scoreboard server";
                return;
            }
            if (!handshakeReceived) return;
            network.GetServerRPC()?.Invoke(Report, journal.Stream, journal.Totals.Deaths, journal.Totals.Kills, journal.Totals.Bosses);
        }

        private void RegisterPeers()
        {
            var current = new HashSet<ZRpc>(network.GetPeers().Select(p => p.m_rpc));
            foreach (var rpc in registered.Where(r => !current.Contains(r)).ToArray())
            {
                Unregister(rpc);
                registered.Remove(rpc);
                negotiated.Remove(rpc);
                connectionStreams.Remove(rpc);
                lastRequests.Remove(rpc);
                lastReports.Remove(rpc);
            }
            foreach (var rpc in current)
            {
                if (!registered.Add(rpc)) continue;
                rpc.Register<int, long>(Hello, OnHello);
                rpc.Register<string, long, long, long>(Report, OnReport);
                rpc.Register<long>(Ack, OnAck);
                rpc.Register<int>(Request, OnRequest);
                rpc.Register<ZPackage>(Board, OnBoard);
            }
        }

        private void OnHello(ZRpc rpc, int version, long ignoredWorld)
        {
            if (!network || pluginFailed || version != Protocol) return;
            try
            {
                if (network.IsServer())
                {
                    if (serverUnavailable || !network.GetPeers().Any(p => p.m_rpc == rpc) || !TryEnsureDatabase()) return;
                    negotiated.Add(rpc);
                    rpc.Invoke(Hello, Protocol, world);
                }
                else if (rpc == network.GetServerRPC())
                {
                    handshakeReceived = true;
                    world = ignoredWorld;
                    status = "Scoreboard server found; waiting for the local player...";
                    TryEnsureJournal(world);
                }
            }
            catch (Exception error) { DisableUnexpected(error); }
        }

        private void OnReport(ZRpc rpc, string stream, long deaths, long kills, long bosses)
        {
            if (!network || !network.IsServer() || database == null || serverUnavailable || !negotiated.Contains(rpc)) return;
            if (Throttled(lastReports, rpc, 0.5f)) return;
            var peer = network.GetPeers().Find(p => p.m_rpc == rpc && p.IsReady());
            if (peer == null || peer.m_playerID == 0 || !Guid.TryParseExact(stream, "N", out _)) return;
            if (connectionStreams.TryGetValue(rpc, out var boundStream) && boundStream != stream) return;
            var totals = new Counters { Deaths = deaths, Kills = kills, Bosses = bosses };
            if (!totals.IsValid() || database.Touch(peer.m_playerID, peer.m_playerName) == null) return;
            if (!database.Apply(peer.m_playerID, stream, totals)) return;
            connectionStreams[rpc] = stream;
            reporting[peer.m_playerID] = Time.realtimeSinceStartup;
            rpc.Invoke(Ack, world);
        }

        private void OnAck(ZRpc rpc, long worldId)
        {
            if (network && !network.IsServer() && rpc == network.GetServerRPC() && worldId == world)
                status = "Statistics received by the server";
        }

        private void OnRequest(ZRpc rpc, int requestedPage)
        {
            if (!network || !network.IsServer() || database == null || serverUnavailable || !negotiated.Contains(rpc) ||
                Throttled(lastRequests, rpc, 0.8f)) return;
            if (!network.GetPeers().Any(p => p.m_rpc == rpc && p.IsReady())) return;
            var snapshot = BuildRows();
            var package = new ZPackage();
            package.Write(world);
            package.Write(snapshot.Count);
            foreach (var row in snapshot)
            {
                package.Write(row.Name); package.Write(row.Online); package.Write(row.Reporting);
                package.Write(row.Seconds); package.Write(row.Deaths); package.Write(row.Kills); package.Write(row.Bosses);
            }
            rpc.Invoke(Board, package);
        }

        private void OnBoard(ZRpc rpc, ZPackage package)
        {
            if (!network || network.IsServer() || rpc != network.GetServerRPC() || !handshakeReceived || pluginFailed) return;
            try
            {
                if (package.Size() > 200000 || package.ReadLong() != world) return;
                var count = package.ReadInt();
                if (count < 0 || count > 500) return;
                var snapshot = new List<BoardRow>(count);
                for (var i = 0; i < count; ++i)
                {
                    var row = new BoardRow
                    {
                        Name = Database.CleanName(package.ReadString()), Online = package.ReadBool(), Reporting = package.ReadBool(),
                        Seconds = package.ReadDouble(), Deaths = package.ReadLong(), Kills = package.ReadLong(), Bosses = package.ReadLong()
                    };
                    if (row.Seconds < 0 || row.Seconds > Database.MaximumSeconds || row.Deaths < 0 || row.Kills < 0 || row.Bosses < 0) return;
                    snapshot.Add(row);
                }
                SetRows(snapshot);
            }
            catch (Exception error) when (error is EndOfStreamException || error is FormatException || error is ArgumentException) { }
        }

        private static bool Throttled(Dictionary<ZRpc, float> times, ZRpc rpc, float interval)
        {
            var now = Time.realtimeSinceStartup;
            if (times.TryGetValue(rpc, out var last) && now - last < interval) return true;
            times[rpc] = now;
            return false;
        }

        private string DataDirectory => Path.Combine(Paths.ConfigPath, "Scoreboard");

        private bool TryEnsureDatabase()
        {
            if (database != null) return true;
            try
            {
                world = network.GetWorldUID();
                databasePath = Path.Combine(DataDirectory, "world-" + world + ".json");
                var loaded = Storage.Load(databasePath, () => new Database { World = world });
                if (!loaded.IsValid(world)) throw new InvalidDataException("Unknown Scoreboard data format or incorrect world ID.");
                database = loaded;
                return true;
            }
            catch (InvalidDataException error) { DisableServer(error); }
            catch (Exception error) { LogStorageRetry("server database", error); }
            return false;
        }

        private bool TryEnsureJournal(long worldId)
        {
            if (journal != null) return true;
            if (!Game.instance || !Player.m_localPlayer) return false;
            try
            {
                var id = Game.instance.GetPlayerProfile().GetPlayerID();
                var path = Path.Combine(DataDirectory, "client-" + worldId + "-" + id + ".json");
                var loaded = Storage.Load(path, () => new ClientJournal());
                if (loaded.Schema != 1 || loaded.Totals == null || !loaded.Totals.IsValid() || !Guid.TryParseExact(loaded.Stream, "N", out _))
                    throw new InvalidDataException("Invalid Scoreboard client journal.");
                world = worldId; character = id; journalPath = path; journal = loaded;
                if (pendingCharacter == id)
                {
                    AddPending(journal.Totals, pending);
                    pending = new Counters(); pendingCharacter = 0;
                }
                journalDirty = true;
                nextJournalSave = 0;
                return true;
            }
            catch (InvalidDataException error) { DisableClient(error); }
            catch (Exception error) { LogStorageRetry("client journal", error); }
            return false;
        }

        private void TrySaveDatabase(float now)
        {
            try
            {
                Storage.Save(databasePath, database);
                nextDatabaseSave = now + 15;
            }
            catch (Exception error)
            {
                nextDatabaseSave = now + StorageRetrySeconds;
                LogStorageRetry("server database", error);
            }
        }

        private void TrySaveJournal(float now)
        {
            try
            {
                Storage.Save(journalPath, journal);
                journalDirty = false;
            }
            catch (Exception error)
            {
                nextJournalSave = now + StorageRetrySeconds;
                LogStorageRetry("client journal", error);
            }
        }

        internal void Count(PlayerProfile profile, PlayerStatType type, float amount)
        {
            if (pluginFailed || clientUnavailable || !network || network.IsDedicated() || !Game.instance ||
                amount <= 0 || float.IsInfinity(amount) || float.IsNaN(amount)) return;
            if (!ReferenceEquals(profile, Game.instance.GetPlayerProfile())) return;
            if (type != PlayerStatType.Deaths && type != PlayerStatType.EnemyKills && type != PlayerStatType.BossKills) return;
            var id = profile.GetPlayerID();
            if (journal != null && id != character) return;
            if (journal == null && pendingCharacter != id) { pending = new Counters(); pendingCharacter = id; }
            var target = journal?.Totals ?? pending;
            var value = (long)amount;
            if (value <= 0 || value > 100000) return;
            switch (type)
            {
                case PlayerStatType.Deaths: target.Deaths = SafeIncrement(target.Deaths, value); break;
                case PlayerStatType.EnemyKills: target.Kills = SafeIncrement(target.Kills, value); break;
                case PlayerStatType.BossKills: target.Bosses = SafeIncrement(target.Bosses, value); break;
            }
            journalDirty = journal != null;
        }

        private static long SafeIncrement(long current, long amount) => Math.Min(Counters.MaximumValue, checked(current + amount));

        private static void AddPending(Counters target, Counters addition)
        {
            target.Deaths = SafeIncrement(target.Deaths, addition.Deaths);
            target.Kills = SafeIncrement(target.Kills, addition.Kills);
            target.Bosses = SafeIncrement(target.Bosses, addition.Bosses);
        }

        private Dictionary<long, string> OnlinePlayers()
        {
            var result = new Dictionary<long, string>();
            foreach (var peer in network.GetPeers())
                if (peer.IsReady() && peer.m_playerID != 0) result[peer.m_playerID] = peer.m_playerName;
            if (!network.IsDedicated() && Player.m_localPlayer)
                result[Player.m_localPlayer.GetPlayerID()] = Player.m_localPlayer.GetPlayerName();
            return result;
        }

        private List<BoardRow> BuildRows()
        {
            var online = OnlinePlayers();
            return database.Players.Values.OrderByDescending(r => online.ContainsKey(r.Id)).ThenByDescending(r => r.LastSeenUtc).Take(500)
                .Select(r => new BoardRow { Name = r.Name, Online = online.ContainsKey(r.Id), Reporting = reporting.TryGetValue(r.Id, out var reportTime) && Time.realtimeSinceStartup - reportTime < 10,
                    Seconds = r.Seconds, Deaths = r.Totals.Deaths, Kills = r.Totals.Kills, Bosses = r.Totals.Bosses }).ToList();
        }

        private void SetRows(List<BoardRow> snapshot)
        {
            rows.Clear(); rows.AddRange(snapshot); SortRows(); lastBoard = Time.realtimeSinceStartup;
        }

        private void SortRows()
        {
            rows.Sort((a, b) =>
            {
                int order;
                switch (sort)
                {
                    case 0: return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                    case 1: order = b.Online.CompareTo(a.Online); break;
                    case 2: order = b.Seconds.CompareTo(a.Seconds); break;
                    case 3: order = b.Kills.CompareTo(a.Kills); break;
                    case 4: order = b.Bosses.CompareTo(a.Bosses); break;
                    default: order = b.Deaths.CompareTo(a.Deaths); break;
                }
                return order != 0 ? order : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            page = Math.Max(0, Math.Min(page, Math.Max(0, (rows.Count - 1) / 12)));
        }

        private bool InputBlocked() => (Chat.instance && Chat.instance.HasFocus()) || global::Console.IsVisible() || TextInput.IsVisible() ||
            InventoryGui.IsVisible() || Menu.IsVisible() || StoreGui.IsVisible() || Minimap.IsOpen() ||
            (TextViewer.instance && TextViewer.instance.IsVisible());

        private void UpdateKeys()
        {
            if (network.IsDedicated()) return;
            if (!Player.m_localPlayer) { visible = false; return; }
            if (InputBlocked()) return;
            if (Input.GetKeyDown(toggle.Value))
            {
                visible = !visible; nextTick = 0;
                if (visible && !handshakeReceived) { helloAttempts = 0; nextHello = 0; }
            }
            if (!visible) return;
            if (Input.GetKeyDown(KeyCode.RightArrow)) { sort = (sort + 1) % 6; SortRows(); }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { sort = (sort + 5) % 6; SortRows(); }
            if (Input.GetKeyDown(KeyCode.PageDown)) { page++; SortRows(); }
            if (Input.GetKeyDown(KeyCode.PageUp)) { page--; SortRows(); }
        }

        private void OnGUI()
        {
            if (!visible || !network || network.IsDedicated() || InputBlocked() || Screen.width < 320 || Screen.height < 240) return;
            var previousMatrix = GUI.matrix;
            try
            {
                var scale = Mathf.Clamp(Mathf.Min((Screen.width - 24) / 820f, (Screen.height - 45) / 540f), 0.35f, 1f);
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
                GUILayout.BeginArea(new Rect((Screen.width / scale - 820) / 2, 30 / scale, 820, 540), GUI.skin.box);
                GUILayout.Label("SCOREBOARD  |  " + toggle.Value + " closes  |  Left/Right sorts  |  PageUp/PageDown changes page");
                GUILayout.Label(status + (Time.realtimeSinceStartup - lastBoard > 5 ? "  (waiting for an update)" : ""));
                GUILayout.Label("Sort: " + columns[sort] + "  |  Page " + (page + 1) + " / " + Math.Max(1, (rows.Count + 11) / 12));
                GUILayout.Space(8);
                DrawLine("Player", "Online", "Time", "Kills", "Bosses", "Deaths");
                foreach (var row in rows.Skip(page * 12).Take(12))
                    DrawLine(row.Name, row.Online ? (row.Reporting ? "Yes" : "Yes *") : "No",
                        (row.Seconds / 3600).ToString("0.0") + " h", row.Kills.ToString(), row.Bosses.ToString(), row.Deaths.ToString());
                GUILayout.Space(8);
                GUILayout.Label("Kills and bosses use Valheim's kill-credit statistics, not only the final hit. Time is connected time.");
                GUILayout.Label("* No recent client report. Historical values remain visible. Limited to 500 recent characters.");
                GUILayout.EndArea();
            }
            finally { GUI.matrix = previousMatrix; }
        }

        private static void DrawLine(params string[] values)
        {
            GUILayout.BeginHorizontal();
            for (var i = 0; i < values.Length; i++) GUILayout.Label(values[i], GUILayout.Width(i == 0 ? 220 : 95));
            GUILayout.EndHorizontal();
        }

        private void DisableServer(Exception error)
        {
            serverUnavailable = true;
            status = "Server statistics are unavailable. Check the BepInEx log.";
            Logger.LogError(error);
        }

        private void DisableClient(Exception error)
        {
            clientUnavailable = true;
            status = "Local statistic reporting is unavailable. Check the BepInEx log.";
            Logger.LogError(error);
        }

        private void DisableUnexpected(Exception error)
        {
            pluginFailed = true;
            status = "Scoreboard stopped after an unexpected error. Check the BepInEx log.";
            Logger.LogError(error);
        }

        private void LogStorageRetry(string component, Exception error)
        {
            if (Time.realtimeSinceStartup < nextStorageLog) return;
            nextStorageLog = Time.realtimeSinceStartup + 30;
            Logger.LogWarning($"Could not access the Scoreboard {component}; retrying: {error.Message}");
        }

        private static void Unregister(ZRpc rpc)
        {
            foreach (var method in new[] { Hello, Report, Ack, Request, Board }) rpc.Unregister(method);
        }

        internal void ResetSession()
        {
            try { if (journal != null && journalDirty) Storage.Save(journalPath, journal); }
            catch (Exception error) { Logger.LogError(error); }
            try { if (database != null) Storage.Save(databasePath, database); }
            catch (Exception error) { Logger.LogError(error); }
            foreach (var rpc in registered) Unregister(rpc);
            registered.Clear(); negotiated.Clear(); connectionStreams.Clear(); lastRequests.Clear(); lastReports.Clear(); reporting.Clear(); rows.Clear();
            database = null; journal = null; network = null; pluginFailed = false; serverUnavailable = false; clientUnavailable = false; journalDirty = false;
            pending = new Counters(); pendingCharacter = 0; handshakeReceived = false;
            world = 0; character = 0; visible = false; page = 0; nextTick = 0; nextDatabaseSave = 0; nextJournalSave = 0;
            nextStorageLog = 0; nextHello = 0; helloAttempts = 0;
            status = "Waiting for the Scoreboard server...";
        }

        private void OnDestroy()
        {
            ResetSession(); harmony?.UnpatchSelf(); Instance = null;
        }

        [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStat), new[] { typeof(PlayerStatType), typeof(float), typeof(bool) })]
        private static class StatPatch
        {
            private static bool Tracked(PlayerStatType type) => type == PlayerStatType.Deaths || type == PlayerStatType.EnemyKills || type == PlayerStatType.BossKills;
            private static void Prefix(PlayerProfile __instance, PlayerStatType __0, out float __state)
                => __state = Tracked(__0) ? __instance.GetStat(__0) : 0;
            private static void Postfix(PlayerProfile __instance, PlayerStatType __0, float __state)
            {
                if (Tracked(__0)) Instance?.Count(__instance, __0, __instance.GetStat(__0) - __state);
            }
        }

        [HarmonyPatch(typeof(ZNet), "OnDestroy", new Type[0])]
        private static class NetworkShutdownPatch
        {
            private static void Prefix() => Instance?.ResetSession();
        }
    }
}
