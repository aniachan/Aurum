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

        if (recentRequests != null && ImGui.BeginTable("ApiLog", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
        {
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("Endpoint", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Duration", ImGuiTableColumnFlags.WidthFixed, 80);
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
        
        if (ImGui.Button("Run VACUUM"))
        {
            plugin.DatabaseService.Vacuum();
        }
        ImGui.SameLine();
        ImGui.Text("Optimizes database file size");
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
