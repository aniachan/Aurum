using System;
using System.IO;
using Aurum.Services;
using Dalamud.Plugin.Services;
using Moq;
using Xunit;
using Aurum.Models;
using System.Collections.Generic;
using System.Linq;

namespace Aurum.IntegrationTests;

public class DatabasePaginationTests
{
    [Fact]
    public void TestCachedProfitPagination()
    {
        // Setup
        var mockLog = new Mock<IPluginLog>();
        var tempPath = Path.GetTempPath();
        var dbPath = Path.Combine(tempPath, "aurum_pagination.db");
        
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        try
        {
            // Use custom DB name for this test
            // Note: DatabaseService uses hardcoded "aurum.db" inside InitializeDatabase connection string.
            // But we pass tempPath as directory.
            // To isolate, we create a subdirectory.
            var isolatedPath = Path.Combine(tempPath, "AurumPaginationTest");
            Directory.CreateDirectory(isolatedPath);
            var isolatedDbPath = Path.Combine(isolatedPath, "aurum.db");
             if (File.Exists(isolatedDbPath)) File.Delete(isolatedDbPath);

            using var db = new DatabaseService(mockLog.Object, isolatedPath);
            
            // Insert 50 records with increasing scores
            for (int i = 1; i <= 50; i++)
            {
                var recipe = new RecipeData
                {
                    RecipeId = (uint)i,
                    ResultItemId = (uint)(1000 + i),
                    Ingredients = new List<RecipeIngredient>()
                };
                
                var profit = new ProfitCalculation
                {
                    NetProfit = i * 1000,
                    ProfitMargin = 20,
                    RiskScore = 10,
                    RecommendationScore = i // Score equals index (1 to 50)
                };
                
                db.UpsertRecipeCache(recipe, profit);
            }
            
            // Act 1: Get top 10 (should be 50 down to 41)
            // Default sort is recommendation_score DESC
            var page1 = db.GetAllCachedProfits(24, 10, 0);
            
            Assert.Equal(10, page1.Count);
            Assert.Equal(50, page1[0].Profit.RecommendationScore);
            Assert.Equal(41, page1[9].Profit.RecommendationScore);
            
            // Act 2: Get next 10 (offset 10, should be 40 down to 31)
            var page2 = db.GetAllCachedProfits(24, 10, 10);
            
            Assert.Equal(10, page2.Count);
            Assert.Equal(40, page2[0].Profit.RecommendationScore);
            Assert.Equal(31, page2[9].Profit.RecommendationScore);
            
            // Act 3: Get specific limit/offset
            var customPage = db.GetAllCachedProfits(24, 5, 45); // Should get last 5 (5 down to 1)
            
            Assert.Equal(5, customPage.Count);
            Assert.Equal(5, customPage[0].Profit.RecommendationScore);
            Assert.Equal(1, customPage[4].Profit.RecommendationScore);
            
             // Cleanup isolated dir
             try { Directory.Delete(isolatedPath, true); } catch {}
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                File.Delete(dbPath);
            }
        }
    }
}
