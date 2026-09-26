using System;

namespace ValheimAnticheat;

internal static class ItemValueRules
{
    internal static string? Check(
        int quality, int stack, float durability, int variant, int worldLevel,
        int prefabMaxQuality, int prefabMaxStack, float prefabMaxDurability,
        int prefabVariants, int absoluteMaxQuality, int maxWorldLevel)
    {
        if (quality < 1 || quality > Math.Max(1, absoluteMaxQuality))
            return "quality " + quality + " exceeds absolute limit";
        if (prefabMaxQuality > 0 && quality > prefabMaxQuality)
            return "quality " + quality + " exceeds prefab maximum " + prefabMaxQuality;
        if (stack < 1 || (prefabMaxStack > 0 && stack > prefabMaxStack))
            return "stack " + stack + " exceeds prefab maximum " + prefabMaxStack;
        if (float.IsNaN(durability) || float.IsInfinity(durability) || durability < 0)
            return "invalid durability " + durability;
        if (prefabMaxDurability > 0 && durability > prefabMaxDurability * 1.25f + 10f)
            return "durability " + durability + " exceeds prefab maximum " + prefabMaxDurability;
        if (variant < 0 || (prefabVariants > 0 && variant >= prefabVariants))
            return "invalid variant " + variant;
        if (worldLevel < 0 || worldLevel > Math.Max(1, maxWorldLevel))
            return "world level " + worldLevel + " exceeds limit";
        return null;
    }

    internal static string? CheckComputed(float value, float maximum, string label)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > Math.Max(1f, maximum))
            return label + " " + value + " exceeds limit " + maximum;
        return null;
    }
}
