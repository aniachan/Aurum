using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Aurum.Models;
using Dalamud.Bindings.ImGui;
// using ImPlotNET; // ImPlot integration paused until dependency is resolved

namespace Aurum.Windows;

public class ChartWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private MarketData? currentData;
    private string itemName = "Unknown Item";

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
        ImGui.Separator();

        if (currentData.RecentHistory == null || !currentData.RecentHistory.Any())
        {
            ImGui.Text("No history data available for chart.");
        }
        else
        {
            var history = currentData.RecentHistory.OrderBy(h => h.Timestamp).ToList();
            
            // Temporary placeholder until ImPlot is available
            ImGui.Text("Price History Data:");
            
            if (ImGui.BeginTable("HistoryTable", 3))
            {
                ImGui.TableSetupColumn("Date");
                ImGui.TableSetupColumn("Price");
                ImGui.TableSetupColumn("Qty");
                ImGui.TableHeadersRow();

                foreach (var entry in history.Take(20))
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
            
            ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), "ImPlot dependency missing - Charts temporarily disabled");
        }
        
        // Debug data dump
        if (ImGui.CollapsingHeader("Raw Data Debug"))
        {
            ImGui.Text($"Listings: {currentData.CurrentListings}");
            ImGui.Text($"Avg Price: {currentData.CurrentAveragePrice:F0}");
            ImGui.Text($"History Entries: {currentData.RecentHistory.Count}");
            
            if (ImGui.BeginTable("HistoryTable", 3))
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
    }

    public void Dispose()
    {
        // Cleanup resources if needed
    }
}
