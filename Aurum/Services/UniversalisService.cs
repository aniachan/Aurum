using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Aurum.Models;
using Aurum.Infrastructure;

namespace Aurum.Services;

/// <summary>
/// Service for fetching market data from Universalis API
/// </summary>
public class UniversalisService : IDisposable
{
    private readonly HttpClient httpClient;
    private readonly IPluginLog log;
    private readonly CacheService cache;
    private readonly DatabaseService database;
    private readonly RateLimiter rateLimiter;
    private readonly Configuration configuration;
    private readonly RequestQueue requestQueue;
    private readonly IDataManager dataManager;
    private readonly CancellationTokenSource disposeCts = new();
    private Task? processingTask;
    private readonly SemaphoreSlim concurrencySemaphore;

    private const string BaseUrl = "https://universalis.app/api/v2";
    private int currentWorldId = 0;
    private string currentWorldName = string.Empty;
    
    public UniversalisService(IPluginLog log, CacheService cache, DatabaseService database, RateLimiter rateLimiter, Configuration configuration, IDataManager dataManager)
    {
        this.log = log;
        this.cache = cache;
        this.database = database;
        this.rateLimiter = rateLimiter;
        this.configuration = configuration;
        this.dataManager = dataManager;
        this.requestQueue = new RequestQueue();
        
        // Initialize concurrency semaphore with configured limit
        // Default to 5 if not set or invalid
        int maxConcurrency = configuration.MaxConcurrentApiRequests > 0 ? configuration.MaxConcurrentApiRequests : 5;
        this.concurrencySemaphore = new SemaphoreSlim(maxConcurrency);

        httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(configuration.ApiRequestTimeoutSeconds > 0 ? configuration.ApiRequestTimeoutSeconds : 30)
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
    
    // Get world ID by name
    public int GetWorldIdByName(string worldName)
    {
        var worldSheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.World>();
        if (worldSheet != null)
        {
            var world = worldSheet.FirstOrDefault(w => 
                w.Name.ToString().Equals(worldName, StringComparison.OrdinalIgnoreCase) && 
                w.IsPublic);
            
            if (world.RowId != 0)
            {
                return (int)world.RowId;
            }
        }
        return 0;
    }
    
    // Auto-detect world ID from world name if not set
    private void EnsureWorldId(string worldName)
    {
        if (currentWorldId == 0 || currentWorldName != worldName)
        {
            // Look up world from Dalamud's data
            var worldSheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.World>();
            if (worldSheet != null)
            {
                var world = worldSheet.FirstOrDefault(w => 
                    w.Name.ToString().Equals(worldName, StringComparison.OrdinalIgnoreCase) && 
                    w.IsPublic);
                
                if (world.RowId != 0)
                {
                    currentWorldId = (int)world.RowId;
                    currentWorldName = worldName;
                    log.Debug($"Resolved world '{worldName}' to ID {currentWorldId}");
                }
                else
                {
                    log.Warning($"Could not find world ID for '{worldName}' in game data");
                }
            }
        }
    }

    /// <summary>
    /// Fetch market data for a single item (synchronous-like wrapper for compatibility)
    /// </summary>
    public virtual MarketData? GetMarketData(uint itemId)
    {
        // TODO: This blocks thread which is bad, but needed for non-async callers for now.
        // Ideally we should propagate async everywhere.
        // For CLI or tools it's fine. For game loop, we need async.
        // Assuming this is used in contexts where we can wait or it's cached.
        
        // We need worldName. If we don't have it, we can't fetch.
        // For now, let's assume "Raiden" or get from config?
        // Actually, let's use currentWorldId if available to map to name, or default.
        
        string worldName = "Raiden"; // Fallback default
        if (currentWorldId > 0)
        {
             // TODO: Map ID to name. For now hardcode or use existing map if we had one.
             // But UniversalisService doesn't have World map.
             // We can just use the ID string for Universalis? It supports IDs too!
             worldName = currentWorldId.ToString();
        }

        try 
        {
            return GetMarketDataAsync(worldName, itemId).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to get market data synchronously for {itemId}");
            return null;
        }
    }

    /// <summary>
    /// Fetch market data for a single item
    /// </summary>
    public async Task<MarketData?> GetMarketDataAsync(string worldName, uint itemId)
    {
        // Auto-detect world ID if not yet set
        EnsureWorldId(worldName);
        
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
        
        // CHECK FOR DEGRADATION MODE
        if (rateLimiter.IsDegraded)
        {
            // In degraded mode, we return stale data if available, or nothing.
            // We DO NOT attempt a fresh network request.
            if (currentWorldId != 0)
            {
                 var staleVal = database.GetMarketData((int)itemId, currentWorldId, TimeSpan.MaxValue);
                 if (staleVal != null)
                 {
                     staleVal.IsCachedData = true;
                     return staleVal;
                 }
            }
            // If completely missing, return null to avoid hammering API
            return null;
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
                 staleVal.IsCachedData = true; // Mark as cached fallback
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
        // Auto-detect world ID if not yet set
        EnsureWorldId(worldName);
        
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

        // CHECK FOR DEGRADATION MODE
        if (rateLimiter.IsDegraded)
        {
            log.Info("Degraded mode active: Skipping batch API request, checking for stale cache.");
            
            // In degraded mode, try to fetch stale data for uncached items
            foreach (var itemId in uncachedItems)
            {
                if (currentWorldId != 0)
                {
                    var staleVal = database.GetMarketData((int)itemId, currentWorldId, TimeSpan.MaxValue);
                    if (staleVal != null)
                    {
                         staleVal.IsCachedData = true;
                         results[itemId] = staleVal;
                    }
                }
            }
            // Return whatever we found (partial results are better than nothing/blocking)
            return results;
        }

        // 2. Fetch uncached items in chunks of 100
        // Universalis API limit is 100 items per request
        // Use configured batch size, clamped to 100 max for API safety
        int batchSize = Math.Min(configuration.ApiBatchSize, 100);
        var chunks = uncachedItems.Chunk(batchSize).ToList();
        
        log.Info($"Fetching {uncachedItems.Count} items in {chunks.Count} batches (size: {batchSize})");
        
        var batchTasks = chunks.Select(chunk => Task.Run(async () => 
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
        
        await Task.WhenAll(batchTasks);
        
        // 3. Re-check cache/DB for the uncached items
        foreach (var itemId in uncachedItems)
        {
            var cacheKey = $"market_{worldName}_{itemId}";
            if (cache.TryGet(cacheKey, out MarketData? val) && val != null)
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
                         staleVal.IsCachedData = true; // Mark as cached fallback
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
        var marketData = MarketDataPool.Get();
        
        marketData.ItemId = itemId;
        marketData.WorldName = worldName;
        marketData.LastUploadTime = DateTimeOffset.FromUnixTimeMilliseconds(response.LastUploadTime).UtcDateTime;
        marketData.CurrentListings = response.Listings?.Count ?? 0;
        // Round doubles to uints (prices in gil are whole numbers)
        marketData.MinPrice = (uint)Math.Round(response.MinPrice);
        marketData.MaxPrice = (uint)Math.Round(response.MaxPrice);
        marketData.CurrentAveragePriceNQ = (uint)Math.Round(response.CurrentAveragePriceNQ);
        marketData.CurrentAveragePriceHQ = (uint)Math.Round(response.CurrentAveragePriceHQ);
        marketData.AveragePriceNQ = (uint)Math.Round(response.AveragePriceNQ);
        marketData.AveragePriceHQ = (uint)Math.Round(response.AveragePriceHQ);
        marketData.MinPriceNQ = (uint)Math.Round(response.MinPriceNQ);
        marketData.MinPriceHQ = (uint)Math.Round(response.MinPriceHQ);
        marketData.MaxPriceNQ = (uint)Math.Round(response.MaxPriceNQ);
        marketData.MaxPriceHQ = (uint)Math.Round(response.MaxPriceHQ);
        marketData.CachedAt = DateTime.UtcNow;

        // Initialize supply/demand metrics that can be derived from raw response
        marketData.ListingsCount = response.Listings?.Count ?? 0;
        
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

    private static void PropagateResult(Task<bool> source, TaskCompletionSource<bool> target)
    {
        if (source.Status == TaskStatus.RanToCompletion && source.Result)
            target.TrySetResult(true);
        else if (source.Status == TaskStatus.Canceled)
            target.TrySetCanceled();
        else if (source.Exception != null)
            target.TrySetException(source.Exception);
        else
            target.TrySetResult(false);
    }

    private async Task ProcessQueueAsync()
    {
        log.Info("UniversalisService queue processor started");
        
        while (!disposeCts.Token.IsCancellationRequested)
        {
            // Pausing processing if RateLimiter is in degraded mode (API is erroring)
            // This allows requests to queue up without hammering the API until it recovers.
            if (rateLimiter.IsDegraded)
            {
                await Task.Delay(5000, disposeCts.Token);
                continue;
            }

            // Wait for a concurrency slot
            // This throttles the number of active processing tasks
            try 
            {
                await concurrencySemaphore.WaitAsync(disposeCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            QueuedRequest? request = null;
            int batchSize = Math.Min(configuration.ApiBatchSize, 100);

            try
            {
                // Use DequeueBatch to get optimized requests
                request = requestQueue.DequeueBatch(maxItems: batchSize);
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error in queue dequeue");
            }

            if (request == null)
            {
                // No requests? Release slot and sleep
                concurrencySemaphore.Release();
                try 
                {
                    await Task.Delay(100, disposeCts.Token);
                }
                catch (OperationCanceledException) { break; }
                continue;
            }

            // Spawn worker task for this request
            _ = Task.Run(async () => 
            {
                try
                {
                    await ProcessRequestInternalAsync(request, batchSize);
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Error in request processing worker");
                }
                finally
                {
                    concurrencySemaphore.Release();
                }
            }, disposeCts.Token);
        }
        
        log.Info("UniversalisService queue processor stopped");
    }

    private async Task ProcessRequestInternalAsync(QueuedRequest request, int batchSize)
    {
        // COALESCING DELAY:
        // If we pulled a request, there might be more incoming very soon (e.g. UI rendering a list).
        // Wait a short window (e.g. 50-100ms) to see if more requests arrive, 
        // so we can batch them into this one if possible.
        if (request.ItemIds.Count < batchSize)
        {
            // Wait for potential coalescing
            try 
            {
                await Task.Delay(100, disposeCts.Token);
            }
            catch (OperationCanceledException) { return; }

            // Check for more items for the SAME world
            var secondRequest = requestQueue.DequeueBatchForWorld(request.WorldName, maxItems: batchSize - request.ItemIds.Count);
            if (secondRequest != null)
            {
                // Merge!
                var combinedIds = request.ItemIds.Concat(secondRequest.ItemIds).Distinct().ToList();
                
                var superRequest = new QueuedRequest(request.WorldName, combinedIds, request.Priority > secondRequest.Priority ? request.Priority : secondRequest.Priority);
                
                // Propagate completion to both originals
                _ = superRequest.CompletionSource.Task.ContinueWith(t => 
                {
                        PropagateResult(t, request.CompletionSource);
                        PropagateResult(t, secondRequest.CompletionSource);
                });

                // Proceed with superRequest
                request = superRequest;
                log.Debug($"Coalesced requests: {combinedIds.Count} items");
            }
        }

        // OPTIMIZATION: Re-check cache before fetching
        // Some items might have been fetched by a parallel request or a previous batch while this was queued.
        var itemsToFetch = new List<uint>();
        foreach (var id in request.ItemIds)
        {
            var cacheKey = $"market_{request.WorldName}_{id}";
            
            // Check memory cache
            if (cache.TryGet<MarketData>(cacheKey, out _)) continue;
            
            // Check DB cache if enabled
            if (currentWorldId != 0) 
            {
                    var dbVal = database.GetMarketData((int)id, currentWorldId, TimeSpan.FromSeconds(configuration.MarketDataCacheDurationSeconds));
                    if (dbVal != null) 
                    {
                        cache.Set(cacheKey, dbVal); // Rehydrate memory cache
                        continue;
                    }
            }
            
            itemsToFetch.Add(id);
        }
        
        // If all items are already cached, we can skip the API call entirely
        if (itemsToFetch.Count == 0)
        {
            request.CompletionSource.TrySetResult(true);
            return;
        }

        int retryCount = 0;
        const int maxRetries = 3;
        bool success = false;

        while (!success && retryCount <= maxRetries && !disposeCts.Token.IsCancellationRequested)
        {
            try
            {
                // Wait for rate limit
                // We can be more granular here if needed. For now, use "api" as the endpoint key for general Universalis calls
                await rateLimiter.WaitForTokenAsync("api", disposeCts.Token, request.Priority);

                // Use itemsToFetch instead of request.ItemIds to avoid re-fetching cached items
                if (itemsToFetch.Count == 1)
                {
                    await FetchSingleItemInternalAsync(request.WorldName, itemsToFetch[0]);
                }
                else
                {
                    await FetchBatchItemsInternalAsync(request.WorldName, itemsToFetch);
                }
                
                success = true;
                request.CompletionSource.TrySetResult(true);
            }
            catch (OperationCanceledException)
            {
                request.CompletionSource.TrySetCanceled();
                break;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                // Handle 429 explicitly
                retryCount++;
                rateLimiter.RecordRetry();
                
                // Default backoff if no Retry-After header
                var retryAfterSeconds = 60; // Default to 1 minute for 429 if header missing
                
                log.Warning($"Universalis API returned 429 (Too Many Requests). Pausing for {retryAfterSeconds} seconds.");
                rateLimiter.PauseRequestsUntil(DateTime.UtcNow.AddSeconds(retryAfterSeconds));
                
                if (retryCount > maxRetries)
                {
                    rateLimiter.RecordError();
                    log.Error(ex, $"Failed to process request after {maxRetries} retries (including 429s). Giving up.");
                    request.CompletionSource.TrySetException(ex);
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(retryAfterSeconds), disposeCts.Token);
            }
            catch (HttpRequestException ex) when ((int)(ex.StatusCode ?? 0) >= 500)
            {
                // Retry on 5xx errors
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

    /// <summary>
    /// Internal method to fetch a single item (bypass queue logic)
    /// </summary>
    private async Task<MarketData?> FetchSingleItemInternalAsync(string worldName, uint itemId)
    {
        var url = $"{BaseUrl}/{worldName}/{itemId}?listings=20&entries=50";
        log.Info($"Fetching market data: {url}");
        
        // Update timeout based on config before request
        httpClient.Timeout = TimeSpan.FromSeconds(configuration.ApiRequestTimeoutSeconds > 0 ? configuration.ApiRequestTimeoutSeconds : 30);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var statusCode = 0;
        var success = false;
        
        try 
        {
            var response = await httpClient.GetAsync(url, disposeCts.Token);
            statusCode = (int)response.StatusCode;
            success = response.IsSuccessStatusCode;
            
            // Log to DB asynchronously
            stopwatch.Stop();
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            
            // Fire and forget logging
            _ = Task.Run(() => database.LogApiRequest($"item/{itemId}", DateTime.UtcNow, elapsedMs, statusCode, success));
            
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
            stopwatch.Stop();
            if (statusCode == 0 && ex is HttpRequestException hrex && hrex.StatusCode.HasValue)
            {
                statusCode = (int)hrex.StatusCode.Value;
            }
            
            // Fire and forget logging for error case
            _ = Task.Run(() => database.LogApiRequest($"item/{itemId}", DateTime.UtcNow, stopwatch.ElapsedMilliseconds, statusCode, false));
            
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
        
        // Setup vars that need to be available for logging in catch
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var statusCode = 0;
        var success = false;
        
        try
        {
            var itemList = string.Join(",", itemIds);
            var url = $"{BaseUrl}/{worldName}/{itemList}?listings=20&entries=50";
            
            // Update timeout based on config before request
            httpClient.Timeout = TimeSpan.FromSeconds(configuration.ApiRequestTimeoutSeconds > 0 ? configuration.ApiRequestTimeoutSeconds : 30);

            log.Info($"Fetching batch market data for {itemIds.Count} items");

            var response = await httpClient.GetAsync(url, disposeCts.Token);
            statusCode = (int)response.StatusCode;
            success = response.IsSuccessStatusCode;
            
            stopwatch.Stop();
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            
            // Fire and forget logging
            _ = Task.Run(() => database.LogApiRequest($"items/batch/{itemIds.Count}", DateTime.UtcNow, elapsedMs, statusCode, success));
            
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(disposeCts.Token);
            var apiResponse = JsonSerializer.Deserialize<UniversalisBatchResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (apiResponse?.Items != null)
            {
                foreach (var kvp in apiResponse.Items)
                {
                    var itemId = uint.Parse(kvp.Key);
                    var marketData = ConvertToMarketData(kvp.Value, worldName, itemId);
                    results[itemId] = marketData;
                    
                    var cacheKey = $"market_{worldName}_{kvp.Key}";
                    cache.Set(cacheKey, marketData);
                }
                
                if (currentWorldId != 0 && results.Count > 0)
                {
                    // Use bulk upsert for better performance
                    database.UpsertMarketDataBulk(results.Values, currentWorldId);
                }
            }
            
            return results;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            if (statusCode == 0 && ex is HttpRequestException hrex && hrex.StatusCode.HasValue)
            {
                statusCode = (int)hrex.StatusCode.Value;
            }
            
            // Fire and forget logging for error case
            _ = Task.Run(() => database.LogApiRequest($"items/batch/{itemIds.Count}", DateTime.UtcNow, stopwatch.ElapsedMilliseconds, statusCode, false));

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
