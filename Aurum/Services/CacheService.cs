using System;
using System.Collections.Generic;
using System.Linq;
using Aurum.Models;

namespace Aurum.Services;

public class CacheService
{
    private readonly Dictionary<string, CacheEntry> cache = new();
    private readonly object lockObject = new();
    private readonly ICacheConfig config;

    private long _hits;
    private long _misses;

    public CacheService(ICacheConfig config)
    {
        this.config = config;
    }

    public virtual bool TryGet<T>(string key, out T? value) where T : class
    {
        lock (lockObject)
        {
            if (cache.TryGetValue(key, out var entry))
            {
                if (!entry.IsExpired)
                {
                    entry.LastAccessed = DateTime.UtcNow;
                    System.Threading.Interlocked.Increment(ref _hits);
                    value = entry.Value as T;
                    return value != null;
                }
                else
                {
                    cache.Remove(key);
                }
            }

            System.Threading.Interlocked.Increment(ref _misses);
            value = null;
            return false;
        }
    }

    public virtual void Set<T>(string key, T value) where T : class
    {
        Set(key, value, TimeSpan.FromSeconds(config.MarketDataCacheDurationSeconds));
    }

    public virtual void Set<T>(string key, T value, TimeSpan ttl) where T : class
    {
        lock (lockObject)
        {
            if (cache.Count >= config.MaxCacheEntries && !cache.ContainsKey(key))
            {
                EvictLru();
            }

            cache[key] = new CacheEntry
            {
                Value = value,
                ExpiresAt = DateTime.UtcNow.Add(ttl),
                LastAccessed = DateTime.UtcNow
            };
        }
    }

    private void EvictLru()
    {
        if (cache.Count == 0) return;

        if (RemoveExpired() > 0 && cache.Count < config.MaxCacheEntries)
            return;

        string? lruKey = null;
        DateTime oldestAccess = DateTime.MaxValue;

        foreach (var kvp in cache)
        {
            if (kvp.Value.LastAccessed < oldestAccess)
            {
                oldestAccess = kvp.Value.LastAccessed;
                lruKey = kvp.Key;
            }
        }

        if (lruKey != null)
            cache.Remove(lruKey);
    }

    public void Invalidate(string key)
    {
        lock (lockObject)
        {
            cache.Remove(key);
        }
    }

    public void InvalidatePattern(string pattern)
    {
        lock (lockObject)
        {
            var keysToRemove = cache.Keys
                .Where(k => k.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var key in keysToRemove)
                cache.Remove(key);
        }
    }

    public void Clear()
    {
        lock (lockObject)
        {
            cache.Clear();
        }
    }

    public int RemoveExpired()
    {
        lock (lockObject)
        {
            var expiredKeys = cache.Where(kvp => kvp.Value.IsExpired).Select(kvp => kvp.Key).ToList();
            foreach (var key in expiredKeys)
                cache.Remove(key);
            return expiredKeys.Count;
        }
    }

    public CacheStats GetStats()
    {
        lock (lockObject)
        {
            var expired = cache.Values.Count(e => e.IsExpired);
            return new CacheStats
            {
                TotalEntries = cache.Count,
                ExpiredEntries = expired,
                ActiveEntries = cache.Count - expired,
                Hits = System.Threading.Interlocked.Read(ref _hits),
                Misses = System.Threading.Interlocked.Read(ref _misses)
            };
        }
    }

    public class CacheEntrySnapshot
    {
        public string Key { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime LastAccessed { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public uint? ItemId { get; set; }
        public string? WorldName { get; set; }
    }

    public List<CacheEntrySnapshot> GetSnapshot()
    {
        lock (lockObject)
        {
            return cache.Select(kvp =>
            {
                var snapshot = new CacheEntrySnapshot
                {
                    Key = kvp.Key,
                    ExpiresAt = kvp.Value.ExpiresAt,
                    LastAccessed = kvp.Value.LastAccessed,
                    TypeName = kvp.Value.Value?.GetType().Name ?? "null"
                };

                if (kvp.Value.Value is MarketData md)
                {
                    snapshot.ItemId = md.ItemId;
                    snapshot.WorldName = md.WorldName;
                }

                return snapshot;
            }).ToList();
        }
    }

    private class CacheEntry
    {
        public object Value { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
        public DateTime LastAccessed { get; set; } = DateTime.UtcNow;
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }
}

public class CacheStats
{
    public int TotalEntries { get; set; }
    public int ExpiredEntries { get; set; }
    public int ActiveEntries { get; set; }
    public long Hits { get; set; }
    public long Misses { get; set; }

    public float HitRate => (Hits + Misses) > 0
        ? (float)Hits / (Hits + Misses) * 100
        : 0;
}
