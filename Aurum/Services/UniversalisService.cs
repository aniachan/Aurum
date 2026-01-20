using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Aurum.Models;

namespace Aurum.Services;

/// <summary>
/// Service for fetching market data from Universalis API
/// </summary>
public class UniversalisService : IDisposable
{
    private readonly HttpClient httpClient;
    private readonly IPluginLog log;
    private readonly CacheService cache;
    private readonly DatabaseService database; // Add DatabaseService dependency
    private readonly RateLimiter rateLimiter;
    private readonly Configuration configuration;
    private const string BaseUrl = "https://universalis.app/api/v2";
    private int currentWorldId = 0; // Need to track this, maybe pass in ctor or resolve dynamically?
    
    // Updated constructor to accept DatabaseService
    public UniversalisService(IPluginLog log, CacheService cache, DatabaseService database, RateLimiter rateLimiter, Configuration configuration)
    {
        this.log = log;
        this.cache = cache;
        this.database = database;
        this.rateLimiter = rateLimiter;
        this.configuration = configuration;
        httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Aurum FFXIV Crafting Calculator/1.0");
    }
    
    // Method to update world ID (call this when player logs in or changes worlds)
    public void SetCurrentWorld(int worldId)
    {
        this.currentWorldId = worldId;
    }

    /// <summary>
    /// Fetch market data for a single item
    /// </summary>
    public async Task<MarketData?> GetMarketDataAsync(string worldName, uint itemId)
    {
        var cacheKey = $"market_{worldName}_{itemId}";
        
        // 1. Check Memory Cache first (fastest)
        if (cache.TryGet(cacheKey, out MarketData? memCached))
        {
            return memCached;
        }
        
        // 2. Check Database Cache (persistent)
        // Note: We need worldId. If not set, we might miss DB hits.
        // Assuming worldName matches currentWorldId logic or we need to lookup WorldId from WorldName
        // For now, we'll skip DB read if we don't have a reliable WorldId, or rely on caller to set it.
        // To be safe, if we have a currentWorldId, we use it.
        
        if (currentWorldId != 0) // Basic check
        {
            var dbCached = database.GetMarketData((int)itemId, currentWorldId, TimeSpan.FromSeconds(configuration.MarketDataCacheDurationSeconds)); // Use configurable TTL
            if (dbCached != null)
            {
                // Re-hydrate memory cache
                cache.Set(cacheKey, dbCached);
                return dbCached;
            }
        }
        
        try
        {
            var url = $"{BaseUrl}/{worldName}/{itemId}?listings=20&entries=50";
            
            // Respect rate limits
            await rateLimiter.WaitForTokenAsync();
            
            log.Info($"Fetching market data: {url}");
            
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<UniversalisItemResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (apiResponse == null)
            {
                log.Warning($"Failed to parse market data for item {itemId}");
                return null;
            }
            
            var marketData = ConvertToMarketData(apiResponse, worldName, itemId);
            
            // 3. Save to Memory Cache
            cache.Set(cacheKey, marketData);
            
            // 4. Save to Database Cache
            if (currentWorldId != 0)
            {
                // Fire and forget or await? Safe to do async but SQLite is sync.
                // Run in background to not block UI? DatabaseService uses locks so it's thread-safeish but blocking.
                // Let's just do it synchronously for now to ensure data integrity.
                database.UpsertMarketData(marketData, currentWorldId);
            }
            
            return marketData;
        }
        catch (HttpRequestException ex)
        {
            log.Error(ex, $"HTTP error fetching market data for {itemId}");
            return null;
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Unexpected error fetching market data for {itemId}");
            return null;
        }
    }
    
    /// <summary>
    /// Fetch market data for multiple items in batch (up to 100)
    /// </summary>
    public async Task<Dictionary<uint, MarketData>> GetMarketDataBatchAsync(string worldName, IEnumerable<uint> itemIds)
    {
        var items = itemIds.Take(100).ToList();  // API limit
        var results = new Dictionary<uint, MarketData>();
        
        if (items.Count == 0)
            return results;
        
        // Check cache for all items first
        var uncachedItems = new List<uint>();
        foreach (var itemId in items)
        {
            var cacheKey = $"market_{worldName}_{itemId}";
            
            // 1. Memory Cache
            if (cache.TryGet(cacheKey, out MarketData? memCached) && memCached != null)
            {
                results[itemId] = memCached;
            }
            // 2. Database Cache
            else if (currentWorldId != 0)
            {
                 var dbCached = database.GetMarketData((int)itemId, currentWorldId, TimeSpan.FromSeconds(configuration.MarketDataCacheDurationSeconds));
                 if (dbCached != null)
                 {
                     results[itemId] = dbCached;
                     cache.Set(cacheKey, dbCached); // Re-hydrate memory
                 }
                 else
                 {
                     uncachedItems.Add(itemId);
                 }
            }
            else
            {
                uncachedItems.Add(itemId);
            }
        }
        
        if (uncachedItems.Count == 0)
        {
            log.Info($"All {items.Count} items found in cache");
            return results;
        }
        
        try
        {
            var itemList = string.Join(",", uncachedItems);
            var url = $"{BaseUrl}/{worldName}/{itemList}?listings=20&entries=50";
            
            // Respect rate limits
            await rateLimiter.WaitForTokenAsync();
            
            log.Info($"Fetching batch market data for {uncachedItems.Count} items");
            
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<UniversalisBatchResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (apiResponse?.Items == null)
            {
                log.Warning("Failed to parse batch market data");
                return results;
            }
            
            foreach (var kvp in apiResponse.Items)
            {
                var itemId = uint.Parse(kvp.Key);
                var marketData = ConvertToMarketData(kvp.Value, worldName, itemId);
                results[itemId] = marketData;
                
                // 3. Cache results
                var cacheKey = $"market_{worldName}_{kvp.Key}";
                cache.Set(cacheKey, marketData);
                
                // 4. Persist to DB
                if (currentWorldId != 0)
                {
                    database.UpsertMarketData(marketData, currentWorldId);
                }
            }
            
            log.Info($"Successfully fetched {results.Count} items from Universalis");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error fetching batch market data");
        }
        
        return results;
    }
    
