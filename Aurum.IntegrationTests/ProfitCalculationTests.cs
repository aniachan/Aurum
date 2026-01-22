using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aurum.Models;
using Aurum.Services;
using Dalamud.Plugin.Services;
using Moq;
using Xunit;

namespace Aurum.IntegrationTests;

public class ProfitCalculationTests
{
    private readonly Mock<IPluginLog> _mockLog;
    private readonly Configuration _config;
    private readonly Mock<RecipeService> _mockRecipeService;
    private readonly Mock<UniversalisService> _mockUniversalisService;
    private readonly Mock<MarketAnalysisService> _mockMarketAnalysisService;
    private readonly ProfitService _profitService;

    public ProfitCalculationTests()
    {
        _mockLog = new Mock<IPluginLog>();
        _config = new Configuration();
        
        var mockDataManager = new Mock<IDataManager>();
        var mockCacheService = new Mock<CacheService>(_config); 
        var mockDatabaseService = new Mock<DatabaseService>(_mockLog.Object, "."); // Pass plugin dir instead of config + null string
        var mockRateLimiter = new Mock<RateLimiter>(_mockLog.Object, _config, new Mock<Dalamud.Plugin.Services.IChatGui>().Object, null!);

        
        // We can't easily partial mock UniversalisService without parameterless constructor or matching args
        // But we can create a mock of it, but we need to supply constructor arguments
        // Wait, if we Mock<UniversalisService>, we are creating a subclass proxy.
        // It needs to call the base constructor.
        
        // Let's just create a real one with mocked dependencies? 
        // No, we want to mock its methods (GetMarketDataAsync).
        
        // For partial mocks of classes with constructor args, we pass args to Mock constructor
        
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
    public async Task CalculateProfit_HandlesZeroMarketPrice_Gracefully()
    {
        // Arrange
        var recipe = new RecipeData
        {
            RecipeId = 1,
            ResultItemId = 1001,
            ItemName = "Test Item",
            Ingredients = new List<RecipeIngredient>
            {
                new RecipeIngredient { ItemId = 2001, AmountNeeded = 1 }
            }
        };

        var marketData = new MarketData
        {
            ItemId = 1001,
            MinPrice = 0,
            CurrentAveragePriceNQ = 0,
            CurrentAveragePriceHQ = 0,
            Listings = new List<MarketListing>()
        };

        _mockUniversalisService
            .Setup(s => s.GetMarketDataAsync(It.IsAny<string>(), 1001))
            .ReturnsAsync(marketData);

        _mockUniversalisService
            .Setup(s => s.GetMarketDataAsync(It.IsAny<string>(), 2001))
            .ReturnsAsync(new MarketData { ItemId = 2001, MinPrice = 100 }); // Ingredient has cost

        // Act
        var result = await _profitService.CalculateProfitAsync(recipe, "TestWorld");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0u, result.ExpectedSalePrice); // Should handle zero price (uint 0)
        // Profit can be 0 or negative depending on config (tax/fees). If price is 0, profit is -100
        Assert.True(result.RawProfit <= 0); // Should be loss or zero since material cost > 0 and price is 0
    }

    [Fact]
    public async Task CalculateProfit_HandlesCycleInIngredients()
    {
        // Arrange
        // Item A needs Item B, Item B needs Item A
        var recipeA = new RecipeData
        {
            RecipeId = 1,
            ResultItemId = 1000,
            ItemName = "Item A",
            Ingredients = new List<RecipeIngredient>
            {
                new RecipeIngredient { ItemId = 2000, AmountNeeded = 1, SubRecipeId = 2 }
            }
        };

        var recipeB = new RecipeData
        {
            RecipeId = 2,
            ResultItemId = 2000,
            ItemName = "Item B",
            Ingredients = new List<RecipeIngredient>
            {
                new RecipeIngredient { ItemId = 1000, AmountNeeded = 1, SubRecipeId = 1 }
            }
        };

        _mockUniversalisService
            .Setup(s => s.GetMarketDataAsync(It.IsAny<string>(), It.IsAny<uint>()))
            .ReturnsAsync((string w, uint id) => new MarketData { ItemId = id, MinPrice = 500 });

        _mockRecipeService
            .Setup(s => s.GetRecipe(1))
            .Returns(recipeA);
        
        _mockRecipeService
            .Setup(s => s.GetRecipe(2))
            .Returns(recipeB);

        // Act
        var result = await _profitService.CalculateProfitAsync(recipeA, "TestWorld");

        // Assert
        Assert.NotNull(result);
        // Should break cycle and use market price for the recursive dependency
        Assert.Contains(result.IngredientTree.FlatIngredientList, i => i.ItemId == 1000 || i.ItemId == 2000);
    }
    
