using System;
using Xunit;
using Aurum;
using Aurum.Models;
using System.IO;
using System.Collections.Generic;

namespace Aurum.IntegrationTests;

public class ConfigurationPersistenceTests
{
    private string GetTempConfigPath()
    {
        return Path.Combine(Path.GetTempPath(), $"aurum_test_config_{Guid.NewGuid()}.json");
    }

    [Fact]
    public void SaveAndLoad_ShouldPersistAllSettings()
    {
        // 1. Setup a configuration with non-default values
        var originalConfig = new Configuration();
        
        // UI Settings
        originalConfig.IsConfigWindowMovable = false;
        originalConfig.UIScale = 1.5f;
        originalConfig.EnableAnimatedCharts = false;
        originalConfig.RowsPerPage = 25;
        originalConfig.HiddenColumns = new List<string> { "Risk", "Velocity" };
        originalConfig.ColorTheme = Theme.Dark;
        
        // API Settings
        originalConfig.PreferredWorld = "Gilgamesh";
        originalConfig.RememberLastWorld = false;
        originalConfig.MarketDataCacheDurationSeconds = 600;
        originalConfig.MaxCacheEntries = 500;
        originalConfig.MaxConcurrentApiRequests = 2;
        originalConfig.ApiRateLimitPerMinute = 100;
        originalConfig.ApiErrorThreshold = 5;
        originalConfig.ApiDegradationMinutes = 10;
        
        // Calculation Settings
        originalConfig.DefaultCostMode = CostMode.Cheapest;
        originalConfig.IncludeMarketTax = false;
        originalConfig.UseHQPricesWhenAvailable = false;
        originalConfig.CalculateOpportunityCost = false;
        originalConfig.DefaultCraftingTimeSeconds = 45;
        
        // Display Settings
        originalConfig.ShowOnlyProfitableItems = false;
        originalConfig.MinimumProfitFilter = 5000;
        originalConfig.DefaultSortMode = SortMode.HighestMargin;
        originalConfig.FavoriteItems = new List<uint> { 1234, 5678 };
        
        // User Preferences
        originalConfig.RecentSearches = new List<string> { "Potion", "Sword" };
        
        // Notification Settings
        originalConfig.EnablePriceAlerts = true;
        originalConfig.WatchedItems = new List<uint> { 9999 };
        
        // Integration Settings
        originalConfig.ArtisanAutoDetect = false;
        originalConfig.ShowArtisanButtons = false;
        
        // Risk Tolerance
        originalConfig.MaxAcceptableRisk = RiskLevel.Low;
        originalConfig.ShowHighRiskItems = false;
        
        // Fetch Settings
        originalConfig.TopItemsToFetch = 100;

        // 2. Serialize to JSON (mimicking what PluginInterface.SavePluginConfig does)
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(originalConfig);
        
        // 3. Deserialize back to a new object
        var loadedConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<Configuration>(json);
        
        // 4. Verify all values match
        Assert.NotNull(loadedConfig);
        
        // UI Settings
        Assert.Equal(originalConfig.IsConfigWindowMovable, loadedConfig.IsConfigWindowMovable);
        Assert.Equal(originalConfig.UIScale, loadedConfig.UIScale);
        Assert.Equal(originalConfig.EnableAnimatedCharts, loadedConfig.EnableAnimatedCharts);
        Assert.Equal(originalConfig.RowsPerPage, loadedConfig.RowsPerPage);
        Assert.Equal(originalConfig.HiddenColumns, loadedConfig.HiddenColumns);
        Assert.Equal(originalConfig.ColorTheme, loadedConfig.ColorTheme);
        
        // API Settings
        Assert.Equal(originalConfig.PreferredWorld, loadedConfig.PreferredWorld);
        Assert.Equal(originalConfig.RememberLastWorld, loadedConfig.RememberLastWorld);
        Assert.Equal(originalConfig.MarketDataCacheDurationSeconds, loadedConfig.MarketDataCacheDurationSeconds);
        Assert.Equal(originalConfig.MaxCacheEntries, loadedConfig.MaxCacheEntries);
        Assert.Equal(originalConfig.MaxConcurrentApiRequests, loadedConfig.MaxConcurrentApiRequests);
        Assert.Equal(originalConfig.ApiRateLimitPerMinute, loadedConfig.ApiRateLimitPerMinute);
        Assert.Equal(originalConfig.ApiErrorThreshold, loadedConfig.ApiErrorThreshold);
        Assert.Equal(originalConfig.ApiDegradationMinutes, loadedConfig.ApiDegradationMinutes);
        
        // Calculation Settings
        Assert.Equal(originalConfig.DefaultCostMode, loadedConfig.DefaultCostMode);
        Assert.Equal(originalConfig.IncludeMarketTax, loadedConfig.IncludeMarketTax);
        Assert.Equal(originalConfig.UseHQPricesWhenAvailable, loadedConfig.UseHQPricesWhenAvailable);
        Assert.Equal(originalConfig.CalculateOpportunityCost, loadedConfig.CalculateOpportunityCost);
        Assert.Equal(originalConfig.DefaultCraftingTimeSeconds, loadedConfig.DefaultCraftingTimeSeconds);
        
        // Display Settings
        Assert.Equal(originalConfig.ShowOnlyProfitableItems, loadedConfig.ShowOnlyProfitableItems);
        Assert.Equal(originalConfig.MinimumProfitFilter, loadedConfig.MinimumProfitFilter);
        Assert.Equal(originalConfig.DefaultSortMode, loadedConfig.DefaultSortMode);
        Assert.Equal(originalConfig.FavoriteItems, loadedConfig.FavoriteItems);
        
        // User Preferences
        Assert.Equal(originalConfig.RecentSearches, loadedConfig.RecentSearches);
        
        // Notification Settings
        Assert.Equal(originalConfig.EnablePriceAlerts, loadedConfig.EnablePriceAlerts);
        Assert.Equal(originalConfig.WatchedItems, loadedConfig.WatchedItems);
        
        // Integration Settings
        Assert.Equal(originalConfig.ArtisanAutoDetect, loadedConfig.ArtisanAutoDetect);
        Assert.Equal(originalConfig.ShowArtisanButtons, loadedConfig.ShowArtisanButtons);
        
        // Risk Tolerance
        Assert.Equal(originalConfig.MaxAcceptableRisk, loadedConfig.MaxAcceptableRisk);
        Assert.Equal(originalConfig.ShowHighRiskItems, loadedConfig.ShowHighRiskItems);
        
        // Fetch Settings
        Assert.Equal(originalConfig.TopItemsToFetch, loadedConfig.TopItemsToFetch);
    }
}
