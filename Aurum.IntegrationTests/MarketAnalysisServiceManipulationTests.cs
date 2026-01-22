using System;
using System.Collections.Generic;
using System.Linq;
using Aurum.Models;
using Aurum.Services;
using Dalamud.Plugin.Services;
using Moq;
using Xunit;

namespace Aurum.IntegrationTests;

public class MarketAnalysisServiceManipulationTests
{
    private readonly Mock<IPluginLog> _mockLog;
    private readonly Configuration _config;
    private readonly MarketAnalysisService _service;

    public MarketAnalysisServiceManipulationTests()
    {
        _mockLog = new Mock<IPluginLog>();
        _config = new Configuration();
        _service = new MarketAnalysisService(_mockLog.Object, _config);
    }

    [Fact]
    public void DetectsPriceGouging_WhenCurrentPriceIsHighAboveMedian()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var history = new List<SaleRecord>();
        
        // Create 10 sales around 100 gil
        for(int i=0; i<10; i++)
        {
            history.Add(new SaleRecord { Timestamp = now.AddHours(-i), PricePerUnit = 100, Quantity = 1 });
        }

        var marketData = new MarketData
        {
            ItemId = 123,
            RecentHistory = history,
            MinPrice = 500, // 5x median (threshold is 3x)
            Listings = new List<MarketListing>()
        };

        // Act
        _service.AnalyzeMarket(marketData);

        // Assert
        var warning = marketData.Warnings.FirstOrDefault(w => w.Type == MarketWarning.PriceManipulation);
        Assert.NotNull(warning);
        Assert.Contains("Price Gouging", warning.Message);
        Assert.Equal(WarningLevel.Warning, warning.Level);
    }

    [Fact]
    public void DetectsSuspiciousSales_WhenOutlierExists()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var history = new List<SaleRecord>();
        
        // Create 10 sales around 100 gil
        for(int i=0; i<9; i++)
        {
            history.Add(new SaleRecord { Timestamp = now.AddHours(-i), PricePerUnit = 100, Quantity = 1 });
        }
        
        // Add one massive outlier (10000 gil - 100x median)
        history.Add(new SaleRecord { Timestamp = now, PricePerUnit = 10000, Quantity = 1 });

        var marketData = new MarketData
        {
            ItemId = 123,
            RecentHistory = history,
            MinPrice = 110,
            Listings = new List<MarketListing>()
        };

        // Act
        _service.AnalyzeMarket(marketData);

        // Assert
        var warning = marketData.Warnings.FirstOrDefault(w => w.Type == MarketWarning.PriceManipulation);
        Assert.NotNull(warning);
        Assert.Contains("Suspicious Sales", warning.Message);
        Assert.Equal(WarningLevel.Warning, warning.Level);
    }

    [Fact]
    public void IgnoresNormalFluctuations()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var history = new List<SaleRecord>();
        
        // Create sales fluctuating normally (100-200)
        for(int i=0; i<10; i++)
        {
            history.Add(new SaleRecord { Timestamp = now.AddHours(-i), PricePerUnit = (uint)(100 + (i * 10)), Quantity = 1 });
        }

        var marketData = new MarketData
        {
            ItemId = 123,
            RecentHistory = history,
            MinPrice = 250, // 2.5x base, < 3x threshold
            Listings = new List<MarketListing>()
        };

        // Act
        _service.AnalyzeMarket(marketData);

        // Assert
        Assert.DoesNotContain(marketData.Warnings, w => w.Type == MarketWarning.PriceManipulation);
    }
}
