using BepInEx.Configuration;

namespace ValheimAnticheat;

internal sealed class GuardSettings
{
    internal readonly ConfigEntry<bool> Enforce;
    internal readonly ConfigEntry<int> MaxRoutedBytes;
    internal readonly ConfigEntry<int> MaxZdoBytes;
    internal readonly ConfigEntry<int> MaxDestroyCount;
    internal readonly ConfigEntry<int> MaxZdoUpdates;
    internal readonly ConfigEntry<int> MaxRoutedPerSecond;
    internal readonly ConfigEntry<int> MaxZdoUpdatesPerSecond;
    internal readonly ConfigEntry<int> MaxBytesPerSecond;
    internal readonly ConfigEntry<float> MaxObjectDistance;
    internal readonly ConfigEntry<float> MaxObjectMoveDistance;
    internal readonly ConfigEntry<float> MaxWorldCoordinate;
    internal readonly ConfigEntry<int> RateStrikesToKick;
    internal readonly ConfigEntry<bool> InspectItemValues;
    internal readonly ConfigEntry<bool> RejectSuspiciousWorldItems;
    internal readonly ConfigEntry<bool> ReportCheatMarkedItems;
    internal readonly ConfigEntry<bool> RejectCheatMarkedItems;
    internal readonly ConfigEntry<bool> KickForSuspiciousEquipment;
    internal readonly ConfigEntry<int> MaxAbsoluteItemQuality;
    internal readonly ConfigEntry<int> MaxWorldLevel;
    internal readonly ConfigEntry<float> MaxItemDamage;
    internal readonly ConfigEntry<float> MaxItemArmor;
    internal readonly ConfigEntry<bool> EnableClientInventoryScanner;
    internal readonly ConfigEntry<bool> RequireClientInventoryScanner;
    internal readonly ConfigEntry<bool> KickForReportedInventoryValues;
    internal readonly ConfigEntry<int> ClientScannerIntervalSeconds;
    internal readonly ConfigEntry<int> ClientScannerJoinTimeoutSeconds;
    internal readonly ConfigEntry<int> ClientScannerReplyTimeoutSeconds;
    internal readonly ConfigEntry<bool> InspectPlayerHealth;
    internal readonly ConfigEntry<float> MaxPlayerHealth;
    internal readonly ConfigEntry<bool> KickForInvalidPlayerHealth;
    internal readonly ConfigEntry<bool> KickForReportedInvulnerability;
    internal readonly ConfigEntry<bool> InspectDamageRpcs;
    internal readonly ConfigEntry<float> MaxRoutedDamage;
    internal readonly ConfigEntry<bool> TrackInventoryBetweenSessions;

