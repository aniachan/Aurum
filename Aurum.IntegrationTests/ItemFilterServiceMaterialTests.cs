using Aurum.Models;
using Aurum.Services.Filtering;
using Xunit;
using System.Collections.Generic;
using Aurum;

namespace Aurum.UnitTests.Services.Filtering;

public class ItemFilterServiceMaterialTests
{
    private ItemFilterService _filterService;
    private FilterCriteria _criteria;
    private Configuration _config;

    public ItemFilterServiceMaterialTests()
    {
        _config = new Configuration();
        _filterService = new ItemFilterService(_config);
        _criteria = new FilterCriteria();
        // Reset defaults
        _criteria.IncludeMaterials = true;
    }

    [Fact]
    public void FilterItems_ShouldExcludeMaterials_WhenFlagIsFalse()
    {
        // Arrange
        _criteria.IncludeMaterials = false;
        var items = new List<ProfitCalculation>
        {
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Material, ItemName = "Ingot", ClassJobLevel = 90, RecipeLevel = 580, ItemLevel = 580 }, RawProfit = 1000, IsDataComplete = true },
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Combat, ItemName = "Sword", ClassJobLevel = 90, RecipeLevel = 580, ItemLevel = 580 }, RawProfit = 1000, IsDataComplete = true }
        };

        // Act
        var result = _filterService.FilterItems(items, _criteria);

        // Assert
        Assert.Single(result);
        Assert.DoesNotContain(result, x => x.Recipe.MainCategory == ItemMainCategory.Material);
    }
    
    [Fact]
    public void FilterItems_ShouldIncludeMaterials_WhenFlagIsTrue()
    {
        // Arrange
        _criteria.IncludeMaterials = true;
        var items = new List<ProfitCalculation>
        {
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Material, ItemName = "Ingot", ClassJobLevel = 90, RecipeLevel = 580, ItemLevel = 580 }, RawProfit = 1000, IsDataComplete = true },
            new() { Recipe = new RecipeData { MainCategory = ItemMainCategory.Combat, ItemName = "Sword", ClassJobLevel = 90, RecipeLevel = 580, ItemLevel = 580 }, RawProfit = 1000, IsDataComplete = true }
        };

        // Act
        var result = _filterService.FilterItems(items, _criteria);

        // Assert
        Assert.Equal(2, result.Count);
    }
}
