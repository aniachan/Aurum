using System;
using System.IO;
using Aurum.Services;
// using Dalamud.Plugin.Services;
using Moq;
using Xunit;

namespace Aurum.IntegrationTests;

public interface IPluginLog {
    void Information(string message);
    void Error(Exception ex, string message);
    void Error(string message);
}

public class DatabaseTests
{
    [Fact]
    public void TestDatabaseInitialization()
    {
        // Setup
        var mockLog = new Mock<IPluginLog>();
        var tempPath = Path.GetTempPath();
        var dbPath = Path.Combine(tempPath, "aurum_test.db");
        
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        try 
        {
            // Act
            using var db = new DatabaseService(mockLog.Object, tempPath);
            
            // Assert
            Assert.True(File.Exists(Path.Combine(tempPath, "aurum.db")), "Database file should exist");
            
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
            if (File.Exists(Path.Combine(tempPath, "aurum.db")))
            {
                // Ensure connections are closed before deleting
                GC.Collect();
                GC.WaitForPendingFinalizers();
                File.Delete(Path.Combine(tempPath, "aurum.db"));
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
}
