using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace Aurum.Services;

/// <summary>
/// Background service that proactively refreshes market data for high-priority items.
/// </summary>
public class RefreshService : IDisposable
{
    private readonly IPluginLog log;
    private readonly Configuration configuration;
    private readonly UniversalisService universalis;
    private readonly DatabaseService database;
    private readonly RecipeService recipeService;
    private readonly IClientState clientState;

    private readonly CancellationTokenSource disposeCts = new();
    private Task? refreshTask;
    private bool isRunning = false;

    private const int REFRESH_CHECK_INTERVAL_MS = 60_000;

    public RefreshService(
        IPluginLog log,
        Configuration configuration,
        UniversalisService universalis,
        DatabaseService database,
        RecipeService recipeService,
        IClientState clientState)
    {
        this.log = log;
        this.configuration = configuration;
        this.universalis = universalis;
        this.database = database;
        this.recipeService = recipeService;
        this.clientState = clientState;

        Start();
    }

    public void Start()
    {
        if (isRunning) return;
        isRunning = true;
        refreshTask = Task.Run(RefreshLoopAsync);
        log.Info("Background RefreshService started");
    }

    public void Stop() => isRunning = false;

    private async Task RefreshLoopAsync()
    {
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

            await Task.Delay(REFRESH_CHECK_INTERVAL_MS, disposeCts.Token);
        }
    }

    private async Task PerformRefreshCycle()
    {
        var priorities = database.GetAllItemPriorities();

        if (priorities.Count == 0)
        {
            var bootstrapRecipes = recipeService.GetRecipesByLevel(90, 100).Take(20).ToList();
            foreach (var recipe in bootstrapRecipes)
            {
                database.UpsertItemPriority((int)recipe.ResultItemId, 50);
                priorities[(int)recipe.ResultItemId] = 50;
            }

            if (priorities.Count == 0)
                return;
        }

        var itemsToRefresh = new List<uint>();
        int maxRefreshPerCycle = configuration.TopItemsToFetch;

        foreach (var kvp in priorities.OrderByDescending(x => x.Value))
        {
            if (itemsToRefresh.Count >= maxRefreshPerCycle) break;

            var itemId = (uint)kvp.Key;
            var score = kvp.Value;

            var marketData = database.GetMarketData((int)itemId, 0, TimeSpan.MaxValue);

            if (marketData != null)
            {
                if ((DateTime.UtcNow - marketData.LastUploadTime).TotalDays > 30)
                    continue;

                // Refresh items with score >= 50 whose data is older than 10 minutes
                if (score >= 50 && (DateTime.UtcNow - marketData.LastUploadTime).TotalMinutes > 10)
                    itemsToRefresh.Add(itemId);
            }
            else if (score > 50)
            {
                itemsToRefresh.Add(itemId);
            }
        }

        if (itemsToRefresh.Count > 0)
        {
            log.Debug($"Background refresh: {itemsToRefresh.Count} items queued");
            // World name must be known to call Universalis; the dashboard sets it when the player loads in
        }

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        isRunning = false;
        disposeCts.Cancel();
        try { refreshTask?.Wait(2000); } catch { }
        disposeCts.Dispose();
    }
}
