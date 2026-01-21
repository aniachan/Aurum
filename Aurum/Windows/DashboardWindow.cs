using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Aurum.Models;
using Aurum.Utils;
using Dalamud.Interface;

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
    // private int itemsPerPage = 50; // Removed in favor of config
    
    private readonly string[] craftingClasses = { "All", "CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL" };
    private string lastErrorMessage = string.Empty;
    private string lastErrorSuggestion = string.Empty;
    private DateTime lastErrorTime = DateTime.MinValue;

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
        
        if (!string.IsNullOrEmpty(lastErrorMessage) && (DateTime.UtcNow - lastErrorTime).TotalMinutes < 5)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.3f, 0.3f, 1f));
            ImGui.TextWrapped($"⚠️ {lastErrorMessage}");
            
            // Show suggestion if available
            var suggestion = ErrorMessageUtils.GetSuggestion(new Exception(lastErrorMessage)); // Hacky, but works since we store the friendly message mostly
            // Better approach: Store the exception or get suggestion when error occurs
            // But we only have lastErrorMessage string here.
            // Let's modify Draw() to use a stored suggestion string
            if (!string.IsNullOrEmpty(lastErrorSuggestion))
            {
                ImGui.TextDisabled($"💡 {lastErrorSuggestion}");
            }
            
            ImGui.PopStyleColor();
            ImGui.Separator();
        }

        DrawFilters();
        ImGui.Separator();
        
        // Reserve space for status bar at bottom
        float footerHeight = ImGui.GetFrameHeightWithSpacing();
        float availableHeight = ImGui.GetContentRegionAvail().Y - footerHeight;
        
        // Create a child window for the list to ensure it doesn't overlap the footer
        if (ImGui.BeginChild("ListRegion", new Vector2(0, availableHeight), false, ImGuiWindowFlags.None))
        {
            DrawProfitList();
            ImGui.EndChild();
        }
        
        ImGui.Separator();
        DrawStatusBar();
    }

    private void DrawStatusBar()
    {
        // API Stats
        var limiter = plugin.RateLimiter;
        var requests = limiter.RequestsLastMinute;
        var limit = plugin.Configuration.ApiRateLimitPerMinute;
        
        // Determine color and status text
        Vector4 apiColor;
        string statusText;
        
        if (limiter.IsDegraded)
        {
             apiColor = new Vector4(1f, 0.3f, 0.3f, 1f); // Red
             statusText = "API: Degraded (Cache Only)";
        }
        else if (requests > limit * 0.9)
        {
             apiColor = new Vector4(1f, 0.3f, 0.3f, 1f); // Red
             statusText = $"API: {requests}/{limit} (1m)";
        }
        else if (requests > limit * 0.7)
        {
             apiColor = new Vector4(1f, 0.7f, 0f, 1f); // Orange
             statusText = $"API: {requests}/{limit} (1m)";
        }
        else
        {
             apiColor = new Vector4(0.7f, 0.7f, 0.7f, 1f); // Grey
             statusText = $"API: {requests}/{limit} (1m)";
        }

        ImGui.TextColored(apiColor, statusText);
        
        // Enhanced Tooltip
        if (ImGui.IsItemHovered()) 
        {
            ImGui.BeginTooltip();
            ImGui.Text("Universalis API Usage");
            ImGui.Separator();
            ImGui.Text($"Last Minute: {requests}/{limit}");
            ImGui.Text($"Last Hour: {limiter.RequestsLastHour}");
            ImGui.Text($"Today: {limiter.RequestsToday}");
            ImGui.Text($"Total Session: {limiter.TotalRequests}");
            
            if (limiter.RateLimitedRequests > 0)
            {
                ImGui.TextColored(new Vector4(1f, 0.7f, 0f, 1f), $"Throttled: {limiter.RateLimitedRequests} times");
            }
            
            if (limiter.TotalErrors > 0)
            {
                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), $"Errors: {limiter.TotalErrors}");
            }
            
            if (limiter.IsDegraded)
            {
                 ImGui.Separator();
                 ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "Status: Degraded (High Error Rate)");
                 ImGui.Text("The plugin is temporarily avoiding API calls\nto prevent further errors.");
            }
            else 
            {
                 ImGui.TextColored(new Vector4(0.3f, 1f, 0.3f, 1f), "Status: Healthy");
            }
            
            ImGui.EndTooltip();
        }

        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "|");
        ImGui.SameLine();

        // Cache Stats
        var cacheStats = plugin.CacheService.GetStats();
        
        // Color based on hit rate quality
        Vector4 cacheColor;
        if (cacheStats.HitRate > 80) cacheColor = new Vector4(0.3f, 1f, 0.3f, 1f); // Green for excellent
        else if (cacheStats.HitRate > 50) cacheColor = new Vector4(0.7f, 1f, 0.3f, 1f); // Yellow-Green for good
        else if (cacheStats.HitRate > 20) cacheColor = new Vector4(1f, 0.7f, 0f, 1f); // Orange for okay
        else cacheColor = new Vector4(1f, 0.3f, 0.3f, 1f); // Red for poor
        
        ImGui.TextColored(cacheColor, $"Cache: {cacheStats.HitRate:F0}%");
        
        if (ImGui.IsItemHovered()) 
        {
            ImGui.BeginTooltip();
            ImGui.Text("Cache Performance");
            ImGui.Separator();
            ImGui.Text($"Hit Rate: {cacheStats.HitRate:F1}%");
            ImGui.Text($"Hits: {cacheStats.Hits}");
            ImGui.Text($"Misses: {cacheStats.Misses}");
            ImGui.Separator();
            ImGui.Text($"Active Entries: {cacheStats.ActiveEntries}");
            ImGui.Text($"Expired: {cacheStats.ExpiredEntries}");
            ImGui.Text($"Total: {cacheStats.TotalEntries}");
            ImGui.EndTooltip();
        }
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
            
            // Error Reporting Button (shows when an error has occurred recently)
            if (!string.IsNullOrEmpty(lastErrorMessage) && (DateTime.UtcNow - lastErrorTime).TotalMinutes < 5)
            {
                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button($"{FontAwesomeIcon.Bug.ToIconString()}##ReportError"))
                {
                     // Open GitHub issues page with pre-filled title
                     try 
                     {
                         var title = $"[Bug] Error: {lastErrorMessage}";
                         var url = $"https://github.com/Dicklesworthstone/Aurum/issues/new?title={Uri.EscapeDataString(title)}&body=Describe%20what%20you%20were%20doing%20when%20this%20occurred...";
                         
                         Process.Start(new ProcessStartInfo
                         {
                             FileName = url,
                             UseShellExecute = true
                         });
                     }
                     catch { /* Ignore navigation errors */ }
                }
                ImGui.PopFont();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Report this error on GitHub");
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
            if (ImGui.InputTextWithHint("##search", "Search items...", ref searchText, 256))
            {
                ApplyFilters();
                if (!string.IsNullOrWhiteSpace(searchText) && !plugin.Configuration.RecentSearches.Contains(searchText))
                {
                   plugin.Configuration.RecentSearches.Insert(0, searchText);
                   if (plugin.Configuration.RecentSearches.Count > 10) plugin.Configuration.RecentSearches.RemoveAt(10);
                   plugin.Configuration.Save();
                }
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
        int itemsPerPage = plugin.Configuration.RowsPerPage;
        int totalPages = (int)Math.Ceiling(filteredResults.Count / (double)itemsPerPage);
        currentPage = Math.Clamp(currentPage, 1, totalPages);
        
        ImGui.Text($"Page {currentPage} of {totalPages} ({filteredResults.Count} items)");
        ImGui.SameLine();
        if (ImGui.Button("<") && currentPage > 1) currentPage--;
        ImGui.SameLine();
        if (ImGui.Button(">") && currentPage < totalPages) currentPage++;
        
        // Table header
        // Count visible columns to set table column count correctly
        var allColumns = new[] { "Item", "Class", "Profit", "Margin", "Gil/Hr", "Demand", "Risk", "Score", "Actions" };
        var visibleColumns = allColumns.Where(c => !plugin.Configuration.HiddenColumns.Contains(c)).ToList();
        
        if (ImGui.BeginTable("ProfitTable", visibleColumns.Count, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            
            // Define columns with original widths
            if (!plugin.Configuration.HiddenColumns.Contains("Item")) ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthFixed, 250);
            if (!plugin.Configuration.HiddenColumns.Contains("Class")) ImGui.TableSetupColumn("Class", ImGuiTableColumnFlags.WidthFixed, 50);
            if (!plugin.Configuration.HiddenColumns.Contains("Profit")) ImGui.TableSetupColumn("Profit", ImGuiTableColumnFlags.WidthFixed, 100);
            if (!plugin.Configuration.HiddenColumns.Contains("Margin")) ImGui.TableSetupColumn("Margin", ImGuiTableColumnFlags.WidthFixed, 80);
            if (!plugin.Configuration.HiddenColumns.Contains("Gil/Hr")) ImGui.TableSetupColumn("Gil/Hr", ImGuiTableColumnFlags.WidthFixed, 100);
            if (!plugin.Configuration.HiddenColumns.Contains("Demand")) ImGui.TableSetupColumn("Demand", ImGuiTableColumnFlags.WidthFixed, 80);
            if (!plugin.Configuration.HiddenColumns.Contains("Risk")) ImGui.TableSetupColumn("Risk", ImGuiTableColumnFlags.WidthFixed, 100);
            if (!plugin.Configuration.HiddenColumns.Contains("Score")) ImGui.TableSetupColumn("Score", ImGuiTableColumnFlags.WidthFixed, 80);
            if (!plugin.Configuration.HiddenColumns.Contains("Actions")) ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 50);

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
        if (!plugin.Configuration.HiddenColumns.Contains("Item"))
        {
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
                
                // Risk Analysis Breakdown
                if (profit.MarketData != null && !string.IsNullOrEmpty(profit.MarketData.RiskAnalysis))
                {
                    ImGui.Separator();
                    ImGui.TextColored(new Vector4(0.8f, 0.8f, 1f, 1f), profit.MarketData.RiskAnalysis);
                }
                
                if (profit.Warnings.Any())
                {
                    ImGui.Separator();
                    ImGui.TextColored(new Vector4(1f, 0.5f, 0f, 1f), "Warnings:");
                    foreach (var warning in profit.Warnings)
                    {
                         var icon = UiUtils.GetWarningIcon(warning.Type);
                         var color = UiUtils.GetWarningColor(warning.Level);

                         ImGui.PushFont(UiBuilder.IconFont);
                         ImGui.TextColored(color, icon);
                         ImGui.PopFont();
                         ImGui.SameLine();
                         ImGui.TextWrapped(warning.Message);
                    }
                }
                ImGui.EndTooltip();
            }
        }
        
        // Class
        if (!plugin.Configuration.HiddenColumns.Contains("Class"))
        {
            ImGui.TableNextColumn();
            ImGui.Text(profit.Recipe.CraftingClassName);
        }
        
        // Profit
        if (!plugin.Configuration.HiddenColumns.Contains("Profit"))
        {
            ImGui.TableNextColumn();
            var profitColor = profit.RawProfit > 0 
                ? new Vector4(0f, 1f, 0.5f, 1f)  // Green
                : new Vector4(1f, 0.3f, 0.3f, 1f); // Red
            ImGui.TextColored(profitColor, FormatGil(profit.RawProfit));
        }
        
        // Margin
        if (!plugin.Configuration.HiddenColumns.Contains("Margin"))
        {
            ImGui.TableNextColumn();
            ImGui.Text($"{profit.ProfitMargin:F1}%");
        }
        
        // Gil/Hour
        if (!plugin.Configuration.HiddenColumns.Contains("Gil/Hr"))
        {
            ImGui.TableNextColumn();
            ImGui.Text(FormatGil(profit.GilPerHour));
        }
        
        // Demand (sale velocity)
        if (!plugin.Configuration.HiddenColumns.Contains("Demand"))
        {
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
        }
        
        // Risk
        if (!plugin.Configuration.HiddenColumns.Contains("Risk"))
        {
            ImGui.TableNextColumn();
            UiUtils.DrawRiskBadge(profit.RiskLevel);
        }
        
        // Score (stars)
        if (!plugin.Configuration.HiddenColumns.Contains("Score"))
        {
            ImGui.TableNextColumn();
            var stars = profit.RecommendationScore / 20; // 0-100 -> 0-5 stars
            var starText = new string('⭐', Math.Clamp(stars, 0, 5));
            ImGui.Text($"{starText} {profit.RecommendationScore}");
        }

        // Chart Button
        if (!plugin.Configuration.HiddenColumns.Contains("Actions"))
        {
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
            
            // Get world name logic:
            // 1. If user manually set a world in Config and RememberLastWorld is true, use that (if valid)
            // 2. Else if "Auto" or empty, use PlayerState current world
            // 3. Else use config value directly
            
            var worldName = plugin.Configuration.PreferredWorld;

            // If set to "Auto" or empty, try to get from game state
            if (string.IsNullOrEmpty(worldName) || worldName.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                // CurrentWorld is a nullable value type or similar in Dalamud, check .RowId or just check if it has value if it's nullable struct
                // Looking at Plugin.cs: PlayerState.CurrentWorld is IPlayerState.CurrentWorld
                // It usually returns a Lumina.Excel.GeneratedSheets.World?
                
                // Let's rely on standard null check or .Value access if we are sure
                try 
                {
                    var currentWorld = Plugin.PlayerState.CurrentWorld;
                    if (currentWorld.Value.RowId != 0) // Check if valid
                    {
                        worldName = currentWorld.Value.Name.ToString();
                    }
                }
                catch 
                {
                    // Fallback if not logged in
                }
            }
            
            // If we still don't have a world name, we can't proceed
            if (string.IsNullOrEmpty(worldName) || worldName == "Auto")
            {
                Plugin.Log.Warning("Unable to determine world name. Please log in or specify a world in settings.");
                lastErrorMessage = "Unable to determine world name. Please log in or specify a world in settings.";
                lastErrorTime = DateTime.UtcNow;
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
            lastErrorMessage = ErrorMessageUtils.GetUserFriendlyMessage(ex);
            lastErrorSuggestion = ErrorMessageUtils.GetSuggestion(ex);
            lastErrorTime = DateTime.UtcNow;
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
