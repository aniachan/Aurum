using System;
using System.Collections.Generic;
using System.IO;
using Aurum.Models;
using Aurum.Services;
using Dalamud.Plugin.Services;
using Moq;
using Xunit;

namespace Aurum.IntegrationTests;

public class DatabaseCachingTests
{
    private string GetTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aurum_test_caching_{Guid.NewGuid()}");
        Directory.CreateDirectory(path);
        return path;
    }

    private void Cleanup(string path)
    {
        if (Directory.Exists(path))
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try { Directory.Delete(path, true); } catch { }
        }
    }

    [Fact]
    public void TestRecipeCacheExpiration()
    {
        var mockLog = new Mock<IPluginLog>();
        var tempDir = GetTempDir();

        try
        {
            using var db = new DatabaseService(mockLog.Object, tempDir);
            
            // Insert one fresh record (via API)
            var freshRecipe = new RecipeData { RecipeId = 1, ResultItemId = 101, Ingredients = new() };
            var freshProfit = new ProfitCalculation { NetProfit = 1000, RecommendationScore = 50 };
            db.UpsertRecipeCache(freshRecipe, freshProfit);

            // Insert one stale record (via direct SQL to control timestamp)
            using (var conn = db.GetConnection())
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO RecipeCache (
                        recipe_id, item_id, last_analyzed, profit_snapshot, margin_snapshot, 
                        risk_score, recommendation_score, gil_per_hour, ingredients_json
                    ) VALUES (
                        @recipeId, @itemId, @lastAnalyzed, @profit, @margin,
                        @risk, @recommendation, @gilPerHour, @ingredientsJson
                    )";
                
                cmd.Parameters.AddWithValue("@recipeId", 2);
                cmd.Parameters.AddWithValue("@itemId", 102);
                // 25 hours ago
                cmd.Parameters.AddWithValue("@lastAnalyzed", DateTimeOffset.UtcNow.AddHours(-25).ToUnixTimeSeconds());
                cmd.Parameters.AddWithValue("@profit", 2000);
                cmd.Parameters.AddWithValue("@margin", 0);
                cmd.Parameters.AddWithValue("@risk", 0);
                cmd.Parameters.AddWithValue("@recommendation", 60);
                cmd.Parameters.AddWithValue("@gilPerHour", 0);
                cmd.Parameters.AddWithValue("@ingredientsJson", "[]");
                
                cmd.ExecuteNonQuery();
            }

            // Act - Get cached profits with 24h expiration
            var results = db.GetAllCachedProfits(24);

            // Assert
            Assert.Contains(results, r => r.RecipeId == 1);
            Assert.DoesNotContain(results, r => r.RecipeId == 2);
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Fact]
    public void TestMarketDataCacheExpiration()
    {
        var mockLog = new Mock<IPluginLog>();
        var tempDir = GetTempDir();

        try
        {
            using var db = new DatabaseService(mockLog.Object, tempDir);

            // Insert stale market data via SQL
            using (var conn = db.GetConnection())
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO MarketData (
                        item_id, world_id, last_updated, min_price, average_price, 
                        listing_count, velocity, current_listings_json, recent_sales_json,
                        sales_per_day, demand_ratio
                    ) VALUES (
                        @itemId, @worldId, @lastUpdated, 100, 100,
                        1, 1, '[]', '[]',
                        1, 1
                    )";
                
                cmd.Parameters.AddWithValue("@itemId", 1001);
                cmd.Parameters.AddWithValue("@worldId", 99);
                // 2 hours ago
                cmd.Parameters.AddWithValue("@lastUpdated", DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds());
                
                cmd.ExecuteNonQuery();
            }

            // Act & Assert
            // 1. Request with maxAge = 1 hour (should be expired)
            var expired = db.GetMarketData(1001, 99, TimeSpan.FromHours(1));
            Assert.Null(expired);

            // 2. Request with maxAge = 3 hours (should be valid)
            var valid = db.GetMarketData(1001, 99, TimeSpan.FromHours(3));
            Assert.NotNull(valid);
            Assert.Equal(1001u, valid.ItemId);
        }
        finally
        {
            Cleanup(tempDir);
        }
    }
}
