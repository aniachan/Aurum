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
                // Temporary placeholder until ImPlot is available TODO: Remove table. We have Implot now
                ImGui.Text($"Price History Data ({historyList.Count} sales):");
                
                if (ImGui.BeginTable("HistoryTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
                {
                    ImGui.TableSetupScrollFreeze(0, 1);
                    ImGui.TableSetupColumn("Date", ImGuiTableColumnFlags.WidthFixed, 150f);
                    ImGui.TableSetupColumn("Price", ImGuiTableColumnFlags.WidthFixed, 100f);
                    ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 50f);
                    ImGui.TableHeadersRow();

                    // Show up to 100 entries to avoid lag if list is huge
                    foreach (var entry in historyList.Take(100))
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
                
                if (historyList.Count > 100)
                {
                    ImGui.TextDisabled($"... and {historyList.Count - 100} more entries.");
                }
                
                ImGui.Spacing();
                
                // Draw Chart using ImPlot
                if (ImPlot.BeginPlot("Price History", new Vector2(-1, 300), ImPlotFlags.None))
                {
                    // Setup axes
                    ImPlot.SetupAxes("Date", "Price (Gil)", ImPlotAxisFlags.AutoFit, ImPlotAxisFlags.AutoFit);
                    ImPlot.SetupAxisScale(ImAxis.X1, ImPlotScale.Time);
                    
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

                    // Tooltip logic is handled by ImPlot automatically for basic values
                    
                    ImPlot.EndPlot();
                }

                ImGui.Spacing();
                ImGui.Separator();
                
                // Sale Velocity Chart
                ImGui.Text("Sale Velocity (Daily Volume)");
                
                if (ImPlot.BeginPlot("Daily Sales Volume", new Vector2(-1, 200), ImPlotFlags.None))
                {
                    ImPlot.SetupAxes("Date", "Quantity Sold", ImPlotAxisFlags.AutoFit, ImPlotAxisFlags.AutoFit);
                    ImPlot.SetupAxisScale(ImAxis.X1, ImPlotScale.Time);
                    
                    // Aggregate data by day
                    var dailySales = historyList
                        .GroupBy(x => x.Timestamp.Date)
                        .OrderBy(g => g.Key)
                        .Select(g => new { Date = g.Key, Volume = g.Sum(x => x.Quantity) })
                        .ToList();

                    if (dailySales.Any())
                    {
                        var barXs = dailySales.Select(x => (double)new DateTimeOffset(x.Date).ToUnixTimeSeconds()).ToArray();
                        var barYs = dailySales.Select(x => (double)x.Volume).ToArray();
                        var count = dailySales.Count;
                        var maYs = new double[count];

                        // Calculate Moving Average (3-day) first
                        for (int i = 0; i < count; i++)
                        {
                            int w = 0;
                            double sum = 0;
                            // Look back up to 2 days (window size 3)
                            for (int j = Math.Max(0, i - 2); j <= i; j++) 
                            { 
                                sum += dailySales[j].Volume; 
                                w++; 
                            }
                            maYs[i] = sum / w;
                        }

                        // Separate data into normal and spikes (Volume > 1.5x MA)
                        var normalXs = new List<double>();
                        var normalYs = new List<double>();
                        var spikeXs = new List<double>();
                        var spikeYs = new List<double>();

                        for (int i = 0; i < count; i++)
                        {
                            // Simple spike detection: Volume > 1.5x Moving Average and significant volume (> 5 items)
                            bool isSpike = (barYs[i] > maYs[i] * 1.5) && (barYs[i] > 5);
                            
                            if (isSpike)
                            {
                                spikeXs.Add(barXs[i]);
                                spikeYs.Add(barYs[i]);
                            }
                            else
                            {
                                normalXs.Add(barXs[i]);
                                normalYs.Add(barYs[i]);
                            }
                        }

                        // Width: 1 day in seconds is 86400. Use 0.8 * 86400 for slight gaps.
                        double barWidth = 86400 * 0.8; 

                        if (normalXs.Any())
                        {
                            var nXs = normalXs.ToArray();
                            var nYs = normalYs.ToArray();
                            ImPlot.SetNextFillStyle(new Vector4(0.4f, 0.7f, 1.0f, 0.5f)); // Blue-ish
                            ImPlot.PlotBars("Daily Volume", ref nXs[0], ref nYs[0], nXs.Length, barWidth);
                        }

                        if (spikeXs.Any())
                        {
                            var sXs = spikeXs.ToArray();
                            var sYs = spikeYs.ToArray();
                            ImPlot.SetNextFillStyle(new Vector4(1.0f, 0.3f, 0.3f, 0.7f)); // Red-ish
                            ImPlot.PlotBars("High Activity", ref sXs[0], ref sYs[0], sXs.Length, barWidth);
                        }

                        // Plot MA line
                        if (count >= 3)
                        {
                            ImPlot.SetNextLineStyle(new Vector4(1f, 0.6f, 0f, 1f), 2.0f);
                            ImPlot.PlotLine("3-Day Avg", ref barXs[0], ref maYs[0], count);
                        }
                    }

                    ImPlot.EndPlot();
                }
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
