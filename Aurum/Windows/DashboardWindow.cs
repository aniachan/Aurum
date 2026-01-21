using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Aurum.Models;

namespace Aurum.Windows;

public class DashboardWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    
    // UI State
    private List<ProfitCalculation> profitResults = new();
    private List<ProfitCalculation> filteredResults = new();
    private bool isLoading = false;
    private string searchText = string.Empty;
    private string selectedClass = "All";
    private int minLevel = 1;
    private int maxLevel = 100;
    private int minProfit = 0;
    private SortMode currentSort = SortMode.RecommendationScore;
    private bool showOnlyProfitable = true;
    private DateTime lastRefresh = DateTime.MinValue;
    
    // Pagination
    private int currentPage = 1;
    private int itemsPerPage = 50;
    
    private readonly string[] craftingClasses = { "All", "CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL" };
    
    public DashboardWindow(Plugin plugin) 
        : base("Aurum - Crafting Profit Calculator##Dashboard", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(900, 600),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawHeader();
        ImGui.Separator();
        DrawFilters();
        ImGui.Separator();
        DrawProfitList();
    }
    
    private void DrawHeader()
    {
        // Title and stats
        ImGui.Text("Market-Aware Crafting Profits");
        ImGui.SameLine(ImGui.GetWindowWidth() - 200);
        
        if (ImGui.Button("⚙️ Settings"))
        {
            plugin.ToggleConfigUi();
        }
        
        ImGui.SameLine();
        
        if (isLoading)
        {
            ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), "Loading...");
        }
        else if (ImGui.Button("🔄 Refresh"))
        {
            _ = RefreshDataAsync();
        }

        ImGui.SameLine();

        if (ImGui.Button("📥 Export CSV"))
        {
             _ = ExportToCsvAsync();
        }
        
        // Stats row
        if (profitResults.Any())
        {
            ImGui.Text($"Showing {filteredResults.Count} of {profitResults.Count} recipes");
            ImGui.SameLine(ImGui.GetWindowWidth() - 300);
            
            var avgProfit = filteredResults.Any() ? (int)filteredResults.Average(p => p.RawProfit) : 0;
            ImGui.Text($"Avg Profit: {FormatGil(avgProfit)}");
            
            if (lastRefresh > DateTime.MinValue)
            {
                var timeSinceRefresh = DateTime.UtcNow - lastRefresh;
                ImGui.SameLine();
                ImGui.TextDisabled($"(Updated {timeSinceRefresh.TotalMinutes:F0}m ago)");
            }
        }
        else
        {
            ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), "Click 'Refresh' to load profit data");
        }
    }
    
    private void DrawFilters()
    {
        ImGui.Text("Filters:");
        ImGui.SameLine();
        
        // Search box
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputTextWithHint("##search", "Search items...", ref searchText, 256))
        {
            ApplyFilters();
        }
        
        ImGui.SameLine();
        
        // Class filter
        ImGui.SetNextItemWidth(100);
        if (ImGui.BeginCombo("##class", selectedClass))
        {
            foreach (var cls in craftingClasses)
            {
                if (ImGui.Selectable(cls, selectedClass == cls))
                {
                    selectedClass = cls;
                    ApplyFilters();
                }
            }
            ImGui.EndCombo();
        }
        
        ImGui.SameLine();
        
        // Level range
        ImGui.Text("Level:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(60);
        if (ImGui.InputInt("##minlevel", ref minLevel))
        {
            minLevel = Math.Clamp(minLevel, 1, 100);
            ApplyFilters();
        }
        ImGui.SameLine();
        ImGui.Text("-");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(60);
        if (ImGui.InputInt("##maxlevel", ref maxLevel))
        {
            maxLevel = Math.Clamp(maxLevel, 1, 100);
            ApplyFilters();
        }
        
        ImGui.SameLine();

        // Min Profit
        ImGui.Text("Min Profit:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        if (ImGui.InputInt("##minprofit", ref minProfit, 1000))
        {
            minProfit = Math.Max(0, minProfit);
            ApplyFilters();
        }
        
        ImGui.SameLine();
        
        // Profitable only
        if (ImGui.Checkbox("Profitable Only", ref showOnlyProfitable))
        {
            ApplyFilters();
        }
        
        // Sort options
        ImGui.Text("Sort by:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        var sortNames = Enum.GetNames<SortMode>();
        var currentSortIndex = (int)currentSort;
        if (ImGui.Combo("##sort", ref currentSortIndex, sortNames, sortNames.Length))
        {
            currentSort = (SortMode)currentSortIndex;
            ApplyFilters();
        }
    }
    
    private void DrawProfitList()
    {
        if (!filteredResults.Any())
        {
            if (isLoading)
            {
                ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), "Loading market data...");
            }
            else if (!profitResults.Any())
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "No data loaded. Click 'Refresh' to analyze the market.");
            }
            else
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "No items match your filters.");
            }
            return;
        }

        // Pagination controls
        int totalPages = (int)Math.Ceiling(filteredResults.Count / (double)itemsPerPage);
        currentPage = Math.Clamp(currentPage, 1, totalPages);
        
        ImGui.Text($"Page {currentPage} of {totalPages} ({filteredResults.Count} items)");
        ImGui.SameLine();
        if (ImGui.Button("<") && currentPage > 1) currentPage--;
        ImGui.SameLine();
        if (ImGui.Button(">") && currentPage < totalPages) currentPage++;
        
        // Table header
        if (ImGui.BeginTable("ProfitTable", 9, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthFixed, 250);
            ImGui.TableSetupColumn("Class", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Profit", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Margin", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Gil/Hr", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Demand", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Risk", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Score", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableHeadersRow();
            
            // Draw rows
            var pageItems = filteredResults
                .Skip((currentPage - 1) * itemsPerPage)
                .Take(itemsPerPage);
                
            foreach (var profit in pageItems)
            {
                DrawProfitRow(profit);
            }
            
            ImGui.EndTable();
        }
    }
    
    private void DrawProfitRow(ProfitCalculation profit)
    {
        ImGui.TableNextRow();
        
        // Item name (Clickable to open details)
        ImGui.TableNextColumn();
        if (ImGui.Selectable($"{profit.Recipe.ItemName}##{profit.Recipe.RecipeId}", false, ImGuiSelectableFlags.SpanAllColumns))
        {
            plugin.DetailWindow.SetItem(profit);
        }
        
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text($"Recipe Level: {profit.Recipe.RecipeLevel}");
            ImGui.Text($"Crafting Level: {profit.Recipe.ClassJobLevel}");
            if (profit.Warnings.Any())
            {
                ImGui.Separator();
                ImGui.TextColored(new Vector4(1f, 0.5f, 0f, 1f), "Warnings:");
                foreach (var warning in profit.Warnings)
                {
                    ImGui.TextWrapped($"⚠️ {warning.Message}");
                }
            }
            ImGui.EndTooltip();
        }
        
        // Class
        ImGui.TableNextColumn();
        ImGui.Text(profit.Recipe.CraftingClassName);
        
        // Profit
        ImGui.TableNextColumn();
        var profitColor = profit.RawProfit > 0 
            ? new Vector4(0f, 1f, 0.5f, 1f)  // Green
            : new Vector4(1f, 0.3f, 0.3f, 1f); // Red
        ImGui.TextColored(profitColor, FormatGil(profit.RawProfit));
        
        // Margin
        ImGui.TableNextColumn();
        ImGui.Text($"{profit.ProfitMargin:F1}%");
        
        // Gil/Hour
        ImGui.TableNextColumn();
        ImGui.Text(FormatGil(profit.GilPerHour));
        
        // Demand (sale velocity)
        ImGui.TableNextColumn();
        if (profit.MarketData != null)
        {
            var velocity = profit.MarketData.SaleVelocity;
            var velocityColor = velocity >= 50 ? new Vector4(0f, 1f, 0.5f, 1f) :
                               velocity >= 10 ? new Vector4(0.5f, 1f, 0.5f, 1f) :
                               velocity >= 1 ? new Vector4(1f, 1f, 0.5f, 1f) :
                               new Vector4(1f, 0.5f, 0.5f, 1f);
            
            var icon = velocity >= 50 ? "🔥" :
                      velocity >= 10 ? "📈" :
                      velocity >= 1 ? "➡️" : "🐌";
            
            ImGui.TextColored(velocityColor, $"{icon} {velocity:F1}/d");
        }
        else
        {
            ImGui.TextDisabled("N/A");
        }
        
        // Risk
        ImGui.TableNextColumn();
        var riskColor = profit.RiskLevel switch
        {
            RiskLevel.Low => new Vector4(0f, 1f, 0.5f, 1f),
            RiskLevel.Medium => new Vector4(1f, 1f, 0.5f, 1f),
            RiskLevel.High => new Vector4(1f, 0.7f, 0f, 1f),
            RiskLevel.VeryHigh => new Vector4(1f, 0.3f, 0.3f, 1f),
            _ => new Vector4(1f, 1f, 1f, 1f)
        };
        
        var riskIcon = profit.RiskLevel switch
        {
            RiskLevel.Low => "🟢",
            RiskLevel.Medium => "🟡",
            RiskLevel.High => "🟠",
            RiskLevel.VeryHigh => "🔴",
            _ => "⚪"
        };
        
        ImGui.TextColored(riskColor, $"{riskIcon} {profit.RiskLevel}");
        
            // Score (stars)
        ImGui.TableNextColumn();
        var stars = profit.RecommendationScore / 20; // 0-100 -> 0-5 stars
        var starText = new string('⭐', Math.Clamp(stars, 0, 5));
        ImGui.Text($"{starText} {profit.RecommendationScore}");

        // Chart Button
        ImGui.TableNextColumn();
        if (profit.MarketData != null)
        {
            if (ImGui.Button($"📈##{profit.Recipe.RecipeId}"))
            {
                plugin.ChartWindow.SetMarketData(profit.MarketData, profit.Recipe.ItemName);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("View Price History");
            }
        }
    }
    
    private async Task RefreshDataAsync()
    {
        if (isLoading) return;
        
        isLoading = true;
        profitResults.Clear();
        filteredResults.Clear();
        
        try
        {
            Plugin.Log.Information("Refreshing market data...");
            
            // Get world name
            var worldName = Plugin.PlayerState.CurrentWorld.Value.Name.ToString() 
                ?? plugin.Configuration.PreferredWorld;
            
            if (worldName == "Auto" || string.IsNullOrEmpty(worldName))
            {
                Plugin.Log.Warning("Unable to determine world name");
                return;
            }
            
            // Get recipes to analyze (start with level 90+ for testing)
            var recipes = plugin.RecipeService.GetRecipesByLevel(90, 100).Take(50).ToList();
            
            Plugin.Log.Information($"Analyzing {recipes.Count} recipes for {worldName}...");
            
            // Calculate profits
            profitResults = await plugin.ProfitService.CalculateProfitsBatchAsync(recipes, worldName);
            
            Plugin.Log.Information($"Loaded {profitResults.Count} profit calculations");
            
            lastRefresh = DateTime.UtcNow;
            ApplyFilters();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error refreshing market data");
        }
        finally
        {
            isLoading = false;
        }
    }
    
    private void ApplyFilters()
    {
        filteredResults = profitResults.Where(p =>
        {
            // Search filter
            if (!string.IsNullOrWhiteSpace(searchText) && 
                !p.Recipe.ItemName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                return false;
            
            // Class filter
            if (selectedClass != "All" && p.Recipe.CraftingClassName != selectedClass)
                return false;
            
            // Level filter
            if (p.Recipe.ClassJobLevel < minLevel || p.Recipe.ClassJobLevel > maxLevel)
                return false;
            
            // Profitable only
            if (showOnlyProfitable && p.RawProfit <= 0)
                return false;

            // Min profit filter
            if (p.RawProfit < minProfit)
                return false;
            
            return true;
        }).ToList();
        
        // Reset page on filter change
        currentPage = 1;
        
        // Apply sorting
        filteredResults = currentSort switch
        {
            SortMode.HighestProfit => filteredResults.OrderByDescending(p => p.RawProfit).ToList(),
            SortMode.HighestMargin => filteredResults.OrderByDescending(p => p.ProfitMargin).ToList(),
            SortMode.BestGilPerHour => filteredResults.OrderByDescending(p => p.GilPerHour).ToList(),
            SortMode.FastestSelling => filteredResults.OrderByDescending(p => p.MarketData?.SaleVelocity ?? 0).ToList(),
            SortMode.LowestCompetition => filteredResults.OrderBy(p => p.MarketData?.CurrentListings ?? int.MaxValue).ToList(),
            SortMode.RecommendationScore => filteredResults.OrderByDescending(p => p.RecommendationScore).ToList(),
            _ => filteredResults
        };
    }
    
    private static string FormatGil(int amount)
    {
        if (amount >= 1000000)
            return $"{amount / 1000000.0:F1}M";
        if (amount >= 1000)
            return $"{amount / 1000.0:F1}K";
        return amount.ToString();
    }

    private async Task ExportToCsvAsync()
    {
        if (!profitResults.Any())
        {
             Plugin.Log.Warning("No data to export");
             return;
        }

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Item Name,Recipe Level,Class,Level,Profit,Margin %,Gil/Hr,Velocity,Risk Score,Risk Level");

            foreach (var p in filteredResults)
            {
                var velocity = p.MarketData?.SaleVelocity ?? 0;
                sb.AppendLine($"\"{p.Recipe.ItemName}\",{p.Recipe.RecipeLevel},{p.Recipe.CraftingClassName},{p.Recipe.ClassJobLevel},{p.RawProfit},{p.ProfitMargin:F2},{p.GilPerHour},{velocity:F2},{p.RecommendationScore},{p.RiskLevel}");
            }

            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var filePath = Path.Combine(documentsPath, $"Aurum_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            
            await File.WriteAllTextAsync(filePath, sb.ToString());
            Plugin.Log.Information($"Exported data to {filePath}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to export CSV");
        }
    }
}
