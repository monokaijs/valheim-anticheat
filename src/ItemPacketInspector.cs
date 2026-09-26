using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ValheimAnticheat;

internal sealed class ItemInspection
{
    internal string? WorldValueIssue;
    internal string? CheatMarkedIssue;
    internal string? EquipmentIssue;
}

internal static class ItemPacketInspector
{
    private static readonly HashSet<int> IndexedItemDataKeys = MakeIndexedKeys();

    internal static void Inspect(
        ZDOID id, ZDO? existing, ZNetPeer peer, ZPackage serializedZdo,
        GuardSettings settings, ItemInspection result)
    {
        ZPackage data = new ZPackage(serializedZdo.GetArray());
        int flags = data.ReadUShort();
        int zdoPrefabHash = data.ReadInt();
        if ((flags & 4096) != 0) data.ReadVector3();
        if ((flags & 255) == 0) return;
        if ((flags & 1) != 0)
        {
            data.ReadByte();
            data.ReadZDOID();
        }

        if ((flags & 2) != 0) Skip(data, p => p.ReadSingle());
        if ((flags & 4) != 0) Skip(data, p => p.ReadVector3());
        if ((flags & 8) != 0) Skip(data, p => p.ReadQuaternion());

        Dictionary<int, int>? ints = id == peer.m_characterID ? new Dictionary<int, int>() : null;
        if ((flags & 16) != 0)
        {
            int count = Count(data);
            for (int i = 0; i < count; i++)
            {
                int key = data.ReadInt();
                int value = data.ReadInt();
                if (ints != null) ints[key] = value;
            }
        }
        if ((flags & 32) != 0) Skip(data, p => p.ReadLong());
        if ((flags & 64) != 0) Skip(data, p => p.ReadString());

        if ((flags & 128) != 0)
        {
            int count = Count(data);
            for (int i = 0; i < count; i++)
            {
                int key = data.ReadInt();
                byte[] bytes = data.ReadByteArray();
                if (bytes == null || SameAsExisting(existing, key, bytes)) continue;
                if (key == ZDOVars.s_items)
                    InspectInventory(bytes, "container " + id, settings, result);
                else if (key == ZDOVars.s_itemData || IndexedItemDataKeys.Contains(key))
                    InspectItemData(bytes, zdoPrefabHash, id, settings, result);
            }
        }

        if (data.GetPos() != data.Size())
            throw new InvalidDataException("trailing data in serialized ZDO");
        if (ints != null)
            InspectEquipped(ints, id, settings, result);
    }

    internal static void InspectInventory(byte[] bytes, string location, GuardSettings settings, ItemInspection result)
    {
        ZPackage pkg = new ZPackage(bytes);
        int version = pkg.ReadInt();
        if (version < 108) return;
        int count = pkg.ReadUShort();
        if (count > 4096) throw new InvalidDataException("inventory item count exceeds 4096");
        for (int i = 0; i < count; i++)
        {
            var (prefabHash, item) = ItemDrop.ItemData.Load(pkg, (Version.Item)version);
            CheckItem(item, prefabHash, location, settings, result);
        }
        if (pkg.GetPos() != pkg.Size())
            throw new InvalidDataException("trailing inventory data");
    }

    private static void InspectItemData(
        byte[] bytes, int zdoPrefabHash, ZDOID id, GuardSettings settings, ItemInspection result)
    {
        if (bytes.Length < 2) return;
        ZPackage pkg = new ZPackage(bytes);
        int version = pkg.ReadByte();
        if (version < 107) return;
        var (prefabHash, item) = ItemDrop.ItemData.Load(pkg, (Version.Item)version);
        CheckItem(item, prefabHash != 0 ? prefabHash : zdoPrefabHash, "world object " + id, settings, result);
        if (pkg.GetPos() != pkg.Size())
            throw new InvalidDataException("trailing item data");
    }

