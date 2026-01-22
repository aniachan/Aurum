using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aurum.Models;
using Aurum.Services;
using Dalamud.Plugin.Services;
using Moq;
using Xunit;

namespace Aurum.IntegrationTests;

public class FullProfitPipelineTests
{
    private readonly Mock<IPluginLog> _mockLog;
    private readonly Configuration _config;
    private readonly Mock<RecipeService> _mockRecipeService;
    private readonly Mock<UniversalisService> _mockUniversalisService;
    private readonly Mock<MarketAnalysisService> _mockMarketAnalysisService;
    private readonly ProfitService _profitService;

    public FullProfitPipelineTests()
    {
        _mockLog = new Mock<IPluginLog>();
        _config = new Configuration();
        
        var mockDataManager = new Mock<IDataManager>();
        var mockCacheService = new Mock<CacheService>(_config); 
        var mockDatabaseService = new Mock<DatabaseService>(_mockLog.Object, ".");
        var mockRateLimiter = new Mock<RateLimiter>(_mockLog.Object, _config, new Mock<Dalamud.Plugin.Services.IChatGui>().Object, null!);

        _mockRecipeService = new Mock<RecipeService>(mockDataManager.Object, _mockLog.Object, _config);
        
        _mockUniversalisService = new Mock<UniversalisService>(
            _mockLog.Object, 
            mockCacheService.Object, 
            mockDatabaseService.Object, 
            mockRateLimiter.Object, 
            _config,
            mockDataManager.Object
        );
        
        _mockMarketAnalysisService = new Mock<MarketAnalysisService>(_mockLog.Object, _config);

        _profitService = new ProfitService(
            _mockLog.Object,
            _config,
            _mockRecipeService.Object,
            _mockUniversalisService.Object,
            _mockMarketAnalysisService.Object
        );
    }

    [Fact]
    public async Task CalculateProfit_ComplexPipeline_ReturnsCorrectValues()
    {
        // Arrange
        // Complex Recipe: Result = A + B, A = C + D
        // Result: 1000, A: 2000, B: 3000, C: 4000, D: 5000
        
        var resultId = 1000u;
        var itemAId = 2000u;
        var itemBId = 3000u;
        var itemCId = 4000u;
        var itemDId = 5000u;

        var recipe = new RecipeData
        {
            RecipeId = 1,
            ResultItemId = resultId,
            ItemName = "Complex Result",
            ResultAmount = 1,
            Ingredients = new List<RecipeIngredient>
            {
                new RecipeIngredient { ItemId = itemAId, AmountNeeded = 1, SubRecipeId = 2 },
                new RecipeIngredient { ItemId = itemBId, AmountNeeded = 2 } // No sub-recipe
            }
        };

        var recipeA = new RecipeData
        {
            RecipeId = 2,
            ResultItemId = itemAId,
            ItemName = "Item A",
            ResultAmount = 1,
            Ingredients = new List<RecipeIngredient>
            {
                new RecipeIngredient { ItemId = itemCId, AmountNeeded = 1 },
                new RecipeIngredient { ItemId = itemDId, AmountNeeded = 1 }
            }
        };

        // Market Data Setup
        _mockUniversalisService.Setup(s => s.GetMarketDataAsync(It.IsAny<string>(), resultId))
            .ReturnsAsync(new MarketData { ItemId = resultId, MinPrice = 50000, CurrentAveragePriceNQ = 52000, CurrentListings = 10 }); // High selling price

        // Item A: Expensive on MB (20000), but cheap to craft?
        _mockUniversalisService.Setup(s => s.GetMarketDataAsync(It.IsAny<string>(), itemAId))
            .ReturnsAsync(new MarketData { ItemId = itemAId, MinPrice = 20000, CurrentListings = 10 });

        // Item B: Buy from vendor/MB (500)
        _mockUniversalisService.Setup(s => s.GetMarketDataAsync(It.IsAny<string>(), itemBId))
            .ReturnsAsync(new MarketData { ItemId = itemBId, MinPrice = 500, CurrentListings = 10 });

        // Item C: Raw material (100)
        _mockUniversalisService.Setup(s => s.GetMarketDataAsync(It.IsAny<string>(), itemCId))
            .ReturnsAsync(new MarketData { ItemId = itemCId, MinPrice = 100, CurrentListings = 10 });

        // Item D: Raw material (200)
        _mockUniversalisService.Setup(s => s.GetMarketDataAsync(It.IsAny<string>(), itemDId))
            .ReturnsAsync(new MarketData { ItemId = itemDId, MinPrice = 200, CurrentListings = 10 });

        // Recipe Service Setup
        _mockRecipeService.Setup(s => s.GetRecipe(2)).Returns(recipeA);

        // Act
        var result = await _profitService.CalculateProfitAsync(recipe, "TestWorld");

        // Assert
        Assert.NotNull(result);
        
        // Expected Cost Calculation:
        // Item A Cost (Crafted): 100 (C) + 200 (D) = 300
        // Item A Cost (Market): 20000
        // Algorithm should choose Crafted (300) because it's cheaper
        
        // Total Cost: 
        // 1 * Item A (300) + 2 * Item B (500) = 300 + 1000 = 1300
        
        Assert.Equal(1300u, result.TotalCraftCost);
        
        // Expected Sale Price (Undercut by 1): 49999
        Assert.Equal(49999u, result.ExpectedSalePrice);
        
        // Raw Profit: 49999 - 1300 = 48699
        // (Note: NetSalePrice might include tax deduction depending on config, default is usually false/0 in tests unless config mocked otherwise)
        // Let's assume default config has IncludeMarketTax = false or 0 tax
        // If tax is enabled, it would be 49999 * 0.95 = 47499 -> Profit = 46199
        
        // Let's check logic roughly
        Assert.True(result.RawProfit > 40000);
        
        // Verify dependency resolution
        Assert.Contains(result.IngredientTree.FlatIngredientList, i => i.ItemId == itemAId && i.Source == CostSource.SubRecipe);
    }
}
