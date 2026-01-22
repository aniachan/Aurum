using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Aurum.Services;

namespace Aurum.Utils;

/// <summary>
/// Utility for profiling memory usage
/// </summary>
public class MemoryProfiler
{
    private readonly CacheService _cacheService;
    
    public MemoryProfiler(CacheService cacheService)
    {
        _cacheService = cacheService;
    }

    /// <summary>
    /// Gets current memory usage statistics
    /// </summary>
    public MemoryStats GetMemoryStats()
    {
        var process = Process.GetCurrentProcess();
        
        return new MemoryStats
        {
            WorkingSet = process.WorkingSet64,
            PrivateMemory = process.PrivateMemorySize64,
            ManagedHeapSize = GC.GetTotalMemory(false),
            GCTotalCollections = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2),
            CacheEntryCount = _cacheService.GetStats().TotalEntries
        };
    }

    /// <summary>
    /// Logs current memory usage to the provided logger
    /// </summary>
    public void LogMemoryUsage(Dalamud.Plugin.Services.IPluginLog log, string label = "Current")
    {
        var stats = GetMemoryStats();
        log.Info($"[{label}] Memory Usage:");
        log.Info($"  Working Set: {FormatBytes(stats.WorkingSet)}");
        log.Info($"  Private Memory: {FormatBytes(stats.PrivateMemory)}");
        log.Info($"  Managed Heap: {FormatBytes(stats.ManagedHeapSize)}");
        log.Info($"  GC Collections: {stats.GCTotalCollections}");
        log.Info($"  Cache Entries: {stats.CacheEntryCount}");
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = (decimal)bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number = number / 1024;
            counter++;
        }
        return string.Format("{0:n1}{1}", number, suffixes[counter]);
    }
}

public class MemoryStats
{
    public long WorkingSet { get; set; }
    public long PrivateMemory { get; set; }
    public long ManagedHeapSize { get; set; }
    public int GCTotalCollections { get; set; }
    public int CacheEntryCount { get; set; }
}
