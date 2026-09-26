using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace ValheimAnticheat;

[BepInPlugin("dev.monokaijs.valheim.anticheat", "Valheim Anticheat", "0.0.1")]
public sealed class AnticheatPlugin : BaseUnityPlugin
{
    internal static AnticheatPlugin? Instance { get; private set; }
    internal GuardSettings Settings { get; private set; } = null!;

    private Harmony? _harmony;
    private readonly Dictionary<ZRpc, PeerState> _states = new Dictionary<ZRpc, PeerState>();
    private DateTime _nextCleanup;
    private ZRpc? _clientHelloRpc;
    private string? _clientPendingNonce;
    private byte _clientObservedModes;
    private InventoryOriginTracker _inventoryOrigins = null!;

    private void Awake()
    {
        Instance = this;
        Settings = new GuardSettings(Config);
        _inventoryOrigins = new InventoryOriginTracker(
            Path.Combine(Paths.ConfigPath, "ValheimAnticheat", "inventories"));
        _harmony = new Harmony("dev.monokaijs.valheim.anticheat");
        _harmony.PatchAll(typeof(AnticheatPlugin).Assembly);
        Logger.LogInfo("Server protocol guard loaded. Enforce=" + Settings.Enforce.Value);
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        _states.Clear();
        Instance = null;
    }

    private void Update()
    {
        ZNet? net = ZNet.instance;
        if (net == null) return;
        if (!net.IsServer())
        {
            UpdateClientScanner(net);
            return;
        }
        if (_states.Count == 0) return;

        DateTime now = DateTime.UtcNow;
        if (now < _nextCleanup)
            return;
        _nextCleanup = now.AddSeconds(2);

        List<ZRpc> stale = new List<ZRpc>();
        List<ZNetPeer> kick = new List<ZNetPeer>();
        foreach (KeyValuePair<ZRpc, PeerState> entry in _states)
        {
            ZNetPeer? peer = FindPeer(net, entry.Key);
            if (peer == null)
                stale.Add(entry.Key);
            else if (entry.Value.PendingKick)
                kick.Add(peer);
            else
                UpdateServerScanner(peer, entry.Value, now);
        }

        foreach (ZRpc rpc in stale)
            _states.Remove(rpc);
        foreach (ZNetPeer peer in kick)
        {
            Logger.LogWarning("Disconnecting " + Describe(peer) + " after protocol violation");
            net.Disconnect(peer);
            _states.Remove(peer.m_rpc);
        }
    }

    internal void RegisterConnection(ZNet net, ZNetPeer peer)
    {
        peer.m_rpc.Register<ZPackage>("AC_ItemHello", ReceiveItemHello);
        peer.m_rpc.Register<ZPackage>("AC_ItemScanRequest", ReceiveItemScanRequest);
        peer.m_rpc.Register<ZPackage>("AC_ItemReport", ReceiveItemReport);
        if (net.IsServer()) GetState(peer);
        else
        {
            _clientHelloRpc = null;
            _clientPendingNonce = null;
            _clientObservedModes = 0;
        }
    }

    private void UpdateClientScanner(ZNet net)
    {
        if (!Settings.EnableClientInventoryScanner.Value || Player.m_localPlayer == null) return;
        ZRpc? rpc = net.GetServerRPC();
        if (rpc == null || !rpc.IsConnected()) return;

        if (!ReferenceEquals(_clientHelloRpc, rpc))
        {
            _clientHelloRpc = rpc;
            _clientObservedModes = 0;
            rpc.Invoke("AC_ItemHello", new ZPackage());
        }

        Player player = Player.m_localPlayer;
        if (player.InGodMode()) _clientObservedModes |= 1;
        if (player.InGhostMode()) _clientObservedModes |= 2;
        if (player.InDebugFlyMode()) _clientObservedModes |= 4;

        if (_clientPendingNonce == null) return;
        try
        {
            ZPackage inventory = new ZPackage();
            player.GetInventory().Save(inventory);
            ZPackage report = new ZPackage();
            report.Write(_clientPendingNonce);
            report.Write(inventory.GetArray());
            report.Write(_clientObservedModes);
            rpc.Invoke("AC_ItemReport", report);
            _clientPendingNonce = null;
            _clientObservedModes = 0;
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Could not serialize local inventory for scanner: " + ex.GetType().Name);
        }
    }

