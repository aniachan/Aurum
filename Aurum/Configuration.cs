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
    public Theme ColorTheme { get; set; } = Theme.Default;
    public int RowsPerPage { get; set; } = 50;
    public List<string> HiddenColumns { get; set; } = new();
    public List<string> RecentSearches { get; set; } = new();

    // API Settings
    public string PreferredWorld { get; set; } = "Auto";
    public bool RememberLastWorld { get; set; } = true;
    public int MarketDataCacheDurationSeconds { get; set; } = 300;
    public int MaxCacheEntries { get; set; } = 1000;
    public int MaxRecipeCacheEntries { get; set; } = 2000;
    public int MaxConcurrentApiRequests { get; set; } = 5;
    public int ApiRateLimitPerMinute { get; set; } = 900;
    public int ApiBatchSize { get; set; } = 20;
    public int ApiRequestTimeoutSeconds { get; set; } = 30;
    public bool WorkOffline { get; set; } = false;
    public int ApiErrorThreshold { get; set; } = 10;
    public int ApiDegradationMinutes { get; set; } = 5;

    // Calculation Settings
    public CostMode DefaultCostMode { get; set; } = CostMode.Cheapest;
    public bool IncludeMarketTax { get; set; } = true;
    public bool UseHQPricesWhenAvailable { get; set; } = true;
    public bool CalculateOpportunityCost { get; set; } = true;
    public int CrossWorldTravelCost { get; set; } = 2000;
    public int DefaultCraftingTimeSeconds { get; set; } = 20;
    public int EstimatedGatheringTimeSeconds { get; set; } = 15;

    // Display Settings
    public bool ShowOnlyProfitableItems { get; set; } = true;
    public int MinimumProfitFilter { get; set; } = 1000;

    // Category Filters
    public bool FilterIncludeCombat { get; set; } = true;
    public bool FilterIncludeCraftingGathering { get; set; } = true;
    public bool FilterIncludeFurniture { get; set; } = true;
    public bool FilterIncludeConsumables { get; set; } = true;
    public bool FilterIncludeMaterials { get; set; } = true;

    public SortMode DefaultSortMode { get; set; } = SortMode.RecommendationScore;
    public List<uint> FavoriteItems { get; set; } = new();

    // Risk Tolerance
    public RiskLevel MaxAcceptableRisk { get; set; } = RiskLevel.Medium;
    public bool ShowHighRiskItems { get; set; } = true;

    // Logging
    public bool EnableDebugLogging { get; set; } = false;

    // Fetch Settings
    public int TopItemsToFetch { get; set; } = 50;
    public int MaxItemsToTrack { get; set; } = 2000;
    public int MaxRecipesToAnalyze { get; set; } = 1000;

    // Database Settings
    public int DatabaseVacuumFrequencyDays { get; set; } = 7;
    public DateTime LastDatabaseVacuum { get; set; } = DateTime.MinValue;

    // Filter Presets
    public Dictionary<string, (string Name, FilterCriteria Criteria)> FilterPresets { get; set; } = new();

    public virtual void Save()
    {
        Plugin.PluginInterface?.SavePluginConfig(this);
    }
}

public enum SortMode
{
    HighestProfit,
    HighestMargin,
    BestGilPerHour,
    FastestSelling,
    LowestCompetition,
    RecommendationScore,
}

public enum Theme
{
    Default,
    Dark,
    Light,
    HighContrast,
}
