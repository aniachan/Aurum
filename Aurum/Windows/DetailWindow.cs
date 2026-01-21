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
            ImGui.Spacing();

            if (ImGui.BeginTabBar("MarketDetailsTabs"))
            {
                if (ImGui.BeginTabItem("Current Listings"))
                {
                    DrawCurrentListings();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Recent Sales"))
                {
                    DrawRecentSales();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
            
            // Right Column: Risk & Warnings
            ImGui.TableNextColumn();
            DrawRiskAnalysis();
            DrawRecommendation();
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

            if (currentItem.VendorPrice > 0)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text("Vendor Price:");
                ImGui.TableNextColumn(); ImGui.TextDisabled($"{currentItem.VendorPrice:N0} gil");
            }

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

        DrawCostDetails();
    }

    private void DrawCostDetails()
    {
        if (currentItem?.IngredientTree?.RootIngredients == null || !currentItem.IngredientTree.RootIngredients.Any()) return;

        ImGui.Spacing();
        ImGui.TextDisabled("INGREDIENT BREAKDOWN");

        if (ImGui.BeginTable("CostDetailsTable", 3, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 30);
            ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.WidthFixed, 60);
            
            foreach (var ingredient in currentItem.IngredientTree.RootIngredients)
            {
                ImGui.TableNextRow();
                
                ImGui.TableNextColumn();
                ImGui.Text(ingredient.ItemName);
                if (ingredient.IsHQ)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(1, 1, 0, 1), "");
                }
                
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"Unit Cost: {ingredient.UnitCost:N0} gil\nSource: {ingredient.Source}");
                }

                ImGui.TableNextColumn();
                ImGui.Text($"{ingredient.Quantity}");

                ImGui.TableNextColumn();
                ImGui.Text($"{ingredient.TotalCost:N0}");
            }
            ImGui.EndTable();
        }
    }

    private void DrawCurrentListings()
    {
        if (currentItem?.MarketData == null || !currentItem.MarketData.Listings.Any())
        {
            ImGui.TextDisabled("No current listings found.");
            return;
        }

        var listings = currentItem.MarketData.Listings.Take(20).ToList();

        if (ImGui.BeginTable("CurrentListingsTable", 4, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("Price", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 40);
            ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Retainer", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            foreach (var listing in listings)
            {
                ImGui.TableNextRow();
                
                // Price Column
                ImGui.TableNextColumn();
                ImGui.Text($"{listing.PricePerUnit:N0}");
                if (listing.IsHQ)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(1, 1, 0, 1), ""); // HQ Icon
                }

                // Quantity Column
                ImGui.TableNextColumn();
                ImGui.Text($"{listing.Quantity}");

                // Total Column
                ImGui.TableNextColumn();
                ImGui.Text($"{listing.Total:N0}");

                // Retainer Column
                ImGui.TableNextColumn();
                ImGui.Text(listing.RetainerName);
                if (!string.IsNullOrEmpty(listing.RetainerCity))
                {
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip($"City: {listing.RetainerCity}");
                    }
                }
            }

            ImGui.EndTable();
        }
    }

    private void DrawRecentSales()
    {
        if (currentItem?.MarketData == null) return;
        var sales = currentItem.MarketData.RecentHistory.Take(5).ToList(); // Show top 5 recent sales

        if (!sales.Any()) return;

        ImGui.TextDisabled("RECENT SALES");

        if (ImGui.BeginTable("RecentSalesTable", 3, ImGuiTableFlags.BordersInnerH))
        {
            ImGui.TableSetupColumn("Price", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 40);
            ImGui.TableSetupColumn("Buyer", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            foreach (var sale in sales)
            {
                ImGui.TableNextRow();
                
                // Price Column
                ImGui.TableNextColumn();
                ImGui.Text($"{sale.PricePerUnit:N0}");
                if (sale.IsHQ)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(1, 1, 0, 1), ""); // HQ Icon
                }

                // Quantity Column
                ImGui.TableNextColumn();
                ImGui.Text($"{sale.Quantity}");

                // Buyer Column
                ImGui.TableNextColumn();
                ImGui.Text(sale.BuyerName);
                
                // Show relative time in tooltip
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"Sold: {sale.Timestamp.ToLocalTime()}");
                }
            }

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
                var warningColor = UiUtils.GetWarningColor(w.Level);

                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.TextColored(warningColor, warningIcon);
                ImGui.PopFont();
                
                ImGui.SameLine();
                
                // If there are details, make the message part of a tree node, or add a small expander
                if (!string.IsNullOrEmpty(w.Details))
                {
                    // Use the message as the header for the tree node
                    // We need to style it to match the warning color
                    ImGui.PushStyleColor(ImGuiCol.Text, warningColor);
                    bool expanded = ImGui.TreeNodeEx($"##Details_{w.Type}_{w.GetHashCode()}", 
                        ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.FramePadding | ImGuiTreeNodeFlags.AllowItemOverlap, 
                        w.Message);
                    ImGui.PopStyleColor();

                    if (expanded)
                    {
                        ImGui.Indent();
                        ImGui.TextDisabled(w.Details);
                        ImGui.Unindent();
                    }
                }
                else
                {
                    ImGui.TextWrapped(w.Message);
                }
            }
        }
    }

    private void DrawRecommendation()
    {
        if (currentItem == null) return;
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled("RECOMMENDATION");

        // Weighted score (Profit vs Risk)
        ImGui.Text("Weighted Score:");
        ImGui.SameLine();
        
        Vector4 scoreColor;
        if (currentItem.RecommendationScore >= 75) scoreColor = new Vector4(0f, 1f, 0.5f, 1f); // Green
        else if (currentItem.RecommendationScore >= 50) scoreColor = new Vector4(1f, 1f, 0.5f, 1f); // Yellow
        else if (currentItem.RecommendationScore >= 25) scoreColor = new Vector4(1f, 0.7f, 0f, 1f); // Orange
        else scoreColor = new Vector4(1f, 0.3f, 0.3f, 1f); // Red

        ImGui.TextColored(scoreColor, $"{currentItem.RecommendationScore}/100");

        // Recommended Action
        string actionText;
        Vector4 actionColor;
        
        if (currentItem.RecommendationScore >= 75)
        {
            actionText = "Recommended to Craft";
            actionColor = new Vector4(0f, 1f, 0.5f, 1f);
        }
        else if (currentItem.RecommendationScore >= 50)
        {
            actionText = "Craft with Caution";
            actionColor = new Vector4(1f, 1f, 0.5f, 1f);
        }
        else
        {
            actionText = "Not Recommended";
            actionColor = new Vector4(1f, 0.3f, 0.3f, 1f);
        }
        
        ImGui.TextColored(actionColor, actionText);

        // Suggested Quantity
        if (currentItem.RecommendationScore >= 50 && currentItem.RecommendedQuantity > 0)
        {
            ImGui.Text($"Suggested Batch: {currentItem.RecommendedQuantity} (Max Safe: {currentItem.MaxSafeQuantity})");
            ImGui.TextDisabled($"Est. Sell Time: {currentItem.EstimatedSellTimeDays:F1} days");
        }

        // Suggested Price
        ImGui.Spacing();
        ImGui.Text("Suggested Price:");
        ImGui.SameLine();
        ImGui.Text($"{currentItem.ExpectedSalePrice:N0} gil");
        if (currentItem.MarketData != null)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"(undercuts by 1 gil)");
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