    /// <summary>
    /// Convert Universalis API response to our MarketData model
    /// </summary>
    private MarketData ConvertToMarketData(UniversalisItemResponse response, string worldName, uint itemId)
    {
        var marketData = new MarketData
        {
            ItemId = itemId,
            WorldName = worldName,
            LastUploadTime = DateTimeOffset.FromUnixTimeMilliseconds(response.LastUploadTime).UtcDateTime,
            CurrentListings = response.Listings?.Count ?? 0,
            // Round doubles to uints (prices in gil are whole numbers)
            MinPrice = (uint)Math.Round(response.MinPrice),
            MaxPrice = (uint)Math.Round(response.MaxPrice),
            CurrentAveragePriceNQ = (uint)Math.Round(response.CurrentAveragePriceNQ),
            CurrentAveragePriceHQ = (uint)Math.Round(response.CurrentAveragePriceHQ),
            AveragePriceNQ = (uint)Math.Round(response.AveragePriceNQ),
            AveragePriceHQ = (uint)Math.Round(response.AveragePriceHQ),
            MinPriceNQ = (uint)Math.Round(response.MinPriceNQ),
            MinPriceHQ = (uint)Math.Round(response.MinPriceHQ),
            MaxPriceNQ = (uint)Math.Round(response.MaxPriceNQ),
            MaxPriceHQ = (uint)Math.Round(response.MaxPriceHQ),
            CachedAt = DateTime.UtcNow
        };
        
        // Convert listings
        if (response.Listings != null)
        {
            marketData.Listings = response.Listings.Select(l => new MarketListing
            {
                ItemId = itemId,
                IsHQ = l.Hq,
                PricePerUnit = l.PricePerUnit,
                Quantity = l.Quantity,
                Total = l.Total,
                RetainerName = l.RetainerName ?? string.Empty,
                RetainerCity = GetCityName(l.RetainerCity),
                ListingTime = DateTimeOffset.FromUnixTimeSeconds(l.LastReviewTime).UtcDateTime,
                OnMannequin = l.OnMannequin,
                SellerName = l.SellerID ?? string.Empty
            }).ToList();
        }
        
        // Convert recent history
        if (response.RecentHistory != null)
        {
            marketData.RecentHistory = response.RecentHistory.Select(h => new SaleRecord
            {
                ItemId = itemId,
                IsHQ = h.Hq,
                PricePerUnit = h.PricePerUnit,
                Quantity = h.Quantity,
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(h.Timestamp).UtcDateTime,
                BuyerName = h.BuyerName ?? string.Empty,
                OnMannequin = h.OnMannequin
            }).ToList();
            
            if (marketData.RecentHistory.Any())
            {
                marketData.LastSaleTime = marketData.RecentHistory.Max(h => h.Timestamp);
            }
        }
        
        return marketData;
    }
    
    /// <summary>
    /// Convert city ID to city name
    /// </summary>
    private static string GetCityName(int cityId)
    {
        return cityId switch
        {
            1 => "Limsa Lominsa",
            2 => "Gridania",
            3 => "Ul'dah",
            4 => "Ishgard",
            7 => "Kugane",
            10 => "Crystarium",
            12 => "Old Sharlayan",
            13 => "Radz-at-Han",
            _ => $"City {cityId}"
        };
    }
    
    public void Dispose()
    {
        httpClient?.Dispose();
    }
}

#region API Response Models

/// <summary>
/// Universalis API response for a single item
/// </summary>
public class UniversalisItemResponse
{
    public long LastUploadTime { get; set; }
    public List<UniversalisListing>? Listings { get; set; }
    public List<UniversalisHistoryEntry>? RecentHistory { get; set; }
    
    // Price fields are doubles because Universalis returns floating point averages
    public double MinPrice { get; set; }
    public double MaxPrice { get; set; }
    public double CurrentAveragePriceNQ { get; set; }
    public double CurrentAveragePriceHQ { get; set; }
    public double AveragePriceNQ { get; set; }
    public double AveragePriceHQ { get; set; }
    public double MinPriceNQ { get; set; }
    public double MinPriceHQ { get; set; }
    public double MaxPriceNQ { get; set; }
    public double MaxPriceHQ { get; set; }
}

public class UniversalisListing
{
    public uint PricePerUnit { get; set; }
    public uint Quantity { get; set; }
    public uint Total { get; set; }
    public bool Hq { get; set; }
    public string? RetainerName { get; set; }
    public int RetainerCity { get; set; }  // City ID, not name
    public long LastReviewTime { get; set; }
    public string? SellerID { get; set; }
    public bool OnMannequin { get; set; }
}

public class UniversalisHistoryEntry
{
    public bool Hq { get; set; }
    public uint PricePerUnit { get; set; }
    public uint Quantity { get; set; }
    public long Timestamp { get; set; }
    public string? BuyerName { get; set; }
    public bool OnMannequin { get; set; }
}

/// <summary>
/// Universalis API response for batch request
/// </summary>
public class UniversalisBatchResponse
{
    public Dictionary<string, UniversalisItemResponse>? Items { get; set; }
}

#endregion
