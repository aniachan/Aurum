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
    private const string BaseUrl = "https://universalis.app/api/v2";
    
    public UniversalisService(IPluginLog log, CacheService cache)
    {
        this.log = log;
        this.cache = cache;
        httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Aurum FFXIV Crafting Calculator/1.0");
    }
    
    /// <summary>
    /// Fetch market data for a single item
    /// </summary>
    public async Task<MarketData?> GetMarketDataAsync(string worldName, uint itemId)
    {
        var cacheKey = $"market_{worldName}_{itemId}";
        
        // Check cache first
        if (cache.TryGet(cacheKey, out MarketData? cached))
        {
            return cached;
        }
        
        try
        {
            var url = $"{BaseUrl}/{worldName}/{itemId}?listings=20&entries=50";
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
            
            // Cache the result
            cache.Set(cacheKey, marketData);
            
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
            if (cache.TryGet(cacheKey, out MarketData? cached) && cached != null)
            {
                results[itemId] = cached;
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
                var marketData = ConvertToMarketData(kvp.Value, worldName, uint.Parse(kvp.Key));
                results[uint.Parse(kvp.Key)] = marketData;
                
                // Cache each result
                var cacheKey = $"market_{worldName}_{kvp.Key}";
                cache.Set(cacheKey, marketData);
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
            MinPrice = response.MinPrice,
            MaxPrice = response.MaxPrice,
            CurrentAveragePriceNQ = response.CurrentAveragePriceNQ,
            CurrentAveragePriceHQ = response.CurrentAveragePriceHQ,
            AveragePriceNQ = response.AveragePriceNQ,
            AveragePriceHQ = response.AveragePriceHQ,
            MinPriceNQ = response.MinPriceNQ,
            MinPriceHQ = response.MinPriceHQ,
            MaxPriceNQ = response.MaxPriceNQ,
            MaxPriceHQ = response.MaxPriceHQ,
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
    public uint MinPrice { get; set; }
    public uint MaxPrice { get; set; }
    public uint CurrentAveragePriceNQ { get; set; }
    public uint CurrentAveragePriceHQ { get; set; }
    public uint AveragePriceNQ { get; set; }
    public uint AveragePriceHQ { get; set; }
    public uint MinPriceNQ { get; set; }
    public uint MinPriceHQ { get; set; }
    public uint MaxPriceNQ { get; set; }
    public uint MaxPriceHQ { get; set; }
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
