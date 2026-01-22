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

            if (ImGui.BeginTabItem("Cache & DB"))
            {
                DrawDatabaseTab();
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
}
