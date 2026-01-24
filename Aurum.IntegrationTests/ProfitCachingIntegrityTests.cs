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

/// <summary>
/// Integration tests to ensure profit calculations are correctly cached and restored
/// with all critical fields intact. Tests the full pipeline: calculate -> cache -> load.
/// </summary>
public class ProfitCachingIntegrityTests : IDisposable
{
    private readonly Mock<IPluginLog> _mockLog;
    private readonly Configuration _mockConfig;
    private readonly Mock<RecipeService> _mockRecipeService;
    private readonly Mock<UniversalisService> _mockUniversalisService;
    private readonly Mock<MarketAnalysisService> _mockMarketAnalysisService;
    private readonly ProfitService _profitService;
    private readonly DatabaseService _databaseService;
    private readonly string _testDbPath;

    public ProfitCachingIntegrityTests()
    {
        _mockLog = new Mock<IPluginLog>();
        _mockConfig = new Configuration();
        _mockRecipeService = new Mock<RecipeService>(MockBehavior.Loose, null!, null!);
        _mockUniversalisService = new Mock<UniversalisService>(MockBehavior.Loose, null!, null!, null!);
        _mockMarketAnalysisService = new Mock<MarketAnalysisService>(MockBehavior.Loose, null!);

        // Setup default config
        _mockConfig.DefaultCostMode = CostMode.MarketBoard;
        _mockConfig.IncludeMarketTax = true;
        _mockConfig.DefaultCraftingTimeSeconds = 20;

        _profitService = new ProfitService(
            _mockLog.Object,
            _mockConfig,
            _mockRecipeService.Object,
            _mockUniversalisService.Object,
            _mockMarketAnalysisService.Object
        );

        // Create temp database for testing
        _testDbPath = Path.Combine(Path.GetTempPath(), $"aurum_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDbPath);
        _databaseService = new DatabaseService(_mockLog.Object, _testDbPath);
    }

    [Fact]
    public async Task ProfitCalculation_WhenCached_PreservesAllCriticalFields()
    {
        // Arrange - Create a profitable recipe
        var recipe = new RecipeData
        {
            RecipeId = 1001,
            ResultItemId = 5001,
            ItemName = "Titanium Ingot",
            ClassJobLevel = 80,
            CraftingClassName = "Blacksmith",
            ResultAmount = 1,
            EstimatedCraftTimeSeconds = 25,
            Ingredients = new List<RecipeIngredient>
            {
                new RecipeIngredient { ItemId = 6001, ItemName = "Titanium Ore", AmountNeeded = 4 }
            }
        };

        var marketData = new MarketData
        {
            ItemId = 5001,
            WorldName = "TestWorld",
            MinPrice = 50000,
            CurrentAveragePriceNQ = 55000,
            Listings = new List<MarketListing>
            {
                new MarketListing { PricePerUnit = 50000, Quantity = 10, RetainerName = "TestRetainer" }
            },
            RecentHistory = new List<SaleRecord>
            {
                new SaleRecord { PricePerUnit = 49000, Quantity = 2, Timestamp = DateTime.UtcNow.AddHours(-1) },
                new SaleRecord { PricePerUnit = 51000, Quantity = 1, Timestamp = DateTime.UtcNow.AddHours(-2) }
            },
            SaleVelocity = 15.5f,
            EstimatedSellTimeDays = 0.2f
        };

        var ingredientMarketData = new MarketData
        {
            ItemId = 6001,
            MinPrice = 2500,
            CurrentAveragePriceNQ = 2800
        };

        _mockUniversalisService
            .Setup(s => s.GetMarketDataAsync("TestWorld", 5001))
            .ReturnsAsync(marketData);

        _mockUniversalisService
            .Setup(s => s.GetMarketDataAsync("TestWorld", 6001))
            .ReturnsAsync(ingredientMarketData);

        _mockRecipeService
            .Setup(s => s.GetRecipe(1001))
            .Returns(recipe);

        // Act - Calculate profit
        var calculatedProfit = await _profitService.CalculateProfitAsync(recipe, "TestWorld");

        // Assert - Verify calculation is sensible
        Assert.NotNull(calculatedProfit);
        Assert.True(calculatedProfit.ExpectedSalePrice > 0, "Expected sale price should be set");
        Assert.True(calculatedProfit.TotalCraftCost > 0, "Total craft cost should be calculated");
        Assert.Equal(49999u, calculatedProfit.ExpectedSalePrice); // Undercut by 1
        Assert.Equal(10000u, calculatedProfit.TotalCraftCost); // 4 * 2500
        
        // Calculate expected values
        var expectedTax = _mockConfig.IncludeMarketTax ? (uint)(calculatedProfit.ExpectedSalePrice * 0.05) : 0u;
        var expectedNetSale = calculatedProfit.ExpectedSalePrice - expectedTax;
        var expectedProfit = (int)(expectedNetSale - calculatedProfit.TotalCraftCost);

        Assert.Equal(expectedTax, calculatedProfit.MarketBoardTax);
        Assert.Equal(expectedNetSale, calculatedProfit.NetSalePrice);
        Assert.Equal(expectedProfit, calculatedProfit.RawProfit);
        Assert.True(calculatedProfit.RawProfit > 0, "Recipe should be profitable");
        Assert.True(calculatedProfit.ProfitMargin > 0, "Profit margin should be positive");
        Assert.True(calculatedProfit.GilPerHour > 0, "Gil per hour should be calculated");

        // Act - Cache the profit
        _databaseService.UpsertRecipeCache(recipe, calculatedProfit);

        // Act - Load from cache
        var cachedProfits = _databaseService.GetAllCachedProfits(maxAgeHours: 24, limit: 100, offset: 0);
        
        // Assert - Verify cache contains the record
        Assert.NotEmpty(cachedProfits);
        var (cachedRecipeId, cachedProfit, _) = cachedProfits.First(p => p.RecipeId == 1001);
        
        Assert.Equal(1001u, cachedRecipeId);
        
        // CRITICAL: These fields must be preserved
        Assert.Equal(calculatedProfit.ExpectedSalePrice, cachedProfit.ExpectedSalePrice);
        Assert.Equal(calculatedProfit.TotalCraftCost, cachedProfit.TotalCraftCost);
        Assert.Equal(calculatedProfit.MarketBoardTax, cachedProfit.MarketBoardTax);
        Assert.Equal(calculatedProfit.NetSalePrice, cachedProfit.NetSalePrice);
        Assert.Equal(calculatedProfit.RawProfit, cachedProfit.RawProfit);
        Assert.Equal(calculatedProfit.ProfitMargin, cachedProfit.ProfitMargin);
        Assert.Equal(calculatedProfit.GilPerHour, cachedProfit.GilPerHour);
        Assert.Equal(calculatedProfit.RecommendationScore, cachedProfit.RecommendationScore);
        Assert.Equal(calculatedProfit.RiskScore, cachedProfit.RiskScore);
    }

    [Fact]
    public async Task ProfitCalculation_UnprofitableRecipe_StillCachesCorrectly()
    {
        // Arrange - Create an unprofitable recipe (costs more than it sells for)
        var recipe = new RecipeData
        {
            RecipeId = 2001,
            ResultItemId = 5002,
            ItemName = "Overpriced Widget",
            ClassJobLevel = 50,
            CraftingClassName = "Goldsmith",
            ResultAmount = 1,
            EstimatedCraftTimeSeconds = 30,
            Ingredients = new List<RecipeIngredient>
            {
                new RecipeIngredient { ItemId = 6002, ItemName = "Expensive Material", AmountNeeded = 10 }
            }
        };

        var marketData = new MarketData
        {
            ItemId = 5002,
            WorldName = "TestWorld",
            MinPrice = 5000,
            CurrentAveragePriceNQ = 5500,
            Listings = new List<MarketListing>
            {
                new MarketListing { PricePerUnit = 5000, Quantity = 50, RetainerName = "TestRetainer" }
            }
        };

        var ingredientMarketData = new MarketData
        {
            ItemId = 6002,
            MinPrice = 800,
            CurrentAveragePriceNQ = 850
        };

        _mockUniversalisService
            .Setup(s => s.GetMarketDataAsync("TestWorld", 5002))
            .ReturnsAsync(marketData);

        _mockUniversalisService
            .Setup(s => s.GetMarketDataAsync("TestWorld", 6002))
            .ReturnsAsync(ingredientMarketData);

        _mockRecipeService
            .Setup(s => s.GetRecipe(2001))
            .Returns(recipe);

        // Act
        var calculatedProfit = await _profitService.CalculateProfitAsync(recipe, "TestWorld");

        // Assert - Verify unprofitable calculation
        Assert.NotNull(calculatedProfit);
        Assert.Equal(4999u, calculatedProfit.ExpectedSalePrice); // Undercut by 1
        Assert.Equal(8000u, calculatedProfit.TotalCraftCost); // 10 * 800
        Assert.True(calculatedProfit.RawProfit < 0, "Recipe should be unprofitable");

        // Cache and reload
        _databaseService.UpsertRecipeCache(recipe, calculatedProfit);
        var cachedProfits = _databaseService.GetAllCachedProfits(maxAgeHours: 24, limit: 100, offset: 0);
        var cached = cachedProfits.First(p => p.RecipeId == 2001).Profit;

        // Verify negative profit is preserved
        Assert.Equal(calculatedProfit.ExpectedSalePrice, cached.ExpectedSalePrice);
        Assert.Equal(calculatedProfit.TotalCraftCost, cached.TotalCraftCost);
        Assert.Equal(calculatedProfit.RawProfit, cached.RawProfit);
        Assert.True(cached.RawProfit < 0, "Cached profit should remain negative");
    }

    [Fact]
    public async Task ProfitCalculation_HighMarginRecipe_MarkersIndicateCraftingWorthwhile()
    {
        // Arrange - Create a highly profitable, fast-selling recipe
        var recipe = new RecipeData
        {
            RecipeId = 3001,
            ResultItemId = 5003,
            ItemName = "Popular Consumable",
            ClassJobLevel = 90,
            CraftingClassName = "Alchemist",
            ResultAmount = 3, // Makes 3 at a time
            EstimatedCraftTimeSeconds = 15,
            Ingredients = new List<RecipeIngredient>
            {
                new RecipeIngredient { ItemId = 6003, ItemName = "Cheap Herb", AmountNeeded = 2 }
            }
        };

        var marketData = new MarketData
        {
            ItemId = 5003,
            WorldName = "TestWorld",
            MinPrice = 15000,
            CurrentAveragePriceNQ = 16000,
            Listings = new List<MarketListing>
            {
                new MarketListing { PricePerUnit = 15000, Quantity = 5, RetainerName = "TestRetainer" }
            },
            RecentHistory = Enumerable.Range(0, 50).Select(i => new SaleRecord
            {
                PricePerUnit = 15000 + (uint)(i % 3) * 100,
                Quantity = (uint)(1 + i % 4),
                Timestamp = DateTime.UtcNow.AddHours(-i * 0.5)
            }).ToList(),
            SaleVelocity = 80f, // Very high velocity
            EstimatedSellTimeDays = 0.05f // Sells very quickly
        };

        var ingredientMarketData = new MarketData
        {
            ItemId = 6003,
            MinPrice = 100,
            CurrentAveragePriceNQ = 120
        };

        _mockUniversalisService
            .Setup(s => s.GetMarketDataAsync("TestWorld", 5003))
            .ReturnsAsync(marketData);

        _mockUniversalisService
            .Setup(s => s.GetMarketDataAsync("TestWorld", 6003))
            .ReturnsAsync(ingredientMarketData);

        _mockRecipeService
            .Setup(s => s.GetRecipe(3001))
            .Returns(recipe);

        // Act
        var calculatedProfit = await _profitService.CalculateProfitAsync(recipe, "TestWorld");

        // Assert - This should be marked as highly recommended
        Assert.NotNull(calculatedProfit);
        Assert.True(calculatedProfit.RawProfit > 10000, "Should have substantial profit");
        Assert.True(calculatedProfit.ProfitMargin > 50, "Should have high margin");
        Assert.True(calculatedProfit.GilPerHour > 100000, "Should have excellent gil/hour");
        Assert.True(calculatedProfit.RecommendationScore > 50, "Should be highly recommended");
        Assert.True(calculatedProfit.RiskScore < 50, "Should be low risk with high velocity");

        // Cache and verify all quality indicators are preserved
        _databaseService.UpsertRecipeCache(recipe, calculatedProfit);
        var cachedProfits = _databaseService.GetAllCachedProfits(maxAgeHours: 24, limit: 100, offset: 0);
        var cached = cachedProfits.First(p => p.RecipeId == 3001).Profit;

        Assert.Equal(calculatedProfit.RecommendationScore, cached.RecommendationScore);
        Assert.Equal(calculatedProfit.RiskScore, cached.RiskScore);
        Assert.Equal(calculatedProfit.GilPerHour, cached.GilPerHour);
        Assert.Equal(calculatedProfit.ProfitMargin, cached.ProfitMargin);
    }

    [Fact]
    public void CachedProfit_WithZeroValues_DoesNotDisplayCorrectly()
    {
        // Arrange - Simulate the bug: cached data with missing fields
        var recipe = new RecipeData
        {
            RecipeId = 9999,
            ResultItemId = 9999,
            ItemName = "Bugged Item",
            Ingredients = new List<RecipeIngredient>()
        };

        // Create a profit calculation with all fields properly set
        var fullProfit = new ProfitCalculation
        {
            Recipe = recipe,
            ExpectedSalePrice = 90000,
            TotalCraftCost = 4537,
            MarketBoardTax = 4500,
            NetSalePrice = 85500,
            RawProfit = 80963,
            ProfitMargin = 90,
            GilPerHour = 14573520,
            RecommendationScore = 75,
            RiskScore = 25
        };

        // Cache it
        _databaseService.UpsertRecipeCache(recipe, fullProfit);

        // Load it back
        var cachedProfits = _databaseService.GetAllCachedProfits(maxAgeHours: 24, limit: 100, offset: 0);
        var cached = cachedProfits.First(p => p.RecipeId == 9999).Profit;

        // Assert - The bug would cause these to be 0
        Assert.NotEqual(0u, cached.ExpectedSalePrice);
        Assert.NotEqual(0u, cached.TotalCraftCost);
        
        // These should match exactly
        Assert.Equal(fullProfit.ExpectedSalePrice, cached.ExpectedSalePrice);
        Assert.Equal(fullProfit.TotalCraftCost, cached.TotalCraftCost);
        Assert.Equal(fullProfit.RawProfit, cached.RawProfit);
        
        // Verify the math is consistent
        var expectedProfit = (int)(cached.NetSalePrice - cached.TotalCraftCost);
        Assert.Equal(expectedProfit, cached.RawProfit);
    }

    public void Dispose()
    {
        _databaseService?.Dispose();
        
        if (Directory.Exists(_testDbPath))
        {
            try
            {
                Directory.Delete(_testDbPath, true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
