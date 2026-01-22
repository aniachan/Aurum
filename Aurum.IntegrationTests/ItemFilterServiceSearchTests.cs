using Aurum.Models;
using Aurum.Services.Filtering;
using Xunit;
using System.Collections.Generic;

namespace Aurum.IntegrationTests;

public class ItemFilterServiceSearchTests
{
    private ItemFilterService _filterService;
    private FilterCriteria _criteria;
    private Configuration _configuration;

    public ItemFilterServiceSearchTests()
    {
        _configuration = new Configuration();
        _filterService = new ItemFilterService(_configuration);
        _criteria = new FilterCriteria();
        
        // Ensure we don't get filtered out by other things
        // _criteria.IncludeUntradeable = true; // Removed property
        _criteria.ExcludeUntradable = false; // Set to false to include untradeables
        _criteria.IncludeCrafted = true;
        // Search filter works on Recipe.ItemName, so we need Recipe populated.
        // And IsDataComplete must be true.
    }

    [Fact]
    public void FilterItems_ShouldFilterByName_WhenSearchStringProvided()
    {
        // Arrange
        _criteria.NameSearch = "Iron";
        var items = new List<ProfitCalculation>
        {
            new() { 
                Recipe = new RecipeData { ItemName = "Iron Ingot", RecipeId = 1, ClassJobLevel = 1, RecipeLevel = 1, ItemLevel = 1 }, 
                IsDataComplete = true, 
                MarketData = new MarketData() 
            },
            new() { 
                Recipe = new RecipeData { ItemName = "Bronze Ingot", RecipeId = 2, ClassJobLevel = 1, RecipeLevel = 1, ItemLevel = 1 }, 
                IsDataComplete = true, 
                MarketData = new MarketData() 
            },
            new() { 
                Recipe = new RecipeData { ItemName = "High Iron Ingot", RecipeId = 3, ClassJobLevel = 1, RecipeLevel = 1, ItemLevel = 1 }, 
                IsDataComplete = true, 
                MarketData = new MarketData() 
            }
        };

        // Act
        var result = _filterService.FilterItems(items, _criteria);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.Recipe.ItemName == "Iron Ingot");
        Assert.Contains(result, x => x.Recipe.ItemName == "High Iron Ingot");
        Assert.DoesNotContain(result, x => x.Recipe.ItemName == "Bronze Ingot");
    }

    [Fact]
    public void FilterItems_ShouldBeCaseInsensitive()
    {
        // Arrange
        _criteria.NameSearch = "iron";
        var items = new List<ProfitCalculation>
        {
            new() { 
                Recipe = new RecipeData { ItemName = "Iron Ingot", RecipeId = 1, ClassJobLevel = 1, RecipeLevel = 1, ItemLevel = 1 }, 
                IsDataComplete = true, 
                MarketData = new MarketData() 
            }
        };

        // Act
        var result = _filterService.FilterItems(items, _criteria);

        // Assert
        Assert.Single(result);
    }
    
    [Fact]
    public void FilterItems_ShouldIncludeAll_WhenSearchIsEmpty()
    {
        // Arrange
        _criteria.NameSearch = "";
        var items = new List<ProfitCalculation>
        {
            new() { 
                Recipe = new RecipeData { ItemName = "Iron Ingot", RecipeId = 1, ClassJobLevel = 1, RecipeLevel = 1, ItemLevel = 1 }, 
                IsDataComplete = true, 
                MarketData = new MarketData() 
            },
            new() { 
                Recipe = new RecipeData { ItemName = "Bronze Ingot", RecipeId = 2, ClassJobLevel = 1, RecipeLevel = 1, ItemLevel = 1 }, 
                IsDataComplete = true, 
                MarketData = new MarketData() 
            }
        };

        // Act
        var result = _filterService.FilterItems(items, _criteria);

        // Assert
        Assert.Equal(2, result.Count);
    }
}