    private void UpdateServerScanner(ZNetPeer peer, PeerState state, DateTime now)
    {
        if (peer.m_uid == 0 || peer.m_characterID.IsNone()) return;
        if (state.ReadyAt == default) state.ReadyAt = now;

        if (!state.ScannerAvailable)
        {
            if (Settings.RequireClientInventoryScanner.Value &&
                now - state.ReadyAt > TimeSpan.FromSeconds(Math.Max(5, Settings.ClientScannerJoinTimeoutSeconds.Value)))
                Violation(peer, "client inventory scanner did not connect");
            return;
        }

        if (state.PendingNonce != null)
        {
            if (now - state.ScanRequestedAt > TimeSpan.FromSeconds(Math.Max(5, Settings.ClientScannerReplyTimeoutSeconds.Value)))
            {
                state.PendingNonce = null;
                state.NextScanAt = now.AddSeconds(Math.Max(5, Settings.ClientScannerIntervalSeconds.Value));
                if (Settings.RequireClientInventoryScanner.Value)
                    Violation(peer, "client inventory scanner did not answer");
                else
                    Logger.LogWarning("Client inventory scanner timed out for " + Describe(peer));
            }
            return;
        }

        if (now < state.NextScanAt || !peer.m_rpc.IsConnected()) return;
        state.PendingNonce = Guid.NewGuid().ToString("N");
        state.ScanRequestedAt = now;
        ZPackage request = new ZPackage();
        request.Write(state.PendingNonce);
        peer.m_rpc.Invoke("AC_ItemScanRequest", request);
    }

    private static void ReceiveItemHello(ZRpc rpc, ZPackage package)
    {
        AnticheatPlugin? guard = Instance;
        if (guard == null || !IsActiveServer()) return;
        ZNetPeer? peer = FindPeer(ZNet.instance, rpc);
        if (peer == null) return;
        PeerState state = guard.GetState(peer);
        if (state.ScannerAvailable) return;
        state.ScannerAvailable = true;
        state.NextScanAt = DateTime.UtcNow;
        guard.Logger.LogInfo("Client inventory scanner available for " + Describe(peer));
    }

    private static void ReceiveItemScanRequest(ZRpc rpc, ZPackage package)
    {
        if (Instance == null || ZNet.instance == null || ZNet.instance.IsServer() ||
            !ReferenceEquals(ZNet.instance.GetServerRPC(), rpc) || package == null || package.Size() > 128)
            return;
        try
        {
            string nonce = package.ReadString();
            if (nonce.Length == 32 && package.GetPos() == package.Size())
                Instance._clientPendingNonce = nonce;
        }
        catch { /* Ignore malformed scanner requests. */ }
    }

