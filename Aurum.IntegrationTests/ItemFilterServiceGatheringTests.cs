using Aurum.Models;
using Aurum.Services.Filtering;
using Xunit;
using System.Collections.Generic;
using Aurum;

namespace Aurum.IntegrationTests;

public class ItemFilterServiceGatheringTests
{
    private ItemFilterService _filterService;
    private FilterCriteria _criteria;
    private Configuration _config;

    public ItemFilterServiceGatheringTests()
    {
        _config = new Configuration();
        _filterService = new ItemFilterService(_config);
        _criteria = new FilterCriteria();
        _criteria.IncludeCraftingGatheringGear = true;
    }

    [Fact]
    public void FilterItems_ShouldIncludeGatheringGear_WhenFlagIsTrue()
    {
        // Arrange
        _criteria.IncludeCraftingGatheringGear = true;
        var items = new List<ProfitCalculation>
        {
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Gathering, ItemName = "Gathering Item" }, RawProfit = 1000, IsDataComplete = true }
        };

        // Act
        var result = _filterService.FilterItems(items, _criteria);

        // Assert
        Assert.Single(result);
        Assert.Equal(ItemMainCategory.Gathering, result[0].Recipe.MainCategory);
    }

    [Fact]
    public void FilterItems_ShouldExcludeGatheringGear_WhenFlagIsFalse()
    {
        // Arrange
        _criteria.IncludeCraftingGatheringGear = false;
        var items = new List<ProfitCalculation>
        {
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Gathering, ItemName = "Gathering Item" }, RawProfit = 1000, IsDataComplete = true },
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Crafting, ItemName = "Crafting Item" }, RawProfit = 1000, IsDataComplete = true }
        };

        // Act
        var result = _filterService.FilterItems(items, _criteria);

        // Assert
        Assert.Empty(result);
    }
}
