using System;
using System.Collections.Generic;
using Aurum;
using Aurum.Models;
using Aurum.Services.Filtering;
using Xunit;

namespace Aurum.IntegrationTests.Services.Filtering;

public class ItemFilterServiceFavoriteTests
{
    private readonly ItemFilterService _service;
    private readonly Configuration _config;

    public ItemFilterServiceFavoriteTests()
    {
        _config = new Configuration();
        _service = new ItemFilterService(_config);
    }

    private ProfitCalculation CreateMockItem(uint itemId)
    {
        return new ProfitCalculation
        {
            IsDataComplete = true,
            Recipe = new RecipeData { ResultItemId = itemId, ItemName = $"Item {itemId}", ClassJobLevel = 90, RecipeLevel = 580, ItemLevel = 580 },
            MarketData = new MarketData(),
            RawProfit = 1000 // Ensure it passes profit filter default
        };
    }

    [Fact]
    public void FilterItems_ShouldFilterByFavorites_WhenFlagIsTrue()
    {
        // Arrange
        _config.FavoriteItems = new List<uint> { 101, 103 };
        
        var items = new List<ProfitCalculation>
        {
            CreateMockItem(101), // Favorite
            CreateMockItem(102), // Not favorite
            CreateMockItem(103), // Favorite
            CreateMockItem(104)  // Not favorite
        };

        var criteria = new FilterCriteria { OnlyFavorites = true };

        // Act
        var result = _service.FilterItems(items, criteria);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, i => i.ItemId == 101);
        Assert.Contains(result, i => i.ItemId == 103);
        Assert.DoesNotContain(result, i => i.ItemId == 102);
    }

    [Fact]
    public void FilterItems_ShouldNotFilterByFavorites_WhenFlagIsFalse()
    {
        // Arrange
        _config.FavoriteItems = new List<uint> { 101 };
        
        var items = new List<ProfitCalculation>
        {
            CreateMockItem(101),
            CreateMockItem(102)
        };

        var criteria = new FilterCriteria { OnlyFavorites = false };

        // Act
        var result = _service.FilterItems(items, criteria);

        // Assert
        Assert.Equal(2, result.Count);
    }
}