    private static void ReceiveItemReport(ZRpc rpc, ZPackage package)
    {
        AnticheatPlugin? guard = Instance;
        if (guard == null || !IsActiveServer()) return;
        ZNetPeer? peer = FindPeer(ZNet.instance, rpc);
        if (peer == null) return;
        PeerState state = guard.GetState(peer);
        if (package == null || package.Size() > 524288)
        {
            guard.Violation(peer, "oversized client inventory report");
            return;
        }

        byte[] inventory;
        byte modes = 0;
        try
        {
            string nonce = package.ReadString();
            inventory = package.ReadByteArray();
            if (package.GetPos() < package.Size())
                modes = package.ReadByte();
            if (nonce != state.PendingNonce || package.GetPos() != package.Size())
            {
                guard.Violation(peer, "unexpected client inventory report");
                return;
            }
            if ((modes & ~7) != 0)
            {
                guard.Violation(peer, "invalid client mode flags");
                return;
            }
        }
        catch (Exception ex)
        {
            guard.Violation(peer, "unreadable client inventory report: " + ex.GetType().Name);
            return;
        }

        state.PendingNonce = null;
        state.NextScanAt = DateTime.UtcNow.AddSeconds(Math.Max(5, guard.Settings.ClientScannerIntervalSeconds.Value));
        if (modes != 0) guard.ReportedModeFinding(peer, modes);
        bool validInventoryForLedger = true;
        if (guard.Settings.InspectItemValues.Value)
        {
            try
            {
                var result = new ItemInspection();
                ItemPacketInspector.InspectInventory(inventory, "reported inventory", guard.Settings, result);
                if (result.WorldValueIssue != null)
                {
                    validInventoryForLedger = false;
                    guard.ItemFinding(peer, result.WorldValueIssue,
                        guard.Settings.KickForReportedInventoryValues.Value,
                        guard.Settings.KickForReportedInventoryValues.Value);
                }
                if (result.CheatMarkedIssue != null && guard.Settings.ReportCheatMarkedItems.Value)
                    guard.ItemFinding(peer, result.CheatMarkedIssue, false, false);
            }
            catch (Exception ex)
            {
                guard.Violation(peer, "invalid client inventory report: " + ex.GetType().Name);
                return;
            }
        }
        if (guard.Settings.TrackInventoryBetweenSessions.Value && validInventoryForLedger && peer.m_playerID != 0)
        {
            try
            {
                string account = peer.m_socket.GetHostName();
                if (!string.IsNullOrWhiteSpace(account))
                {
                    string? finding = guard._inventoryOrigins.CompareAndStore(
                        account, peer.m_playerID, inventory, !state.InventoryBaselineChecked);
                    state.InventoryBaselineChecked = true;
                    if (finding != null)
                        guard.Logger.LogWarning("Possible outside inventory " + Describe(peer) + ": " + finding);
                }
            }
            catch (Exception ex)
            {
                guard.Logger.LogWarning("Could not compare inventory history for " + Describe(peer) +
                    ": " + ex.GetType().Name);
            }
        }
    }

    internal static bool IsActiveServer()
    {
        return Instance != null && ZNet.instance != null && ZNet.instance.IsServer();
    }

    internal static ZNetPeer? FindPeer(ZNet net, ZRpc rpc)
    {
        foreach (ZNetPeer peer in net.GetPeers())
            if (ReferenceEquals(peer.m_rpc, rpc))
                return peer;
        return null;
    }

    internal bool Record(ZNetPeer peer, int rpcs, int zdos, int bytes)
    {
        PeerState state = GetState(peer);
        if (state.PendingKick)
            return false;
        if (state.Traffic.Record(rpcs, zdos, bytes,
                Settings.MaxRoutedPerSecond.Value,
                Settings.MaxZdoUpdatesPerSecond.Value,
                Settings.MaxBytesPerSecond.Value))
            return true;

        DateTime now = DateTime.UtcNow;
        if (now - state.LastRateStrike >= TimeSpan.FromSeconds(1))
        {
            if (now - state.FirstRateStrike > TimeSpan.FromSeconds(30))
            {
                state.FirstRateStrike = now;
                state.RateStrikes = 0;
            }
            state.LastRateStrike = now;
            state.RateStrikes++;
            Logger.LogWarning("Rate limit: " + Describe(peer) + " (strike " + state.RateStrikes + ")");
            if (state.RateStrikes >= Math.Max(1, Settings.RateStrikesToKick.Value) && Settings.Enforce.Value)
                state.PendingKick = true;
        }
        return !Settings.Enforce.Value;
    }

    internal bool Violation(ZNetPeer? peer, string reason)
    {
        Logger.LogWarning("Protocol violation " + Describe(peer) + ": " + reason);
        if (peer != null && Settings.Enforce.Value)
            GetState(peer).PendingKick = true;
        return !Settings.Enforce.Value;
    }

    internal bool ItemFinding(ZNetPeer peer, string reason, bool reject, bool kick)
    {
        PeerState state = GetState(peer);
        DateTime now = DateTime.UtcNow;
        if (reason != state.LastItemReason || now >= state.NextItemLog)
        {
            Logger.LogWarning("Suspicious item " + Describe(peer) + ": " + reason);
            state.LastItemReason = reason;
            state.NextItemLog = now.AddSeconds(10);
        }
        if (kick && Settings.Enforce.Value)
            state.PendingKick = true;
        return !Settings.Enforce.Value || !reject;
    }

