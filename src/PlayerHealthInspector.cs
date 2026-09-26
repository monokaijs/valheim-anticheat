using System;

namespace ValheimAnticheat;

internal static class PlayerHealthInspector
{
    internal static string? Inspect(ZDOID id, ZDOID characterId, ZDO? existing,
        ZPackage serializedZdo, float ceiling)
    {
        if (id != characterId) return null;

        ZPackage data = new ZPackage(serializedZdo.GetArray());
        int flags = data.ReadUShort();
        data.ReadInt(); // prefab hash
        if ((flags & 4096) != 0) data.ReadVector3();
        if ((flags & 1) != 0)
        {
            data.ReadByte();
            data.ReadZDOID();
        }

        float? health = null;
        float? maxHealth = null;
        if ((flags & 2) != 0)
        {
            int count = data.ReadNumItems();
            if (count < 0 || count > 4096)
                throw new InvalidOperationException("invalid player float field count " + count);
            for (int i = 0; i < count; i++)
            {
                int key = data.ReadInt();
                float value = data.ReadSingle();
                if (key == ZDOVars.s_health) health = value;
                else if (key == ZDOVars.s_maxHealth) maxHealth = value;
            }
        }

        // ZDO updates can omit unchanged fields; use the server's last known maximum.
        if (!maxHealth.HasValue && health.HasValue && existing != null)
        {
            float oldMax = existing.GetFloat(ZDOVars.s_maxHealth, float.NaN);
            if (!float.IsNaN(oldMax)) maxHealth = oldMax;
        }

        string? issue = HealthValueRules.Check(health, maxHealth, ceiling);
        return issue == null ? null : "player " + id + " sent " + issue;
    }
}
