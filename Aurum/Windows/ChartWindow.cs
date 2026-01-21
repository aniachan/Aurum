using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Aurum.Models;
using Dalamud.Bindings.ImGui;
// Implot is now imported through dalamud. Fancy charts can now be added.
using Dalamud.Bindings.ImPlot;

namespace Aurum.Windows;

public class ChartWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private MarketData? currentData;
    private string itemName = "Unknown Item";
    private int selectedTimeRange = 30; // Default 30 days, -1 for All

    public ChartWindow(Plugin plugin) : base("Price History Chart##AurumCharts", ImGuiWindowFlags.None)
    {
        this.plugin = plugin;
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void SetMarketData(MarketData data, string name)
    {
        currentData = data;
        itemName = name;
        IsOpen = true;
    }

    public override void Draw()
    {
        if (currentData == null)
        {
            ImGui.Text("No market data available.");
            return;
        }

        ImGui.Text($"Price History: {itemName}");
        
        // Time Range Selector
        ImGui.Separator();
        ImGui.Text("Time Range:");
        ImGui.SameLine();
        
        if (ImGui.RadioButton("1 Day", selectedTimeRange == 1)) selectedTimeRange = 1;
        ImGui.SameLine();
        if (ImGui.RadioButton("7 Days", selectedTimeRange == 7)) selectedTimeRange = 7;
        ImGui.SameLine();
        if (ImGui.RadioButton("30 Days", selectedTimeRange == 30)) selectedTimeRange = 30;
        ImGui.SameLine();
        if (ImGui.RadioButton("All Time", selectedTimeRange == -1)) selectedTimeRange = -1;

        ImGui.Separator();

        if (currentData.RecentHistory == null || !currentData.RecentHistory.Any())
        {
            ImGui.Text("No history data available for chart.");
        }
        else
        {
            var history = currentData.RecentHistory.OrderBy(h => h.Timestamp).AsEnumerable();
            
            if (selectedTimeRange > 0)
            {
                var cutoff = DateTime.UtcNow.AddDays(-selectedTimeRange);
                history = history.Where(h => h.Timestamp >= cutoff);
            }

            var historyList = history.ToList();

            if (!historyList.Any())
            {
                ImGui.TextColored(new Vector4(1f, 0.5f, 0f, 1f), $"No sales found in the last {selectedTimeRange} days.");
            }
            else
            {
                // Aggregate data for Volume (Quantity) analysis
                // We do this first so we can use it in the main plot if we want, or just keep it organized.
                var dailySales = historyList
                    .GroupBy(x => x.Timestamp.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new { Date = g.Key, Volume = g.Sum(x => x.Quantity) })
                    .ToList();

                // Draw Chart using ImPlot
                // We use a bit more height since we are combining views
                if (ImPlot.BeginPlot("Price History & Volume", new Vector2(-1, 400), ImPlotFlags.None))
                {
                    // Setup axes
                    ImPlot.SetupAxes("Date", "Price (Gil)", ImPlotAxisFlags.AutoFit, ImPlotAxisFlags.AutoFit);
                    ImPlot.SetupAxisScale(ImAxis.X1, ImPlotScale.Time);
                    
                    // Setup Y2 axis for Volume
                    ImPlot.SetupAxis(ImAxis.Y2, "Daily Volume", ImPlotAxisFlags.AutoFit | ImPlotAxisFlags.AuxDefault);
                    
                    // ---------------------------------------------------------
                    // PLOT 2 (Background): Daily Volume Bars on Y2 Axis
                    // ---------------------------------------------------------
                    if (dailySales.Any())
                    {
                        var barXs = dailySales.Select(x => (double)new DateTimeOffset(x.Date).ToUnixTimeSeconds()).ToArray();
                        var barYs = dailySales.Select(x => (double)x.Volume).ToArray();
                        
                        // Width: 1 day in seconds is 86400. Use 0.8 * 86400 for slight gaps.
                        double barWidth = 86400 * 0.8;

                        // Switch to Y2 axis
                        ImPlot.SetAxes(ImAxis.X1, ImAxis.Y2);
                        
                        // Make bars semi-transparent so they don't obscure price lines too much
                        ImPlot.SetNextFillStyle(new Vector4(0.5f, 0.5f, 0.5f, 0.25f)); 
                        ImPlot.PlotBars("Daily Volume", ref barXs[0], ref barYs[0], barXs.Length, barWidth);
                    }

                    // ---------------------------------------------------------
                    // PLOT 1 (Foreground): Price Line & Scatter on Y1 Axis
                    // ---------------------------------------------------------
                    // Switch back to Y1 axis (default, but good to be explicit after using Y2)
                    ImPlot.SetAxes(ImAxis.X1, ImAxis.Y1);

                    // Convert data to arrays for ImPlot
                    var xs = historyList.Select(x => (double)new DateTimeOffset(x.Timestamp.ToLocalTime()).ToUnixTimeSeconds()).ToArray();
                    var ys = historyList.Select(x => (double)x.PricePerUnit).ToArray();
                    
                    // Plot line
                    ImPlot.SetNextLineStyle(new Vector4(0f, 0.8f, 1f, 1f), 2.0f);
                    ImPlot.PlotLine("Price", ref xs[0], ref ys[0], xs.Length);

                    // Plot scatter points for individual sales
                    ImPlot.SetNextMarkerStyle(ImPlotMarker.Circle, 4, new Vector4(1f, 1f, 1f, 0.8f), 1f, new Vector4(0f, 0.8f, 1f, 1f));
                    ImPlot.PlotScatter("Sales", ref xs[0], ref ys[0], xs.Length);

                    // Add markers for HQ items if any
                    var hqItems = historyList.Where(x => x.IsHQ).ToList();
                    if (hqItems.Any())
                    {
                        var hqXs = hqItems.Select(x => (double)new DateTimeOffset(x.Timestamp.ToLocalTime()).ToUnixTimeSeconds()).ToArray();
                        var hqYs = hqItems.Select(x => (double)x.PricePerUnit).ToArray();
                        
                        ImPlot.SetNextMarkerStyle(ImPlotMarker.Diamond, 6, new Vector4(1f, 0.8f, 0f, 1f), 1f, new Vector4(1f, 0.5f, 0f, 1f));
                        ImPlot.PlotScatter("HQ Sales", ref hqXs[0], ref hqYs[0], hqXs.Length);
                    }
                    
                    ImPlot.EndPlot();
                }

                ImGui.Spacing();
                ImGui.Separator();
            }
        }
        
        // Debug data dump
        if (ImGui.CollapsingHeader("Raw Data Debug"))
        {
            ImGui.Text($"Listings: {currentData.CurrentListings}");
            ImGui.Text($"Avg Price: {currentData.CurrentAveragePrice:F0}");
            
            if (currentData.RecentHistory != null)
            {
                ImGui.Text($"History Entries: {currentData.RecentHistory.Count}");
                
                if (ImGui.BeginTable("HistoryTableDebug", 3))
                {
                    ImGui.TableSetupColumn("Date");
                    ImGui.TableSetupColumn("Price");
                    ImGui.TableSetupColumn("Qty");
                    ImGui.TableHeadersRow();
    
                    foreach (var entry in currentData.RecentHistory.Take(10))
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text(entry.Timestamp.ToLocalTime().ToString("g"));
                        ImGui.TableNextColumn();
                        ImGui.Text(entry.PricePerUnit.ToString("N0"));
                        ImGui.TableNextColumn();
                        ImGui.Text(entry.Quantity.ToString());
                    }
                    ImGui.EndTable();
                }
            }
            else
            {
                ImGui.Text("History Entries: 0 (null)");
            }
        }
    }

    public void Dispose()
    {
        // Cleanup resources if needed
    }
}
