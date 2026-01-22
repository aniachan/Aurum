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
    public void AnalyzeMarket_NoHistory_SetsHighRisk()
    {
        var marketData = new MarketData
        {
            ItemId = 123,
            RecentHistory = new List<SaleRecord>()
        };

        _service.AnalyzeMarket(marketData);

        Assert.Equal(0, marketData.SaleVelocity);
        Assert.Equal(float.MaxValue, marketData.SupplyDemandRatio);
        Assert.Equal(100, marketData.RiskScore);
        Assert.Equal(RiskLevel.VeryHigh, marketData.RiskLevel);
        Assert.Single(marketData.Warnings);
        Assert.Equal(MarketWarning.StaleMarket, marketData.Warnings[0].Type);
    }

    [Fact]
    public void AnalyzeMarket_CalculatesVelocityCorrectly()
    {
        var now = DateTime.UtcNow;
        var history = new List<SaleRecord>
        {
            new SaleRecord { Timestamp = now.AddDays(-2), Quantity = 10, PricePerUnit = 100 },
            new SaleRecord { Timestamp = now.AddDays(-1), Quantity = 5, PricePerUnit = 100 },
            new SaleRecord { Timestamp = now, Quantity = 5, PricePerUnit = 100 }
        };

        var marketData = new MarketData
        {
            ItemId = 123,
            RecentHistory = history,
            Listings = new List<MarketListing>()
        };

        _service.AnalyzeMarket(marketData);

        // Time span is 2 days. Total quantity is 20. Velocity should be 10/day.
        Assert.Equal(10f, marketData.SaleVelocity, 1);
    }

    [Fact]
    public void AnalyzeMarket_CalculatesSupplyDemandRatio()
    {
        var now = DateTime.UtcNow;
        var history = new List<SaleRecord>
        {
            new SaleRecord { Timestamp = now.AddDays(-2), Quantity = 20, PricePerUnit = 100 },
            new SaleRecord { Timestamp = now, Quantity = 20, PricePerUnit = 100 }
        };
        // Velocity = 40 / 2 = 20/day

        var marketData = new MarketData
        {
            ItemId = 123,
            RecentHistory = history,
            Listings = new List<MarketListing>(), // Count will be used
            CurrentListings = 100 // Manually setting for test simplicity if property isn't auto-calc from Listings
        };

        // Ensure Listings count matches if logic uses Listings.Count
        for(int i=0; i<100; i++) marketData.Listings.Add(new MarketListing{ PricePerUnit = 100 });

        _service.AnalyzeMarket(marketData);

        // S/D Ratio = Listings / Velocity = 100 / 20 = 5
        Assert.Equal(5f, marketData.SupplyDemandRatio, 1);
    }

    [Fact]
    public void AnalyzeMarket_DetectsPriceTrend_Rising()
    {
        var now = DateTime.UtcNow;
        var history = new List<SaleRecord>();
        
        // Older half (lower prices)
        for(int i=0; i<5; i++)
        {
            history.Add(new SaleRecord { Timestamp = now.AddDays(-10 + i), Quantity = 1, PricePerUnit = 100 });
        }

        // Newer half (higher prices)
        for(int i=0; i<5; i++)
        {
            history.Add(new SaleRecord { Timestamp = now.AddDays(-5 + i), Quantity = 1, PricePerUnit = 150 });
        }

        var marketData = new MarketData
        {
            ItemId = 123,
            RecentHistory = history,
            Listings = new List<MarketListing>()
        };

        _service.AnalyzeMarket(marketData);

        Assert.Equal(PriceTrend.Rising, marketData.Trend);
    }

    [Fact]
    public void AnalyzeMarket_DetectsPriceTrend_Falling()
    {
        var now = DateTime.UtcNow;
        var history = new List<SaleRecord>();

        // Older half (higher prices)
        for (int i = 0; i < 5; i++)
        {
            history.Add(new SaleRecord { Timestamp = now.AddDays(-10 + i), Quantity = 1, PricePerUnit = 150 });
        }

        // Newer half (lower prices)
        for (int i = 0; i < 5; i++)
        {
            history.Add(new SaleRecord { Timestamp = now.AddDays(-5 + i), Quantity = 1, PricePerUnit = 100 });
        }

        var marketData = new MarketData
        {
            ItemId = 123,
            RecentHistory = history,
            Listings = new List<MarketListing>()
        };

        _service.AnalyzeMarket(marketData);

        Assert.Equal(PriceTrend.Falling, marketData.Trend);
    }

    [Fact]
    public void AnalyzeMarket_GeneratesMarketCrashRiskWarning()
    {
        var now = DateTime.UtcNow;
        var history = new List<SaleRecord>
        {
            new SaleRecord { Timestamp = now.AddDays(-2), Quantity = 2, PricePerUnit = 100 },
            new SaleRecord { Timestamp = now, Quantity = 2, PricePerUnit = 100 }
        };
        // Velocity = 4 / 2 = 2/day

        var marketData = new MarketData
        {
            ItemId = 123,
            RecentHistory = history,
            CurrentListings = 20, // S/D Ratio = 10 (> 5.0f)
            Listings = new List<MarketListing>()
        };
        for (int i = 0; i < 20; i++) marketData.Listings.Add(new MarketListing { PricePerUnit = 100 });


        _service.AnalyzeMarket(marketData);

        Assert.Contains(marketData.Warnings, w => w.Type == MarketWarning.MarketCrashRisk);
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

    [Fact]
    public void AnalyzeMarket_DetectsMarketSaturation_WhenDemandIsFallingAndSupplyIsModerate()
    {
        // Arrange
        var now = DateTime.UtcNow;
        // Construct history to show falling demand
        // Old history: High velocity
        // Recent history: Low velocity
        var history = new List<SaleRecord>();
        
        // Old sales (7 days ago to 2 days ago) - 5 days duration
        // 10 sales per day = 50 sales
        for (int i = 0; i < 50; i++)
        {
            history.Add(new SaleRecord 
            { 
                Timestamp = now.AddDays(-7 + (i * 0.1)), 
                Quantity = 1, 
                PricePerUnit = 100 
            });
        }

        // Recent sales (last 2 days) - 2 days duration
        // 2 sales per day = 4 sales (Significant drop from 10/day)
        for (int i = 0; i < 4; i++)
        {
            history.Add(new SaleRecord 
            { 
                Timestamp = now.AddDays(-2 + (i * 0.5)), 
                Quantity = 1, 
                PricePerUnit = 100 
            });
        }

        var marketData = new MarketData
        {
            ItemId = 123,
            RecentHistory = history,
            // Current listings: 
            // Average velocity over 7 days: (50+4)/7 = ~7.7/day
            // Recent velocity over 2 days: 2/day
            // Overall velocity calculation might average it.
            // Let's ensure SupplyDemandRatio ends up > 2.0 but < 5.0
            // If velocity is ~7.7, then 20 listings = 2.6 days supply.
            CurrentListings = 25 
        };

        // Act
        _service.AnalyzeMarket(marketData);

        // Assert
        // Should detect falling momentum
        Assert.True(marketData.MarketMomentum < -0.2f, $"Momentum {marketData.MarketMomentum} should be less than -0.2");
        
        // Should detect saturation warning
        var warning = marketData.Warnings.FirstOrDefault(w => w.Type == MarketWarning.OversupplyExpected);
        Assert.NotNull(warning);
        Assert.Equal(WarningLevel.Warning, warning.Level);
        Assert.Contains("falling demand", warning.Details.ToLower());
    }

    [Fact]
    public void AnalyzeMarket_DetectsMarketSaturation_WhenSupplyIsAccumulating()
    {
        // Arrange
        var now = DateTime.UtcNow;
        // Steady sales, but listings are high (but not crash level)
        // 10 sales/day
        var history = new List<SaleRecord>();
        for (int i = 0; i < 70; i++)
        {
            history.Add(new SaleRecord 
            { 
                Timestamp = now.AddDays(-7 + (i * 0.1)), 
                Quantity = 1, 
                PricePerUnit = 100 
            });
        }

        var marketData = new MarketData
        {
            ItemId = 456,
            RecentHistory = history,
            // Velocity ~10/day.
            // Crash risk is > 5.0 days (50 listings)
            // Saturation risk is > 3.0 days (30 listings)
            CurrentListings = 35 // 3.5 days supply
        };

        // Act
        _service.AnalyzeMarket(marketData);

        // Assert
        var warning = marketData.Warnings.FirstOrDefault(w => w.Type == MarketWarning.OversupplyExpected);
        Assert.NotNull(warning);
        Assert.Contains("accumulating", warning.Details.ToLower());
    }
}
