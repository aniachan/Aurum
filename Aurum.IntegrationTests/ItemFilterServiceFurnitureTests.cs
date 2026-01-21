using Aurum.Models;
using Aurum.Services.Filtering;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using Aurum;

namespace Aurum.IntegrationTests;

public class ItemFilterServiceFurnitureTests
{
    private ItemFilterService _filterService;
    private FilterCriteria _criteria;
    private Configuration _config;

    public ItemFilterServiceFurnitureTests()
    {
        _config = new Configuration();
        _filterService = new ItemFilterService(_config);
        _criteria = new FilterCriteria();
        // Reset defaults
        _criteria.IncludeFurniture = true;
    }

    [Fact]
    public void FilterItems_ShouldExcludeFurniture_WhenFlagIsFalse()
    {
        // Arrange
        _criteria.IncludeFurniture = false;
        var items = new List<ProfitCalculation>
        {
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Furniture, ItemName = "Table", ClassJobLevel = 90, RecipeLevel = 580, ItemLevel = 580 }, RawProfit = 1000, IsDataComplete = true },
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Crafting, ItemName = "Hammer", ClassJobLevel = 90, RecipeLevel = 580, ItemLevel = 580 }, RawProfit = 1000, IsDataComplete = true },
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Consumable, ItemName = "Food", ClassJobLevel = 90, RecipeLevel = 580, ItemLevel = 580 }, RawProfit = 1000, IsDataComplete = true }
        };

        // Act
        var result = _filterService.FilterItems(items, _criteria);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, x => x.Recipe.MainCategory == ItemMainCategory.Furniture);
    }

    [Fact]
    public void FilterItems_ShouldIncludeFurniture_WhenFlagIsTrue()
    {
        // Arrange
        _criteria.IncludeFurniture = true;
        var items = new List<ProfitCalculation>
        {
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Furniture, ItemName = "Table", ClassJobLevel = 90, RecipeLevel = 580, ItemLevel = 580 }, RawProfit = 1000, IsDataComplete = true },
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Crafting, ItemName = "Hammer", ClassJobLevel = 90, RecipeLevel = 580, ItemLevel = 580 }, RawProfit = 1000, IsDataComplete = true }
        };

        // Act
        var result = _filterService.FilterItems(items, _criteria);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.Recipe.MainCategory == ItemMainCategory.Furniture);
    }
}
