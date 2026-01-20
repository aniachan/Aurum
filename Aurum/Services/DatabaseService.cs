using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
// using Dalamud.Plugin.Services;
using System.Threading.Tasks;
using System.Text.Json;
using Aurum.Models;
using System.Linq;

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
        // Enable pooling explicitly, though it is often default.
        this.connectionString = $"Data Source={dbPath};Pooling=True";
        
        log.Information($"Initializing database at: {dbPath}");
        
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        try
        {
            using var connection = GetConnection();
            connection.Open();

            // Enable WAL mode for better concurrency and performance
            using (var pragmaCmd = connection.CreateCommand())
            {
                pragmaCmd.CommandText = "PRAGMA journal_mode=WAL;";
                pragmaCmd.ExecuteNonQuery();
            }
            
            // Ensure SchemaVersion table exists
            var createSchemaVersionTable = @"
                CREATE TABLE IF NOT EXISTS SchemaVersion (
                    version INTEGER PRIMARY KEY,
                    applied_at INTEGER NOT NULL
                );";
            
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = createSchemaVersionTable;
                cmd.ExecuteNonQuery();
            }

            var currentVersion = GetSchemaVersion(connection);
            log.Information($"Current database schema version: {currentVersion}");
            
            // Apply migrations
            if (currentVersion < 1) ApplyMigration(connection, 1, Migration_1_InitialSchema);
            if (currentVersion < 2) ApplyMigration(connection, 2, Migration_2_AddHistoryMetrics);
            if (currentVersion < 3) ApplyMigration(connection, 3, Migration_3_AddPriorityScores);
            
            log.Information("Database initialization complete");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to initialize database");
            throw;
        }
    }

    private int GetSchemaVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT MAX(version) FROM SchemaVersion";
        var result = cmd.ExecuteScalar();
        if (result == DBNull.Value || result == null) return 0;
        return Convert.ToInt32(result);
    }

    private void ApplyMigration(SqliteConnection connection, int version, Action<SqliteConnection, SqliteTransaction> migrationAction)
    {
        using var transaction = connection.BeginTransaction();
        try 
        {
            log.Information($"Applying migration version {version}...");
            migrationAction(connection, transaction);
            
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "INSERT INTO SchemaVersion (version, applied_at) VALUES (@version, @appliedAt)";
            cmd.Parameters.AddWithValue("@version", version);
            cmd.Parameters.AddWithValue("@appliedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            cmd.ExecuteNonQuery();
            
            transaction.Commit();
            log.Information($"Successfully applied migration version {version}");
        }
        catch(Exception ex)
        {
            log.Error(ex, $"Failed to apply migration {version}");
            throw;
        }
    }

    private void Migration_1_InitialSchema(SqliteConnection connection, SqliteTransaction transaction)
    {
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
    }

    private void Migration_2_AddHistoryMetrics(SqliteConnection connection, SqliteTransaction transaction)
    {
        // This migration is reserved for future metrics if needed.
        // Currently PriceHistory schema in Migration 1 covers the basics.
        // We might add volatility or other computed metrics here later.
        // For now, it ensures we have a slot for schema evolution.
    }

    private void Migration_3_AddPriorityScores(SqliteConnection connection, SqliteTransaction transaction)
    {
        // Add priority tracking table
        var createPriorityTable = @"
            CREATE TABLE IF NOT EXISTS ItemPriorities (
                item_id INTEGER PRIMARY KEY,
                priority_score INTEGER NOT NULL,
                last_calculated INTEGER NOT NULL
            );
        ";
        ExecuteNonQuery(connection, createPriorityTable, transaction);
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

    public void UpsertMarketData(MarketData data, int worldId)
    {
        lock (dbLock)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var transaction = connection.BeginTransaction();
                using var command = connection.CreateCommand();
                command.Transaction = transaction;

                // 1. Upsert into MarketData
                command.CommandText = @"
                    INSERT OR REPLACE INTO MarketData (
                        item_id, world_id, last_updated, min_price, average_price, 
                        listing_count, velocity, current_listings_json, recent_sales_json
                    ) VALUES (
                        @itemId, @worldId, @lastUpdated, @minPrice, @avgPrice,
                        @listingCount, @velocity, @listingsJson, @salesJson
                    );
                ";

                command.Parameters.AddWithValue("@itemId", data.ItemId);
                command.Parameters.AddWithValue("@worldId", worldId);
                command.Parameters.AddWithValue("@lastUpdated", ((DateTimeOffset)data.LastUploadTime).ToUnixTimeSeconds());
                command.Parameters.AddWithValue("@minPrice", data.MinPrice);
                command.Parameters.AddWithValue("@avgPrice", data.CurrentAveragePrice);
                command.Parameters.AddWithValue("@listingCount", data.CurrentListings);
                command.Parameters.AddWithValue("@velocity", data.SaleVelocity);
                command.Parameters.AddWithValue("@listingsJson", JsonSerializer.Serialize(data.Listings));
                command.Parameters.AddWithValue("@salesJson", JsonSerializer.Serialize(data.RecentHistory));
                
                command.ExecuteNonQuery();

                // 2. Insert recent sales into PriceHistory
                // Get latest sale timestamp
                command.CommandText = "SELECT MAX(timestamp) FROM PriceHistory WHERE item_id = @itemId AND world_id = @worldId AND is_sale = 1";
                var lastSaleTimestampObj = command.ExecuteScalar();
                long lastSaleTimestamp = 0;
                if (lastSaleTimestampObj != DBNull.Value && lastSaleTimestampObj != null)
                {
                    lastSaleTimestamp = (long)lastSaleTimestampObj;
                }

                foreach (var sale in data.RecentHistory.OrderBy(s => s.Timestamp))
                {
                    long saleUnix = ((DateTimeOffset)sale.Timestamp).ToUnixTimeSeconds();
                    if (saleUnix > lastSaleTimestamp)
                    {
                        using var saleCmd = connection.CreateCommand();
                        saleCmd.Transaction = transaction;
                        saleCmd.CommandText = @"
                            INSERT INTO PriceHistory (item_id, world_id, timestamp, price, quantity, is_sale)
                            VALUES (@itemId, @worldId, @timestamp, @price, @quantity, 1);
                        ";
                        saleCmd.Parameters.AddWithValue("@itemId", data.ItemId);
                        saleCmd.Parameters.AddWithValue("@worldId", worldId);
                        saleCmd.Parameters.AddWithValue("@timestamp", saleUnix);
                        saleCmd.Parameters.AddWithValue("@price", sale.PricePerUnit);
                        saleCmd.Parameters.AddWithValue("@quantity", sale.Quantity);
                        saleCmd.ExecuteNonQuery();
                    }
                }

                // NEW: Update volatile metrics in MarketData if needed
                // We're already updating velocity and averages in the main upsert above,
                // but if we calculate volatility elsewhere, we might want to store it here too.
                // The current schema has 'velocity' but not explicit 'volatility' column.
                // For now, we rely on MarketAnalysisService to populate the object before upsert.


                // 3. Insert SNAPSHOT of current price/listings into PriceHistory
                // This allows tracking listing price over time, not just sales
                // We mark these with is_sale = 0
                
                // Only insert a snapshot if we haven't done so recently (e.g. within last hour)
                // This prevents spamming the history table if we refresh frequently
                command.CommandText = "SELECT MAX(timestamp) FROM PriceHistory WHERE item_id = @itemId AND world_id = @worldId AND is_sale = 0";
                var lastSnapshotTimestampObj = command.ExecuteScalar();
                long lastSnapshotTimestamp = 0;
                if (lastSnapshotTimestampObj != DBNull.Value && lastSnapshotTimestampObj != null)
                {
                    lastSnapshotTimestamp = (long)lastSnapshotTimestampObj;
                }
                
                long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (currentTimestamp - lastSnapshotTimestamp > 3600) // 1 hour
                {
                    using var snapshotCmd = connection.CreateCommand();
                    snapshotCmd.Transaction = transaction;
                    snapshotCmd.CommandText = @"
                        INSERT INTO PriceHistory (item_id, world_id, timestamp, price, quantity, is_sale)
                        VALUES (@itemId, @worldId, @timestamp, @price, @quantity, 0);
                    ";
                    snapshotCmd.Parameters.AddWithValue("@itemId", data.ItemId);
                    snapshotCmd.Parameters.AddWithValue("@worldId", worldId);
                    snapshotCmd.Parameters.AddWithValue("@timestamp", currentTimestamp);
                    snapshotCmd.Parameters.AddWithValue("@price", data.MinPrice); // Track min price
                    snapshotCmd.Parameters.AddWithValue("@quantity", data.CurrentListings); // Track listing count
                    snapshotCmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                log.Error(ex, $"Failed to upsert market data for item {data.ItemId}");
            }
        }
    }

    public void UpsertMarketDataBulk(IEnumerable<MarketData> dataList, int worldId)
    {
        lock (dbLock)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var transaction = connection.BeginTransaction();
                
                // Prepared statement for MarketData upsert
                using var marketCmd = connection.CreateCommand();
                marketCmd.Transaction = transaction;
                marketCmd.CommandText = @"
                    INSERT OR REPLACE INTO MarketData (
                        item_id, world_id, last_updated, min_price, average_price, 
                        listing_count, velocity, current_listings_json, recent_sales_json
                    ) VALUES (
                        @itemId, @worldId, @lastUpdated, @minPrice, @avgPrice,
                        @listingCount, @velocity, @listingsJson, @salesJson
                    );
                ";
                
                // Add parameters once
                var pItemId = marketCmd.Parameters.Add("@itemId", SqliteType.Integer);
                var pWorldId = marketCmd.Parameters.Add("@worldId", SqliteType.Integer);
                var pLastUpdated = marketCmd.Parameters.Add("@lastUpdated", SqliteType.Integer);
                var pMinPrice = marketCmd.Parameters.Add("@minPrice", SqliteType.Integer);
                var pAvgPrice = marketCmd.Parameters.Add("@avgPrice", SqliteType.Real);
                var pListingCount = marketCmd.Parameters.Add("@listingCount", SqliteType.Integer);
                var pVelocity = marketCmd.Parameters.Add("@velocity", SqliteType.Real);
                var pListingsJson = marketCmd.Parameters.Add("@listingsJson", SqliteType.Text);
                var pSalesJson = marketCmd.Parameters.Add("@salesJson", SqliteType.Text);

                // Prepared statement for checking last sale
                using var lastSaleCmd = connection.CreateCommand();
                lastSaleCmd.Transaction = transaction;
                lastSaleCmd.CommandText = "SELECT MAX(timestamp) FROM PriceHistory WHERE item_id = @itemId AND world_id = @worldId AND is_sale = 1";
                var pLastSaleItemId = lastSaleCmd.Parameters.Add("@itemId", SqliteType.Integer);
                var pLastSaleWorldId = lastSaleCmd.Parameters.Add("@worldId", SqliteType.Integer);

                // Prepared statement for inserting history
                using var historyCmd = connection.CreateCommand();
                historyCmd.Transaction = transaction;
                historyCmd.CommandText = @"
                    INSERT INTO PriceHistory (item_id, world_id, timestamp, price, quantity, is_sale)
                    VALUES (@itemId, @worldId, @timestamp, @price, @quantity, 1);
                ";
                var pHItemId = historyCmd.Parameters.Add("@itemId", SqliteType.Integer);
                var pHWorldId = historyCmd.Parameters.Add("@worldId", SqliteType.Integer);
                var pHTimestamp = historyCmd.Parameters.Add("@timestamp", SqliteType.Integer);
                var pHPrice = historyCmd.Parameters.Add("@price", SqliteType.Integer);
                var pHQuantity = historyCmd.Parameters.Add("@quantity", SqliteType.Integer);

                // Prepared statements for snapshot logic
                using var checkSnapshotCmd = connection.CreateCommand();
                checkSnapshotCmd.Transaction = transaction;
                checkSnapshotCmd.CommandText = "SELECT MAX(timestamp) FROM PriceHistory WHERE item_id = @itemId AND world_id = @worldId AND is_sale = 0";
                var pSnapCheckItemId = checkSnapshotCmd.Parameters.Add("@itemId", SqliteType.Integer);
                var pSnapCheckWorldId = checkSnapshotCmd.Parameters.Add("@worldId", SqliteType.Integer);

                using var insertSnapshotCmd = connection.CreateCommand();
                insertSnapshotCmd.Transaction = transaction;
                insertSnapshotCmd.CommandText = @"
                    INSERT INTO PriceHistory (item_id, world_id, timestamp, price, quantity, is_sale)
                    VALUES (@itemId, @worldId, @timestamp, @price, @quantity, 0);
                ";
                var pSnapItemId = insertSnapshotCmd.Parameters.Add("@itemId", SqliteType.Integer);
                var pSnapWorldId = insertSnapshotCmd.Parameters.Add("@worldId", SqliteType.Integer);
                var pSnapTimestamp = insertSnapshotCmd.Parameters.Add("@timestamp", SqliteType.Integer);
                var pSnapPrice = insertSnapshotCmd.Parameters.Add("@price", SqliteType.Integer);
                var pSnapQuantity = insertSnapshotCmd.Parameters.Add("@quantity", SqliteType.Integer);

                long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                foreach (var data in dataList)
                {
                    // 1. Upsert MarketData
                    pItemId.Value = data.ItemId;
                    pWorldId.Value = worldId;
                    pLastUpdated.Value = ((DateTimeOffset)data.LastUploadTime).ToUnixTimeSeconds();
                    pMinPrice.Value = data.MinPrice;
                    pAvgPrice.Value = data.CurrentAveragePrice;
                    pListingCount.Value = data.CurrentListings;
                    pVelocity.Value = data.SaleVelocity;
                    pListingsJson.Value = JsonSerializer.Serialize(data.Listings);
                    pSalesJson.Value = JsonSerializer.Serialize(data.RecentHistory);
                    
                    marketCmd.ExecuteNonQuery();

                    // 2. Insert PriceHistory (Sales)
                    pLastSaleItemId.Value = data.ItemId;
                    pLastSaleWorldId.Value = worldId;
                    
                    var lastSaleTimestampObj = lastSaleCmd.ExecuteScalar();
                    long lastSaleTimestamp = 0;
                    if (lastSaleTimestampObj != DBNull.Value && lastSaleTimestampObj != null)
                    {
                        lastSaleTimestamp = (long)lastSaleTimestampObj;
                    }

                    foreach (var sale in data.RecentHistory.OrderBy(s => s.Timestamp))
                    {
                        long saleUnix = ((DateTimeOffset)sale.Timestamp).ToUnixTimeSeconds();
                        if (saleUnix > lastSaleTimestamp)
                        {
                            pHItemId.Value = data.ItemId;
                            pHWorldId.Value = worldId;
                            pHTimestamp.Value = saleUnix;
                            pHPrice.Value = sale.PricePerUnit;
                            pHQuantity.Value = sale.Quantity;
                            
                            historyCmd.ExecuteNonQuery();
                        }
                    }

                    // 3. Insert PriceHistory (Snapshot)
                    pSnapCheckItemId.Value = data.ItemId;
                    pSnapCheckWorldId.Value = worldId;
                    
                    var lastSnapshotTimestampObj = checkSnapshotCmd.ExecuteScalar();
                    long lastSnapshotTimestamp = 0;
                    if (lastSnapshotTimestampObj != DBNull.Value && lastSnapshotTimestampObj != null)
                    {
                        lastSnapshotTimestamp = (long)lastSnapshotTimestampObj;
                    }

                    if (currentTimestamp - lastSnapshotTimestamp > 3600) // 1 hour
                    {
                        pSnapItemId.Value = data.ItemId;
                        pSnapWorldId.Value = worldId;
                        pSnapTimestamp.Value = currentTimestamp;
                        pSnapPrice.Value = data.MinPrice;
                        pSnapQuantity.Value = data.CurrentListings;
                        
                        insertSnapshotCmd.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to perform bulk upsert of market data");
            }
        }
    }

    public MarketData? GetMarketData(int itemId, int worldId, TimeSpan maxAge)
    {
        lock (dbLock)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    SELECT last_updated, min_price, average_price, listing_count, velocity, current_listings_json, recent_sales_json
                    FROM MarketData
                    WHERE item_id = @itemId AND world_id = @worldId
                ";
                command.Parameters.AddWithValue("@itemId", itemId);
                command.Parameters.AddWithValue("@worldId", worldId);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    long lastUpdatedUnix = reader.GetInt64(0);
                    var lastUpdated = DateTimeOffset.FromUnixTimeSeconds(lastUpdatedUnix).UtcDateTime;

                    if (DateTime.UtcNow - lastUpdated > maxAge)
                    {
                        return null; // Cache expired
                    }

                    var marketData = new MarketData
                    {
                        ItemId = (uint)itemId,
                        LastUploadTime = lastUpdated,
                        MinPrice = (uint)reader.GetInt64(1),
                        // AveragePrice is double/real in DB
                        CurrentAveragePriceNQ = (uint)reader.GetDouble(2), // Simplified mapping
                        CurrentListings = reader.GetInt32(3),
                        SaleVelocity = (float)reader.GetDouble(4),
                        Listings = JsonSerializer.Deserialize<List<MarketListing>>(reader.GetString(5)) ?? new(),
                        RecentHistory = JsonSerializer.Deserialize<List<SaleRecord>>(reader.GetString(6)) ?? new(),
                        CachedAt = DateTime.UtcNow // Set to now as it's fresh from DB
                    };

                    return marketData;
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, $"Failed to get market data for item {itemId}");
            }
            return null;
        }
    }

    public void UpsertRecipeCache(RecipeData recipe, ProfitCalculation profit)
    {
        lock (dbLock)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var transaction = connection.BeginTransaction();
                using var command = connection.CreateCommand();
                command.Transaction = transaction;

                command.CommandText = @"
                    INSERT OR REPLACE INTO RecipeCache (
                        recipe_id, item_id, last_analyzed, profit_snapshot, margin_snapshot, 
                        risk_score, recommendation_score, ingredients_json
                    ) VALUES (
                        @recipeId, @itemId, @lastAnalyzed, @profit, @margin,
                        @risk, @recommendation, @ingredientsJson
                    );
                ";

                command.Parameters.AddWithValue("@recipeId", recipe.RecipeId);
                command.Parameters.AddWithValue("@itemId", recipe.ResultItemId);
                command.Parameters.AddWithValue("@lastAnalyzed", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                command.Parameters.AddWithValue("@profit", profit.NetProfit);
                command.Parameters.AddWithValue("@margin", profit.ProfitMargin);
                command.Parameters.AddWithValue("@risk", profit.RiskScore);
                command.Parameters.AddWithValue("@recommendation", profit.RecommendationScore);
                command.Parameters.AddWithValue("@ingredientsJson", JsonSerializer.Serialize(recipe.Ingredients));
                
                command.ExecuteNonQuery();
                transaction.Commit();
            }
            catch (Exception ex)
            {
                log.Error(ex, $"Failed to upsert recipe cache for recipe {recipe.RecipeId}");
            }
        }
    }

    public void UpsertItemPriority(int itemId, int priorityScore)
    {
        lock (dbLock)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    INSERT OR REPLACE INTO ItemPriorities (item_id, priority_score, last_calculated)
                    VALUES (@itemId, @score, @lastCalc);
                ";
                command.Parameters.AddWithValue("@itemId", itemId);
                command.Parameters.AddWithValue("@score", priorityScore);
                command.Parameters.AddWithValue("@lastCalc", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                log.Error(ex, $"Failed to upsert priority for item {itemId}");
            }
        }
    }

    public Dictionary<int, int> GetAllItemPriorities()
    {
        lock (dbLock)
        {
            var result = new Dictionary<int, int>();
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = connection.CreateCommand();

                command.CommandText = "SELECT item_id, priority_score FROM ItemPriorities";
                
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    result[reader.GetInt32(0)] = reader.GetInt32(1);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to load item priorities");
            }
            return result;
        }
    }

    public ProfitCalculation? GetCachedProfit(uint recipeId)
    {
        lock (dbLock)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    SELECT profit_snapshot, margin_snapshot, risk_score, recommendation_score, last_analyzed
                    FROM RecipeCache
                    WHERE recipe_id = @recipeId
                ";
                command.Parameters.AddWithValue("@recipeId", recipeId);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    // Construct a partial ProfitCalculation from cache
                    // Note: This won't have full breakdown, just key metrics
                    return new ProfitCalculation
                    {
                        NetProfit = reader.GetInt32(0),
                        ProfitMargin = reader.GetFloat(1),
                        RiskScore = reader.GetInt32(2),
                        RecommendationScore = reader.GetInt32(3),
                        // We might want to add a Timestamp property to ProfitCalculation or just use it to validate freshness
                    };
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, $"Failed to get cached profit for recipe {recipeId}");
            }
            return null;
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
