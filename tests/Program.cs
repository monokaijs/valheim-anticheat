using System;
using ValheimAnticheat;

long now = 0;
var window = new TrafficWindow(() => now);

void Check(bool actual, bool expected, string caseName)
{
    if (actual != expected) throw new Exception(caseName);
}

Check(window.Record(2, 0, 100, 3, 3, 1024), true, "initial traffic");
now = 900;
Check(window.Record(1, 0, 100, 3, 3, 1024), true, "traffic across slices");
Check(window.Record(1, 0, 100, 3, 3, 1024), false, "rate limit across slices");
now = 1000;
Check(window.Record(2, 0, 100, 3, 3, 1024), false, "only oldest slice expires");
now = 2000;
Check(window.Record(1, 0, 100, 3, 3, 1024), true, "full window expiry");
now = 4000;
Check(window.Record(0, 4, 100, 3, 3, 1024), false, "ZDO count limit");
now = 6000;
Check(window.Record(0, 0, 2048, 3, 3, 1024), false, "byte limit");
now = 8000;
Check(window.Record(1, 0, 100, 3, 3, 1024), true, "window recovers");

Console.WriteLine("Traffic window checks passed");

void CheckItem(string? actual, bool expectedIssue, string caseName)
{
    if ((actual != null) != expectedIssue) throw new Exception(caseName + ": " + actual);
}

CheckItem(ItemValueRules.Check(4, 1, 80, 0, 1, 4, 1, 100, 1, 100, 100), false,
    "valid upgraded weapon");
CheckItem(ItemValueRules.Check(500, 1, 80, 0, 1, 4, 1, 100, 1, 100, 100), true,
    "impossible quality");
CheckItem(ItemValueRules.Check(4, 20, 80, 0, 1, 4, 1, 100, 1, 100, 100), true,
    "oversized equipment stack");
CheckItem(ItemValueRules.Check(4, 1, 500, 0, 1, 4, 1, 100, 1, 100, 100), true,
    "impossible durability");
CheckItem(ItemValueRules.Check(4, 1, 80, 0, 1, 4, 1, 100, 1, 100, 100), false,
    "quality at prefab maximum");
CheckItem(ItemValueRules.CheckComputed(float.NaN, 2000, "damage"), true,
    "invalid computed damage");
CheckItem(ItemValueRules.CheckComputed(2100, 2000, "damage"), true,
    "extreme computed damage");

Console.WriteLine("Item value checks passed");

CheckItem(HealthValueRules.Check(100, 100, 1000), false, "ordinary health");
CheckItem(HealthValueRules.Check(0, 100, 1000), false, "dead character");
CheckItem(HealthValueRules.Check(150, null, 1000), false, "health before maximum is known");
CheckItem(HealthValueRules.Check(120, 100, 1000), true, "health above maximum");
CheckItem(HealthValueRules.Check(float.NaN, 100, 1000), true, "NaN health");
CheckItem(HealthValueRules.Check(float.PositiveInfinity, 100, 1000), true, "infinite health");
CheckItem(HealthValueRules.Check(-1, 100, 1000), true, "negative health");
CheckItem(HealthValueRules.Check(100, 10000, 1000), true, "excessive maximum health");

Console.WriteLine("Health value checks passed");
