# Anti-cheat approach review

The most useful checks distinguish data the server sees directly from claims made by the player's client. Valheim gives clients control over their character inventory and much combat state. A ban requires evidence stronger than one unusual packet or one changed inventory.

| Approach | Value | Status here |
| --- | --- | --- |
| Validate network sender, object ownership, packet size, world position and destroy requests | Server-observed integrity checks | Implemented |
| Validate raw damage RPC values before routing | Stops impossible hit payloads from being accepted | Implemented |
| Inspect world items and equipped quality against server prefabs | Catches extreme forged items visible to the server | Implemented |
| Inspect Valheim's cheated-item marker | Flags many items made with cheats, including some brought from elsewhere | Implemented; report by default |
| Compare a character's last inventory report with its first report after reconnecting | Flags possible offline imports | Implemented; audit only |
| Keep character inventory on the server | Prevents ordinary outside inventory from becoming the accepted character inventory | Separate server-character mod needed |
| Check item crafter ID or whether the item has a legitimate drop/crafting route | Can identify some spawned equipment | Future audit idea; client can forge crafter ID and mods add item sources |
| Validate structure health against server prefabs | Detects some indestructible buildings | Future server-observed check |

The [Valheim Enforcer project](https://github.com/MidnightsFX/valheim_enforcer) demonstrates server-held character progression, server-side packet checks, item-origin heuristics, and audit logs. Its [character progression guide](https://github.com/MidnightsFX/valheim_enforcer/blob/master/docs/character-progression.md) describes first-join migration and crash/reconnect tradeoffs. The [Dyrr project](https://github.com/Ezomic/valheim-dyrr) compares inventory reports between sessions but deliberately logs differences without punishing players because crashes and local save rollbacks can produce the same pattern. [ServerGuard](https://github.com/yesu0725/Valheim-ServerGuard) uses Valheim's cheated-item marker and documents a way that marker can be bypassed; it is useful evidence, not a complete provenance system. [Iron Gate acknowledged items being incorrectly marked as cheated in patch 1.0.15](https://www.valheimgame.com/news/patch-1-0-15/), another reason to avoid automatic bans from that marker.

For this mod, an inventory difference is logged with the player's connection identity so an admin can inspect it alongside world item findings and gameplay history. The snapshot begins on the first report after installation; it cannot identify imports already present in that first inventory. The client can also forge its report. Automatic permanent bans from this signal would punish some legitimate crash victims and still miss determined cheaters.

If an admin confirms misconduct, Valheim's [dedicated-server guide](https://www.valheimgame.com/support/a-guide-to-dedicated-servers/) documents its `ban` command and `bannedlist.txt`. Use the Platform User ID from the server log or F2 player list when editing the ban list.
