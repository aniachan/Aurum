using Aurum.Models;
using Aurum.Services.Filtering;
using Xunit;
using System.Collections.Generic;

namespace Aurum.UnitTests.Services.Filtering;

public class ItemFilterServiceCategoryTests
{
    private ItemFilterService _filterService;
    private FilterCriteria _criteria;

    public ItemFilterServiceCategoryTests()
    {
        _filterService = new ItemFilterService();
        _criteria = new FilterCriteria();
        // Reset defaults if needed
        _criteria.IncludeCombatGear = true;
        _criteria.IncludeCraftingGatheringGear = true;
    }

    [Fact]
    public void FilterItems_ShouldExcludeCombatGear_WhenFlagIsFalse()
    {
        // Arrange
        _criteria.IncludeCombatGear = false;
        var items = new List<ProfitCalculation>
        {
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Combat, ItemName = "Combat Item" }, RawProfit = 1000, IsDataComplete = true },
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Crafting, ItemName = "Crafting Item" }, RawProfit = 1000, IsDataComplete = true },
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Consumable, ItemName = "Food" }, RawProfit = 1000, IsDataComplete = true }
        };

        // Act
        var result = _filterService.FilterItems(items, _criteria);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, x => x.Recipe.MainCategory == ItemMainCategory.Combat);
    }

    [Fact]
    public void FilterItems_ShouldExcludeCraftingGear_WhenFlagIsFalse()
    {
        // Arrange
        _criteria.IncludeCraftingGatheringGear = false;
        var items = new List<ProfitCalculation>
        {
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Combat, ItemName = "Combat Item" }, RawProfit = 1000, IsDataComplete = true },
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Crafting, ItemName = "Crafting Item" }, RawProfit = 1000, IsDataComplete = true },
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Consumable, ItemName = "Food" }, RawProfit = 1000, IsDataComplete = true }
        };

        // Act
        var result = _filterService.FilterItems(items, _criteria);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, x => x.Recipe.MainCategory == ItemMainCategory.Crafting);
    }
    
    [Fact]
    public void FilterItems_ShouldIncludeAll_WhenFlagsAreTrue()
    {
        // Arrange
        _criteria.IncludeCombatGear = true;
        _criteria.IncludeCraftingGatheringGear = true;
        var items = new List<ProfitCalculation>
        {
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Combat }, RawProfit = 1000, IsDataComplete = true },
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Crafting }, RawProfit = 1000, IsDataComplete = true }
        };

        // Act
        var result = _filterService.FilterItems(items, _criteria);

        // Assert
        Assert.Equal(2, result.Count);
    }
}