    internal bool HealthFinding(ZNetPeer peer, string reason)
    {
        PeerState state = GetState(peer);
        DateTime now = DateTime.UtcNow;
        if (reason != state.LastHealthReason || now >= state.NextHealthLog)
        {
            Logger.LogWarning("Invalid player health " + Describe(peer) + ": " + reason);
            state.LastHealthReason = reason;
            state.NextHealthLog = now.AddSeconds(10);
        }
        if (Settings.Enforce.Value && Settings.KickForInvalidPlayerHealth.Value)
            state.PendingKick = true;
        return !Settings.Enforce.Value;
    }

    private void ReportedModeFinding(ZNetPeer peer, byte modes)
    {
        PeerState state = GetState(peer);
        string description = ((modes & 1) != 0 ? "god mode " : "") +
                             ((modes & 2) != 0 ? "ghost mode " : "") +
                             ((modes & 4) != 0 ? "debug flight" : "");
        DateTime now = DateTime.UtcNow;
        if (modes != state.LastReportedModes || now >= state.NextModeLog)
        {
            Logger.LogWarning("Client reported invulnerability mode " + Describe(peer) + ": " + description.Trim());
            state.LastReportedModes = modes;
            state.NextModeLog = now.AddSeconds(10);
        }
        if (Settings.Enforce.Value && Settings.KickForReportedInvulnerability.Value)
            state.PendingKick = true;
    }

    private PeerState GetState(ZNetPeer peer)
    {
        if (!_states.TryGetValue(peer.m_rpc, out PeerState state))
        {
            state = new PeerState();
            _states.Add(peer.m_rpc, state);
        }
        return state;
    }

    private static string Describe(ZNetPeer? peer)
    {
        if (peer == null) return "unknown peer";
        string name = (peer.m_playerName ?? "<joining>").Replace('\r', ' ').Replace('\n', ' ');
        if (name.Length > 80) name = name.Substring(0, 80);
        string account = (peer.m_socket?.GetHostName() ?? "<unknown>").Replace('\r', ' ').Replace('\n', ' ');
        if (account.Length > 100) account = account.Substring(0, 100);
        return "peer=" + peer.m_uid + " account=" + account + " name=" + name;
    }

    private sealed class PeerState
    {
        internal readonly TrafficWindow Traffic = new TrafficWindow();
        internal DateTime FirstRateStrike;
        internal DateTime LastRateStrike;
        internal int RateStrikes;
        internal bool PendingKick;
        internal string? LastItemReason;
        internal DateTime NextItemLog;
        internal DateTime ReadyAt;
        internal DateTime NextScanAt;
        internal DateTime ScanRequestedAt;
        internal string? PendingNonce;
        internal bool ScannerAvailable;
        internal string? LastHealthReason;
        internal DateTime NextHealthLog;
        internal byte LastReportedModes;
        internal DateTime NextModeLog;
        internal bool InventoryBaselineChecked;
    }
}

[HarmonyPatch(typeof(ZNet), "OnNewConnection")]
internal static class ConnectionPatch
{
    private static void Postfix(ZNet __instance, ZNetPeer __0)
    {
        AnticheatPlugin.Instance?.RegisterConnection(__instance, __0);
    }
}