    private static void CheckItem(
        ItemDrop.ItemData item, int prefabHash, string location,
        GuardSettings settings, ItemInspection result)
    {
        ItemDrop? prefab = FindItem(prefabHash);
        ItemDrop.ItemData.SharedData? shared = prefab?.m_itemData?.m_shared;
        string name = prefab != null ? prefab.name : "hash=" + prefabHash;

        float maxDurability = 0;
        if (shared != null)
        {
            item.m_shared = shared;
            try { maxDurability = item.GetMaxDurability(); }
            catch { /* A custom prefab may not support this calculation. */ }
        }

        string? issue = ItemValueRules.Check(
            item.m_quality, item.m_stack, item.m_durability, item.m_variant, item.m_worldLevel,
            shared?.m_maxQuality ?? 0, shared?.m_maxStackSize ?? 0, maxDurability,
            shared?.m_variants ?? 0,
            settings.MaxAbsoluteItemQuality.Value, settings.MaxWorldLevel.Value);

        if (issue == null && shared != null)
        {
            try
            {
                if (item.IsWeapon())
                    issue = ItemValueRules.CheckComputed(item.GetDamage().GetTotalDamage(),
                        settings.MaxItemDamage.Value, "damage");
                if (issue == null && item.IsEquipable())
                    issue = ItemValueRules.CheckComputed(item.GetArmor(),
                        settings.MaxItemArmor.Value, "armor");
            }
            catch { /* A custom prefab may not support these calculations. */ }
        }

        if (issue != null && result.WorldValueIssue == null)
            result.WorldValueIssue = location + " item " + name + ": " + issue;
        if (item.m_cheated && result.CheatMarkedIssue == null)
            result.CheatMarkedIssue = location + " item " + name + " carries the cheated marker";
    }

    private static void InspectEquipped(
        Dictionary<int, int> ints, ZDOID id, GuardSettings settings, ItemInspection result)
    {
        CheckEquipped(ints, ZDOVars.s_rightItem, ZDOVars.s_rightItemQuality, "right hand", id, settings, result);
        CheckEquipped(ints, ZDOVars.s_leftItem, ZDOVars.s_leftItemQuality, "left hand", id, settings, result);
        CheckEquipped(ints, ZDOVars.s_rightBackItem, ZDOVars.s_rightBackItemQuality, "right back", id, settings, result);
        CheckEquipped(ints, ZDOVars.s_leftBackItem, ZDOVars.s_leftBackItemQuality, "left back", id, settings, result);
        CheckEquipped(ints, ZDOVars.s_shoulderItem, ZDOVars.s_shoulderItemQuality, "shoulder", id, settings, result);
    }

    private static void CheckEquipped(
        Dictionary<int, int> ints, int itemKey, int qualityKey, string slot,
        ZDOID id, GuardSettings settings, ItemInspection result)
    {
        if (result.EquipmentIssue != null || !ints.TryGetValue(itemKey, out int hash) || hash == 0 ||
            !ints.TryGetValue(qualityKey, out int quality)) return;

        ItemDrop? prefab = FindItem(hash);
        int max = prefab?.m_itemData?.m_shared?.m_maxQuality ?? 0;
        if (quality >= 1 && quality <= Math.Max(1, settings.MaxAbsoluteItemQuality.Value) &&
            (max <= 0 || quality <= max)) return;
        string name = prefab != null ? prefab.name : "hash=" + hash;
        result.EquipmentIssue = "player " + id + " advertises " + slot + " " + name +
            " quality " + quality + " (prefab maximum " + max + ")";
    }

    private static ItemDrop? FindItem(int hash)
    {
        if (hash == 0 || ObjectDB.instance == null) return null;
        GameObject prefab = ObjectDB.instance.GetItemPrefab(hash);
        return prefab != null ? prefab.GetComponent<ItemDrop>() : null;
    }

    private static bool SameAsExisting(ZDO? zdo, int key, byte[] value)
    {
        if (zdo == null) return false;
        byte[] old = zdo.GetByteArray(key, null);
        if (old == null || old.Length != value.Length) return false;
        for (int i = 0; i < old.Length; i++)
            if (old[i] != value[i]) return false;
        return true;
    }

    private static int Count(ZPackage pkg)
    {
        int count = pkg.ReadNumItems();
        if (count < 0 || count > 4096) throw new InvalidDataException("invalid ZDO field count " + count);
        return count;
    }

    private static void Skip<T>(ZPackage pkg, Func<ZPackage, T> read)
    {
        int count = Count(pkg);
        for (int i = 0; i < count; i++)
        {
            pkg.ReadInt();
            read(pkg);
        }
    }

    private static HashSet<int> MakeIndexedKeys()
    {
        var keys = new HashSet<int>();
        for (int i = 0; i < 64; i++)
            keys.Add(StringExtensionMethods.GetStableHashCode(i + "_itemData"));
        return keys;
    }
}
