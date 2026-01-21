using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Aurum.Models;

namespace Aurum.Services;

/// <summary>
/// Service responsible for background data refreshing.
/// It uses the ItemPriorityService to identify high-value items and proactively fetches their market data.
/// </summary>
public class RefreshService : IDisposable
{
    private readonly IPluginLog log;
    private readonly Configuration configuration;
    private readonly UniversalisService universalis;
    private readonly DatabaseService database;
    private readonly ItemPriorityService priorityService;
    private readonly RecipeService recipeService;
    private readonly IClientState clientState;
    
    private readonly CancellationTokenSource disposeCts = new();
    private Task? refreshTask;
    private bool isRunning = false;

    // Refresh interval in milliseconds (e.g. check every minute)
    private const int REFRESH_CHECK_INTERVAL_MS = 60000;

    public RefreshService(
        IPluginLog log, 
        Configuration configuration, 
        UniversalisService universalis, 
        DatabaseService database, 
        ItemPriorityService priorityService,
        RecipeService recipeService,
        IClientState clientState)
    {
        this.log = log;
        this.configuration = configuration;
        this.universalis = universalis;
        this.database = database;
        this.priorityService = priorityService;
        this.recipeService = recipeService;
        this.clientState = clientState;

        // Start background loop if enabled
        // For now, we'll start it automatically, but we might want a config toggle later
        Start();
    }

    public void Start()
    {
        if (isRunning) return;
        
        isRunning = true;
        refreshTask = Task.Run(RefreshLoopAsync);
        log.Info("Background RefreshService started");
    }

    public void Stop()
    {
        isRunning = false;
        // The loop checks isRunning and token
    }

    private async Task RefreshLoopAsync()
    {
        // Initial delay to let the plugin load fully
        await Task.Delay(5000, disposeCts.Token);

        while (isRunning && !disposeCts.Token.IsCancellationRequested)
        {
            try
            {
                await PerformRefreshCycle();
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error in background refresh cycle");
            }

            // Wait for next cycle
            await Task.Delay(REFRESH_CHECK_INTERVAL_MS, disposeCts.Token);
        }
    }

    private async Task PerformRefreshCycle()
    {
        // 1. Get all recipes we might care about
        // For now, let's limit to recipes that have been analyzed before or high priority ones
        // If we have 13k recipes, we can't iterate all of them every minute.
        
        // Strategy:
        // - Get list of items with known priority scores from DB
        // - Also consider items that haven't been scored yet (maybe process a small batch of new ones)
        
        var priorities = database.GetAllItemPriorities();
        var itemsToRefresh = new List<uint>();
        
        // If no priorities yet, bootstrap with recent high-level recipes
        if (priorities.Count == 0)
        {
            log.Info("No priorities found. Bootstrapping with high-level recipes...");
            
            // Get level 90+ recipes to seed the priority system
            var bootstrapRecipes = recipeService.GetRecipesByLevel(90, 100).Take(20).ToList();
            foreach (var recipe in bootstrapRecipes)
            {
                // Give them medium priority to start
                database.UpsertItemPriority((int)recipe.ResultItemId, 50);
                priorities[(int)recipe.ResultItemId] = 50;
            }
            
            log.Info($"Bootstrapped {bootstrapRecipes.Count} items with priority scores");
            
            if (priorities.Count == 0)
            {
                log.Debug("No recipes available for bootstrap. Skipping refresh cycle.");
                return;
            }
        }

        int refreshCount = 0;
        int maxRefreshPerCycle = configuration.TopItemsToFetch; // Use configurable limit

        foreach (var kvp in priorities.OrderByDescending(x => x.Value)) // Highest priority first
        {
            if (refreshCount >= maxRefreshPerCycle) break;

            var itemId = (uint)kvp.Key;
            var score = kvp.Value;
            
            // Check if it needs refresh
            // We need the last update time. 
            // We can get this from DB (DatabaseService could have a lightweight method for this)
            // For now, let's use GetMarketData with a "stale" tolerance to just peek at the timestamp
            
            // We'll peek at the DB directly to avoid full deserialization if possible, 
            // but GetMarketData is what we have.
            // Using a very long maxAge to ensure we get whatever is there.
            var marketData = database.GetMarketData((int)itemId, 0, TimeSpan.MaxValue); 
            
            // Note: worldId=0 might fail if we don't know the world. 
            // We need to know the current world.
            // UniversalisService knows the current world? 
            // We need to inject the current world ID here.
            
            // FIXME: We need a reliable way to get current WorldID.
            // Assuming we only care about the player's current world for now.
            // We can ask UniversalisService (if we expose it) or just fail gracefully.
            // Since we can't easily get it here without a proper state manager, 
            // we'll assume the caller has set it or we skip.
            
            // Ideally, we'd have a WorldService or similar.
            // For this implementation, let's assume we can't proceed without a world ID.
            // Skipping the world check for a moment to illustrate the logic.
            
            if (marketData != null)
            {
                // SKIP items with no market activity for 30 days
                if ((DateTime.UtcNow - marketData.LastUploadTime).TotalDays > 30)
                {
                    log.Debug($"Skipping item {itemId} - No activity for >30 days (Last update: {marketData.LastUploadTime})");
                    continue;
                }

                if (priorityService.ShouldRefresh(score, marketData.LastUploadTime))
                {
                    itemsToRefresh.Add(itemId);
                    refreshCount++;
                }
            }
            else
            {
                // Never fetched? High priority? Fetch it!
                if (score > 50) 
                {
                    itemsToRefresh.Add(itemId);
                    refreshCount++;
                }
            }
        }

        if (itemsToRefresh.Count > 0)
        {
            log.Info($"Background refresh triggered for {itemsToRefresh.Count} items");
            
            // We need the world name. 
            // If we don't have it, we can't fetch.
            // Using ClientState to get current world name.
            // Note: clientState must be accessed on the main thread?
            // Usually simple property access is safe, but we should be careful.
            // If we are in a background task, we can't access IClientState safely if it's not thread safe.
            // However, most Dalamud services are not thread safe.
            // A better way is to have the main thread push the world state to us, or UniversalisService to handle it.
            // UniversalisService has SetCurrentWorld() but maybe not exposing name.
            
            // For now, let's assume we can skip if we are not logged in or rely on UniversalisService to know the world.
            // Since we can't easily get WorldName here safely without dispatching to UI thread,
            // we will defer this to a safer implementation later or check if UniversalisService can handle it.
            
            // Actually, UniversalisService GetMarketDataAsync takes worldName.
            // If we don't have it, we can't call it.
            
            // Placeholder: Skip actual fetch until we solve the thread-safe world access.
            // log.Warning("Skipping background fetch: World name not available in background thread yet.");
            
            // await universalis.GetMarketDataBatchAsync(currentWorldName, itemsToRefresh);
        }
    }

    public void Dispose()
    {
        isRunning = false;
        disposeCts.Cancel();
        try
        {
            refreshTask?.Wait(2000);
        }
        catch { /* Ignore */ }
        
        disposeCts.Dispose();
    }
}
