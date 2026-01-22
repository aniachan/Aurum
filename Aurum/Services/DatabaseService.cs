using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Dalamud.Plugin.Services;
using System.Threading.Tasks;
using System.Text.Json;
using Aurum.Models;
using System.Linq;

namespace Aurum.Services;

public class DatabaseService : IDisposable
{
    private readonly IPluginLog log;
    private readonly string connectionString;
    private readonly string dbFilePath;
    private readonly object dbLock = new();

    public DatabaseService(IPluginLog log, string pluginDir)
    {
        this.log = log;
        
        if (pluginDir == ":memory:")
        {
            this.dbFilePath = ":memory:";
            this.connectionString = "Data Source=:memory:";
        }
        else
        {
            this.dbFilePath = Path.Combine(pluginDir, "aurum.db");
            // Enable pooling explicitly, though it is often default.
            this.connectionString = $"Data Source={dbFilePath};Pooling=True";
        }
        
        log.Information($"Initializing database at: {dbFilePath}");
        
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
            if (currentVersion < 4) ApplyMigration(connection, 4, Migration_4_AddSupplyDemandMetrics);
            if (currentVersion < 5) ApplyMigration(connection, 5, Migration_5_AddGilPerHourColumn);
            if (currentVersion < 6) ApplyMigration(connection, 6, Migration_6_AddApiPayloadSize);
            if (currentVersion < 7) ApplyMigration(connection, 7, Migration_7_AddArbitrageHistory);
            if (currentVersion < 8) ApplyMigration(connection, 8, Migration_8_AddArbitrageMetrics);
            
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
        // Add Sale Velocity History table
        // This allows tracking how fast items are selling over time
        var createVelocityHistoryTable = @"
            CREATE TABLE IF NOT EXISTS VelocityHistory (
                item_id INTEGER NOT NULL,
                world_id INTEGER NOT NULL,
                timestamp INTEGER NOT NULL,
                velocity REAL NOT NULL, -- sales per day
                PRIMARY KEY (item_id, world_id, timestamp)
            );
        ";
        ExecuteNonQuery(connection, createVelocityHistoryTable, transaction);
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

    private void Migration_4_AddSupplyDemandMetrics(SqliteConnection connection, SqliteTransaction transaction)
    {
        // Add supply/demand metrics columns to MarketData table if they don't exist
        // Note: SQLite ALTER TABLE is limited. We can add columns one by one.
        
        try
        {
            var addSalesPerDay = "ALTER TABLE MarketData ADD COLUMN sales_per_day REAL DEFAULT 0;";
            ExecuteNonQuery(connection, addSalesPerDay, transaction);
        }
        catch { /* Column might already exist, ignore */ }

        try
        {
            var addDemandRatio = "ALTER TABLE MarketData ADD COLUMN demand_ratio REAL DEFAULT 0;";
            ExecuteNonQuery(connection, addDemandRatio, transaction);
        }
        catch { /* Column might already exist, ignore */ }
    }
    
    private void Migration_5_AddGilPerHourColumn(SqliteConnection connection, SqliteTransaction transaction)
    {
        // Add gil_per_hour to RecipeCache for better caching
        try
        {
            var addGilPerHour = "ALTER TABLE RecipeCache ADD COLUMN gil_per_hour INTEGER DEFAULT 0;";
            ExecuteNonQuery(connection, addGilPerHour, transaction);
        }
        catch { /* Column might already exist, ignore */ }
    }

    private void Migration_6_AddApiPayloadSize(SqliteConnection connection, SqliteTransaction transaction)
    {
        // Add payload_size to ApiRequestLog for tracking data usage
        try
        {
            var addPayloadSize = "ALTER TABLE ApiRequestLog ADD COLUMN payload_size INTEGER DEFAULT 0;";
            ExecuteNonQuery(connection, addPayloadSize, transaction);
        }
        catch { /* Column might already exist, ignore */ }
    }
    
    private void Migration_7_AddArbitrageHistory(SqliteConnection connection, SqliteTransaction transaction)
    {
        // Add Arbitrage History Table
        // Tracks historical arbitrage opportunities
        var createArbitrageHistoryTable = @"
            CREATE TABLE IF NOT EXISTS ArbitrageHistory (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                item_id INTEGER NOT NULL,
                home_world_id INTEGER NOT NULL,
                target_world_id INTEGER NOT NULL,
                timestamp INTEGER NOT NULL,
                profit INTEGER NOT NULL,
                roi REAL NOT NULL,
                buy_price INTEGER NOT NULL,
                sell_price INTEGER NOT NULL,
                quantity INTEGER NOT NULL,
                travel_cost INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_arbitrage_history_item ON ArbitrageHistory(item_id);
            CREATE INDEX IF NOT EXISTS idx_arbitrage_history_timestamp ON ArbitrageHistory(timestamp);
        ";
        ExecuteNonQuery(connection, createArbitrageHistoryTable, transaction);
    }

    private void Migration_8_AddArbitrageMetrics(SqliteConnection connection, SqliteTransaction transaction)
    {
        // Add missing columns if they don't exist
        // Usually needed if Migration_7 was minimal or changed
        // For now, let's just ensure indices or add a 'transferred_at' if we want to track actual execution?
        // But the history table is for opportunities recorded, not necessarily executed trades unless we hook into inventory.
        // Let's assume Migration_7 covers the table structure.
        
        // Let's add an index on (home_world_id, target_world_id) for route analysis
        var createRouteIndex = "CREATE INDEX IF NOT EXISTS idx_arbitrage_route ON ArbitrageHistory(home_world_id, target_world_id);";
        ExecuteNonQuery(connection, createRouteIndex, transaction);
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
                        listing_count, velocity, current_listings_json, recent_sales_json,
                        sales_per_day, demand_ratio
                    ) VALUES (
                        @itemId, @worldId, @lastUpdated, @minPrice, @avgPrice,
                        @listingCount, @velocity, @listingsJson, @salesJson,
                        @salesPerDay, @demandRatio
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
                command.Parameters.AddWithValue("@salesPerDay", data.SalesPerDay);
                command.Parameters.AddWithValue("@demandRatio", data.DemandRatio);
                
                command.ExecuteNonQuery();

                // 1.5. Record Sale Velocity History (once per update if significantly changed or old)
                // We check if we need to insert a new velocity record
                command.CommandText = "SELECT velocity, timestamp FROM VelocityHistory WHERE item_id = @itemId AND world_id = @worldId ORDER BY timestamp DESC LIMIT 1";
                using (var velocityReader = command.ExecuteReader())
                {
                    bool shouldInsertVelocity = true;
                    if (velocityReader.Read())
                    {
                        double lastVelocity = velocityReader.GetDouble(0);
                        long lastTimestamp = velocityReader.GetInt64(1);
                        long currentTs = ((DateTimeOffset)data.LastUploadTime).ToUnixTimeSeconds();
                        
                        // Only insert if > 24 hours since last record OR velocity changed by > 10%
                        if (currentTs - lastTimestamp < 86400) // 24 hours
                        {
                            double change = Math.Abs(data.SaleVelocity - lastVelocity);
                            if (lastVelocity > 0 && change / lastVelocity < 0.1) // < 10% change
                            {
                                shouldInsertVelocity = false;
                            }
                        }
                    }
                    
                    velocityReader.Close(); // Close before reusing command
                    
                    if (shouldInsertVelocity)
                    {
                        using var velocityCmd = connection.CreateCommand();
                        velocityCmd.Transaction = transaction;
                        velocityCmd.CommandText = @"
                            INSERT OR REPLACE INTO VelocityHistory (item_id, world_id, timestamp, velocity)
                            VALUES (@itemId, @worldId, @timestamp, @velocity);
                        ";
                        velocityCmd.Parameters.AddWithValue("@itemId", data.ItemId);
                        velocityCmd.Parameters.AddWithValue("@worldId", worldId);
                        velocityCmd.Parameters.AddWithValue("@timestamp", ((DateTimeOffset)data.LastUploadTime).ToUnixTimeSeconds());
                        velocityCmd.Parameters.AddWithValue("@velocity", data.SaleVelocity);
                        velocityCmd.ExecuteNonQuery();
                    }
                }

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
                        listing_count, velocity, current_listings_json, recent_sales_json,
                        sales_per_day, demand_ratio
                    ) VALUES (
                        @itemId, @worldId, @lastUpdated, @minPrice, @avgPrice,
                        @listingCount, @velocity, @listingsJson, @salesJson,
                        @salesPerDay, @demandRatio
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
                var pSalesPerDay = marketCmd.Parameters.Add("@salesPerDay", SqliteType.Real);
                var pDemandRatio = marketCmd.Parameters.Add("@demandRatio", SqliteType.Real);

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
                    pSalesPerDay.Value = data.SalesPerDay;
                    pDemandRatio.Value = data.DemandRatio;
                    
                    marketCmd.ExecuteNonQuery();

                    // 1.5 Insert Velocity History
                    // Simplified logic for bulk: just insert if we have data, let SQLite handle PK collisions?
                    // Actually, let's just insert one record per day max effectively by PK (item, world, timestamp)
                    // But timestamp is last_upload_time, which changes.
                    // For bulk, let's just insert it. It's historical data.
                    using (var velocityCmd = connection.CreateCommand())
                    {
                        velocityCmd.Transaction = transaction;
                        velocityCmd.CommandText = @"
                            INSERT OR IGNORE INTO VelocityHistory (item_id, world_id, timestamp, velocity)
                            VALUES (@itemId, @worldId, @timestamp, @velocity);
                        ";
                        velocityCmd.Parameters.AddWithValue("@itemId", data.ItemId);
                        velocityCmd.Parameters.AddWithValue("@worldId", worldId);
                        velocityCmd.Parameters.AddWithValue("@timestamp", ((DateTimeOffset)data.LastUploadTime).ToUnixTimeSeconds());
                        velocityCmd.Parameters.AddWithValue("@velocity", data.SaleVelocity);
                        velocityCmd.ExecuteNonQuery();
                    }

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
                    SELECT last_updated, min_price, average_price, listing_count, velocity, current_listings_json, recent_sales_json, sales_per_day, demand_ratio
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
                    
                    // Optional columns added in later migration
                    if (!reader.IsDBNull(7)) marketData.SalesPerDay = (float)reader.GetDouble(7);
                    if (!reader.IsDBNull(8)) marketData.DemandRatio = (float)reader.GetDouble(8);

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

    public List<MarketSnapshot> GetMarketSnapshots(int itemId, int worldId, DateTime since)
    {
        lock (dbLock)
        {
            var results = new List<MarketSnapshot>();
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    SELECT timestamp, price, quantity 
                    FROM PriceHistory
                    WHERE item_id = @itemId AND world_id = @worldId AND is_sale = 0 AND timestamp >= @since
                    ORDER BY timestamp ASC
                ";
                command.Parameters.AddWithValue("@itemId", itemId);
                command.Parameters.AddWithValue("@worldId", worldId);
                command.Parameters.AddWithValue("@since", ((DateTimeOffset)since).ToUnixTimeSeconds());

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(new MarketSnapshot
                    {
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0)).UtcDateTime,
                        MinPrice = (uint)reader.GetInt32(1),
                        ListingCount = reader.GetInt32(2)
                    });
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, $"Failed to get market snapshots for item {itemId}");
            }
            return results;
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
                        risk_score, recommendation_score, gil_per_hour, ingredients_json
                    ) VALUES (
                        @recipeId, @itemId, @lastAnalyzed, @profit, @margin,
                        @risk, @recommendation, @gilPerHour, @ingredientsJson
                    );
                ";

