using System;
using System.Collections.Generic;
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
    
    // Cross-world state
    private MarketData? crossWorldData;
    private bool loadingCrossWorld;
    private string? crossWorldError;

    public DetailWindow(Plugin plugin) : base("Item Details##AurumDetail", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 500),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }
    
    public override void PreDraw()
    {
        ThemeManager.PushWindowStyles(plugin.Configuration.ColorTheme);
        ImGui.GetIO().FontGlobalScale = plugin.Configuration.UIScale;
    }
    
    public override void PostDraw()
    {
        ThemeManager.PopWindowStyles();
        ImGui.GetIO().FontGlobalScale = 1.0f;
    }

    public void SetItem(ProfitCalculation item)
    {
        currentItem = item;
        IsOpen = true;
        
        // Reset cross-world state when changing items
        crossWorldData = null;
        loadingCrossWorld = false;
        crossWorldError = null;
        
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
            DrawMarketTiming();
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

        if (ImGui.Button("Add to Chart"))
        {
             if (currentItem?.MarketData != null)
             {
                 plugin.ChartWindow.IsOpen = true;
                 plugin.ChartWindow.AddComparisonData(currentItem.MarketData, currentItem.Recipe.ItemName);
             }
        }
        ImGui.SameLine();

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
        ImGui.SameLine();
        
        if (ImGui.Button("Copy"))
        {
            CopyToClipboard();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Copy analysis to clipboard");
        }
        ImGui.SameLine();

        if (ImGui.Button("Share Link"))
        {
            if (currentItem != null)
            {
                var link = ShareUtils.GenerateShareLink(currentItem);
                ImGui.SetClipboardText(link);
            }
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Copy shareable link to clipboard");
        }
        ImGui.SameLine();
        
        // Cross-world button
        if (loadingCrossWorld)
        {
            ImGui.TextDisabled("Checking Worlds...");
        }
        else if (crossWorldData == null)
        {
            if (ImGui.Button("Check Cheapest World"))
            {
                FetchCrossWorldData();
            }
        }
        else
        {
            ImGui.TextColored(new Vector4(0.5f, 1.0f, 0.5f, 1.0f), "DC Data Loaded");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Data Center market data has been fetched.\nSee 'Cheapest' row in Market Snapshot below.");
            }
        }

        if (!string.IsNullOrEmpty(crossWorldError))
        {
             ImGui.SameLine();
             ImGui.TextColored(new Vector4(1, 0, 0, 1), "!");
             if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Error: {crossWorldError}");
        }
    }

    private void FetchCrossWorldData()
    {
        if (currentItem == null) return;
        
        loadingCrossWorld = true;
        crossWorldError = null;
        
        System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                // We use the same world name (which resolves to the current DC in the service if needed, 
                // but UniversalisService.GetMarketDataCrossWorldAsync handles fetching for the whole DC)
                var result = await plugin.UniversalisService.GetMarketDataCrossWorldAsync(
                    currentItem.MarketData?.WorldName ?? "", 
                    currentItem.Recipe.ResultItemId
                );
                
                crossWorldData = result;
            }
            catch (Exception ex)
            {
                crossWorldError = ex.Message;
            }
            finally
            {
                loadingCrossWorld = false;
            }
        });
    }

    private void CopyToClipboard()
    {
        if (currentItem == null) return;
        
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"--- {currentItem.Recipe.ItemName} Analysis ---");
        sb.AppendLine($"Expected Sale: {currentItem.ExpectedSalePrice:N0} gil");
        sb.AppendLine($"Crafting Cost: {currentItem.TotalCraftCost:N0} gil");
        sb.AppendLine($"Net Profit:    {currentItem.RawProfit:N0} gil");
        sb.AppendLine($"Margin:        {currentItem.ProfitMargin:F0}%");
        sb.AppendLine($"Risk Level:    {currentItem.RiskLevel}");
        sb.AppendLine($"Recommendation: {currentItem.Recommendation}");
        
        ImGui.SetClipboardText(sb.ToString());
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

            // Cross-World Cheapest Price Row
            if (crossWorldData != null)
            {
                var cheapestListing = crossWorldData.Listings.OrderBy(l => l.PricePerUnit).FirstOrDefault();
                if (cheapestListing != null)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn(); ImGui.Text("Cheapest World:");
                    ImGui.TableNextColumn();
                    
                    // Highlight if cheaper than local
                    var localMin = currentItem.MarketData.CurrentMinPrice;
                    var isCheaper = cheapestListing.PricePerUnit < localMin;
                    var color = isCheaper ? new Vector4(0, 1, 0, 1) : new Vector4(1, 1, 1, 1);
                    
                    ImGui.TextColored(color, $"{cheapestListing.PricePerUnit:N0} @ {cheapestListing.WorldName}");
                    
                    if (isCheaper)
                    {
                        if (ImGui.IsItemHovered())
                        {
                            var diff = localMin - cheapestListing.PricePerUnit;
                            ImGui.SetTooltip($"Save {diff:N0} gil per item by buying on {cheapestListing.WorldName}");
                        }
                    }
                }
            }

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
            ImGui.TableNextColumn(); ImGui.Text("Gil / Hour:");
            ImGui.TableNextColumn(); ImGui.Text($"{currentItem.GilPerHour:N0}");

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

        if (ImGui.BeginTable("CostDetailsTable", 4, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 40);
            ImGui.TableSetupColumn("Unit", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableHeadersRow();
            
            foreach (var ingredient in currentItem.IngredientTree.RootIngredients)
            {
                DrawIngredientRow(ingredient);
            }
            ImGui.EndTable();
        }
    }

    private void DrawIngredientRow(IngredientCost ingredient)
    {
        ImGui.TableNextRow();
        
        ImGui.TableNextColumn();
        bool hasSubIngredients = ingredient.SubIngredients != null && ingredient.SubIngredients.Any();
        
        if (hasSubIngredients)
        {
            bool open = ImGui.TreeNodeEx($"##Node_{ingredient.ItemId}_{ingredient.GetHashCode()}", 
                ImGuiTreeNodeFlags.SpanFullWidth, ingredient.ItemName);
                
            if (ingredient.IsHQ)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 1, 0, 1), "");
            }

            // Draw rest of the columns for the parent row
            DrawIngredientColumns(ingredient);

            if (open)
            {
                foreach (var sub in ingredient.SubIngredients!)
                {
                    DrawIngredientRow(sub);
                }
                ImGui.TreePop();
            }
        }
        else
        {
            // Indent to align with tree node text
            ImGui.Indent(ImGui.GetTreeNodeToLabelSpacing());
            ImGui.Text(ingredient.ItemName);
            if (ingredient.IsHQ)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 1, 0, 1), "");
            }
            ImGui.Unindent(ImGui.GetTreeNodeToLabelSpacing());
            
            DrawIngredientColumns(ingredient);
        }
    }

    private void DrawIngredientColumns(IngredientCost ingredient)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip($"Source: {ingredient.Source}");
        }

        ImGui.TableNextColumn();
        ImGui.Text($"{ingredient.Quantity}");

        ImGui.TableNextColumn();
        ImGui.Text($"{ingredient.UnitCost:N0}");

        ImGui.TableNextColumn();
        ImGui.Text($"{ingredient.TotalCost:N0}");
    }

    private void DrawCurrentListings()
    {
        // Use cross-world data if loaded, otherwise fallback to item's local data
        var data = crossWorldData ?? currentItem?.MarketData;
        
        if (data == null || !data.Listings.Any())
        {
            ImGui.TextDisabled("No current listings found.");
            return;
        }

        var listings = data.Listings.Take(20).ToList();
        
        // Check if we have multiple worlds
        var hasMultipleWorlds = listings.Select(l => l.WorldName).Distinct().Count() > 1;
        int columns = hasMultipleWorlds ? 5 : 4;

        if (ImGui.BeginTable("CurrentListingsTable", columns, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("Price", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 40);
            ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.WidthFixed, 80);
            if (hasMultipleWorlds)
            {
                ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthFixed, 100);
            }
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

                // World Column (if applicable)
                if (hasMultipleWorlds)
                {
                    ImGui.TableNextColumn();
                    ImGui.Text(listing.WorldName);
                }

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
        else scoreColor = new Vector4(1f, 0.84f, 0f, 1f); // Gold

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
            actionColor = new Vector4(1f, 0.84f, 0f, 1f);
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

    private void DrawMarketTiming()
    {
        if (currentItem?.MarketData == null) return;
        var md = currentItem.MarketData;

        // Skip if we don't have enough data
        if (!md.BestDaysToSell.Any() && !md.BestHoursToSell.Any()) return;

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled("MARKET TIMING");

        if (!string.IsNullOrEmpty(md.PeakDemandAnalysis))
        {
             ImGui.TextWrapped(md.PeakDemandAnalysis);
        }

        // Draw visual indicators for best days
        if (md.BestDaysToSell.Any())
        {
            ImGui.Spacing();
            ImGui.Text("Best Days:");
            ImGui.SameLine();
            
            // Just list them comma separated for now
            var days = string.Join(", ", md.BestDaysToSell);
            ImGui.TextColored(new Vector4(0.5f, 1.0f, 0.5f, 1.0f), days);
        }

        // Draw visual indicators for best hours
        if (md.BestHoursToSell.Any())
        {
            ImGui.Text("Peak Hours (UTC):");
            ImGui.SameLine();
            var hours = string.Join(", ", md.BestHoursToSell.Select(h => $"{h:00}:00"));
            ImGui.TextColored(new Vector4(0.5f, 1.0f, 0.5f, 1.0f), hours);
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
