using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
// using Dalamud.Plugin.Services;
using System.Threading.Tasks;

namespace Aurum.Services;

public class DatabaseService : IDisposable
{
    private readonly dynamic log; // Use dynamic to avoid dependency on Dalamud
    private readonly string connectionString;
    private readonly object dbLock = new();

    public DatabaseService(dynamic log, string pluginDir)
    {
        this.log = log;
        
        var dbPath = Path.Combine(pluginDir, "aurum.db");
        this.connectionString = $"Data Source={dbPath}";
        
        log.Information($"Initializing database at: {dbPath}");
        
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        try
        {
            using var connection = GetConnection();
            connection.Open();
            
            using var transaction = connection.BeginTransaction();
            
            // 1. Market Data Table
            // Stores the latest market snapshot for an item on a world
            var createMarketDataTable = @"
                CREATE TABLE IF NOT EXISTS MarketData (
                    item_id INTEGER NOT NULL,
                    world_id INTEGER NOT NULL,
                    last_updated INTEGER NOT NULL, -- Unix timestamp
                    min_price INTEGER,
                    average_price REAL,
                    listing_count INTEGER,
                    velocity REAL,
                    current_listings_json TEXT,
                    recent_sales_json TEXT,
                    PRIMARY KEY (item_id, world_id)
                );";
            ExecuteNonQuery(connection, createMarketDataTable, transaction);
            
            // 2. Price History Table
            // Stores historical price points for trend analysis
            var createPriceHistoryTable = @"
                CREATE TABLE IF NOT EXISTS PriceHistory (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    item_id INTEGER NOT NULL,
                    world_id INTEGER NOT NULL,
                    timestamp INTEGER NOT NULL,
                    price INTEGER NOT NULL,
                    quantity INTEGER,
                    is_sale BOOLEAN NOT NULL -- true = sale, false = listing snapshot
                );
                CREATE INDEX IF NOT EXISTS idx_price_history_item_world ON PriceHistory(item_id, world_id);
                CREATE INDEX IF NOT EXISTS idx_price_history_timestamp ON PriceHistory(timestamp);
            ";
            ExecuteNonQuery(connection, createPriceHistoryTable, transaction);
            
            // 3. Recipe Cache Table
            // Stores analysis results to avoid re-calculating everything
            var createRecipeCacheTable = @"
                CREATE TABLE IF NOT EXISTS RecipeCache (
                    recipe_id INTEGER PRIMARY KEY,
                    item_id INTEGER NOT NULL,
                    last_analyzed INTEGER NOT NULL,
                    profit_snapshot INTEGER,
                    margin_snapshot REAL,
                    risk_score INTEGER,
                    recommendation_score INTEGER,
                    ingredients_json TEXT
                );
            ";
            ExecuteNonQuery(connection, createRecipeCacheTable, transaction);
            
            // 4. Item Metadata Table
            // Stores static/semi-static item info to avoid looking up in game data constantly
            var createItemMetadataTable = @"
                CREATE TABLE IF NOT EXISTS ItemMetadata (
                    item_id INTEGER PRIMARY KEY,
                    name TEXT,
                    item_level INTEGER,
                    category_id INTEGER,
                    can_be_hq BOOLEAN,
                    is_marketable BOOLEAN
                );
            ";
            ExecuteNonQuery(connection, createItemMetadataTable, transaction);
            
            // 5. API Request Log Table
            // Tracks API usage for rate limiting and debugging
            var createApiRequestLogTable = @"
                CREATE TABLE IF NOT EXISTS ApiRequestLog (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    endpoint TEXT NOT NULL,
                    timestamp INTEGER NOT NULL,
                    response_time_ms INTEGER,
                    status_code INTEGER,
                    success BOOLEAN
                );
                CREATE INDEX IF NOT EXISTS idx_api_log_timestamp ON ApiRequestLog(timestamp);
            ";
            ExecuteNonQuery(connection, createApiRequestLogTable, transaction);

            transaction.Commit();
            log.Information("Database initialization complete");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to initialize database");
            throw;
        }
    }
    
    private void ExecuteNonQuery(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (transaction != null)
        {
            command.Transaction = transaction;
        }
        command.ExecuteNonQuery();
    }

    public SqliteConnection GetConnection()
    {
        return new SqliteConnection(connectionString);
    }
    
    // Example generic method for executing non-query commands safely
    public void ExecuteSafe(string sql, Dictionary<string, object>? parameters = null)
    {
        lock (dbLock)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue(param.Key, param.Value);
                    }
                }
                
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                log.Error(ex, $"Database execution failed: {sql}");
            }
        }
    }
    
    public void Vacuum()
    {
        log.Information("Running database VACUUM...");
        ExecuteSafe("VACUUM;");
    }

    public void Dispose()
    {
        // SQLite connection pooling handles actual connection closing
        SqliteConnection.ClearAllPools();
    }
}