    [Fact]
    public async Task CalculateProfit_HandlesMissingMarketData()
    {
        // Arrange
        var recipe = new RecipeData { ResultItemId = 9999 };
        
        _mockUniversalisService
            .Setup(s => s.GetMarketDataAsync(It.IsAny<string>(), 9999))
            .ReturnsAsync((MarketData?)null);

        // Act
        var result = await _profitService.CalculateProfitAsync(recipe, "TestWorld");

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsDataComplete);
        Assert.Contains(result.Warnings, w => w.Message.Contains("No market data"));
    }

    [Fact]
    public async Task CalculateProfit_HandlesExtremePrices()
    {
        // Arrange
        var recipe = new RecipeData
        {
            RecipeId = 1,
            ResultItemId = 1001,
            ItemName = "High Value Item",
            Ingredients = new List<RecipeIngredient>
            {
                new RecipeIngredient { ItemId = 2001, AmountNeeded = 1 }
            }
        };

        var highPrice = 999_999_999u;
        var marketData = new MarketData
        {
            ItemId = 1001,
            MinPrice = highPrice,
            CurrentAveragePriceNQ = highPrice,
            Listings = new List<MarketListing>()
        };

        _mockUniversalisService
            .Setup(s => s.GetMarketDataAsync(It.IsAny<string>(), 1001))
            .ReturnsAsync(marketData);

        _mockUniversalisService
            .Setup(s => s.GetMarketDataAsync(It.IsAny<string>(), 2001))
            .ReturnsAsync(new MarketData { ItemId = 2001, MinPrice = 100 });

        // Act
        var result = await _profitService.CalculateProfitAsync(recipe, "TestWorld");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ExpectedSalePrice > 900_000_000);
        Assert.True(result.RawProfit > 0);
    }

    [Fact]
    public async Task CalculateProfit_HandlesZeroCostIngredients()
    {
        // Arrange
        var recipe = new RecipeData
        {
            RecipeId = 1,
            ResultItemId = 1001,
            ItemName = "Free Craft",
            Ingredients = new List<RecipeIngredient>
            {
                new RecipeIngredient { ItemId = 2001, AmountNeeded = 1 }
            }
        };

        var marketData = new MarketData
        {
            ItemId = 1001,
            MinPrice = 1000,
            CurrentAveragePriceNQ = 1000,
            Listings = new List<MarketListing>()
        };

        _mockUniversalisService
            .Setup(s => s.GetMarketDataAsync(It.IsAny<string>(), 1001))
            .ReturnsAsync(marketData);

        // Ingredient cost is 0 (e.g. gathered yourself or dirt cheap)
        _mockUniversalisService
            .Setup(s => s.GetMarketDataAsync(It.IsAny<string>(), 2001))
            .ReturnsAsync(new MarketData { ItemId = 2001, MinPrice = 0 });

        // Act
        var result = await _profitService.CalculateProfitAsync(recipe, "TestWorld");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0u, result.TotalCraftCost);
        // Expected sale price is undercut by 1 (999) if > 1
        Assert.Equal(999u, result.ExpectedSalePrice);
        Assert.True(result.ProfitMargin > 90); // Should be near 100% margin
    }
}
