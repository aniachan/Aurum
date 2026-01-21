using System;
using System.Collections.Generic;
using Aurum.Models;
using Aurum.Services.Filtering;
using Xunit;

namespace Aurum.IntegrationTests.Services.Filtering;

public class ItemFilterServiceTests
{
    private readonly ItemFilterService _service;

    public ItemFilterServiceTests()
    {
        _service = new ItemFilterService();
    }

    private ProfitCalculation CreateMockItem(
        int profit, 
        float margin, 
        float velocity, 
        float supplyDemand, 
        int riskScore, 
        PriceTrend trend)
    {
        return new ProfitCalculation
        {
            IsDataComplete = true,
            RawProfit = profit,
            ProfitMargin = margin,
            ROI = margin, // Simplifying for test
            RiskScore = riskScore,
            RiskLevel = riskScore switch
            {
                < 25 => RiskLevel.Low,
                < 50 => RiskLevel.Medium,
                < 75 => RiskLevel.High,
                _ => RiskLevel.VeryHigh
            },
            MarketData = new MarketData
            {
                SaleVelocity = velocity,
                SupplyDemandRatio = supplyDemand,
                Trend = trend,
                Warnings = new List<MarketWarningInfo>()
            }
        };
    }

    [Fact]
    public void FilterItems_ShouldFilterByProfit()
    {
        var items = new List<ProfitCalculation>
        {
            CreateMockItem(10000, 20, 5, 1, 10, PriceTrend.Stable),
            CreateMockItem(50000, 20, 5, 1, 10, PriceTrend.Stable)
        };

        var criteria = new FilterCriteria { MinProfitAmount = 20000 };
        var result = _service.FilterItems(items, criteria);

        Assert.Single(result);
        Assert.Equal(50000, result[0].RawProfit);
    }

    [Fact]
    public void FilterItems_ShouldFilterByDemand()
    {
        var items = new List<ProfitCalculation>
        {
            CreateMockItem(10000, 20, 1, 1, 10, PriceTrend.Stable), // Slow
            CreateMockItem(10000, 20, 10, 1, 10, PriceTrend.Stable) // Fast
        };

        var criteria = new FilterCriteria { MinSaleVelocity = 5 };
        var result = _service.FilterItems(items, criteria);

        Assert.Single(result);
        Assert.Equal(10, result[0].MarketData?.SaleVelocity);
    }

    [Fact]
    public void FilterItems_ShouldFilterByRisk()
    {
        var items = new List<ProfitCalculation>
        {
            CreateMockItem(10000, 20, 5, 1, 10, PriceTrend.Stable), // Low Risk
            CreateMockItem(10000, 20, 5, 1, 80, PriceTrend.Stable)  // High Risk
        };

        var criteria = new FilterCriteria { MaxRiskScore = 50 };
        var result = _service.FilterItems(items, criteria);

        Assert.Single(result);
        Assert.Equal(10, result[0].RiskScore);
    }

    [Fact]
    public void SortItems_ShouldSortByRecommendationScore()
    {
        var item1 = CreateMockItem(10000, 20, 5, 1, 10, PriceTrend.Stable);
        item1.RecommendationScore = 50;
        
        var item2 = CreateMockItem(10000, 20, 5, 1, 10, PriceTrend.Stable);
        item2.RecommendationScore = 90;

        var items = new List<ProfitCalculation> { item1, item2 };
        var result = _service.SortItems(items, SortStrategy.RecommendationScore);

        Assert.Equal(90, result[0].RecommendationScore);
        Assert.Equal(50, result[1].RecommendationScore);
    }
}
