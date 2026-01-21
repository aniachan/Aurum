using System;
using System.Threading.Tasks;
using Aurum;
using Aurum.Services;

namespace Aurum.IntegrationTests;

public static class CacheServiceTests
{
    public static void Run()
    {
        Console.WriteLine("Running CacheServiceTests...");
        TestBasicSetGet();
        TestExpiration();
        TestLruEviction();
        Console.WriteLine("CacheServiceTests Passed!");
    }

    private static void TestBasicSetGet()
    {
        var config = new MockCacheConfig();
        var cache = new CacheService(config);

        cache.Set("test1", "value1");
        
        if (!cache.TryGet<string>("test1", out var val) || val != "value1")
        {
            throw new Exception("TestBasicSetGet failed: Could not retrieve value");
        }
        
        if (cache.TryGet<string>("nonexistent", out _))
        {
            throw new Exception("TestBasicSetGet failed: Retrieved nonexistent value");
        }
        
        Console.WriteLine("- TestBasicSetGet passed");
    }

    private static void TestExpiration()
    {
        var config = new MockCacheConfig();
        var cache = new CacheService(config);

        // Set with very short TTL
        cache.Set("expired", "value", TimeSpan.FromMilliseconds(10));
        
        System.Threading.Thread.Sleep(50);
        
        if (cache.TryGet<string>("expired", out _))
        {
            throw new Exception("TestExpiration failed: Value should have expired");
        }
        
        Console.WriteLine("- TestExpiration passed");
    }

    private static void TestLruEviction()
    {
        var config = new MockCacheConfig();
        config.MaxCacheEntries = 3; // We need to add this to Configuration first
        var cache = new CacheService(config);

        // Fill cache
        cache.Set("k1", "v1");
        cache.Set("k2", "v2");
        cache.Set("k3", "v3");

        // Access k1 to make it recently used
        cache.TryGet<string>("k1", out _);
        
        // Add one more to trigger eviction
        // If LRU works, k2 (least recently used) should be evicted
        // k1 was just used, k3 was added recently
        cache.Set("k4", "v4");

        if (!cache.TryGet<string>("k1", out _))
             throw new Exception("TestLruEviction failed: k1 should be present (recently used)");
        
        if (!cache.TryGet<string>("k3", out _))
             throw new Exception("TestLruEviction failed: k3 should be present (recently added)");
             
        if (!cache.TryGet<string>("k4", out _))
             throw new Exception("TestLruEviction failed: k4 should be present (just added)");

        if (cache.TryGet<string>("k2", out _))
             throw new Exception("TestLruEviction failed: k2 should have been evicted");

        Console.WriteLine("- TestLruEviction passed");
    }
}
