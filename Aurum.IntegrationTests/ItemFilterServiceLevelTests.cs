using System;
using System.Collections.Generic;
using Aurum.Models;
using Aurum.Services.Filtering;
using Xunit;
using Aurum;

namespace Aurum.IntegrationTests.Services.Filtering;

public class ItemFilterServiceLevelTests
{
    private readonly ItemFilterService _service;
    private readonly Configuration _config;

    public ItemFilterServiceLevelTests()
    {
        _config = new Configuration();
        _service = new ItemFilterService(_config);
    }

    private ProfitCalculation CreateMockItemWithRecipe(int classJobLevel, int recipeLevel)
    {
        return new ProfitCalculation
        {
            IsDataComplete = true,
            RawProfit = 10000,
            ProfitMargin = 20,
            Recipe = new RecipeData
            {
                ClassJobLevel = classJobLevel,
                RecipeLevel = recipeLevel,
                MainCategory = ItemMainCategory.Combat // Default valid category
            },
            MarketData = new MarketData()
        };
    }

    [Fact]
    public void FilterItems_ShouldFilterByJobLevel()
    {
        var items = new List<ProfitCalculation>
        {
            CreateMockItemWithRecipe(80, 500), // Too low
            CreateMockItemWithRecipe(90, 600), // Just right
            CreateMockItemWithRecipe(100, 700) // Too high
        };

        var criteria = new FilterCriteria 
        { 
            MinJobLevel = 85,
            MaxJobLevel = 95
        };

        var result = _service.FilterItems(items, criteria);

        Assert.Single(result);
        Assert.Equal(90, result[0].Recipe.ClassJobLevel);
    }

    [Fact]
    public void FilterItems_ShouldFilterByRecipeLevel()
    {
        var items = new List<ProfitCalculation>
        {
            CreateMockItemWithRecipe(90, 500), // Too low
            CreateMockItemWithRecipe(90, 580), // Just right
            CreateMockItemWithRecipe(90, 700)  // Too high
        };

        var criteria = new FilterCriteria 
        { 
            MinRecipeLevel = 550,
            MaxRecipeLevel = 600
        };

        var result = _service.FilterItems(items, criteria);

        Assert.Single(result);
        Assert.Equal(580, result[0].Recipe.RecipeLevel);
    }

    [Fact]
    public void FilterItems_ShouldAllowItemsWithoutRecipe_IfCriteraIgnored_OrFail()
    {
        // Decision: If an item has no recipe (e.g. gathered item?) but we are filtering by levels, 
        // effectively we can't determine its level, so it should probably fail or be handled carefully.
        // Current implementation: Checks `if (item.Recipe != null)`. 
        // If item.Recipe is null, it skips the level checks and returns true (passes).
        // Let's verify this behavior.
        
        var items = new List<ProfitCalculation>
        {
            new ProfitCalculation
            {
                IsDataComplete = true,
                RawProfit = 1000,
                Recipe = null!, // No recipe, intentionally null for test
                MarketData = new MarketData()
            }
        };

        var criteria = new FilterCriteria 
        { 
            MinJobLevel = 85,
            MaxJobLevel = 95
        };

        var result = _service.FilterItems(items, criteria);

        // Since it skips the check, it should remain.
        Assert.Single(result);
    }
}
