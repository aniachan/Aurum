using System;
using System.Collections.Generic;
using System.Linq;
using Aurum.Models;
using Aurum.Services;
using Dalamud.Plugin.Services;
using Moq;
using Xunit;

namespace Aurum.IntegrationTests;

public class MarketAnalysisServiceTests
{
    private readonly Mock<IPluginLog> _mockLog;
    private readonly Configuration _config;
    private readonly MarketAnalysisService _service;

    public MarketAnalysisServiceTests()
    {
        _mockLog = new Mock<IPluginLog>();
        _config = new Configuration();
        _service = new MarketAnalysisService(_mockLog.Object, _config);
    }

    [Fact]
    public void CalculatePeakDemand_IdentifiesBestDaysAndHours()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var marketData = new MarketData
        {
            ItemId = 123,
            RecentHistory = new List<SaleRecord>
            {
                // 3 sales on Monday at 10:00
                new SaleRecord { Timestamp = new DateTime(2023, 10, 23, 10, 0, 0, DateTimeKind.Utc), Quantity = 1, PricePerUnit = 100 }, // Monday
                new SaleRecord { Timestamp = new DateTime(2023, 10, 23, 10, 30, 0, DateTimeKind.Utc), Quantity = 1, PricePerUnit = 100 },
                new SaleRecord { Timestamp = new DateTime(2023, 10, 23, 10, 45, 0, DateTimeKind.Utc), Quantity = 1, PricePerUnit = 100 },
                
                // 1 sale on Tuesday at 12:00
                new SaleRecord { Timestamp = new DateTime(2023, 10, 24, 12, 0, 0, DateTimeKind.Utc), Quantity = 1, PricePerUnit = 120 }, // Tuesday
            }
        };

        // Act
        _service.AnalyzeMarket(marketData);

        // Assert
        Assert.Contains(DayOfWeek.Monday, marketData.BestDaysToSell);
        Assert.Equal(DayOfWeek.Monday, marketData.BestDaysToSell.First());
        
        Assert.Contains(10, marketData.BestHoursToSell);
        Assert.Equal(10, marketData.BestHoursToSell.First());
        
        Assert.Contains("Monday", marketData.PeakDemandAnalysis);
        Assert.Contains("10:00 UTC", marketData.PeakDemandAnalysis);
    }

    [Fact]
    public void CalculatePeakDemand_IdentifiesHigherPriceDays()
    {
        // Arrange
        var marketData = new MarketData
        {
            ItemId = 123,
            RecentHistory = new List<SaleRecord>
            {
                // Many sales on Monday, low price
                new SaleRecord { Timestamp = new DateTime(2023, 10, 23, 10, 0, 0, DateTimeKind.Utc), Quantity = 1, PricePerUnit = 100 },
                new SaleRecord { Timestamp = new DateTime(2023, 10, 23, 11, 0, 0, DateTimeKind.Utc), Quantity = 1, PricePerUnit = 100 },
                
                // Few sales on Tuesday, high price
                new SaleRecord { Timestamp = new DateTime(2023, 10, 24, 12, 0, 0, DateTimeKind.Utc), Quantity = 1, PricePerUnit = 200 },
            }
        };

        // Act
        _service.AnalyzeMarket(marketData);

        // Assert
        Assert.Contains("Highest prices on Tuesday", marketData.PeakDemandAnalysis);
    }
}
