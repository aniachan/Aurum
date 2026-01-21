using System;
using System.Collections.Generic;
using Aurum.Models;
using Aurum.Services.Filtering;
using Xunit;

namespace Aurum.IntegrationTests.Services.Filtering;

public class ItemFilterServiceConsumableTests
{
    private readonly ItemFilterService _service;
    private readonly Configuration _config;

    public ItemFilterServiceConsumableTests()
    {
        _config = new Configuration();
        _service = new ItemFilterService(_config);
    }

    private ProfitCalculation CreateMockItem(ItemMainCategory category)
    {
        return new ProfitCalculation
        {
            IsDataComplete = true,
            RawProfit = 1000,
            ProfitMargin = 20,
            ROI = 20,
            RiskScore = 10,
            RiskLevel = RiskLevel.Low,
            Recipe = new RecipeData
            {
                MainCategory = category,
                ClassJobLevel = 90,
                RecipeLevel = 580,
                ItemLevel = 580
            },
            MarketData = new MarketData
            {
                SaleVelocity = 10,
                SupplyDemandRatio = 1,
                Trend = PriceTrend.Stable,
                Warnings = new List<MarketWarningInfo>()
            }
        };
    }

    [Fact]
    public void FilterItems_ShouldFilterConsumables()
    {
        var items = new List<ProfitCalculation>
        {
            CreateMockItem(ItemMainCategory.Consumable),
            CreateMockItem(ItemMainCategory.Combat)
        };

        // Test exclusion
        var excludeCriteria = new FilterCriteria { IncludeConsumables = false };
        var excludeResult = _service.FilterItems(items, excludeCriteria);
        Assert.Single(excludeResult);
        Assert.Equal(ItemMainCategory.Combat, excludeResult[0].Recipe.MainCategory);

        // Test inclusion (default)
        var includeCriteria = new FilterCriteria { IncludeConsumables = true };
        var includeResult = _service.FilterItems(items, includeCriteria);
        Assert.Equal(2, includeResult.Count);
    }
}
