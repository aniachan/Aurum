using System;
using System.Collections.Generic;
using Aurum.Infrastructure;
using Aurum.Models;

namespace Aurum.Services;

/// <summary>
/// Simple in-memory cache with TTL support
/// </summary>
public class CacheService
{
    private readonly Dictionary<string, CacheEntry> cache = new();
    private readonly object lockObject = new();
    private readonly ICacheConfig config;
    
    // Statistics
    private long _hits;
    private long _misses;
    
    public CacheService(ICacheConfig config)
    {
        this.config = config;
    }
    
    /// <summary>
    /// Try to get a cached value
    /// </summary>
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
                    // Remove expired entry
                    var expiredValue = cache[key].Value;
                    cache.Remove(key);
                    
                    // If it was a MarketData object, return it to the pool
                    if (expiredValue is MarketData marketData)
                    {
                        MarketDataPool.Return(marketData);
                    }
                }
            }
            
            System.Threading.Interlocked.Increment(ref _misses);
            value = null;
            return false;
        }
    }
    
    /// <summary>
    /// Set a cached value with default TTL from config
    /// </summary>
    public virtual void Set<T>(string key, T value) where T : class
    {
        Set(key, value, TimeSpan.FromSeconds(config.MarketDataCacheDurationSeconds));
    }
    
    /// <summary>
    /// Set a cached value with custom TTL
    /// </summary>
    public virtual void Set<T>(string key, T value, TimeSpan ttl) where T : class
    {
        lock (lockObject)
        {
            // If cache is full and we're adding a new key, evict LRU
            if (cache.Count >= config.MaxCacheEntries && !cache.ContainsKey(key))
            {
                EvictLru();
            }

            // If overwriting existing key, check if we need to return old value to pool
            if (cache.TryGetValue(key, out var existingEntry))
            {
                if (existingEntry.Value is MarketData oldMarketData && !ReferenceEquals(oldMarketData, value))
                {
                    MarketDataPool.Return(oldMarketData);
                }
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

        // First remove any expired entries
        if (RemoveExpired() > 0 && cache.Count < config.MaxCacheEntries)
        {
            return;
        }

        // Find the oldest accessed entry
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
        {
            var valueToRemove = cache[lruKey].Value;
            cache.Remove(lruKey);
            
            if (valueToRemove is MarketData marketData)
            {
                MarketDataPool.Return(marketData);
            }
        }
    }
    
    /// <summary>
    /// Invalidate a specific cache entry
    /// </summary>
    public void Invalidate(string key)
    {
        lock (lockObject)
        {
            if (cache.TryGetValue(key, out var entry))
            {
                cache.Remove(key);
                if (entry.Value is MarketData marketData)
                {
                    MarketDataPool.Return(marketData);
                }
            }
        }
    }
    
    /// <summary>
    /// Invalidate all cache entries matching a pattern
    /// </summary>
    public void InvalidatePattern(string pattern)
    {
        lock (lockObject)
        {
            var keysToRemove = new List<string>();
            foreach (var key in cache.Keys)
            {
                if (key.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    keysToRemove.Add(key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                if (cache.TryGetValue(key, out var entry))
                {
                    cache.Remove(key);
                    if (entry.Value is MarketData marketData)
                    {
                        MarketDataPool.Return(marketData);
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Clear all cached data
    /// </summary>
    public void Clear()
    {
        lock (lockObject)
        {
            foreach (var entry in cache.Values)
            {
                if (entry.Value is MarketData marketData)
                {
                    MarketDataPool.Return(marketData);
                }
            }
            cache.Clear();
        }
    }
    
    /// <summary>
    /// Remove expired entries (cleanup)
    /// </summary>
    public int RemoveExpired()
    {
        lock (lockObject)
        {
            var expiredKeys = new List<string>();
            foreach (var kvp in cache)
            {
                if (kvp.Value.IsExpired)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }
            
            foreach (var key in expiredKeys)
            {
                if (cache.TryGetValue(key, out var entry))
                {
                    cache.Remove(key);
                    if (entry.Value is MarketData marketData)
                    {
                        MarketDataPool.Return(marketData);
                    }
                }
            }
            
            return expiredKeys.Count;
        }
    }
    
    /// <summary>
    /// Get cache statistics
    /// </summary>
    public CacheStats GetStats()
    {
        lock (lockObject)
        {
            var expired = 0;
            foreach (var entry in cache.Values)
            {
                if (entry.IsExpired)
                    expired++;
            }
            
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
