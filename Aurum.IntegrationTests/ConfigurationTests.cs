using System;
using Xunit;
using Aurum;
using Aurum.Models;

namespace Aurum.IntegrationTests;

public class ConfigurationTests
{
    [Fact]
    public void TestConfigurationDefaults()
    {
        var config = new Configuration();
        
        // UI Settings
        Assert.True(config.IsConfigWindowMovable);
        Assert.Equal(1.0f, config.UIScale);
        Assert.True(config.EnableAnimatedCharts);
        
        // API Settings
        Assert.Equal("Auto", config.PreferredWorld);
        Assert.Equal(300, config.MarketDataCacheDurationSeconds);
        Assert.Equal(5, config.MaxConcurrentApiRequests);
        Assert.Equal(900, config.ApiRateLimitPerMinute);
        
        // Calculation Settings
        Assert.Equal(CostMode.Cheapest, config.DefaultCostMode);
        Assert.True(config.IncludeMarketTax);
        Assert.True(config.UseHQPricesWhenAvailable);
        Assert.Equal(20, config.DefaultCraftingTimeSeconds);
        
        // Display Settings
        Assert.True(config.ShowOnlyProfitableItems);
        Assert.Equal(1000, config.MinimumProfitFilter);
        Assert.Equal(SortMode.RecommendationScore, config.DefaultSortMode);
        
        // Notification Settings
        Assert.False(config.EnablePriceAlerts);
        Assert.NotNull(config.WatchedItems);
        Assert.Empty(config.WatchedItems);
        
        // Integration Settings
        Assert.True(config.ArtisanAutoDetect);
        Assert.True(config.ShowArtisanButtons);
        
        // Risk Tolerance
        Assert.Equal(RiskLevel.Medium, config.MaxAcceptableRisk);
        Assert.True(config.ShowHighRiskItems);
    }

    [Fact]
    public void TestConfigurationValuesCanBeChanged()
    {
        var config = new Configuration();
        
        config.IsConfigWindowMovable = false;
        config.UIScale = 1.5f;
        config.PreferredWorld = "Gilgamesh";
        
        Assert.False(config.IsConfigWindowMovable);
        Assert.Equal(1.5f, config.UIScale);
        Assert.Equal("Gilgamesh", config.PreferredWorld);
    }
}