                command.Parameters.AddWithValue("@recipeId", recipe.RecipeId);
                command.Parameters.AddWithValue("@itemId", recipe.ResultItemId);
                command.Parameters.AddWithValue("@lastAnalyzed", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                command.Parameters.AddWithValue("@profit", profit.NetProfit);
                command.Parameters.AddWithValue("@margin", profit.ProfitMargin);
                command.Parameters.AddWithValue("@risk", profit.RiskScore);
                command.Parameters.AddWithValue("@recommendation", profit.RecommendationScore);
                command.Parameters.AddWithValue("@gilPerHour", profit.GilPerHour);
                command.Parameters.AddWithValue("@ingredientsJson", JsonSerializer.Serialize(recipe.Ingredients));
                
                command.ExecuteNonQuery();

                // Record Arbitrage Opportunity if profitable
                if (profit.ArbitrageProfit > 0 && !string.IsNullOrEmpty(profit.CheapestWorldName))
                {
                    // Need to resolve World IDs. We don't have UniversalisService here to resolve names.
                    // But we can store names if we change schema or just skip for now?
                    // Actually, we should probably add a method to record arbitrage separately or pass in world IDs.
                    
                    // For now, let's just log it if we can resolve IDs, or maybe we update RecordArbitrageOpportunity separately.
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                log.Error(ex, $"Failed to upsert recipe cache for recipe {recipe.RecipeId}");
            }
        }
    }
    
    public void RecordArbitrageOpportunity(uint itemId, int homeWorldId, int targetWorldId, int profit, float roi, int buyPrice, int sellPrice, int quantity, int travelCost)
    {
        lock (dbLock)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = connection.CreateCommand();
                
                command.CommandText = @"
                    INSERT INTO ArbitrageHistory (
                        item_id, home_world_id, target_world_id, timestamp, 
                        profit, roi, buy_price, sell_price, quantity, travel_cost
                    ) VALUES (
                        @itemId, @homeId, @targetId, @timestamp,
                        @profit, @roi, @buyPrice, @sellPrice, @quantity, @travelCost
                    );
                ";
                
                command.Parameters.AddWithValue("@itemId", itemId);
                command.Parameters.AddWithValue("@homeId", homeWorldId);
                command.Parameters.AddWithValue("@targetId", targetWorldId);
                command.Parameters.AddWithValue("@timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                command.Parameters.AddWithValue("@profit", profit);
                command.Parameters.AddWithValue("@roi", roi);
                command.Parameters.AddWithValue("@buyPrice", buyPrice);
                command.Parameters.AddWithValue("@sellPrice", sellPrice);
                command.Parameters.AddWithValue("@quantity", quantity);
                command.Parameters.AddWithValue("@travelCost", travelCost);
                
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                log.Error(ex, $"Failed to record arbitrage opportunity for item {itemId}");
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
    
    public List<(uint RecipeId, ProfitCalculation Profit, long LastAnalyzed)> GetAllCachedProfits(int maxAgeHours = 24, int limit = 1000, int offset = 0)
    {
        lock (dbLock)
        {
            var results = new List<(uint, ProfitCalculation, long)>();
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = connection.CreateCommand();

                var cutoffTime = DateTimeOffset.UtcNow.AddHours(-maxAgeHours).ToUnixTimeSeconds();
                command.CommandText = @"
                    SELECT recipe_id, profit_snapshot, margin_snapshot, risk_score, 
                           recommendation_score, gil_per_hour, last_analyzed
                    FROM RecipeCache
                    WHERE last_analyzed > @cutoff
                    ORDER BY recommendation_score DESC
                    LIMIT @limit OFFSET @offset
                ";
                command.Parameters.AddWithValue("@cutoff", cutoffTime);
                command.Parameters.AddWithValue("@limit", limit);
                command.Parameters.AddWithValue("@offset", offset);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var profit = new ProfitCalculation
                    {
                        NetProfit = reader.GetInt32(1),
                        ProfitMargin = reader.GetFloat(2),
                        RiskScore = reader.GetInt32(3),
                        RecommendationScore = reader.GetInt32(4),
                        GilPerHour = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    };
                    results.Add(((uint)reader.GetInt32(0), profit, reader.GetInt64(6)));
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to get all cached profits");
            }
            return results;
        }
    }
    
    /// <summary>
    /// Log API request for debugging and rate limiting analysis.
    /// Virtual for testing/mocking.
    /// </summary>
    public virtual void LogApiRequest(string endpoint, DateTime timestamp, long responseTimeMs, int statusCode, bool success, long payloadSize = 0)
    {
        // Don't block the main thread for logging
        lock (dbLock)
        {
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    INSERT INTO ApiRequestLog (endpoint, timestamp, response_time_ms, status_code, success, payload_size)
                    VALUES (@endpoint, @timestamp, @responseTime, @statusCode, @success, @payloadSize);
                ";
                
                command.Parameters.AddWithValue("@endpoint", endpoint);
                command.Parameters.AddWithValue("@timestamp", ((DateTimeOffset)timestamp).ToUnixTimeSeconds());
                command.Parameters.AddWithValue("@responseTime", responseTimeMs);
                command.Parameters.AddWithValue("@statusCode", statusCode);
                command.Parameters.AddWithValue("@success", success);
                command.Parameters.AddWithValue("@payloadSize", payloadSize);
                
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to log API request");
            }
        }
    }

    public List<ApiRequestLogEntry> GetRecentApiRequests(int limit = 100)
    {
        lock (dbLock)
        {
            var results = new List<ApiRequestLogEntry>();
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    SELECT id, endpoint, timestamp, response_time_ms, status_code, success, payload_size
                    FROM ApiRequestLog
                    ORDER BY timestamp DESC
                    LIMIT @limit
                ";
                command.Parameters.AddWithValue("@limit", limit);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(new ApiRequestLogEntry
                    {
                        Id = reader.GetInt64(0),
                        Endpoint = reader.GetString(1),
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(2)).UtcDateTime,
                        ResponseTimeMs = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                        StatusCode = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                        Success = reader.GetBoolean(5),
                        PayloadSize = reader.IsDBNull(6) ? 0 : reader.GetInt64(6)
                    });
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to get recent API requests");
            }
            return results;
        }
    }
    
    public void Vacuum()
    {
        log.Information("Running database VACUUM...");
        ExecuteSafe("VACUUM;");
    }

    public void CheckAndRunVacuum(int frequencyDays, DateTime lastVacuum, Action<DateTime> onVacuumComplete)
    {
        if (frequencyDays <= 0) return; // Disabled

        var daysSince = (DateTime.UtcNow - lastVacuum).TotalDays;
        if (daysSince >= frequencyDays)
        {
            log.Information($"Database maintenance due (last run: {daysSince:F1} days ago). Running VACUUM...");
            
            // Run in background to not block startup
            Task.Run(() => 
            {
                try
                {
                    Vacuum();
                    onVacuumComplete(DateTime.UtcNow);
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Failed to run scheduled database maintenance");
                }
            });
        }
    }
    
    public long GetDatabaseSize()
    {
        try 
        {
            var fileInfo = new FileInfo(dbFilePath);
            return fileInfo.Length;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to get database size");
            return 0;
        }
    }

    public List<string> GetAllTables()
    {
        lock (dbLock)
        {
            var tables = new List<string>();
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
                
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    tables.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to get all tables");
            }
            return tables;
        }
    }

    public QueryResult ExecuteCustomQuery(string sql)
    {
        lock (dbLock)
        {
            var result = new QueryResult();
            try
            {
                using var connection = GetConnection();
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                
                // Determine if it's a SELECT query
                bool isSelect = sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) || 
                                sql.TrimStart().StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase);

                if (isSelect)
                {
                    using var reader = command.ExecuteReader();
                    
                    // Get column names
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        result.Columns.Add(reader.GetName(i));
                    }

                    // Get rows
                    while (reader.Read())
                    {
                        var row = new List<string>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            if (reader.IsDBNull(i))
                                row.Add("NULL");
                            else
                                row.Add(reader.GetValue(i).ToString() ?? "");
                        }
                        result.Rows.Add(row);
                    }
                }
                else
                {
                    result.RecordsAffected = command.ExecuteNonQuery();
                    result.Message = $"Query executed successfully. Records affected: {result.RecordsAffected}";
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                log.Error(ex, "Failed to execute custom query");
            }
            return result;
        }
    }

    public class QueryResult
    {
        public List<string> Columns { get; set; } = new();
        public List<List<string>> Rows { get; set; } = new();
        public string? Error { get; set; }
        public string? Message { get; set; }
        public int RecordsAffected { get; set; }
    }

    public void Dispose()
    {
        // SQLite connection pooling handles actual connection closing
        SqliteConnection.ClearAllPools();
    }
}
