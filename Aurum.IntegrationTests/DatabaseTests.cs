using System;
using System.IO;
using Aurum.Services;
using Dalamud.Plugin.Services;
using Moq;
using Xunit;
using Aurum.Models;
using System.Collections.Generic;

namespace Aurum.IntegrationTests;

// public interface IPluginLog {
//     void Information(string message);
//     void Error(Exception ex, string message);
//     void Error(string message);
// }

public class DatabaseTests
{
    [Fact]
    public void TestDatabaseInitialization()
    {
        // Setup
        var mockLog = new Mock<IPluginLog>();
        var tempPath = Path.GetTempPath();
        var dbPath = Path.Combine(tempPath, "aurum.db"); // Changed from aurum_test.db to match service behavior
        
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        try 
        {
            // Act
            using var db = new DatabaseService(mockLog.Object, tempPath);
            
            // Assert
            Assert.True(File.Exists(dbPath), "Database file should exist");
            
            using var connection = db.GetConnection();
            connection.Open();
            
            // Verify tables exist
            VerifyTableExists(connection, "MarketData");
            VerifyTableExists(connection, "PriceHistory");
            VerifyTableExists(connection, "RecipeCache");
            VerifyTableExists(connection, "ItemMetadata");
            VerifyTableExists(connection, "ApiRequestLog");
        }
        finally
        {
            // Cleanup
            if (File.Exists(dbPath))
            {
                // Ensure connections are closed before deleting
                GC.Collect();
                GC.WaitForPendingFinalizers();
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public void TestMarketDataCRUD()
    {
        // Setup
        var mockLog = new Mock<IPluginLog>();
        var tempPath = Path.GetTempPath();
        var dbPath = Path.Combine(tempPath, "aurum.db");
        
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        try
        {
            using var db = new DatabaseService(mockLog.Object, tempPath);
            
            var testData = new MarketData
            {
                ItemId = 12345,
                LastUploadTime = DateTime.UtcNow,
                MinPrice = 1000,
                CurrentAveragePriceNQ = 1200, // This sets AveragePrice property logic in model
                CurrentListings = 5,
                SaleVelocity = 1.5f,
                Listings = new List<MarketListing> 
                { 
                    new MarketListing { ItemId = 12345, PricePerUnit = 1000, Quantity = 1 } 
                },
                RecentHistory = new List<SaleRecord>
                {
                    new SaleRecord { ItemId = 12345, PricePerUnit = 1100, Quantity = 1, Timestamp = DateTime.UtcNow.AddHours(-1) }
                }
            };
            
            // Act: Upsert
            db.UpsertMarketData(testData, 99); // 99 = Test World
            
            // Act: Retrieve
            var retrieved = db.GetMarketData(12345, 99, TimeSpan.FromHours(1));
            
            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(testData.ItemId, retrieved.ItemId);
            Assert.Equal(testData.MinPrice, retrieved.MinPrice);
            Assert.Single(retrieved.Listings);
            Assert.Single(retrieved.RecentHistory);
            
            // Act: Retrieve expired
            // We can't easily mock time passing without injecting a time provider, 
            // but we can request with zero max age
            var expired = db.GetMarketData(12345, 99, TimeSpan.Zero);
            Assert.Null(expired);
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

    private void VerifyTableExists(Microsoft.Data.Sqlite.SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@tableName;";
        command.Parameters.AddWithValue("@tableName", tableName);
        var result = command.ExecuteScalar();
        Assert.NotNull(result);
        Assert.Equal(tableName, result.ToString());
    }

    [Fact]
    public void TestRecipeCache()
    {
        // Setup
        var mockLog = new Mock<IPluginLog>();
        var tempPath = Path.GetTempPath();
        var dbPath = Path.Combine(tempPath, "aurum.db");
        
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        try
        {
            using var db = new DatabaseService(mockLog.Object, tempPath);
            
            var recipe = new RecipeData
            {
                RecipeId = 555,
                ResultItemId = 123,
                Ingredients = new List<RecipeIngredient>()
            };
            
            var profit = new ProfitCalculation
            {
                NetProfit = 50000,
                ProfitMargin = 25.5f,
                RiskScore = 30,
                RecommendationScore = 80
            };
            
            // Act
            db.UpsertRecipeCache(recipe, profit);
            var cached = db.GetCachedProfit(555);
            
            // Assert
            Assert.NotNull(cached);
            Assert.Equal(50000, cached.NetProfit);
            Assert.Equal(25.5f, cached.ProfitMargin);
            Assert.Equal(30, cached.RiskScore);
            Assert.Equal(80, cached.RecommendationScore);
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
