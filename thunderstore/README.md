# Valheim Anticheat

A server-first integrity guard for Valheim. It checks suspicious network messages, impossible item values, excessive combat values, and invalid health updates. It logs evidence about possible outside item imports for admin review.

## Install

Install this package and its BepInEx dependency on the dedicated server or the machine hosting the world. Restart and check `BepInEx/LogOutput.log` for `Server protocol guard loaded`.

The server checks work without client installation. To add carried-inventory and debug-mode reports, install the same version on every player's game. Enable `RequireClientInventoryScanner` only after all players have it.

## Checks

- Rejects forged sender IDs, malformed or excessive RPC and ZDO traffic, unauthorized ownership changes, and implausible object positions.
- Compares dropped items and container contents with server item prefabs, including quality, stack, durability, damage, and armor. Checks visible equipped-item quality.
- Flags suspicious inventory gains between sessions for review. It does not automatically ban or delete imported items.
- Rejects invalid or implausible health and damage values. A client installation can also report god mode, ghost mode, and debug flight.

## Limits and configuration

Valheim lets clients simulate combat, inventory, and movement. A modified client can forge plausible reports. Ordinary gear has no reliable world-of-origin marker, and a client that ignores damage while reporting normal health may escape detection. Review logs before punishing players.

Configuration is generated at `BepInEx/config/dev.monokaijs.valheim.anticheat.cfg`. See the [full README and settings](https://github.com/monokaijs/valheim-anticheat#readme) and [detection approach](https://github.com/monokaijs/valheim-anticheat/blob/main/docs/anti-cheat-approaches.md).
