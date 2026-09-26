using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ValheimAnticheat;

// This is an audit signal, not proof of where an item was obtained. The input
// comes from the client's inventory report and can be forged or simply stale
// after a crash.
internal sealed class InventoryOriginTracker
{
    private readonly string _directory;

    internal InventoryOriginTracker(string directory) => _directory = directory;

    internal string? CompareAndStore(string account, long characterId, byte[] inventory, bool compare)
    {
        if (string.IsNullOrWhiteSpace(account) || characterId == 0) return null;
        Snapshot current = Snapshot.Parse(inventory);
        string path = Path.Combine(_directory, KeyFor(account, characterId) + ".dat");
        Snapshot? previous = compare ? Snapshot.Load(path) : null;
        current.Store(path);
        return previous == null ? null : DescribeAdditions(previous, current);
    }

    private static string KeyFor(string account, long characterId)
    {
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(account + "\n" + characterId));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private static string? DescribeAdditions(Snapshot previous, Snapshot current)
    {
        var additions = new List<(ItemKey Key, int Count)>();
        foreach (KeyValuePair<ItemKey, int> entry in current.Items)
        {
            previous.Items.TryGetValue(entry.Key, out int oldCount);
            if (entry.Value > oldCount)
                additions.Add((entry.Key, entry.Value - oldCount));
        }
        if (additions.Count == 0) return null;
        additions.Sort((a, b) => b.Count.CompareTo(a.Count));
        return "inventory gained items between sessions: " +
               string.Join(", ", additions.Take(6).Select(x => "+" + x.Count + " " + x.Key.Describe())) +
               (additions.Count > 6 ? " (and " + (additions.Count - 6) + " more types)" : "");
    }

    private readonly struct ItemKey : IEquatable<ItemKey>
    {
        internal readonly int Prefab;
        internal readonly int Quality;
        internal readonly int Variant;
        internal readonly int WorldLevel;
        internal readonly long Crafter;

        internal ItemKey(int prefab, ItemDrop.ItemData item)
            : this(prefab, item.m_quality, item.m_variant, item.m_worldLevel, item.m_crafterID) { }

        internal ItemKey(int prefab, int quality, int variant, int worldLevel, long crafter)
        {
            Prefab = prefab;
            Quality = quality;
            Variant = variant;
            WorldLevel = worldLevel;
            Crafter = crafter;
        }

        public bool Equals(ItemKey other) => Prefab == other.Prefab && Quality == other.Quality &&
            Variant == other.Variant && WorldLevel == other.WorldLevel && Crafter == other.Crafter;

        public override bool Equals(object? obj) => obj is ItemKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Prefab;
                hash = hash * 31 + Quality;
                hash = hash * 31 + Variant;
                hash = hash * 31 + WorldLevel;
                return hash * 31 + Crafter.GetHashCode();
            }
        }

        internal string Describe()
        {
            GameObject? prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(Prefab) : null;
            string name = prefab != null ? prefab.name : "hash=" + Prefab;
            return name + (Quality > 1 ? " q" + Quality : "") +
                   (Crafter != 0 ? " crafter=" + Crafter : "");
        }
    }

    private sealed class Snapshot
    {
        internal readonly Dictionary<ItemKey, int> Items = new Dictionary<ItemKey, int>();

        internal static Snapshot Parse(byte[] bytes)
        {
            ZPackage pkg = new ZPackage(bytes);
            int version = pkg.ReadInt();
            if (version < 108) throw new InvalidDataException("unsupported inventory version " + version);
            int count = pkg.ReadUShort();
            if (count > 4096) throw new InvalidDataException("inventory item count exceeds 4096");
            var result = new Snapshot();
            for (int i = 0; i < count; i++)
            {
                var (prefab, item) = ItemDrop.ItemData.Load(pkg, (Version.Item)version);
                if (item.m_stack < 1 || item.m_stack > 1000000)
                    throw new InvalidDataException("invalid item stack in inventory report");
                var key = new ItemKey(prefab, item);
                result.Items.TryGetValue(key, out int oldCount);
                result.Items[key] = checked(oldCount + item.m_stack);
            }
            if (pkg.GetPos() != pkg.Size()) throw new InvalidDataException("trailing inventory data");
            return result;
        }

        internal static Snapshot? Load(string path)
        {
            if (!File.Exists(path)) return null;
            using var reader = new BinaryReader(File.OpenRead(path));
            if (reader.ReadInt32() != 1) throw new InvalidDataException("unknown inventory snapshot version");
            int count = reader.ReadInt32();
            if (count < 0 || count > 4096) throw new InvalidDataException("invalid inventory snapshot count");
            var result = new Snapshot();
            for (int i = 0; i < count; i++)
            {
                var key = new ItemKey(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
                    reader.ReadInt32(), reader.ReadInt64());
                int quantity = reader.ReadInt32();
                if (quantity < 1) throw new InvalidDataException("invalid inventory snapshot quantity");
                result.Items.Add(key, quantity);
            }
            if (reader.BaseStream.Position != reader.BaseStream.Length)
                throw new InvalidDataException("trailing inventory snapshot data");
            return result;
        }

        internal void Store(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(1);
                writer.Write(Items.Count);
                foreach (KeyValuePair<ItemKey, int> entry in Items)
                {
                    writer.Write(entry.Key.Prefab);
                    writer.Write(entry.Key.Quality);
                    writer.Write(entry.Key.Variant);
                    writer.Write(entry.Key.WorldLevel);
                    writer.Write(entry.Key.Crafter);
                    writer.Write(entry.Value);
                }
            }
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(temporary, stream.ToArray());
            if (File.Exists(path))
                File.Replace(temporary, path, null);
            else
                File.Move(temporary, path);
        }
    }
}
