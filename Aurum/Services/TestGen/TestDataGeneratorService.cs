using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Aurum.Models;

namespace Aurum.Services.TestGen;

/// <summary>
/// Service for generating test data and simulating scenarios for development and debugging.
/// </summary>
public class TestDataGeneratorService
{
    private readonly IPluginLog log;
    private readonly DatabaseService database;
    private readonly CacheService cache;
    private readonly RateLimiter rateLimiter;
    private readonly Random random = new();

    public TestDataGeneratorService(IPluginLog log, DatabaseService database, CacheService cache, RateLimiter rateLimiter)
    {
        this.log = log;
        this.database = database;
        this.cache = cache;
        this.rateLimiter = rateLimiter;
    }

    /// <summary>
    /// Generates mock market data for a list of item IDs.
    /// </summary>
    public async Task GenerateMockMarketDataAsync(int worldId, IEnumerable<uint> itemIds, bool simulateSlow = false)
    {
        log.Information($"Generating mock market data for {itemIds.Count()} items on World {worldId}...");

        if (simulateSlow)
        {
            await Task.Delay(2000); // Simulate network latency
        }

        var marketDataList = new List<MarketData>();
        
        foreach (var itemId in itemIds)
        {
            marketDataList.Add(CreateMockMarketData(itemId, worldId));
        }

        // Use bulk upsert
        database.UpsertMarketDataBulk(marketDataList, worldId);
        
        // Also populate cache
        foreach(var data in marketDataList)
        {
            // Assuming world name "TestWorld" for now if we don't have lookup, 
            // but in real app worldId -> name mapping should be used.
            // For test gen, we'll just skip memory cache population if we can't easily get world name,
            // or we use a placeholder.
            var cacheKey = $"market_{worldId}_{data.ItemId}"; 
            cache.Set(cacheKey, data);
        }

        log.Information("Mock data generation complete.");
    }

    private MarketData CreateMockMarketData(uint itemId, int worldId)
    {
        var now = DateTime.UtcNow;
        var minPrice = (uint)random.Next(100, 100000);
        var avgPrice = (uint)(minPrice * (1.0 + (random.NextDouble() * 0.5))); // 0-50% higher than min
        var listingCount = random.Next(1, 50);

        var data = new MarketData
        {
            ItemId = itemId,
            WorldName = worldId.ToString(), // Placeholder
            LastUploadTime = now,
            MinPrice = minPrice,
            CurrentAveragePriceNQ = avgPrice,
            CurrentAveragePriceHQ = (uint)(avgPrice * 1.5),
            CurrentListings = listingCount,
            SaleVelocity = (float)(random.NextDouble() * 20), // 0-20 sales/day
            CachedAt = now,
            Listings = new List<MarketListing>(),
            RecentHistory = new List<SaleRecord>(),
            
            // Randomly assign some metrics
            SalesPerDay = (float)(random.NextDouble() * 10),
            DemandRatio = (float)(random.NextDouble() * 2)
        };

        // Generate some listings
        for (int i = 0; i < Math.Min(listingCount, 10); i++)
        {
            data.Listings.Add(new MarketListing
            {
                ItemId = itemId,
                PricePerUnit = (uint)(minPrice * (1.0 + (i * 0.05))),
                Quantity = (uint)random.Next(1, 99),
                Total = (uint)(minPrice * (1.0 + (i * 0.05))) * (uint)random.Next(1, 99),
                RetainerName = $"Retainer{i}",
                RetainerCity = "Limsa Lominsa",
                ListingTime = now.AddMinutes(-random.Next(1, 1440)),
                WorldName = data.WorldName
            });
        }

        // Generate some history
        for (int i = 0; i < 20; i++)
        {
            data.RecentHistory.Add(new SaleRecord
            {
                ItemId = itemId,
                PricePerUnit = (uint)(avgPrice * (0.8 + (random.NextDouble() * 0.4))),
                Quantity = (uint)random.Next(1, 10),
                Timestamp = now.AddHours(-random.Next(1, 72)),
                BuyerName = $"Buyer{i}",
                WorldName = data.WorldName
            });
        }

        return data;
    }

    /// <summary>
    /// Simulates API errors by logging them to the database.
    /// </summary>
    public void SimulateApiErrors(int count = 5)
    {
        log.Information($"Simulating {count} API errors...");
        for (int i = 0; i < count; i++)
        {
            database.LogApiRequest(
                $"test/error/{i}", 
                DateTime.UtcNow, 
                random.Next(50, 500), 
                500 + random.Next(0, 4), // 500-503
                false
            );
        }
        
        // Also trigger rate limiter error count
        rateLimiter.RecordError();
    }

    /// <summary>
    /// Clears all market data from the database.
    /// </summary>
    public void ClearAllMarketData()
    {
        log.Information("Clearing all market data...");
        // Use custom query to delete
        database.ExecuteCustomQuery("DELETE FROM MarketData;");
        database.ExecuteCustomQuery("DELETE FROM PriceHistory;");
        database.ExecuteCustomQuery("DELETE FROM VelocityHistory;");
        
        cache.Clear();
        log.Information("Market data cleared.");
    }
}
