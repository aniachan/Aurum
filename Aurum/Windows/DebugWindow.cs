using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Aurum.Services;

namespace Aurum.Windows;

public class DebugWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private Models.ApiRequestLogEntry[]? recentRequests;
    private string customQuery = "";
    private DatabaseService.QueryResult? queryResult;
    private string[]? tableList;
    private string selectedTable = "";

    // Performance Graph State
    private readonly float[] throughputHistory = new float[60];
    private int historyOffset = 0;
    private DateTime lastGraphUpdate = DateTime.MinValue;

    public DebugWindow(Plugin plugin) : base("Aurum Debug Tools")
    {
        this.plugin = plugin;
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("DebugTabs"))
        {
            if (ImGui.BeginTabItem("General"))
            {
                DrawGeneralTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("API & Network"))
            {
                DrawNetworkTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Rate Limiter"))
            {
                DrawRateLimiterTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Cache & DB"))
            {
                DrawDatabaseTab();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Request Queue"))
            {
                DrawRequestQueueTab();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("DB Browser"))
            {
                DrawDbBrowserTab();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Performance"))
            {
                DrawPerformanceTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Test Gen"))
            {
                DrawTestGenTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawGeneralTab()
    {
        ImGui.Text("Aurum Debug Interface");
        ImGui.Separator();
        
        ImGui.Text($"Plugin Version: {plugin.GetType().Assembly.GetName().Version}");
        
        if (ImGui.Button("Clear All Cache"))
        {
            plugin.CacheService.Clear();
        }
        
        ImGui.Separator();
        ImGui.Text("Services Status:");
        ImGui.Text($"Universalis: {(plugin.RateLimiter.IsDegraded ? "Degraded" : "Healthy")}");
        ImGui.Text($"Database Size: {plugin.DatabaseService.GetDatabaseSize() / 1024.0 / 1024.0:F2} MB");
    }

    private void DrawNetworkTab()
    {
        ImGui.Text("Universalis API Monitor");
        
        if (ImGui.Button("Refresh Log"))
        {
            recentRequests = plugin.DatabaseService.GetRecentApiRequests(50).ToArray();
        }

        ImGui.SameLine();
        ImGui.Text($"Degraded Mode: {plugin.RateLimiter.IsDegraded}");

        if (recentRequests != null && ImGui.BeginTable("ApiLog", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
        {
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("Endpoint", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Duration", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Size", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Result", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableHeadersRow();

            foreach (var req in recentRequests)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(req.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff"));
                
                ImGui.TableNextColumn();
                ImGui.Text(req.Endpoint);
                
                ImGui.TableNextColumn();
                ImGui.Text(req.StatusCode.ToString());
                
                ImGui.TableNextColumn();
                ImGui.Text($"{req.ResponseTimeMs}ms");

                ImGui.TableNextColumn();
                ImGui.Text($"{req.PayloadSize}B");
                
                ImGui.TableNextColumn();
                if (req.Success)
                    ImGui.TextColored(new Vector4(0, 1, 0, 1), "OK");
                else
                    ImGui.TextColored(new Vector4(1, 0, 0, 1), "FAIL");
            }
            ImGui.EndTable();
        }
        else if (recentRequests == null)
        {
            ImGui.Text("Click Refresh to load logs.");
        }
    }

    private void DrawRateLimiterTab()
    {
        ImGui.Text("Rate Limiter Status");
        ImGui.Separator();

        var limiter = plugin.RateLimiter;

        // Current status overview
        if (limiter.IsDegraded)
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "STATUS: DEGRADED / BACKOFF MODE");
        }
        else if (DateTime.UtcNow < limiter.PausedUntil)
        {
            var timeLeft = (limiter.PausedUntil - DateTime.UtcNow).TotalSeconds;
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), $"STATUS: PAUSED (Retry-After) - {timeLeft:F1}s");
        }
        else
        {
            ImGui.TextColored(new Vector4(0, 1, 0, 1), "STATUS: HEALTHY");
        }

        ImGui.Dummy(new Vector2(0, 10));

        // Real-time Metrics
        ImGui.Columns(2, "RateLimitMetrics", false);
        
        ImGui.Text("Token Bucket:");
        ImGui.ProgressBar((float)(limiter.CurrentTokens / limiter.MaxTokens), new Vector2(-1, 0), $"{limiter.CurrentTokens:F1} / {limiter.MaxTokens:F0}");
        ImGui.Text($"Refill Rate: {limiter.RefillRate:F2} tokens/sec");
        
        ImGui.NextColumn();
        
        ImGui.Text("Queue:");
        ImGui.Text($"Waiting Requests: {limiter.WaitingRequests}");
        ImGui.Text($"Processing Rate: {limiter.RequestsLastMinute / 60.0:F2} req/sec (avg 1m)");

        ImGui.Columns(1);
        ImGui.Separator();

        // Statistics
        if (ImGui.BeginTable("RateStats", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Metric", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
            
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text("Total Requests");
            ImGui.TableNextColumn(); ImGui.Text($"{limiter.TotalRequests}");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text("Rate Limited (Internal)");
            ImGui.TableNextColumn(); ImGui.Text($"{limiter.RateLimitedRequests}");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text("Total Errors");
            ImGui.TableNextColumn(); ImGui.TextColored(limiter.TotalErrors > 0 ? new Vector4(1, 0.5f, 0, 1) : new Vector4(1, 1, 1, 1), $"{limiter.TotalErrors}");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text("Total Retries");
            ImGui.TableNextColumn(); ImGui.Text($"{limiter.TotalRetries}");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text("Last Minute");
            ImGui.TableNextColumn(); ImGui.Text($"{limiter.RequestsLastMinute}");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text("Last Hour");
            ImGui.TableNextColumn(); ImGui.Text($"{limiter.RequestsLastHour}");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text("Today");
            ImGui.TableNextColumn(); ImGui.Text($"{limiter.RequestsToday}");

            ImGui.EndTable();
        }
    }

    private void DrawDatabaseTab()
    {
        ImGui.Text("Database & Cache Management");
        
        var stats = plugin.CacheService.GetStats();
        ImGui.Text($"Memory Cache Entries: {stats.ActiveEntries}");
        ImGui.Text($"Cache Hits: {stats.Hits}");
        ImGui.Text($"Cache Misses: {stats.Misses}");
        ImGui.Text($"Hit Rate: {stats.HitRate:F1}%");
        
        ImGui.Separator();

        if (ImGui.TreeNode("Cache Inspector"))
        {
            var snapshot = plugin.CacheService.GetSnapshot();
            
            ImGui.Text($"Total Entries: {snapshot.Count}");
            
            if (ImGui.Button("Refresh Snapshot"))
            {
                // Re-fetch next frame
            }
            
            ImGui.SameLine();
            if (ImGui.Button("Invalidate Selected"))
            {
                // TODO: Implement selection
            }

            if (ImGui.BeginTable("CacheTable", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable, new Vector2(0, 300)))
            {
                ImGui.TableSetupColumn("Key", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 100);
                ImGui.TableSetupColumn("Expires In", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Age", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableHeadersRow();

                foreach (var entry in snapshot)
                {
                    ImGui.TableNextRow();
                    
                    ImGui.TableNextColumn();
                    ImGui.Text(entry.Key);
                    if (ImGui.IsItemHovered() && entry.ItemId.HasValue)
                    {
                        ImGui.SetTooltip($"Item ID: {entry.ItemId}\nWorld: {entry.WorldName}");
                    }
                    
                    ImGui.TableNextColumn();
                    ImGui.Text(entry.TypeName);
                    
                    ImGui.TableNextColumn();
                    var timeLeft = entry.ExpiresAt - DateTime.UtcNow;
                    if (timeLeft.TotalSeconds < 0)
                        ImGui.TextColored(new Vector4(1, 0, 0, 1), "Expired");
                    else
                        ImGui.Text($"{timeLeft.TotalMinutes:F1}m");
                        
                    ImGui.TableNextColumn();
                    var age = DateTime.UtcNow - entry.LastAccessed;
                    ImGui.Text($"{age.TotalSeconds:F0}s ago");
                    
                    ImGui.TableNextColumn();
                    if (ImGui.Button($"X##{entry.Key}"))
                    {
                        plugin.CacheService.Invalidate(entry.Key);
                    }
                }
                ImGui.EndTable();
            }
            ImGui.TreePop();
        }
        
        ImGui.Separator();
        
        if (ImGui.Button("Run VACUUM"))
        {
            plugin.DatabaseService.Vacuum();
        }
        ImGui.SameLine();
        ImGui.Text("Optimizes database file size");
    }

    private void DrawRequestQueueTab()
    {
        ImGui.Text("Request Queue Visualizer");
        ImGui.SameLine();
        ImGui.TextDisabled("(Items waiting to be sent to Universalis)");
        
        ImGui.Separator();
        
        // Stats Header
        var queue = plugin.RequestQueue;
        var snapshot = queue.GetSnapshot();
        
        ImGui.Columns(3, "QueueStats", false);
        ImGui.Text($"Total Pending: {snapshot.Count}");
        ImGui.NextColumn();
        ImGui.Text($"Distinct Worlds: {snapshot.Select(r => r.WorldName).Distinct().Count()}");
        ImGui.NextColumn();
        ImGui.Text($"Total Items: {snapshot.Sum(r => r.ItemIds.Count)}");
        ImGui.Columns(1);
        
        ImGui.Separator();
        
        // Controls
        if (ImGui.Button("Clear Queue"))
        {
            queue.Clear();
        }
        ImGui.SameLine();
        if (ImGui.Button("Refresh View"))
        {
            // Just redraws
        }
        
        ImGui.Dummy(new Vector2(0, 10));

        // Queue Table
        if (ImGui.BeginTable("QueueTable", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable, new Vector2(0, 400)))
        {
            ImGui.TableSetupColumn("Priority", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Items", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Age", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableHeadersRow();

            foreach (var req in snapshot)
            {
                ImGui.TableNextRow();
                
                ImGui.TableNextColumn();
                Vector4 color = req.Priority switch
                {
                    RequestPriority.Critical => new Vector4(1, 0, 0, 1),
                    RequestPriority.High => new Vector4(1, 0.5f, 0, 1),
                    RequestPriority.Normal => new Vector4(1, 1, 1, 1),
                    RequestPriority.Background => new Vector4(0.5f, 0.5f, 0.5f, 1),
                    _ => new Vector4(1, 1, 1, 1)
                };
                ImGui.TextColored(color, req.Priority.ToString());
                
                ImGui.TableNextColumn();
                ImGui.Text(req.WorldName);
                
                ImGui.TableNextColumn();
                var itemList = string.Join(", ", req.ItemIds.Take(10));
                if (req.ItemIds.Count > 10) itemList += $"... (+{req.ItemIds.Count - 10} more)";
                ImGui.Text(itemList);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(string.Join(", ", req.ItemIds));
                }
                
                ImGui.TableNextColumn();
                var age = DateTimeOffset.UtcNow - req.Timestamp;
                ImGui.Text($"{age.TotalSeconds:F1}s");
                
                ImGui.TableNextColumn();
                ImGui.Text($"{req.ItemIds.Count}");
            }
            ImGui.EndTable();
        }
    }

    private void DrawDbBrowserTab()
    {
        ImGui.Text("Database Browser");
        
        if (tableList == null)
        {
            tableList = plugin.DatabaseService.GetAllTables().ToArray();
            if (tableList.Length > 0)
                selectedTable = tableList[0];
        }

        ImGui.Columns(2);
        ImGui.SetColumnWidth(0, 200);

        // Sidebar: Tables
        ImGui.Text("Tables");
        ImGui.Separator();
        
        if (ImGui.BeginListBox("##Tables", new Vector2(-1, -1)))
        {
            foreach (var table in tableList)
            {
                if (ImGui.Selectable(table, selectedTable == table))
                {
                    selectedTable = table;
                    customQuery = $"SELECT * FROM {selectedTable} LIMIT 100";
                    queryResult = plugin.DatabaseService.ExecuteCustomQuery(customQuery);
                }
            }
            ImGui.EndListBox();
        }

        ImGui.NextColumn();

        // Main Area: Query & Results
        ImGui.Text("SQL Query");
        ImGui.InputTextMultiline("##Query", ref customQuery, 1000, new Vector2(-1, 80));
        
        if (ImGui.Button("Execute Query"))
        {
            queryResult = plugin.DatabaseService.ExecuteCustomQuery(customQuery);
        }

        ImGui.Separator();

        if (queryResult != null)
        {
            if (queryResult.Error != null)
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), $"Error: {queryResult.Error}");
            }
            else if (queryResult.Message != null)
            {
                ImGui.TextColored(new Vector4(0, 1, 0, 1), queryResult.Message);
            }
            else
            {
                ImGui.Text($"Rows: {queryResult.Rows.Count}");
                if (ImGui.BeginTable("Results", queryResult.Columns.Count, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable))
                {
                    foreach (var col in queryResult.Columns)
                    {
                        ImGui.TableSetupColumn(col);
                    }
                    ImGui.TableHeadersRow();

                    foreach (var row in queryResult.Rows)
                    {
                        ImGui.TableNextRow();
                        foreach (var cell in row)
                        {
                            ImGui.TableNextColumn();
                            ImGui.Text(cell);
                        }
                    }
                    ImGui.EndTable();
                }
            }
        }
        else
        {
             ImGui.Text("Select a table or run a query to view data.");
        }

        ImGui.Columns(1);
    }

    private void DrawPerformanceTab()
    {
        ImGui.Text("Performance Profiler");
        
        // Update Graph
        var now = DateTime.UtcNow;
        if ((now - lastGraphUpdate).TotalSeconds >= 1)
        {
            lastGraphUpdate = now;
            var requests = plugin.RateLimiter.RequestsLastMinute;
            throughputHistory[historyOffset] = requests / 60.0f; // Approx req/sec
            historyOffset = (historyOffset + 1) % throughputHistory.Length;
        }

        // 1. Throughput Graph
        ImGui.Text("API Throughput (req/sec - last 60s)");
        ImGui.PlotLines("##Throughput", throughputHistory, throughputHistory.Length, 
            $"{throughputHistory[(historyOffset - 1 + throughputHistory.Length) % throughputHistory.Length]:F1} r/s", 
            0, 5, new Vector2(-1, 80)); // scale 0-5 req/sec

        ImGui.Separator();

        // 2. Key Metrics Grid
        if (ImGui.BeginTable("PerfMetrics", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Metric", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Evaluation", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableHeadersRow();

            // API Latency (from DB stats if available, or just use last request)
            var apiStats = plugin.PerformanceMonitor.GetStats("Universalis_FetchSingle");
            DrawMetricRow("Avg API Latency", apiStats?.AverageMs ?? 0, "ms", 500, 2000);
            
            // Database Query Time
            var dbStats = plugin.PerformanceMonitor.GetStats("Database_Upsert"); // Example metric
            DrawMetricRow("DB Write Time", dbStats?.AverageMs ?? 0, "ms", 10, 50);

            // Memory Usage
            var memory = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
            DrawMetricRow("Managed Memory", memory, "MB", 100, 500);

            // DB Size
            var dbSize = plugin.DatabaseService.GetDatabaseSize() / 1024.0 / 1024.0;
            DrawMetricRow("Database Size", dbSize, "MB", 50, 200);

            ImGui.EndTable();
        }

        ImGui.Dummy(new Vector2(0, 10));

        // 3. Detailed Stats Table (Existing)
        ImGui.Text("Detailed Execution Stats");
        if (ImGui.Button("Refresh Stats"))
        {
            // Just triggers redraw with fresh data
        }

        if (ImGui.BeginTable("PerfStats", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable))
        {
            ImGui.TableSetupColumn("Operation", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Avg (ms)", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Max (ms)", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableHeadersRow();

            var stats = plugin.PerformanceMonitor.GetAllStats();
            foreach (var stat in stats)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(stat.Key);
                
                ImGui.TableNextColumn();
                ImGui.Text($"{stat.AverageMs:F2}");
                
                ImGui.TableNextColumn();
                ImGui.Text($"{stat.MaxMs}");
                
                ImGui.TableNextColumn();
                ImGui.Text($"{stat.Count}");
            }
            ImGui.EndTable();
        }
    }

    private void DrawMetricRow(string label, double value, string unit, double warnThreshold, double critThreshold)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text(label);
        
        ImGui.TableNextColumn();
        ImGui.Text($"{value:F2} {unit}");
        
        ImGui.TableNextColumn();
        if (value > critThreshold)
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "CRITICAL");
        else if (value > warnThreshold)
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "WARNING");
        else
            ImGui.TextColored(new Vector4(0, 1, 0, 1), "GOOD");
    }

    private void DrawTestGenTab()
    {
        ImGui.Text("Test Data Generator");
        ImGui.TextDisabled("Use these tools to populate the database with mock data for testing UI and performance.");
        
        ImGui.Separator();
        
        ImGui.Text("Market Data Generation");
        
        if (ImGui.Button("Generate 100 Mock Items"))
        {
            // Generate IDs 1000-1100
            var ids = Enumerable.Range(1000, 100).Select(i => (uint)i);
            // Default to Gilgamesh (63) for testing if not set, or use current
            var worldId = 63; 
            
            System.Threading.Tasks.Task.Run(async () => 
            {
                await plugin.TestDataGenerator.GenerateMockMarketDataAsync(worldId, ids);
            });
        }
        ImGui.SameLine();
        ImGui.Text("(IDs 1000-1100)");

        if (ImGui.Button("Generate 1000 Mock Items"))
        {
             var ids = Enumerable.Range(2000, 1000).Select(i => (uint)i);
             var worldId = 63;
             System.Threading.Tasks.Task.Run(async () => 
             {
                 await plugin.TestDataGenerator.GenerateMockMarketDataAsync(worldId, ids);
             });
        }
        ImGui.SameLine();
        ImGui.Text("(IDs 2000-3000)");
        
        ImGui.Separator();
        ImGui.Text("Simulation");
        
        if (ImGui.Button("Simulate 5 API Errors"))
        {
            plugin.TestDataGenerator.SimulateApiErrors(5);
        }
        
        if (ImGui.Button("Clear All Market Data"))
        {
            plugin.TestDataGenerator.ClearAllMarketData();
        }
    }
}
