using System;
using System.IO;
using System.Runtime.Serialization;
using ValheimAnticheat;

static byte[] Item(int quality)
{
    var package = new ZPackage();
    package.Write(0);            // Durability times 100.
    package.Write((byte)0);      // Inventory X.
    package.Write((byte)0);      // Inventory Y.
    package.Write((byte)1);      // World level.
    package.Write((byte)4);      // Quality is present.
    package.Write((ushort)quality);
    package.Write((byte)0);      // Cheat marker for item format 109.
    return package.GetArray();
}

static ItemInspection InspectInventoryItem(int quality)
{
    var inventory = new ZPackage();
    inventory.Write(109);
    inventory.Write((ushort)1);
    foreach (byte value in Item(quality)) inventory.Write(value);

    var zdoData = new ZPackage();
    zdoData.Write((ushort)128);  // One or more byte-array fields.
    zdoData.Write(0);            // No prefab on the container in this smoke check.
    zdoData.WriteNumItems(1);
    zdoData.Write(ZDOVars.s_items);
    zdoData.Write(inventory.GetArray());

#pragma warning disable SYSLIB0050
    var peer = (ZNetPeer)FormatterServices.GetUninitializedObject(typeof(ZNetPeer));
#pragma warning restore SYSLIB0050
    var result = new ItemInspection();
    ItemPacketInspector.Inspect(new ZDOID(42, 1), null, peer, zdoData,
        new GuardSettings(), result);
    return result;
}

static ItemInspection InspectDrop(int quality)
{
    var itemData = new ZPackage();
    itemData.Write((byte)109);
    foreach (byte value in Item(quality)) itemData.Write(value);

    var zdoData = new ZPackage();
    zdoData.Write((ushort)128);
    zdoData.Write(0);
    zdoData.WriteNumItems(1);
    zdoData.Write(ZDOVars.s_itemData);
    zdoData.Write(itemData.GetArray());

#pragma warning disable SYSLIB0050
    var peer = (ZNetPeer)FormatterServices.GetUninitializedObject(typeof(ZNetPeer));
#pragma warning restore SYSLIB0050
    var result = new ItemInspection();
    ItemPacketInspector.Inspect(new ZDOID(42, 2), null, peer, zdoData,
        new GuardSettings(), result);
    return result;
}

static ItemInspection InspectVisibleQuality(int quality)
{
    var zdoData = new ZPackage();
    zdoData.Write((ushort)16);
    zdoData.Write(0);
    zdoData.WriteNumItems(2);
    zdoData.Write(ZDOVars.s_rightItem);
    zdoData.Write(12345);
    zdoData.Write(ZDOVars.s_rightItemQuality);
    zdoData.Write(quality);

#pragma warning disable SYSLIB0050
    var peer = (ZNetPeer)FormatterServices.GetUninitializedObject(typeof(ZNetPeer));
#pragma warning restore SYSLIB0050
    var id = new ZDOID(42, 3);
    peer.m_characterID = id;
    var result = new ItemInspection();
    ItemPacketInspector.Inspect(id, null, peer, zdoData,
        new GuardSettings(), result);
    return result;
}

if (InspectInventoryItem(4).WorldValueIssue != null)
    throw new Exception("Valid serialized item was flagged");
if (InspectInventoryItem(500).WorldValueIssue == null)
    throw new Exception("Impossible serialized quality was missed");
if (InspectDrop(500).WorldValueIssue == null)
    throw new Exception("Impossible dropped item quality was missed");
if (InspectVisibleQuality(500).EquipmentIssue == null)
    throw new Exception("Impossible visible equipment quality was missed");

System.Console.WriteLine("Serialized Valheim inventory checks passed");

static string? InspectHealth(float health, float maximum, bool matchingCharacter = true)
{
    var zdoData = new ZPackage();
    zdoData.Write((ushort)2);
    zdoData.Write(0);
    zdoData.WriteNumItems(2);
    zdoData.Write(ZDOVars.s_health);
    zdoData.Write(health);
    zdoData.Write(ZDOVars.s_maxHealth);
    zdoData.Write(maximum);
    var id = new ZDOID(42, 4);
    return PlayerHealthInspector.Inspect(id, matchingCharacter ? id : new ZDOID(42, 5),
        null, zdoData, 1000f);
}

if (InspectHealth(80, 100) != null)
    throw new Exception("Valid serialized health was flagged");
if (InspectHealth(120, 100) == null)
    throw new Exception("Health above maximum was missed");
if (InspectHealth(float.PositiveInfinity, 100) == null)
    throw new Exception("Infinite health was missed");
if (InspectHealth(120, 100, false) != null)
    throw new Exception("Another object's health was inspected");

System.Console.WriteLine("Serialized player health checks passed");

static string? InspectDamage(float damage)
{
    var hit = new HitData();
    hit.m_damage.m_blunt = damage;
    var payload = new ZPackage();
    hit.Serialize(ref payload);
    return DamagePacketInspector.Inspect(payload, 10000f);
}

if (InspectDamage(50) != null)
    throw new Exception("Valid damage RPC was flagged");
if (InspectDamage(float.NaN) == null)
    throw new Exception("NaN damage RPC was missed");
if (InspectDamage(-1) == null)
    throw new Exception("Negative damage RPC was missed");
if (InspectDamage(10001) == null)
    throw new Exception("Oversized damage RPC was missed");

System.Console.WriteLine("Serialized damage RPC checks passed");

static byte[] InventoryBytes(int count)
{
    var inventory = new ZPackage();
    inventory.Write(109);
    inventory.Write((ushort)count);
    for (int i = 0; i < count; i++)
        foreach (byte value in Item(4)) inventory.Write(value);
    return inventory.GetArray();
}

string ledgerDirectory = Path.Combine(Path.GetTempPath(), "ValheimAnticheatSmoke-" + Guid.NewGuid());
var tracker = new InventoryOriginTracker(ledgerDirectory);
try
{
    if (tracker.CompareAndStore("account-1", 1234, InventoryBytes(1), true) != null)
        throw new Exception("First inventory report was flagged");
    if (tracker.CompareAndStore("account-1", 1234, InventoryBytes(1), true) != null)
        throw new Exception("Unchanged inventory was flagged");
    if (tracker.CompareAndStore("account-1", 1234, InventoryBytes(2), true) == null)
        throw new Exception("Inventory gain between sessions was missed");
    if (tracker.CompareAndStore("account-1", 1234, InventoryBytes(2), true) != null)
        throw new Exception("Repeated inventory report was flagged");
}
finally
{
    if (Directory.Exists(ledgerDirectory)) Directory.Delete(ledgerDirectory, true);
}

System.Console.WriteLine("Between-session inventory checks passed");

namespace ValheimAnticheat
{
    internal sealed class Entry<T>
    {
        internal T ValueField;
        internal T Value => ValueField;
        internal Entry(T value) { ValueField = value; }
    }

    internal sealed class GuardSettings
    {
        internal readonly Entry<int> MaxAbsoluteItemQuality = new Entry<int>(100);
        internal readonly Entry<int> MaxWorldLevel = new Entry<int>(100);
        internal readonly Entry<float> MaxItemDamage = new Entry<float>(2000);
        internal readonly Entry<float> MaxItemArmor = new Entry<float>(1000);
    }
}
