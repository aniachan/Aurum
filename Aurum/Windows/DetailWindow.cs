using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Aurum.Models;

namespace Aurum.Windows;

public class DetailWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private ProfitCalculation? currentItem;
    
    public DetailWindow(Plugin plugin) : base("Item Details##AurumDetail", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 500),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void SetItem(ProfitCalculation item)
    {
        currentItem = item;
        IsOpen = true;
        
        // Update window title to include item name
        WindowName = $"{item.Recipe.ItemName} Details##AurumDetail";
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (currentItem == null)
        {
            ImGui.Text("No item selected.");
            return;
        }

        // Header Section
        DrawHeader();
        
        ImGui.Separator();
        
        // Main Content - Split into 2 columns
        if (ImGui.BeginTable("DetailLayout", 2, ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("LeftCol", ImGuiTableColumnFlags.WidthStretch, 0.6f);
            ImGui.TableSetupColumn("RightCol", ImGuiTableColumnFlags.WidthStretch, 0.4f);
            
            ImGui.TableNextRow();
            
            // Left Column: Market & Profit Info
            ImGui.TableNextColumn();
            DrawMarketSnapshot();
            ImGui.Spacing();
            DrawProfitBreakdown();
            
            // Right Column: Risk & Warnings
            ImGui.TableNextColumn();
            DrawRiskAnalysis();
            ImGui.Spacing();
            DrawRecipeInfo();
            
            ImGui.EndTable();
        }
    }

    private void DrawHeader()
    {
        ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), currentItem.Recipe.ItemName);
        ImGui.SameLine();
        ImGui.TextDisabled($"(Lvl {currentItem.Recipe.ClassJobLevel} {currentItem.Recipe.CraftingClassName})");
        
        if (ImGui.Button("View on Universalis"))
        {
            // Open browser (placeholder)
            // Util.OpenUrl($"https://universalis.app/market/{currentItem.MarketData?.ItemId}");
        }
    }

    private void DrawMarketSnapshot()
    {
        ImGui.TextDisabled("MARKET SNAPSHOT");
        if (currentItem.MarketData == null) return;

        if (ImGui.BeginTable("MarketStats", 2))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text("Avg Price (NQ):");
            ImGui.TableNextColumn(); ImGui.Text($"{currentItem.MarketData.CurrentAveragePriceNQ:N0} gil");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text("Avg Price (HQ):");
            ImGui.TableNextColumn(); ImGui.Text($"{currentItem.MarketData.CurrentAveragePriceHQ:N0} gil");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text("Sale Velocity:");
            ImGui.TableNextColumn(); ImGui.Text($"{currentItem.MarketData.SaleVelocity:F1} / day");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text("Current Listings:");
            ImGui.TableNextColumn(); ImGui.Text($"{currentItem.MarketData.CurrentListings}");

            ImGui.EndTable();
        }
    }

    private void DrawProfitBreakdown()
    {
        ImGui.TextDisabled("PROFIT ANALYSIS");
        
        if (ImGui.BeginTable("ProfitStats", 2))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text("Expected Sale:");
            ImGui.TableNextColumn(); ImGui.Text($"{currentItem.ExpectedSalePrice:N0} gil");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text("Crafting Cost:");
            ImGui.TableNextColumn(); ImGui.Text($"{currentItem.TotalCraftCost:N0} gil");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text("Market Tax (5%):");
            ImGui.TableNextColumn(); ImGui.Text($"{currentItem.MarketBoardTax:N0} gil");

            ImGui.Separator();

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text("Net Profit:");
            ImGui.TableNextColumn(); 
            var color = currentItem.RawProfit > 0 ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1);
            ImGui.TextColored(color, $"{currentItem.RawProfit:N0} gil");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text("Margin:");
            ImGui.TableNextColumn(); ImGui.Text($"{currentItem.ProfitMargin:F0}%");

            ImGui.EndTable();
        }
    }

    private void DrawRiskAnalysis()
    {
        ImGui.TextDisabled("RISK ASSESSMENT");
        
        var riskColor = currentItem.RiskLevel switch
        {
            RiskLevel.Low => new Vector4(0f, 1f, 0.5f, 1f),
            RiskLevel.Medium => new Vector4(1f, 1f, 0.5f, 1f),
            RiskLevel.High => new Vector4(1f, 0.7f, 0f, 1f),
            RiskLevel.VeryHigh => new Vector4(1f, 0.3f, 0.3f, 1f),
            _ => new Vector4(1f, 1f, 1f, 1f)
        };
        
        ImGui.TextColored(riskColor, $"Risk Level: {currentItem.RiskLevel}");
        ImGui.Text($"Score: {currentItem.RiskScore}/100");
        
        if (currentItem.Warnings.Any())
        {
            ImGui.Spacing();
            ImGui.Text("Warnings:");
            foreach (var w in currentItem.Warnings)
            {
                ImGui.BulletText(w.Message);
            }
        }
    }

    private void DrawRecipeInfo()
    {
        ImGui.TextDisabled("RECIPE INFO");
        ImGui.Text($"Yields: {currentItem.Recipe.ResultAmount}");
        ImGui.Text($"Est. Time: {currentItem.Recipe.EstimatedCraftTimeSeconds}s");
    }
}
