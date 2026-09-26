using System;
using UnityEngine;

namespace ValheimAnticheat;

internal static class WorldPacketValidator
{
    internal static string? ValidateZdoData(
        ZDOMan man, ZNetPeer peer, ZPackage original, GuardSettings settings,
        out int updates, out ItemInspection itemInspection, out string? healthIssue)
    {
        updates = 0;
        itemInspection = new ItemInspection();
        healthIssue = null;
        ZPackage pkg = new ZPackage(original.GetArray());
        int invalidated = pkg.ReadInt();
        if (invalidated < 0 || invalidated > Math.Max(1, settings.MaxZdoUpdates.Value))
            return "invalid ZDO invalidation count " + invalidated;
        for (int i = 0; i < invalidated; i++)
            pkg.ReadZDOID();

        int maxUpdates = Math.Max(1, settings.MaxZdoUpdates.Value);
        while (true)
        {
            ZDOID id = pkg.ReadZDOID();
            if (id.IsNone()) break;
            if (++updates > maxUpdates)
                return "too many ZDO updates in one packet";

            ushort ownerRevision = pkg.ReadUShort();
            uint dataRevision = pkg.ReadUInt();
            long claimedOwner = pkg.ReadLong();
            Vector3 position = pkg.ReadVector3();
            ZPackage serializedZdo = pkg.ReadPackage();

            if (!InWorld(position, settings.MaxWorldCoordinate.Value))
                return "out of bounds ZDO position for " + id;
            ZDO? existing = man.GetZDO(id);
            if (existing == null)
            {
                if (id.UserID != peer.m_uid)
                    return "ZDO creator spoof for " + id;
                if (claimedOwner != 0 && claimedOwner != peer.m_uid && claimedOwner != ZNet.GetUID())
                    return "ZDO owner spoof for " + id;
                if (!NearPeer(peer, position, settings.MaxObjectDistance.Value))
                    return "remote ZDO creation for " + id;
            }
            else
            {
                // Vanilla discards this packet when both revisions are stale. Do not
                // punish a peer for a race with a newer server ownership change.
                if (dataRevision <= existing.DataRevision && ownerRevision <= existing.OwnerRevision)
                    continue;

                if (dataRevision > existing.DataRevision && id != peer.m_characterID &&
                    !WithinMove(existing.GetPosition(), position, settings.MaxObjectMoveDistance.Value))
                    return "large ZDO position jump for " + id;

                long currentOwner = existing.GetOwner();
                if (currentOwner == peer.m_uid)
                {
                    // A client may release to nobody, or hand control back to the host.
                    if (claimedOwner != peer.m_uid && claimedOwner != 0 && claimedOwner != ZNet.GetUID())
                        return "ZDO owner spoof for " + id;
                }
                else if (claimedOwner != peer.m_uid)
                    return "unowned ZDO update for " + id;
                if (currentOwner != peer.m_uid &&
                    !NearPeer(peer, existing.GetPosition(), settings.MaxObjectDistance.Value))
                    return "remote ZDO ownership claim for " + id;
            }

            if (settings.InspectItemValues.Value && (existing == null || dataRevision > existing.DataRevision))
                ItemPacketInspector.Inspect(id, existing, peer, serializedZdo, settings, itemInspection);
            if (settings.InspectPlayerHealth.Value && (existing == null || dataRevision > existing.DataRevision) &&
                healthIssue == null)
                healthIssue = PlayerHealthInspector.Inspect(
                    id, peer.m_characterID, existing, serializedZdo, settings.MaxPlayerHealth.Value);
        }

        return pkg.GetPos() == pkg.Size() ? null : "trailing bytes in ZDO packet";
    }

    internal static string? ValidateDestroy(ZDOMan man, ZNetPeer peer, ZPackage original, GuardSettings settings)
    {
        ZPackage pkg = new ZPackage(original.GetArray());
        int count = pkg.ReadInt();
        if (count < 0 || count > Math.Max(1, settings.MaxDestroyCount.Value))
            return "invalid destroy count " + count;
        for (int i = 0; i < count; i++)
        {
            ZDOID id = pkg.ReadZDOID();
            ZDO? zdo = man.GetZDO(id);
            if (zdo != null && zdo.GetOwner() != peer.m_uid)
                return "attempted destruction of another owner's ZDO " + id;
        }
        return pkg.GetPos() == pkg.Size() ? null : "trailing bytes in destroy request";
    }

    private static bool InWorld(Vector3 value, float max)
    {
        max = Math.Max(1000f, max);
        return Finite(value.x) && Finite(value.y) && Finite(value.z)
            && Math.Abs(value.x) <= max && Math.Abs(value.y) <= max && Math.Abs(value.z) <= max;
    }

    private static bool NearPeer(ZNetPeer peer, Vector3 position, float max)
    {
        if (max <= 0 || peer.m_characterID.IsNone()) return true;
        Vector3 reference = peer.GetRefPos();
        if (!Finite(reference.x) || !Finite(reference.y) || !Finite(reference.z)) return false;
        float deltaX = reference.x - position.x;
        float deltaZ = reference.z - position.z;
        return deltaX * deltaX + deltaZ * deltaZ <= max * max;
    }

    private static bool WithinMove(Vector3 previous, Vector3 current, float max)
    {
        if (max <= 0 || !Finite(previous.x) || !Finite(previous.y) || !Finite(previous.z)) return true;
        float dx = previous.x - current.x;
        float dy = previous.y - current.y;
        float dz = previous.z - current.z;
        return dx * dx + dy * dy + dz * dz <= max * max;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
