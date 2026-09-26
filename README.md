<p align="center"><img src="assets/icon.png" alt="Valheim Anticheat shield icon" width="180"></p>

# Valheim Anticheat

**A server-first integrity guard for Valheim.** This BepInEx 5 plugin validates client network messages before Valheim accepts them, checks implausible combat and item values, and gives admins evidence about suspicious inventory changes. It runs on a dedicated server or the machine hosting a world. Installing the same DLL on clients enables additional inventory and debug-mode reports.

> [Install from Thunderstore](https://thunderstore.io/c/valheim/p/Creaton/ValheimAnticheat/) · [Download the latest GitHub release](https://github.com/monokaijs/valheim-anticheat/releases/latest) · [Review the detection approach](docs/anti-cheat-approaches.md)

## Install

1. Install BepInEx 5 for your Valheim server.
2. Download the latest release ZIP and copy `ValheimAnticheat/ValheimAnticheat.dll` into `BepInEx/plugins/ValheimAnticheat/` on the server.
3. Restart the server and check `BepInEx/LogOutput.log` for `Server protocol guard loaded`.
4. For carried-inventory and debug-mode reports, install the same DLL on each player's game. Set `RequireClientInventoryScanner = true` only after every player has it.

The server-only checks work without client installation. [BepInEx documents plugin loading](https://docs.bepinex.dev/articles/user_guide/installation/index.html); [Valheim documents dedicated server setup](https://www.valheimgame.com/support/a-guide-to-dedicated-servers/).

Thunderstore users can install [Creaton-ValheimAnticheat](https://thunderstore.io/c/valheim/p/Creaton/ValheimAnticheat/) and its BepInEx dependency through a mod manager.

## What it enforces

- Rejects routed RPCs that claim a sender ID different from the connection's ID.
- Rejects malformed, oversized, or excessive routed RPC and ZDO traffic.
- Rejects client ZDO creation using another peer's object ID, implausible coordinates, or locations far from the player's reported position.
- Rejects unauthorized ZDO ownership changes and destroy requests for objects owned by another peer.
- Rejects large jumps by existing world objects while allowing a player's own ZDO to travel through portals.
- Checks incoming dropped items and container contents against the server's item prefabs: quality, stack, durability, variant, world level, computed damage, and armor. It also reports Valheim's cheated-item marker.
- Checks visible quality advertised for equipped hand, back, and shoulder items on the player ZDO.
- Requests a periodic inventory report from clients running the same plugin, allowing it to flag carried weapons and armor before they are dropped. The server can require the client scanner and optionally kick on reported violations.
- Keeps the most recent reported inventory per account and character on the server and logs items gained between sessions. This flags possible outside imports for review; it never removes items or bans players.
- Checks each player's synchronized health and maximum health for non-finite, negative, excessive, or inconsistent values. By default it rejects the update and disconnects that player. The health ceiling is configurable for modded servers.
- When installed on clients, observes Valheim's god mode, ghost mode, and debug flight flags between scans and reports them to the server. By default a report disconnects the player; servers that permit admin debug modes can disable that action.
- Validates damage RPCs before the server routes them, rejecting non-finite, negative, or extreme raw damage and non-finite or extreme push values.
- Logs the connection identity, player name, and reason; disconnects after a protocol violation. Rate breaches require three strikes in 30 seconds by default.

## Limits of detection

Valheim still lets clients simulate combat, movement, inventory, and nearby objects. A modified client can falsify its position, plausible health, or optional inventory and mode reports. In particular:

- A player can ignore damage while sending plausible health. Blocks, dodges, and PvP rules also allow hits without health loss, so the plugin does not auto-punish that pattern.
- The server sees world drops, containers, and some equipped values, but cannot independently read all stats for items kept only in a character's inventory. Client-side prefab edits and mod-specific item metadata may be invisible to these checks.
- Ordinary items have no trustworthy world-of-origin marker. The between-session inventory audit flags additions for review but cannot prove an import or justify an automatic ban.

The plugin does not inspect players' computers or issue automatic permanent bans.

## Build

Install the .NET SDK and BepInEx 5 in your Valheim installation, then run:

```sh
VALHEIM_PATH="/path/to/Valheim" dotnet build -c Release
dotnet run --project tests/TrafficWindowTests.csproj -c Release
dotnet run --project tests/ItemPacketSmoke/ItemPacketSmoke.csproj -c Release
```

On macOS, the project defaults to `~/Library/Application Support/Steam/steamapps/common/Valheim`. `VALHEIM_PATH` must point to the installation root containing `BepInEx/core`. The build uses that installation's `assembly_valheim.dll` and Unity assemblies. Rebuild after a Valheim update, then check the BepInEx log for Harmony patch failures before enabling enforcement again. The compiled DLL is `bin/Release/netstandard2.1/ValheimAnticheat.dll`.

## Release a version

In GitHub, open **Actions → Release → Run workflow** on `main`. Select `current` to publish the version already in the source, or `patch`, `minor`, or `major` to bump it. The workflow updates the assembly, plugin, and Thunderstore versions together; builds and tests against Valheim dedicated-server assemblies; commits and tags the version; and publishes a ZIP and icon to GitHub Releases. It then packages the released DLL for Thunderstore and publishes it to the Valheim community using the `THUNDERSTORE_TOKEN` repository secret. Set that secret to a service account token for the team named in `thunderstore.toml` before running a release. No game assemblies or publishing tokens are stored in this repository.

To publish an existing GitHub release to Thunderstore, run **Actions → Publish Thunderstore → Run workflow** with its version number without the `v` prefix. This uses the DLL already attached to that release and does not create another GitHub release. Thunderstore requires a 256×256 icon and a manifest; the package is assembled using `thunderstore.toml` and `assets/thunderstore-icon.png`.

## Configure

BepInEx generates `BepInEx/config/dev.monokaijs.valheim.anticheat.cfg` after first launch. Restart after changing settings.

| Setting | Default | Purpose |
| --- | ---: | --- |
| `Enforce` | `true` | Reject and disconnect. `false` only logs decisions. |
| `MaxRoutedPacketBytes` | `262144` | Individual routed RPC cap. |
| `MaxZdoPacketBytes` | `262144` | Individual ZDO sync cap. |
| `MaxDestroyCount` | `128` | Objects in one destroy request. |
| `MaxZdoUpdatesPerPacket` | `256` | Object updates in one sync packet. |
| `MaxRoutedRpcsPerSecond` | `300` | Rolling per-peer RPC limit. |
| `MaxZdoUpdatesPerSecond` | `500` | Rolling per-peer object update limit. |
| `MaxTrafficBytesPerSecond` | `1048576` | Combined rolling per-peer traffic limit. |
| `MaxObjectDistanceFromPlayer` | `512` | Horizontal distance for creation or ownership claims; `0` disables the check. |
| `MaxObjectMoveDistancePerUpdate` | `256` | Maximum jump by an existing object other than the player's ZDO; `0` disables the check. |
| `MaxWorldCoordinate` | `20000` | Maximum absolute X, Y, and Z of a client ZDO. |
| `RateStrikesToKick` | `3` | Rate breaches within 30 seconds before disconnect. |
| `InspectItemValues` | `true` | Inspect incoming world items and visible equipment quality. |
| `RejectSuspiciousWorldItems` | `true` | Block updates carrying invalid world item values; does not kick. |
| `ReportCheatMarkedItems` | `true` | Log Valheim's cheated-item marker. |
| `RejectCheatMarkedItems` | `false` | Block cheat-marked items entering world objects. |
| `KickForSuspiciousEquipment` | `false` | Kick for impossible visible equipped quality. Otherwise log. |
| `MaxAbsoluteItemQuality` | `100` | Quality ceiling even for unknown prefabs. Known prefabs use their own maximum too. |
| `MaxItemWorldLevel` | `100` | Maximum item world level. |
| `MaxItemDamage` | `2000` | Maximum total damage using the server's item prefab. |
| `MaxItemArmor` | `1000` | Maximum armor using the server's item prefab. |
| `EnableClientInventoryScanner` | `true` | Answer inventory scan requests when the DLL is installed on a client. |
| `RequireClientInventoryScanner` | `false` | Require the DLL on every client and an answer to each scan. |
| `KickForReportedInventoryValues` | `false` | Kick for suspect carried items reported by a client scanner. |
| `ClientScannerIntervalSeconds` | `15` | Time between inventory scan requests. |
| `ClientScannerJoinTimeoutSeconds` | `30` | Time to wait for a required client scanner after the player spawns. |
| `ClientScannerReplyTimeoutSeconds` | `15` | Time to wait for a required inventory report. |
| `TrackInventoryBetweenSessions` | `true` | Store the last reported inventory and log additions seen on the next connection. Requires the client scanner. |
| `InspectPlayerHealth` | `true` | Inspect player health in client ZDO updates. |
| `MaxPlayerHealth` | `1000` | Hard ceiling for health and maximum health. Raise for health overhaul mods. |
| `KickForInvalidPlayerHealth` | `true` | Disconnect on invalid health. With `Enforce = true`, invalid health updates are rejected even when this is `false`. |
| `KickForReportedInvulnerability` | `true` | Disconnect if a client scanner reports god mode, ghost mode, or debug flight. |
| `InspectDamageRpcs` | `true` | Check damage values in client-origin combat RPCs. |
| `MaxRoutedDamage` | `10000` | Maximum raw damage sum in one routed hit. Raise for combat overhaul mods. |

For a heavily modded server, start with `Enforce = false` and inspect the log while players build, sail, fight, and travel. Raise the specific limit that blocks valid traffic, then enable enforcement. A protocol violation is treated as severe and disconnects immediately when enforcement is enabled. The reported position is client supplied, so proximity checks reduce accidental or casual abuse but cannot stop a custom client that forges both values.

Item value checks use the server's prefab definitions, which lets installed item mods set legitimate quality and stack limits. [Jötunn's item documentation](https://valheim-modding.github.io/Jotunn/tutorials/items.html) describes how mods add and alter item prefabs. Review item thresholds if the server uses gear overhaul mods. A flagged world item update is rejected by default without attributing the item's original creation to that peer; an old suspect item in a container may be relayed by an innocent owner. Visible equipment and optional inventory reports are logged by default. Set `RequireClientInventoryScanner = true` only after distributing the DLL to players; even then, a custom client can forge a clean report. Update client DLLs to this release to enable debug-mode reports; older clients can still answer inventory scans but do not send mode flags.

Between-session inventory snapshots live under `BepInEx/config/ValheimAnticheat/inventories/`. The first report for each account and character establishes a baseline. On later joins, the log lists additions by prefab, quality, and crafter ID. An unsaved gain shortly before a crash or a local save rollback can look like an import, so review these lines before taking action. Reports omit mod-specific extra inventories and can be falsified by a custom client. For a strict no-import server, use server-owned characters; see [the approach review](docs/anti-cheat-approaches.md).

## Compatibility

The plugin was built against the local Valheim `assembly_valheim.dll` with BepInEx 5 and Harmony. It patches `ZNet.OnNewConnection`, `ZRoutedRpc.RPC_RoutedRPC`, `ZDOMan.RPC_ZDOData`, and `ZDOMan.RPC_DestroyZDO`; game updates may change these private methods or the ZDO serialization format. Other mods that intentionally create distant ZDOs, change ownership rules, raise player health, or increase damage may need adjusted limits or may conflict with this guard.
