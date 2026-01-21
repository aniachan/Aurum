using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System.Diagnostics;
using Aurum.Models;
using Aurum.Utils;
using Dalamud.Interface;

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
        if (currentItem == null) return;

        ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), currentItem.Recipe.ItemName);
        ImGui.SameLine();
        ImGui.TextDisabled($"(Lvl {currentItem.Recipe.ClassJobLevel} {currentItem.Recipe.CraftingClassName})");
        
        ImGui.Spacing();

        if (ImGui.Button("Universalis"))
        {
            if (currentItem?.MarketData != null)
                OpenUrl($"https://universalis.app/market/{currentItem.MarketData.ItemId}");
        }
        ImGui.SameLine();
        if (ImGui.Button("Garland Tools"))
        {
            if (currentItem?.MarketData != null)
                OpenUrl($"https://www.garlandtools.org/db/#item/{currentItem.MarketData.ItemId}");
        }
        ImGui.SameLine();
        if (ImGui.Button("Teamcraft"))
        {
            if (currentItem?.MarketData != null)
                OpenUrl($"https://ffxivteamcraft.com/db/en/item/{currentItem.MarketData.ItemId}");
        }
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            // Ideally log this error
        }
    }

    private void DrawMarketSnapshot()
    {
        if (currentItem == null) return;

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
        if (currentItem == null) return;

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
        if (currentItem == null) return;

        ImGui.TextDisabled("RISK ASSESSMENT");
        
        UiUtils.DrawRiskBadge(currentItem.RiskLevel);
        
        ImGui.Text($"Score: {currentItem.RiskScore}/100");
        
        if (!string.IsNullOrEmpty(currentItem.RiskAnalysis))
        {
             ImGui.Spacing();
             ImGui.TextWrapped(currentItem.RiskAnalysis);
        }

        if (currentItem.Warnings.Any())
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Warnings:");
            foreach (var w in currentItem.Warnings)
            {
                var warningIcon = UiUtils.GetWarningIcon(w.Type);
                var warningColor = w.Level switch
                {
                    WarningLevel.Danger => new Vector4(1f, 0.3f, 0.3f, 1f),
                    WarningLevel.Warning => new Vector4(1f, 0.7f, 0f, 1f),
                    _ => new Vector4(0.8f, 0.8f, 1f, 1f)
                };

                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.TextColored(warningColor, warningIcon);
                ImGui.PopFont();
                
                ImGui.SameLine();
                ImGui.TextWrapped(w.Message);
                
                if (!string.IsNullOrEmpty(w.Details))
                {
                    ImGui.Indent();
                    ImGui.TextDisabled(w.Details);
                    ImGui.Unindent();
                }
            }
        }
    }

    private void DrawRecipeInfo()
    {
        if (currentItem == null) return;
        ImGui.TextDisabled("RECIPE INFO");
        ImGui.Text($"Yields: {currentItem.Recipe.ResultAmount}");
        ImGui.Text($"Est. Time: {currentItem.Recipe.EstimatedCraftTimeSeconds}s");
    }
}
