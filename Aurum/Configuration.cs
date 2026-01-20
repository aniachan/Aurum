using Dalamud.Configuration;
using Aurum.Models;
using System;
using System.Collections.Generic;

namespace Aurum;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    // UI Settings
    public bool IsConfigWindowMovable { get; set; } = true;
    public float UIScale { get; set; } = 1.0f;
    public bool EnableAnimatedCharts { get; set; } = true;
    
    // API Settings
    public string PreferredWorld { get; set; } = "Auto";  // or specific world name
    public int MarketDataCacheDurationSeconds { get; set; } = 300;  // 5 minutes
    public int MaxConcurrentApiRequests { get; set; } = 5;
    public int ApiRateLimitPerMinute { get; set; } = 900; // 15 requests/second * 60 = 900
    
    // Calculation Settings
    public CostMode DefaultCostMode { get; set; } = CostMode.Cheapest;
    public bool IncludeMarketTax { get; set; } = true;
    public bool UseHQPricesWhenAvailable { get; set; } = true;
    public int DefaultCraftingTimeSeconds { get; set; } = 20;
    
    // Display Settings
    public bool ShowOnlyProfitableItems { get; set; } = true;
    public int MinimumProfitFilter { get; set; } = 1000;
    public SortMode DefaultSortMode { get; set; } = SortMode.RecommendationScore;
    
    // Notification Settings
    public bool EnablePriceAlerts { get; set; } = false;
    public List<uint> WatchedItems { get; set; } = new();  // ItemIds
    
    // Integration Settings
    public bool ArtisanAutoDetect { get; set; } = true;
    public bool ShowArtisanButtons { get; set; } = true;
    
    // Risk Tolerance
    public RiskLevel MaxAcceptableRisk { get; set; } = RiskLevel.Medium;
    public bool ShowHighRiskItems { get; set; } = true;
    
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
