using System;

namespace ValheimAnticheat;

internal static class HealthValueRules
{
    internal static string? Check(float? health, float? maxHealth, float ceiling)
    {
        float limit = Math.Max(1f, ceiling);
        if (maxHealth.HasValue && (!Finite(maxHealth.Value) || maxHealth.Value < 1f || maxHealth.Value > limit))
            return "invalid maximum health " + maxHealth.Value;
        if (health.HasValue && (!Finite(health.Value) || health.Value < 0f || health.Value > limit))
            return "invalid health " + health.Value;
        if (health.HasValue && maxHealth.HasValue && health.Value > maxHealth.Value + 5f)
            return "health " + health.Value + " exceeds maximum " + maxHealth.Value;
        return null;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
