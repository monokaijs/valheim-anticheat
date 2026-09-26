using System;

namespace ValheimAnticheat;

internal static class DamagePacketInspector
{
    internal static readonly int DamageHash = StringExtensionMethods.GetStableHashCode("RPC_Damage");

    internal static string? Inspect(ZPackage parameters, float maximum)
    {
        ZPackage copy = new ZPackage(parameters.GetArray());
        HitData hit = new HitData();
        hit.Deserialize(ref copy);
        if (copy.GetPos() != copy.Size()) return "trailing data in damage RPC";

        float limit = Math.Max(1f, maximum);
        HitData.DamageTypes damage = hit.m_damage;
        float[] components = {
            damage.m_damage, damage.m_blunt, damage.m_slash, damage.m_pierce,
            damage.m_chop, damage.m_pickaxe, damage.m_fire, damage.m_frost,
            damage.m_lightning, damage.m_poison, damage.m_spirit, damage.m_nonPlayer
        };
        float total = 0f;
        foreach (float value in components)
        {
            if (!Finite(value) || value < 0f || value > limit)
                return "invalid damage component " + value;
            total += value;
        }
        if (!Finite(total) || total > limit)
            return "total damage " + total + " exceeds " + limit;
        if (!Finite(hit.m_pushForce) || Math.Abs(hit.m_pushForce) > 10000f)
            return "invalid damage push force " + hit.m_pushForce;
        if (!Finite(hit.m_staggerMultiplier) || Math.Abs(hit.m_staggerMultiplier) > 10000f)
            return "invalid stagger multiplier " + hit.m_staggerMultiplier;
        return null;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
