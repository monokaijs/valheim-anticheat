using System;

namespace ValheimAnticheat;

internal sealed class TrafficWindow
{
    private readonly long[] _rpcs = new long[10];
    private readonly long[] _zdos = new long[10];
    private readonly long[] _bytes = new long[10];
    private readonly Func<long> _clock;
    private long _latestSlice = -1;

    internal TrafficWindow(Func<long>? clock = null)
    {
        _clock = clock ?? (() => DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond);
    }

    internal bool Record(int rpcCount, int zdoCount, int bytes, int maxRpcs, int maxZdos, int maxBytes)
    {
        long slice = _clock() / 100;
        if (_latestSlice < 0 || slice < _latestSlice || slice - _latestSlice >= 10)
        {
            Array.Clear(_rpcs, 0, _rpcs.Length);
            Array.Clear(_zdos, 0, _zdos.Length);
            Array.Clear(_bytes, 0, _bytes.Length);
        }
        else
        {
            for (long old = _latestSlice + 1; old <= slice; old++)
            {
                int slot = (int)(old % 10);
                _rpcs[slot] = 0;
                _zdos[slot] = 0;
                _bytes[slot] = 0;
            }
        }

        _latestSlice = slice;
        int current = (int)(slice % 10);
        _rpcs[current] += rpcCount;
        _zdos[current] += zdoCount;
        _bytes[current] += bytes;

        long totalRpcs = 0, totalZdos = 0, totalBytes = 0;
        for (int i = 0; i < 10; i++)
        {
            totalRpcs += _rpcs[i];
            totalZdos += _zdos[i];
            totalBytes += _bytes[i];
        }

        return totalRpcs <= Math.Max(1, maxRpcs)
            && totalZdos <= Math.Max(1, maxZdos)
            && totalBytes <= Math.Max(1024, maxBytes);
    }
}
