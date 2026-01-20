using System;
using System.Collections.Generic;

namespace Aurum.Services;

/// <summary>
/// Simple in-memory cache with TTL support
/// </summary>
public class CacheService
{
    private readonly Dictionary<string, CacheEntry> cache = new();
    private readonly object lockObject = new();
    private readonly Configuration config;
    
    public CacheService(Configuration config)
    {
        this.config = config;
    }
    
    /// <summary>
    /// Try to get a cached value
    /// </summary>
    public bool TryGet<T>(string key, out T? value) where T : class
    {
        lock (lockObject)
        {
            if (cache.TryGetValue(key, out var entry))
            {
                if (!entry.IsExpired)
                {
                    value = entry.Value as T;
                    return value != null;
                }
                else
                {
                    // Remove expired entry
                    cache.Remove(key);
                }
            }
            
            value = null;
            return false;
        }
    }
    
    /// <summary>
    /// Set a cached value with default TTL from config
    /// </summary>
    public void Set<T>(string key, T value) where T : class
    {
        Set(key, value, TimeSpan.FromSeconds(config.MarketDataCacheDurationSeconds));
    }
    
    /// <summary>
    /// Set a cached value with custom TTL
    /// </summary>
    public void Set<T>(string key, T value, TimeSpan ttl) where T : class
    {
        lock (lockObject)
        {
            cache[key] = new CacheEntry
            {
                Value = value,
                ExpiresAt = DateTime.UtcNow.Add(ttl)
            };
        }
    }
    
    /// <summary>
    /// Invalidate a specific cache entry
    /// </summary>
    public void Invalidate(string key)
    {
        lock (lockObject)
        {
            cache.Remove(key);
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
                cache.Remove(key);
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
                cache.Remove(key);
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
                ActiveEntries = cache.Count - expired
            };
        }
    }
    
    private class CacheEntry
    {
        public object Value { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }
}

public class CacheStats
{
    public int TotalEntries { get; set; }
    public int ExpiredEntries { get; set; }
    public int ActiveEntries { get; set; }
}
