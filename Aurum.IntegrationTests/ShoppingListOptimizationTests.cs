using System;
using System.Collections.Generic;
using System.Linq;
using Aurum.Models;
using Aurum.Services;
using Dalamud.Plugin.Services;
using Moq;
using Xunit;

namespace Aurum.IntegrationTests;

public static class ShoppingListOptimizationTests
{
    public static void Run()
    {
        Console.WriteLine("Running ShoppingListOptimizationTests...");
        TestYieldOptimization();
        TestPoolingOptimization();
        Console.WriteLine("ShoppingListOptimizationTests Passed!");
    }

    private static void TestYieldOptimization()
    {
        // Recipe A yields 3. Needs 1 Ingredient B.
        // We want 4 A.
        // Naive: 4 * 1 = 4 B.
        // Optimal: Ceil(4/3) = 2 crafts -> 2 * 1 = 2 B.

        var (shoppingService, recipeCache) = SetupService();

        uint itemA = 100;
        uint itemB = 101;

        var recipeA = new RecipeData
        {
            RecipeId = 1,
            ResultItemId = itemA,
            ResultAmount = 3,
            Ingredients = new List<RecipeIngredient>
            {
                new RecipeIngredient { ItemId = itemB, AmountNeeded = 1 }
            }
        };

        recipeCache[1] = recipeA;

        var targets = new List<CraftingTarget>
        {
            new CraftingTarget { RecipeId = 1, AmountToCraft = 4 }
        };

        var list = shoppingService.GenerateShoppingList(targets);

        var itemBEntry = list.Items.FirstOrDefault(i => i.ItemId == itemB);
        
        if (itemBEntry == null) throw new Exception("Item B missing");
        
        // Assert
        if (itemBEntry.AmountNeeded != 2)
            throw new Exception($"Yield Optimization Failed: Expected 2 B (for 2 crafts), got {itemBEntry.AmountNeeded}");
            
        Console.WriteLine("- TestYieldOptimization passed");
    }

    private static void TestPoolingOptimization()
    {
        // Recipe Final1 needs 1 Intermediate.
        // Recipe Final2 needs 1 Intermediate.
        // Recipe Intermediate yields 3. Needs 1 Raw.
        
        // We craft 1 Final1 and 1 Final2.
        // Total Intermediate needed = 2.
        // Intermediate Yield 3. -> 1 Craft needed.
        // Total Raw needed = 1.
        
        // Naive (Recursive without pooling):
        // Final1 -> 1 Intermediate -> 1 Craft -> 1 Raw.
        // Final2 -> 1 Intermediate -> 1 Craft -> 1 Raw.
        // Total 2 Raw.

        var (shoppingService, recipeCache) = SetupService();

        uint final1 = 201;
        uint final2 = 202;
        uint intermediate = 203;
        uint raw = 204;

        var recipeFinal1 = new RecipeData
        {
            RecipeId = 10,
            ResultItemId = final1,
            Ingredients = new List<RecipeIngredient>
            {
                new RecipeIngredient { ItemId = intermediate, AmountNeeded = 1, SubRecipeId = 30 }
            }
        };

        var recipeFinal2 = new RecipeData
        {
            RecipeId = 20,
            ResultItemId = final2,
            Ingredients = new List<RecipeIngredient>
            {
                new RecipeIngredient { ItemId = intermediate, AmountNeeded = 1, SubRecipeId = 30 }
            }
        };

        var recipeIntermediate = new RecipeData
        {
            RecipeId = 30,
            ResultItemId = intermediate,
            ResultAmount = 3,
            Ingredients = new List<RecipeIngredient>
            {
                new RecipeIngredient { ItemId = raw, AmountNeeded = 1 }
            }
        };

        recipeCache[10] = recipeFinal1;
        recipeCache[20] = recipeFinal2;
        recipeCache[30] = recipeIntermediate;

        var targets = new List<CraftingTarget>
        {
            new CraftingTarget { RecipeId = 10, AmountToCraft = 1 },
            new CraftingTarget { RecipeId = 20, AmountToCraft = 1 }
        };

        var list = shoppingService.GenerateShoppingList(targets);
        var rawEntry = list.Items.FirstOrDefault(i => i.ItemId == raw);

        if (rawEntry == null) throw new Exception("Raw missing");

        if (rawEntry.AmountNeeded != 1)
             throw new Exception($"Pooling Optimization Failed: Expected 1 Raw, got {rawEntry.AmountNeeded}");

        Console.WriteLine("- TestPoolingOptimization passed");
    }

    private static (ShoppingListService, Dictionary<uint, RecipeData>) SetupService()
    {
        var mockDataManager = new Mock<IDataManager>();
        var mockLog = new Mock<IPluginLog>();
        var config = new Configuration();
        var recipeService = new RecipeService(mockDataManager.Object, mockLog.Object, config);
        
        // Reflection to access cache
        var recipeCacheField = typeof(RecipeService).GetField("recipeCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (recipeCacheField == null) throw new InvalidOperationException("Could not find recipeCache field");
        var cache = (Dictionary<uint, RecipeData>)recipeCacheField.GetValue(recipeService)!;

        var mockCache = new CacheService(new Mock<ICacheConfig>().Object);
        var mockDb = new Mock<DatabaseService>(mockLog.Object, ".");
        var mockRateLimiter = new Mock<RateLimiter>(mockLog.Object, config, new Mock<IChatGui>().Object, mockDb.Object);
        var universalisService = new UniversalisService(mockLog.Object, mockCache, mockDb.Object, mockRateLimiter.Object, config, mockDataManager.Object);

        var shoppingService = new ShoppingListService(mockDataManager.Object, mockLog.Object, recipeService, universalisService);
        shoppingService.Initialize();

        return (shoppingService, cache);
    }
}
