using System;
using System.Collections.Generic;
using System.Linq;
using Aurum.Models;
using Aurum.Services;
using Dalamud.Plugin.Services;
using Moq;
using Xunit;

namespace Aurum.IntegrationTests;

public static class ShoppingListTests
{
    public static void Run()
    {
        Console.WriteLine("Running ShoppingListTests...");
        TestRecursiveBreakdown();
        Console.WriteLine("ShoppingListTests Passed!");
    }

    private static void TestRecursiveBreakdown()
    {
        // Setup Mocks
        var mockDataManager = new Mock<IDataManager>();
        var mockLog = new Mock<IPluginLog>();
        var config = new Configuration();
        
        // Mock RecipeService
        var recipeService = new RecipeService(mockDataManager.Object, mockLog.Object, config);
        
        // Inject recipes into RecipeService cache via reflection
        var recipeCacheField = typeof(RecipeService).GetField("recipeCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (recipeCacheField == null) throw new Exception("Could not find recipeCache field");
        
        var cache = (Dictionary<uint, RecipeData>)recipeCacheField.GetValue(recipeService)!;

        // Define items
        uint finalProductId = 1000;
        uint intermediateId = 2000;
        uint rawMaterialId = 3000;
        uint crystalId = 4000;

        // Define recipes
        // Recipe 1: Final Product -> 1x Intermediate + 5x Crystal
        var finalRecipe = new RecipeData
        {
            RecipeId = 1,
            ResultItemId = finalProductId,
            Ingredients = new List<RecipeIngredient>
            {
                new RecipeIngredient 
                { 
                    ItemId = intermediateId, 
                    AmountNeeded = 1,
                    SubRecipeId = 2 // Links to intermediate recipe
                }
            },
            Crystals = new List<RecipeIngredient>
            {
                new RecipeIngredient { ItemId = crystalId, AmountNeeded = 5 }
            }
        };

        // Recipe 2: Intermediate -> 3x Raw Material
        var intermediateRecipe = new RecipeData
        {
            RecipeId = 2,
            ResultItemId = intermediateId,
            Ingredients = new List<RecipeIngredient>
            {
                new RecipeIngredient 
                { 
                    ItemId = rawMaterialId, 
                    AmountNeeded = 3
                }
            }
        };

        // Inject into cache
        cache[1] = finalRecipe;
        cache[2] = intermediateRecipe;

        // Mock UniversalisService (we don't care about prices for this test)
        // We can pass null for dependencies we don't use if we're careful, 
        // but ShoppingListService constructor requires them.
        // We'll create a dummy UniversalisService with mocked dependencies.
        var mockCache = new CacheService(new Mock<ICacheConfig>().Object);
        var mockDb = new Mock<DatabaseService>(mockLog.Object, ".");
        var mockRateLimiter = new Mock<RateLimiter>(mockLog.Object, config, new Mock<IChatGui>().Object, mockDb.Object);
        
        // We can't easily mock UniversalisService because it's a concrete class with internal logic in constructor.
        // However, we can just pass it. GetMarketData will fail (return null) which is handled.
        var universalisService = new UniversalisService(mockLog.Object, mockCache, mockDb.Object, mockRateLimiter.Object, config, mockDataManager.Object);

        // Create ShoppingListService
        var shoppingService = new ShoppingListService(mockDataManager.Object, mockLog.Object, recipeService, universalisService);
        shoppingService.Initialize();

        // Perform Test
        var targets = new List<CraftingTarget>
        {
            new CraftingTarget { RecipeId = 1, AmountToCraft = 1 }
        };

        var shoppingList = shoppingService.GenerateShoppingList(targets);

        // Verification
        
        // 1. Should have Raw Material (3x)
        var rawMaterial = shoppingList.Items.FirstOrDefault(i => i.ItemId == rawMaterialId);
        if (rawMaterial == null) throw new Exception("Raw material missing from shopping list");
        if (rawMaterial.AmountNeeded != 3) throw new Exception($"Expected 3 raw materials, got {rawMaterial.AmountNeeded}");

        // 2. Should have Crystals (5x)
        var crystal = shoppingList.Items.FirstOrDefault(i => i.ItemId == crystalId);
        if (crystal == null) throw new Exception("Crystal missing from shopping list");
        if (crystal.AmountNeeded != 5) throw new Exception($"Expected 5 crystals, got {crystal.AmountNeeded}");

        // 3. Should NOT have Intermediate (because we broke it down)
        // NOTE: This depends on the desired behavior. If we recursively break down, we consume the intermediate.
        var intermediate = shoppingList.Items.FirstOrDefault(i => i.ItemId == intermediateId);
        if (intermediate != null) throw new Exception("Intermediate item should be broken down and not appear in list");

        Console.WriteLine("- TestRecursiveBreakdown passed");
    }
}
