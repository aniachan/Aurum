using Dalamud.Configuration;
using Aurum.Models;
using System;
using System.Collections.Generic;

namespace Aurum;

[Serializable]
public class Configuration : IPluginConfiguration, Aurum.Services.ICacheConfig
{
    public int Version { get; set; } = 0;

    // UI Settings
    public bool IsConfigWindowMovable { get; set; } = true;
    public float UIScale { get; set; } = 1.0f;
    public bool EnableAnimatedCharts { get; set; } = true;
    public int RowsPerPage { get; set; } = 50;
    public List<string> HiddenColumns { get; set; } = new();
    public Theme ColorTheme { get; set; } = Theme.Default;
    
    // API Settings
    public string PreferredWorld { get; set; } = "Auto";  // or specific world name
    public bool RememberLastWorld { get; set; } = true; // New setting
    public int MarketDataCacheDurationSeconds { get; set; } = 300;  // 5 minutes
    public int MaxCacheEntries { get; set; } = 1000;
    public int MaxConcurrentApiRequests { get; set; } = 5;
    public int ApiRateLimitPerMinute { get; set; } = 900; // 15 requests/second * 60 = 900
    public int ApiErrorThreshold { get; set; } = 10; // Trigger degradation after 10 errors in a minute
    public int ApiDegradationMinutes { get; set; } = 5; // Stay degraded for 5 minutes
    
    // Calculation Settings
    public CostMode DefaultCostMode { get; set; } = CostMode.Cheapest;
    public bool IncludeMarketTax { get; set; } = true;
    public bool UseHQPricesWhenAvailable { get; set; } = true;
    public bool CalculateOpportunityCost { get; set; } = true;
    public int DefaultCraftingTimeSeconds { get; set; } = 20;
    
    // Display Settings
    public bool ShowOnlyProfitableItems { get; set; } = true;
    public int MinimumProfitFilter { get; set; } = 1000;
    public SortMode DefaultSortMode { get; set; } = SortMode.RecommendationScore;
    public List<uint> FavoriteItems { get; set; } = new(); // User's pinned/favorite items
    
    // Notification Settings
    public bool EnablePriceAlerts { get; set; } = false;
    public List<uint> WatchedItems { get; set; } = new();  // ItemIds
    
    // Integration Settings
    public bool ArtisanAutoDetect { get; set; } = true;
    public bool ShowArtisanButtons { get; set; } = true;
    
    // Risk Tolerance
    public RiskLevel MaxAcceptableRisk { get; set; } = RiskLevel.Medium;
    public bool ShowHighRiskItems { get; set; } = true;

    // Fetch Settings
    public int TopItemsToFetch { get; set; } = 50; // Default limit for API fetches
    
    // The below exists just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}

/// <summary>
/// Sort modes for the profit list
/// </summary>
public enum SortMode
{
    HighestProfit,
    HighestMargin,
    BestGilPerHour,
    FastestSelling,
    LowestCompetition,
    RecommendationScore  // Weighted algorithm
}

/// <summary>
/// Color themes for the UI
/// </summary>
public enum Theme
{
    Default,
    Dark,
    Light,
    HighContrast
}
