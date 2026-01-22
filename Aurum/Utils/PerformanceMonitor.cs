using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Aurum.Utils;

/// <summary>
/// Utility for monitoring performance metrics (execution time)
/// </summary>
public class PerformanceMonitor
{
    private readonly ConcurrentDictionary<string, List<long>> _measurements = new();
    private const int MaxSamplesPerKey = 100;

    /// <summary>
    /// Measure the execution time of an action
    /// </summary>
    public void Measure(string key, Action action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            action();
        }
        finally
        {
            sw.Stop();
            Record(key, sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Measure the execution time of a function
    /// </summary>
    public T Measure<T>(string key, Func<T> func)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return func();
        }
        finally
        {
            sw.Stop();
            Record(key, sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Manually record a measurement
    /// </summary>
    public void Record(string key, long elapsedMilliseconds)
    {
        _measurements.AddOrUpdate(key, 
            _ => new List<long> { elapsedMilliseconds },
            (_, list) => 
            {
                lock (list)
                {
                    if (list.Count >= MaxSamplesPerKey)
                    {
                        list.RemoveAt(0);
                    }
                    list.Add(elapsedMilliseconds);
                    return list;
                }
            });
    }

    /// <summary>
    /// Get statistics for a specific key
    /// </summary>
    public PerformanceStats? GetStats(string key)
    {
        if (!_measurements.TryGetValue(key, out var list))
            return null;

        lock (list)
        {
            if (list.Count == 0) return null;

            return new PerformanceStats
            {
                Key = key,
                Count = list.Count,
                AverageMs = list.Average(),
                MinMs = list.Min(),
                MaxMs = list.Max(),
                LastMs = list.Last()
            };
        }
    }

    /// <summary>
    /// Get statistics for all keys
    /// </summary>
    public List<PerformanceStats> GetAllStats()
    {
        return _measurements.Keys
            .Select(GetStats)
            .Where(s => s != null)
            .OrderByDescending(s => s!.AverageMs)
            .ToList()!;
    }
    
    /// <summary>
    /// Clear all collected metrics
    /// </summary>
    public void Clear()
    {
        _measurements.Clear();
    }
}

public class PerformanceStats
{
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
    public double AverageMs { get; set; }
    public long MinMs { get; set; }
    public long MaxMs { get; set; }
    public long LastMs { get; set; }

    public override string ToString()
    {
        return $"{Key}: Avg={AverageMs:F2}ms, Max={MaxMs}ms, Last={LastMs}ms (n={Count})";
    }
}