    internal GuardSettings(ConfigFile config)
    {
        Enforce = config.Bind("Enforcement", "Enforce", true,
            "Reject invalid traffic and disconnect offenders. Set false to log decisions while tuning thresholds.");
        MaxRoutedBytes = config.Bind("Limits", "MaxRoutedPacketBytes", 262144,
            "Largest accepted routed RPC packet in bytes.");
        MaxZdoBytes = config.Bind("Limits", "MaxZdoPacketBytes", 262144,
            "Largest accepted ZDO synchronization packet in bytes.");
        MaxDestroyCount = config.Bind("Limits", "MaxDestroyCount", 128,
            "Most ZDOs a client may destroy in one request.");
        MaxZdoUpdates = config.Bind("Limits", "MaxZdoUpdatesPerPacket", 256,
            "Most ZDO updates in one synchronization packet.");
        MaxRoutedPerSecond = config.Bind("Limits", "MaxRoutedRpcsPerSecond", 300,
            "Maximum routed RPCs from one peer in a rolling second before a rate strike.");
        MaxZdoUpdatesPerSecond = config.Bind("Limits", "MaxZdoUpdatesPerSecond", 500,
            "Maximum ZDO updates from one peer in a rolling second before a rate strike.");
        MaxBytesPerSecond = config.Bind("Limits", "MaxTrafficBytesPerSecond", 1048576,
            "Combined routed RPC and ZDO bytes from one peer in a rolling second.");
        MaxObjectDistance = config.Bind("World", "MaxObjectDistanceFromPlayer", 512f,
            "Maximum distance of a new ZDO or ownership claim from the peer's reported position. Set 0 to disable.");
        MaxObjectMoveDistance = config.Bind("World", "MaxObjectMoveDistancePerUpdate", 256f,
            "Maximum position jump for an existing ZDO other than the peer's character. Set 0 to disable.");
        MaxWorldCoordinate = config.Bind("World", "MaxWorldCoordinate", 20000f,
            "Maximum absolute coordinate for a client-supplied ZDO position.");
        RateStrikesToKick = config.Bind("Enforcement", "RateStrikesToKick", 3,
            "Rate limit breaches within 30 seconds before disconnection.");

        InspectItemValues = config.Bind("Items", "InspectItemValues", true,
            "Inspect item data in incoming world drops and containers, and visible equipped quality in player ZDOs.");
        RejectSuspiciousWorldItems = config.Bind("Items", "RejectSuspiciousWorldItems", true,
            "Reject client world updates containing invalid item quality, stack, durability, or extreme equipment stats. Does not kick.");
        ReportCheatMarkedItems = config.Bind("Items", "ReportCheatMarkedItems", true,
            "Log items carrying Valheim's built-in cheated marker.");
        RejectCheatMarkedItems = config.Bind("Items", "RejectCheatMarkedItems", false,
            "Reject world updates containing items marked cheated. Useful on servers that forbid admin-spawned or imported items.");
        KickForSuspiciousEquipment = config.Bind("Items", "KickForSuspiciousEquipment", false,
            "Disconnect peers that advertise impossible quality for equipped weapons or shoulder gear. Usually leave off until thresholds are tuned.");
        MaxAbsoluteItemQuality = config.Bind("Items", "MaxAbsoluteItemQuality", 100,
            "Hard quality ceiling even when the item prefab is unknown.");
        MaxWorldLevel = config.Bind("Items", "MaxItemWorldLevel", 100,
            "Largest accepted world level encoded on an item.");
        MaxItemDamage = config.Bind("Items", "MaxItemDamage", 2000f,
            "Largest computed total weapon damage accepted for an item using the server's prefab data.");
        MaxItemArmor = config.Bind("Items", "MaxItemArmor", 1000f,
            "Largest computed armor accepted for an item using the server's prefab data.");
        EnableClientInventoryScanner = config.Bind("Items", "EnableClientInventoryScanner", true,
            "When installed on a client, answer server inventory scan requests.");
        RequireClientInventoryScanner = config.Bind("Items", "RequireClientInventoryScanner", false,
            "Require this plugin on every client and disconnect peers that do not answer inventory scans.");
        KickForReportedInventoryValues = config.Bind("Items", "KickForReportedInventoryValues", false,
            "Disconnect when an optional client inventory scan reports suspicious values. Client reports can be falsified.");
        ClientScannerIntervalSeconds = config.Bind("Items", "ClientScannerIntervalSeconds", 15,
            "Time between inventory scan requests to a client running this plugin.");
        ClientScannerJoinTimeoutSeconds = config.Bind("Items", "ClientScannerJoinTimeoutSeconds", 30,
            "Seconds after a character appears to wait for the client scanner hello when required.");
        ClientScannerReplyTimeoutSeconds = config.Bind("Items", "ClientScannerReplyTimeoutSeconds", 15,
            "Seconds to wait for a requested inventory report when the scanner is required.");

        InspectPlayerHealth = config.Bind("Health", "InspectPlayerHealth", true,
            "Inspect player health and maximum health in client ZDO updates.");
        MaxPlayerHealth = config.Bind("Health", "MaxPlayerHealth", 1000f,
            "Hard ceiling for a player's synchronized health and maximum health. Raise for health overhaul mods.");
        KickForInvalidPlayerHealth = config.Bind("Health", "KickForInvalidPlayerHealth", true,
            "Disconnect a client that synchronizes non-finite, negative, over-maximum, or excessive player health.");
        KickForReportedInvulnerability = config.Bind("Health", "KickForReportedInvulnerability", true,
            "Disconnect a client scanner that reports god mode, ghost mode, or debug flight. Disable for servers that permit these modes.");
        InspectDamageRpcs = config.Bind("Combat", "InspectDamageRpcs", true,
            "Validate damage RPC values sent by clients before routing them to a target.");
        MaxRoutedDamage = config.Bind("Combat", "MaxRoutedDamage", 10000f,
            "Maximum sum of raw damage components in one client-origin damage RPC. Raise for combat overhaul mods.");
        TrackInventoryBetweenSessions = config.Bind("Items", "TrackInventoryBetweenSessions", true,
            "Log items added to a client's reported inventory between sessions. This is only an audit clue; crashes and save rollbacks can cause false alarms.");
    }
}
