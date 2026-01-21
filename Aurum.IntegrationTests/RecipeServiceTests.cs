using System;
using System.Collections.Generic;
using Aurum;
using Aurum.Services;
using Dalamud.Plugin.Services;
using Moq;

namespace Aurum.IntegrationTests;

public static class RecipeServiceTests
{
    public static void Run()
    {
        Console.WriteLine("Running RecipeServiceTests...");
        TestLruEviction();
        Console.WriteLine("RecipeServiceTests Passed!");
    }

    private static void TestLruEviction()
    {
        // Mock dependencies
        var mockDataManager = new Mock<IDataManager>();
        var mockLog = new Mock<IPluginLog>();
        var config = new Configuration();
        
        // Set small cache size for testing
        config.MaxRecipeCacheEntries = 2;
        
        // Create service
        var service = new RecipeService(mockDataManager.Object, mockLog.Object, config);
        
        // We need to access private members or use reflection to manipulate the cache/state
        // Since we can't easily mock the DataManager extension methods and sheets in a simple unit test
        // without a lot of boilerplate, we'll use reflection to inject fake data into the cache
        // and verify eviction logic.
        
        var recipeCacheField = typeof(RecipeService).GetField("recipeCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var recipeLruListField = typeof(RecipeService).GetField("recipeLruList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (recipeCacheField == null || recipeLruListField == null)
            throw new Exception("Could not find private fields via reflection");
            
        var cache = (Dictionary<uint, Aurum.Models.RecipeData>)recipeCacheField.GetValue(service)!;
        var lru = (List<uint>)recipeLruListField.GetValue(service)!;
        
        // Manually trigger the private UpdateLru/EvictLru logic by calling LoadRecipe?
        // LoadRecipe calls dataManager, which is hard to mock fully for Lumina sheets.
        // Instead, let's inject a "fake" LoadRecipe behavior by modifying the state 
        // and calling private methods if possible, or just testing the logic via a wrapper.
        
        // Actually, since we modified LoadRecipe to handle the logic, we really want to test LoadRecipe.
        // But LoadRecipe depends on DataManager.GetExcelSheet<Recipe>().
        
        // Alternative: Subclass RecipeService and override LoadRecipe? No, not virtual.
        // Alternative: Mock DataManager to return null for sheets, but then LoadRecipe returns null.
        
        // Let's rely on the fact that we can call private methods using reflection to test the eviction logic directly.
        var updateLruMethod = typeof(RecipeService).GetMethod("UpdateLru", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var evictLruMethod = typeof(RecipeService).GetMethod("EvictLru", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (updateLruMethod == null || evictLruMethod == null)
            throw new Exception("Could not find private methods via reflection");

        // Simulate adding items
        // 1. Add Item 1
        cache.Add(1, new Aurum.Models.RecipeData { RecipeId = 1 });
        updateLruMethod.Invoke(service, new object[] { 1u });
        
        // 2. Add Item 2
        cache.Add(2, new Aurum.Models.RecipeData { RecipeId = 2 });
        updateLruMethod.Invoke(service, new object[] { 2u });
        
        // Verify state: [1, 2] (2 is most recent)
        if (lru.Count != 2 || lru[0] != 1 || lru[1] != 2)
            throw new Exception("LRU state incorrect after adding 2 items");
            
        // 3. Access Item 1 (make it MRU)
        updateLruMethod.Invoke(service, new object[] { 1u });
        
        // Verify state: [2, 1] (1 is most recent)
        if (lru.Count != 2 || lru[0] != 2 || lru[1] != 1)
            throw new Exception("LRU state incorrect after accessing item 1");
            
        // 4. "Load" Item 3 - should trigger eviction manually since we're bypassing LoadRecipe
        // Check condition manually like LoadRecipe does
        if (cache.Count >= config.MaxRecipeCacheEntries)
        {
            evictLruMethod.Invoke(service, null);
        }
        
        // Verify Item 2 was evicted (it was at index 0)
        if (cache.ContainsKey(2))
            throw new Exception("Item 2 should have been evicted");
        if (!cache.ContainsKey(1))
            throw new Exception("Item 1 should still be in cache");
            
        // Add Item 3
        cache.Add(3, new Aurum.Models.RecipeData { RecipeId = 3 });
        updateLruMethod.Invoke(service, new object[] { 3u });
        
        // Verify final state
        if (cache.Count != 2)
            throw new Exception($"Cache count should be 2, but was {cache.Count}");
        if (!cache.ContainsKey(1) || !cache.ContainsKey(3))
            throw new Exception("Cache should contain 1 and 3");
            
        Console.WriteLine("- TestLruEviction passed");
    }
}
