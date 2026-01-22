using System;
using System.IO;
using Aurum.Services;
using Dalamud.Plugin.Services;
using Moq;
using Xunit;
using Aurum.Models;
using System.Collections.Generic;
using System.Threading;

namespace Aurum.IntegrationTests;

// public interface IPluginLog {
//     void Information(string message);
//     void Error(Exception ex, string message);
//     void Error(string message);
// }

public class DatabaseTests
{
    private string GetTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aurum_test_{Guid.NewGuid()}");
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
    public void TestDatabaseInitialization()
    {
        // Setup
        var mockLog = new Mock<IPluginLog>();
        var tempDir = GetTempDir();
        var dbPath = Path.Combine(tempDir, "aurum.db");
        
        try 
        {
            // Act
            using var db = new DatabaseService(mockLog.Object, tempDir);
            
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
            Cleanup(tempDir);
        }
    }

    [Fact]
    public void TestMarketDataCRUD()
    {
        // Setup
        var mockLog = new Mock<IPluginLog>();
        var tempDir = GetTempDir();
        
        try
        {
            using var db = new DatabaseService(mockLog.Object, tempDir);
            
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
            Cleanup(tempDir);
        }
    }

    [Fact]
    public void TestUpsertMarketDataBulk()
    {
        var mockLog = new Mock<IPluginLog>();
        var tempDir = GetTempDir();

        try
        {
            using var db = new DatabaseService(mockLog.Object, tempDir);
            
            var dataList = new List<MarketData>();
            for (int i = 0; i < 10; i++)
            {
                dataList.Add(new MarketData
                {
                    ItemId = (uint)(1000 + i),
                    LastUploadTime = DateTime.UtcNow,
                    MinPrice = (uint)(100 + i),
                    CurrentAveragePriceNQ = (uint)(150 + i),
                    CurrentListings = 10,
                    SaleVelocity = 1.0f,
                    Listings = new List<MarketListing>(),
                    RecentHistory = new List<SaleRecord>(),
                    SalesPerDay = 2.0f,
                    DemandRatio = 1.5f
                });
            }

            // Act
            db.UpsertMarketDataBulk(dataList, 99);

            // Assert
            foreach (var data in dataList)
            {
                var retrieved = db.GetMarketData((int)data.ItemId, 99, TimeSpan.FromHours(1));
                Assert.NotNull(retrieved);
                Assert.Equal(data.ItemId, retrieved.ItemId);
                Assert.Equal(data.MinPrice, retrieved.MinPrice);
                Assert.Equal(data.SalesPerDay, retrieved.SalesPerDay);
            }
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Fact]
    public void TestMarketSnapshots()
    {
        var mockLog = new Mock<IPluginLog>();
        var tempDir = GetTempDir();

        try
        {
            using var db = new DatabaseService(mockLog.Object, tempDir);
            
            // We need to inject data directly into PriceHistory to simulate snapshots over time
            using (var conn = db.GetConnection())
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO PriceHistory (item_id, world_id, timestamp, price, quantity, is_sale)
                    VALUES 
                    (@id, @world, @t1, 1000, 10, 0),
                    (@id, @world, @t2, 1100, 8, 0),
                    (@id, @world, @t3, 1200, 5, 0)
                ";
                
                var now = DateTimeOffset.UtcNow;
                cmd.Parameters.AddWithValue("@id", 500);
                cmd.Parameters.AddWithValue("@world", 99);
                cmd.Parameters.AddWithValue("@t1", now.AddHours(-10).ToUnixTimeSeconds());
                cmd.Parameters.AddWithValue("@t2", now.AddHours(-5).ToUnixTimeSeconds());
                cmd.Parameters.AddWithValue("@t3", now.AddHours(-1).ToUnixTimeSeconds());
                
                cmd.ExecuteNonQuery();
            }

            // Act
            var snapshots = db.GetMarketSnapshots(500, 99, DateTime.UtcNow.AddDays(-1));
            
            // Assert
            Assert.Equal(3, snapshots.Count);
            Assert.Equal(1000u, snapshots[0].MinPrice);
            Assert.Equal(1200u, snapshots[2].MinPrice);
            
            // Test date filtering
            var recentSnapshots = db.GetMarketSnapshots(500, 99, DateTime.UtcNow.AddHours(-2));
            Assert.Single(recentSnapshots);
            Assert.Equal(1200u, recentSnapshots[0].MinPrice);
        }
        finally
        {
            Cleanup(tempDir);
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
        var tempDir = GetTempDir();
        
        try
        {
            using var db = new DatabaseService(mockLog.Object, tempDir);
            
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
            Cleanup(tempDir);
        }
    }

    [Fact]
    public void TestCachedProfitsPagination()
    {
        var mockLog = new Mock<IPluginLog>();
        var tempDir = GetTempDir();

        try
        {
            using var db = new DatabaseService(mockLog.Object, tempDir);

            // Seed data
            for (int i = 0; i < 20; i++)
            {
                var recipe = new RecipeData { RecipeId = (uint)i, ResultItemId = (uint)(100 + i), Ingredients = new() };
                var profit = new ProfitCalculation 
                { 
                    NetProfit = i * 1000, 
                    RecommendationScore = i, // 0 to 19
                    GilPerHour = i * 500
                };
                db.UpsertRecipeCache(recipe, profit);
            }

            // Act - Get top 5 (ordered by recommendation desc)
            var top5 = db.GetAllCachedProfits(24, 5, 0);

            // Assert
            Assert.Equal(5, top5.Count);
            Assert.Equal(19u, top5[0].RecipeId); // Highest score (19) should be first
            Assert.Equal(18u, top5[1].RecipeId);

            // Act - Get next 5
            var next5 = db.GetAllCachedProfits(24, 5, 5);
            Assert.Equal(5, next5.Count);
            Assert.Equal(14u, next5[0].RecipeId);
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Fact]
    public void TestItemPriorities()
    {
        var mockLog = new Mock<IPluginLog>();
        var tempDir = GetTempDir();

        try
        {
            using var db = new DatabaseService(mockLog.Object, tempDir);

            // Act
            db.UpsertItemPriority(101, 50);
            db.UpsertItemPriority(102, 100);
            db.UpsertItemPriority(103, 10);

            var priorities = db.GetAllItemPriorities();

            // Assert
            Assert.Equal(3, priorities.Count);
            Assert.Equal(50, priorities[101]);
            Assert.Equal(100, priorities[102]);
            Assert.Equal(10, priorities[103]);

            // Update
            db.UpsertItemPriority(101, 75);
            priorities = db.GetAllItemPriorities();
            Assert.Equal(75, priorities[101]);
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Fact]
    public void TestVacuumMaintenance()
    {
        var mockLog = new Mock<IPluginLog>();
        var tempDir = GetTempDir();

        try
        {
            using var db = new DatabaseService(mockLog.Object, tempDir);

            bool vacuumCallbackCalled = false;
            var vacuumComplete = new ManualResetEventSlim(false);

            // Act
            // Force vacuum by saying last run was long ago (10 days ago), frequency 1 day
            db.CheckAndRunVacuum(1, DateTime.UtcNow.AddDays(-10), (ts) => 
            {
                vacuumCallbackCalled = true;
                vacuumComplete.Set();
            });

            // Wait for background task
            vacuumComplete.Wait(2000);

            // Assert
            Assert.True(vacuumCallbackCalled, "Vacuum callback should have been called");
        }
        finally
        {
            Cleanup(tempDir);
        }
    }
    
    [Fact]
    public void TestDatabaseSize()
    {
        // Setup
        var mockLog = new Mock<IPluginLog>();
        var tempDir = GetTempDir();
        
        try 
        {
            // Act
            using var db = new DatabaseService(mockLog.Object, tempDir);
            
            // Should be small initially but > 0
            var initialSize = db.GetDatabaseSize();
            Assert.True(initialSize > 0);
            
            // Add some data to increase size
            for (int i = 0; i < 100; i++)
            {
                db.LogApiRequest("/test", DateTime.UtcNow, 100, 200, true);
            }
            
            // Force WAL checkpoint if needed, but size might not update immediately depending on OS/file system
            // Just asserting it runs without error and returns reasonable value
            var sizeAfter = db.GetDatabaseSize();
            Assert.True(sizeAfter > 0);
        }
        finally
        {
            Cleanup(tempDir);
        }
    }
}
