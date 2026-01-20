using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
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
    private readonly RequestQueue requestQueue; // Add RequestQueue
    private readonly CancellationTokenSource disposeCts = new();
    private Task? processingTask;

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
        this.requestQueue = new RequestQueue();
        
        httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Aurum FFXIV Crafting Calculator/1.0");

        // Start processing loop
        processingTask = Task.Run(ProcessQueueAsync);
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
            // Queue the request
            await requestQueue.EnqueueRequestAsync(worldName, itemId, RequestPriority.Normal);
        }
        catch (Exception ex)
        {
            log.Warning($"API request failed for item {itemId}: {ex.Message}. Attempting to fallback to stale cache.");
        }
        
        // Wait for it to be processed
        // We need to poll cache again after it's done
        // Or wait for the specific Task from EnqueueRequestAsync?
        // EnqueueRequestAsync returns a Task that completes when the request is done.
        
        // Check cache again (it should be there now)
        if (cache.TryGet(cacheKey, out MarketData? freshCached))
        {
            return freshCached;
        }
        
        // If not in memory (maybe it failed?), try DB again
        if (currentWorldId != 0)
        {
             // Try fresh DB first
             var dbVal = database.GetMarketData((int)itemId, currentWorldId, TimeSpan.FromSeconds(configuration.MarketDataCacheDurationSeconds));
             if (dbVal != null) return dbVal;

             // Fallback to stale DB data if available
             var staleVal = database.GetMarketData((int)itemId, currentWorldId, TimeSpan.MaxValue);
             if (staleVal != null)
             {
                 log.Info($"Returning stale data for item {itemId} (Last updated: {staleVal.LastUploadTime})");
                 return staleVal;
             }
        }

        return null; // Request failed or didn't return data
    }
    
    /// <summary>
    /// Fetch market data for multiple items in batch (handles splitting > 100 items)
    /// </summary>
    public async Task<Dictionary<uint, MarketData>> GetMarketDataBatchAsync(string worldName, IEnumerable<uint> itemIds)
    {
        var allItems = itemIds.ToList();
        var results = new Dictionary<uint, MarketData>();
        
        if (allItems.Count == 0)
            return results;
            
        // 1. First pass: Check cache for everything
        var uncachedItems = new List<uint>();
        
        foreach (var itemId in allItems)
        {
            var cacheKey = $"market_{worldName}_{itemId}";
            
            // Memory Cache
            if (cache.TryGet(cacheKey, out MarketData? memCached) && memCached != null)
            {
                results[itemId] = memCached;
            }
            // Database Cache
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
            log.Info($"All {allItems.Count} items found in cache");
            return results;
        }

        // 2. Fetch uncached items in chunks of 100
        // Universalis API limit is 100 items per request
        const int BatchSize = 100;
        var chunks = uncachedItems.Chunk(BatchSize).ToList();
        
        log.Info($"Fetching {uncachedItems.Count} items in {chunks.Count} batches");
        
        // Use SemaphoreSlim to limit concurrency if needed, but RateLimiter is global anyway.
        // We can process chunks in parallel, but they will all hit the same RateLimiter lock.
        // However, this allows overlapping the "wait for rate limit" and "network request" phases slightly if rate limit allows.
        // Or if rate limit is high (e.g. 25/sec), we want to fire multiple requests.
        
        // Actually, EnqueueRequestAsync waits for the queue processor. The queue processor is single-threaded (ProcessQueueAsync).
        // So parallelism here just means queueing them up faster.
        // To truly have parallel processing, we'd need multiple queue processors or a concurrent queue processor.
        // BUT, given the strict rate limits on Universalis, serial processing is safer and likely fast enough.
        // Let's stick to serial queueing for safety, but maybe verify if we can do Task.WhenAll on the results.
        
        var batchTasks = new List<Task>();
        
        foreach (var chunk in chunks)
        {
            // We launch these tasks to enqueue. They return when the request COMPLETEs.
            // So by adding them to a list and waiting WhenAll, we are waiting for all of them to finish.
            // The queue processor will pick them up one by one.
            batchTasks.Add(Task.Run(async () => 
            {
                try
                {
                    await requestQueue.EnqueueRequestAsync(worldName, chunk.ToList(), RequestPriority.Normal);
                }
                catch (Exception ex)
                {
                    log.Warning($"Batch API request failed for chunk: {ex.Message}.");
                }
            }));
        }
        
        await Task.WhenAll(batchTasks);
        
        // 3. Re-check cache/DB for the uncached items
        foreach (var itemId in uncachedItems)
        {
            var cacheKey = $"market_{worldName}_{itemId}";
            if (cache.TryGet(cacheKey, out MarketData? val))
            {
                results[itemId] = val;
            }
            else if (currentWorldId != 0)
            {
                 var dbVal = database.GetMarketData((int)itemId, currentWorldId, TimeSpan.FromSeconds(configuration.MarketDataCacheDurationSeconds));
                 if (dbVal != null) 
                 {
                     results[itemId] = dbVal;
                 }
                 else
                 {
                     // Fallback to stale data
                     var staleVal = database.GetMarketData((int)itemId, currentWorldId, TimeSpan.MaxValue);
                     if (staleVal != null)
                     {
                         results[itemId] = staleVal;
                     }
                 }
            }
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
        disposeCts.Cancel();
        try
        {
            processingTask?.Wait(2000); // Wait for cleanup
        }
        catch { /* Ignore */ }
        
        disposeCts.Dispose();
        httpClient?.Dispose();
    }

    private async Task ProcessQueueAsync()
    {
        log.Info("UniversalisService queue processor started");
        
        while (!disposeCts.Token.IsCancellationRequested)
        {
            var request = requestQueue.Dequeue();
            if (request == null)
            {
                await Task.Delay(100, disposeCts.Token);
                continue;
            }

            int retryCount = 0;
            const int maxRetries = 3;
            bool success = false;

            while (!success && retryCount <= maxRetries && !disposeCts.Token.IsCancellationRequested)
            {
                try
                {
                    // Wait for rate limit
                    await rateLimiter.WaitForTokenAsync(disposeCts.Token);

                    if (request.ItemIds.Count == 1)
                    {
                        await FetchSingleItemInternalAsync(request.WorldName, request.ItemIds[0]);
                    }
                    else
                    {
                        await FetchBatchItemsInternalAsync(request.WorldName, request.ItemIds);
                    }
                    
                    success = true;
                    request.CompletionSource.TrySetResult(true);
                }
                catch (OperationCanceledException)
                {
                    request.CompletionSource.TrySetCanceled();
                    break;
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests || (int)ex.StatusCode >= 500)
                {
                    // Retry on 429 and 5xx errors
                    retryCount++;
                    rateLimiter.RecordRetry();
                    if (retryCount > maxRetries)
                    {
                        rateLimiter.RecordError();
                        log.Error(ex, $"Failed to process request after {maxRetries} retries. Giving up.");
                        request.CompletionSource.TrySetException(ex);
                        break;
                    }

                    // Exponential backoff: 1s, 2s, 4s
                    var delaySeconds = Math.Pow(2, retryCount - 1);
                    log.Warning($"API request failed (Attempt {retryCount}/{maxRetries}). Retrying in {delaySeconds}s...");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), disposeCts.Token);
                }
                catch (Exception ex)
                {
                    // Non-retriable error
                    rateLimiter.RecordError();
                    log.Error(ex, $"Error processing request for {request.ItemIds.Count} items");
                    request.CompletionSource.TrySetException(ex);
                    break;
                }
            }
        }
        
        log.Info("UniversalisService queue processor stopped");
    }

    /// <summary>
    /// Internal method to fetch a single item (bypass queue logic)
    /// </summary>
    private async Task<MarketData?> FetchSingleItemInternalAsync(string worldName, uint itemId)
    {
        try
        {
            var url = $"{BaseUrl}/{worldName}/{itemId}?listings=20&entries=50";
            
            // Note: Rate limiter wait moved to ProcessQueueAsync
            
            log.Info($"Fetching market data: {url}");
            
            var response = await httpClient.GetAsync(url, disposeCts.Token);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(disposeCts.Token);
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
            
            var cacheKey = $"market_{worldName}_{itemId}";
            cache.Set(cacheKey, marketData);
            
            if (currentWorldId != 0)
            {
                database.UpsertMarketData(marketData, currentWorldId);
            }
            
            return marketData;
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Error fetching single item {itemId}");
            throw;
        }
    }

    /// <summary>
    /// Internal method to fetch batch items (bypass queue logic)
    /// </summary>
    private async Task<Dictionary<uint, MarketData>> FetchBatchItemsInternalAsync(string worldName, List<uint> itemIds)
    {
        var results = new Dictionary<uint, MarketData>();
        try
        {
            var itemList = string.Join(",", itemIds);
            var url = $"{BaseUrl}/{worldName}/{itemList}?listings=20&entries=50";
            
            log.Info($"Fetching batch market data for {itemIds.Count} items");
            
            var response = await httpClient.GetAsync(url, disposeCts.Token);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(disposeCts.Token);
            var apiResponse = JsonSerializer.Deserialize<UniversalisBatchResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (apiResponse?.Items == null) return results;
            
            foreach (var kvp in apiResponse.Items)
            {
                var itemId = uint.Parse(kvp.Key);
                var marketData = ConvertToMarketData(kvp.Value, worldName, itemId);
                results[itemId] = marketData;
                
                var cacheKey = $"market_{worldName}_{kvp.Key}";
                cache.Set(cacheKey, marketData);
                
                if (currentWorldId != 0)
                {
                    // Use bulk upsert for better performance
                    database.UpsertMarketDataBulk(results.Values, currentWorldId);
                }
            }
            
            return results;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error fetching batch items");
            throw;
        }
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