[HarmonyPatch(typeof(ZRoutedRpc), "RPC_RoutedRPC")]
internal static class RoutedRpcPatch
{
    private static bool Prefix(ZRpc __0, ZPackage __1)
    {
        if (!AnticheatPlugin.IsActiveServer()) return true;
        AnticheatPlugin guard = AnticheatPlugin.Instance!;
        ZNetPeer? peer = AnticheatPlugin.FindPeer(ZNet.instance, __0);
        if (peer == null) return guard.Violation(null, "routed RPC from an unknown connection");
        if (__1 == null || __1.Size() > Math.Max(1024, guard.Settings.MaxRoutedBytes.Value))
            return guard.Violation(peer, "routed RPC exceeds packet limit");

        try
        {
            ZPackage copy = new ZPackage(__1.GetArray());
            ZRoutedRpc.RoutedRPCData data = new ZRoutedRpc.RoutedRPCData();
            data.Deserialize(copy);
            if (data.m_senderPeerID != peer.m_uid || peer.m_uid == 0)
                return guard.Violation(peer, "forged routed RPC sender " + data.m_senderPeerID);
            if (data.m_parameters == null || copy.GetPos() != copy.Size())
                return guard.Violation(peer, "malformed routed RPC payload");
            if (guard.Settings.InspectDamageRpcs.Value &&
                data.m_methodHash == DamagePacketInspector.DamageHash && !data.m_targetZDO.IsNone())
            {
                string? issue = DamagePacketInspector.Inspect(
                    data.m_parameters, guard.Settings.MaxRoutedDamage.Value);
                if (issue != null) return guard.Violation(peer, issue);
            }
        }
        catch (Exception ex)
        {
            return guard.Violation(peer, "unreadable routed RPC: " + ex.GetType().Name);
        }

        return guard.Record(peer, 1, 0, __1.Size());
    }
}

[HarmonyPatch(typeof(ZDOMan), "RPC_ZDOData")]
internal static class ZdoDataPatch
{
    private static bool Prefix(ZDOMan __instance, ZRpc __0, ZPackage __1)
    {
        if (!AnticheatPlugin.IsActiveServer()) return true;
        AnticheatPlugin guard = AnticheatPlugin.Instance!;
        ZNetPeer? peer = AnticheatPlugin.FindPeer(ZNet.instance, __0);
        if (peer == null) return guard.Violation(null, "ZDO data from an unknown connection");
        if (__1 == null || __1.Size() > Math.Max(1024, guard.Settings.MaxZdoBytes.Value))
            return guard.Violation(peer, "ZDO packet exceeds packet limit");

        int updates;
        ItemInspection itemInspection;
        string? issue;
        string? healthIssue;
        try
        {
            issue = WorldPacketValidator.ValidateZdoData(
                __instance, peer, __1, guard.Settings, out updates, out itemInspection, out healthIssue);
        }
        catch (Exception ex)
        {
            return guard.Violation(peer, "unreadable ZDO packet: " + ex.GetType().Name);
        }
        if (issue != null) return guard.Violation(peer, issue);
        if (healthIssue != null && !guard.HealthFinding(peer, healthIssue)) return false;
        if (itemInspection.WorldValueIssue != null &&
            !guard.ItemFinding(peer, itemInspection.WorldValueIssue,
                guard.Settings.RejectSuspiciousWorldItems.Value, false)) return false;
        if (itemInspection.CheatMarkedIssue != null && guard.Settings.ReportCheatMarkedItems.Value &&
            !guard.ItemFinding(peer, itemInspection.CheatMarkedIssue,
                guard.Settings.RejectCheatMarkedItems.Value, false)) return false;
        if (itemInspection.EquipmentIssue != null &&
            !guard.ItemFinding(peer, itemInspection.EquipmentIssue,
                guard.Settings.KickForSuspiciousEquipment.Value,
                guard.Settings.KickForSuspiciousEquipment.Value)) return false;
        return guard.Record(peer, 0, updates, __1.Size());
    }
}

[HarmonyPatch(typeof(ZDOMan), "RPC_DestroyZDO")]
internal static class DestroyZdoPatch
{
    private static bool Prefix(ZDOMan __instance, long __0, ZPackage __1)
    {
        if (!AnticheatPlugin.IsActiveServer() || __0 == ZNet.GetUID()) return true;
        AnticheatPlugin guard = AnticheatPlugin.Instance!;
        ZNetPeer? peer = ZNet.instance.GetPeer(__0);
        if (peer == null) return guard.Violation(null, "destroy request from an unknown sender");
        if (__1 == null || __1.Size() > Math.Max(1024, guard.Settings.MaxRoutedBytes.Value))
            return guard.Violation(peer, "destroy request exceeds packet limit");

        string? issue;
        try
        {
            issue = WorldPacketValidator.ValidateDestroy(__instance, peer, __1, guard.Settings);
        }
        catch (Exception ex)
        {
            return guard.Violation(peer, "unreadable destroy request: " + ex.GetType().Name);
        }
        return issue == null || guard.Violation(peer, issue);
    }
}
