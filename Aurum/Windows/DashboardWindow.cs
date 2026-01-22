using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
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
    private bool hasLoadedCache = false;
    private string searchText = string.Empty;
    private string selectedClass = "All";
    private int minLevel = 1;
    private int maxLevel = 100;
    private int minItemLevel = 1;
    private int maxItemLevel = 999;
    private int minProfit = 0;
    private SortMode currentSort = SortMode.RecommendationScore;
    private bool showOnlyProfitable = true;
    private DateTime lastRefresh = DateTime.MinValue;
    
    // Column sorting
    private string? sortColumn = null;
    private bool sortAscending = false;
    
    // Pagination - Removed in favor of Virtualization
    // private int currentPage = 1; 
    
    private readonly string[] craftingClasses = { "All", "CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL" };
    private string lastErrorMessage = string.Empty;
    private string lastErrorSuggestion = string.Empty;
    private DateTime lastErrorTime = DateTime.MinValue;

    public DashboardWindow(Plugin plugin) 
        : base("Aurum - Crafting Profit Calculator by aniachan##Dashboard", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(900, 600),
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

    public void Dispose() { }

    public override void Draw()
    {
        // Load cached data on first draw
        if (!hasLoadedCache && !isLoading && !profitResults.Any())
        {
            hasLoadedCache = true;
            _ = LoadCachedDataAsync();
        }
        
        DrawHeader();
        ImGui.Separator();
        
        if (!string.IsNullOrEmpty(lastErrorMessage) && (DateTime.UtcNow - lastErrorTime).TotalMinutes < 5)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.6f, 0f, 1f));
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
             apiColor = new Vector4(1f, 0.84f, 0f, 1f); // Gold
             statusText = "API: Degraded (Cache Only)";
        }
        else if (requests > limit * 0.9)
        {
             apiColor = new Vector4(1f, 0.84f, 0f, 1f); // Gold
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
                ImGui.TextColored(new Vector4(1f, 0.84f, 0f, 1f), $"Errors: {limiter.TotalErrors}");
            }
            
            if (limiter.IsDegraded)
            {
                 ImGui.Separator();
                 ImGui.TextColored(new Vector4(1f, 0.84f, 0f, 1f), "Status: Degraded (High Error Rate)");
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
        else cacheColor = new Vector4(1f, 0.84f, 0f, 1f); // Gold for poor
        
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
        
        // Calculate button widths to position them properly
        float settingsWidth = ImGui.CalcTextSize("⚙️ Settings").X + ImGui.GetStyle().FramePadding.X * 2;
        float refreshWidth = ImGui.CalcTextSize("🔄 Refresh").X + ImGui.GetStyle().FramePadding.X * 2;
        float exportWidth = ImGui.CalcTextSize("📥 Export CSV").X + ImGui.GetStyle().FramePadding.X * 2;
        float loadingWidth = ImGui.CalcTextSize("Loading...").X;
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float rightPadding = 5f; // Right padding
        
        float totalButtonWidth = settingsWidth + refreshWidth + exportWidth + (spacing * 2) + rightPadding;
        
        ImGui.SameLine(ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX() - totalButtonWidth);
        
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

            ImGui.SameLine();
            if (ImGui.Button("📋 Copy Report"))
            {
                CopyReportToClipboard();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Copy a formatted text summary to clipboard for sharing");
            }
            
            // Defer UI rendering until visible - Check visibility before complex drawing
            // Note: ImGui.IsWindowCollapsed() checks if the window is collapsed (rolled up)
            // But for simple visibility check we might just rely on the fact that if we are here, Draw() is being called.
            // However, to be extra safe and optimize, we can check if we are collapsed.
            // ImGui.IsWindowVisible() is not available in all bindings or versions, so let's stick to IsWindowCollapsed.
            if (!ImGui.IsWindowCollapsed())
            {
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
                    
                    var avgProfit = filteredResults.Any() ? (int)filteredResults.Average(p => p.RawProfit) : 0;
                    var profitText = $"Avg Profit: {FormatGil(avgProfit)}";
                    
                    // Calculate proper alignment for right-side stats
                    var cursorPos = ImGui.GetCursorPosX();
                    var availWidth = ImGui.GetContentRegionAvail().X;
                    
                    if (lastRefresh > DateTime.MinValue)
                    {
                        var timeSinceRefresh = DateTime.UtcNow - lastRefresh;
                        var updateText = $"(Updated {timeSinceRefresh.TotalMinutes:F0}m ago)";
                        var padding = 10f; // Add some padding to the right
                        var combinedText = $"{profitText} {updateText}";
                        var textWidth = ImGui.CalcTextSize(combinedText).X + padding;
                        
                        ImGui.SetCursorPosX(cursorPos + availWidth - textWidth);
                        ImGui.Text(profitText);
                        ImGui.SameLine(0, ImGui.GetStyle().ItemSpacing.X);
                        ImGui.TextDisabled(updateText);
                    }
                    else
                    {
                        var textWidth = ImGui.CalcTextSize(profitText).X;
                        ImGui.SetCursorPosX(cursorPos + availWidth - textWidth);
                        ImGui.Text(profitText);
                    }
                }
                else
                {
                    ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), "Click 'Refresh' to load profit data");
                }
            }
        }
    
    private void DrawFilters()
    {
        ImGui.Text("Filters:");
        ImGui.SameLine();
        
        // Calculate widths for all controls after search box
        float classWidth = 100f;
        float levelLabelWidth = ImGui.CalcTextSize("Level:").X;
        float levelInputWidth = 60f;
        float itemLevelLabelWidth = ImGui.CalcTextSize("ILvl:").X;
        float dashWidth = ImGui.CalcTextSize("-").X;
        float minProfitLabelWidth = ImGui.CalcTextSize("Min Profit:").X;
        float minProfitWidth = 80f;
        float checkboxWidth = ImGui.CalcTextSize("Profitable Only").X + 30f; // checkbox + text
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        
        // Calculate remaining width for search box
        float totalRightSideWidth = classWidth + levelLabelWidth + levelInputWidth + dashWidth + levelInputWidth + 
                                    itemLevelLabelWidth + levelInputWidth + dashWidth + levelInputWidth +
                                    minProfitLabelWidth + minProfitWidth + checkboxWidth + (spacing * 13);
        float searchWidth = Math.Max(150f, ImGui.GetContentRegionAvail().X - totalRightSideWidth);
        
        // Search box with dynamic width
        ImGui.SetNextItemWidth(searchWidth);
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
        
        // Item Level range
        ImGui.Text("ILvl:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(60);
        if (ImGui.InputInt("##minitemlevel", ref minItemLevel))
        {
            minItemLevel = Math.Max(1, minItemLevel);
            ApplyFilters();
        }
        ImGui.SameLine();
        ImGui.Text("-");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(60);
        if (ImGui.InputInt("##maxitemlevel", ref maxItemLevel))
        {
            maxItemLevel = Math.Max(1, maxItemLevel);
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
        
        ImGui.SameLine();
        
        // Category Filter Popup
        if (ImGui.Button("Categories..."))
        {
            ImGui.OpenPopup("CategoryFilters");
        }

        ImGui.SameLine();

        // Column Visibility Popup
        if (ImGui.Button("Columns..."))
        {
            ImGui.OpenPopup("ColumnVisibility");
        }

        if (ImGui.BeginPopup("ColumnVisibility"))
        {
            var allCols = new[] { "Item", "Class", "Profit", "Margin", "Gil/Hr", "Demand", "Risk", "Score", "Actions" };
            bool changed = false;

            ImGui.Text("Show/Hide Columns:");
            ImGui.Separator();

            foreach (var col in allCols)
            {
                // Logic: if it's NOT in HiddenColumns, it is visible
                bool isVisible = !plugin.Configuration.HiddenColumns.Contains(col);
                
                if (ImGui.Checkbox(col, ref isVisible))
                {
                    if (isVisible)
                    {
                        plugin.Configuration.HiddenColumns.Remove(col);
                    }
                    else
                    {
                        if (!plugin.Configuration.HiddenColumns.Contains(col))
                            plugin.Configuration.HiddenColumns.Add(col);
                    }
                    changed = true;
                }
            }

            if (changed)
            {
                plugin.Configuration.Save();
            }

            ImGui.EndPopup();
        }

        if (ImGui.BeginPopup("CategoryFilters"))
        {
            bool changed = false;

            var includeCombat = plugin.Configuration.FilterIncludeCombat;
            if (ImGui.Checkbox("Combat Gear", ref includeCombat))
            {
                plugin.Configuration.FilterIncludeCombat = includeCombat;
                changed = true;
            }

            var includeCraftGather = plugin.Configuration.FilterIncludeCraftingGathering;
            if (ImGui.Checkbox("Crafting & Gathering", ref includeCraftGather))
            {
                plugin.Configuration.FilterIncludeCraftingGathering = includeCraftGather;
                changed = true;
            }
            
            var includeFurniture = plugin.Configuration.FilterIncludeFurniture;
            if (ImGui.Checkbox("Furniture", ref includeFurniture))
            {
                plugin.Configuration.FilterIncludeFurniture = includeFurniture;
                changed = true;
            }

            var includeConsumables = plugin.Configuration.FilterIncludeConsumables;
            if (ImGui.Checkbox("Consumables", ref includeConsumables))
            {
                plugin.Configuration.FilterIncludeConsumables = includeConsumables;
                changed = true;
            }

            var includeMaterials = plugin.Configuration.FilterIncludeMaterials;
            if (ImGui.Checkbox("Materials", ref includeMaterials))
            {
                plugin.Configuration.FilterIncludeMaterials = includeMaterials;
                changed = true;
            }

            if (changed)
            {
                plugin.Configuration.Save();
                ApplyFilters();
            }

            ImGui.EndPopup();
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
            sortColumn = null; // Clear column sort when using dropdown
            ApplyFilters();
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Reset Sort"))
        {
            sortColumn = null;
            currentSort = SortMode.RecommendationScore;
            ApplyFilters();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Reset to default sort (Recommendation Score)");
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

        // Virtualization: We render all items in a scrolling list using ListClipper
        // This is much more performant than pagination for large lists in ImGui
        
        // Table header
        // Count visible columns to set table column count correctly
        var allColumns = new[] { "Item", "Class", "Profit", "Margin", "Gil/Hr", "Demand", "Risk", "Score", "Actions" };
        var visibleColumns = allColumns.Where(c => !plugin.Configuration.HiddenColumns.Contains(c)).ToList();
        
        // Use a child window to constrain height for scrolling if not already in one (though Draw() calls us inside a child)
        // But the table itself handles scrolling via ScrollY flag.
        
        if (ImGui.BeginTable("ProfitTable", visibleColumns.Count, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Hideable))
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

            // Draw custom clickable headers
            ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
            
            if (!plugin.Configuration.HiddenColumns.Contains("Item"))
            {
                ImGui.TableSetColumnIndex(0);
                DrawSortableHeader("Item");
            }
            if (!plugin.Configuration.HiddenColumns.Contains("Class"))
            {
                ImGui.TableNextColumn();
                DrawSortableHeader("Class");
            }
            if (!plugin.Configuration.HiddenColumns.Contains("Profit"))
            {
                ImGui.TableNextColumn();
                DrawSortableHeader("Profit");
            }
            if (!plugin.Configuration.HiddenColumns.Contains("Margin"))
            {
                ImGui.TableNextColumn();
                DrawSortableHeader("Margin");
            }
            if (!plugin.Configuration.HiddenColumns.Contains("Gil/Hr"))
            {
                ImGui.TableNextColumn();
                DrawSortableHeader("Gil/Hr");
            }
            if (!plugin.Configuration.HiddenColumns.Contains("Demand"))
            {
                ImGui.TableNextColumn();
                DrawSortableHeader("Demand");
            }
            if (!plugin.Configuration.HiddenColumns.Contains("Risk"))
            {
                ImGui.TableNextColumn();
                DrawSortableHeader("Risk");
            }
            if (!plugin.Configuration.HiddenColumns.Contains("Score"))
            {
                ImGui.TableNextColumn();
                DrawSortableHeader("Score");
            }
            if (!plugin.Configuration.HiddenColumns.Contains("Actions"))
            {
                ImGui.TableNextColumn();
                ImGui.Text("Actions");
            }
            
            // VIRTUALIZATION (Manual)
            // Using manual virtualization since bindings vary
            
            float itemHeight = ImGui.GetTextLineHeight() + (ImGui.GetStyle().CellPadding.Y * 2); 
            int totalItems = filteredResults.Count;
            
            // We are inside a child window "ListRegion", so GetScrollY works for the list
            float scrollY = ImGui.GetScrollY();
            float viewHeight = ImGui.GetWindowHeight();
            
            int displayStart = (int)(scrollY / itemHeight);
            int displayEnd = displayStart + (int)(viewHeight / itemHeight) + 5; // Extra buffer
            
            displayStart = Math.Clamp(displayStart, 0, totalItems);
            displayEnd = Math.Clamp(displayEnd, 0, totalItems);
            
            // Top spacer
            if (displayStart > 0)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, displayStart * itemHeight);
            }
            
            // Draw visible items
            for (int i = displayStart; i < displayEnd; i++)
            {
                if (i < filteredResults.Count)
                {
                    DrawProfitRow(filteredResults[i]);
                }
            }
            
            // Bottom spacer
            if (displayEnd < totalItems)
            {
                float remainingHeight = (totalItems - displayEnd) * itemHeight;
                ImGui.TableNextRow(ImGuiTableRowFlags.None, remainingHeight);
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
            if (ImGui.Selectable($"{profit.Recipe.ItemName}##{profit.Recipe.RecipeId}", false))
            {
                plugin.DetailWindow.SetItem(profit);
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text($"Recipe Level: {profit.Recipe.RecipeLevel}");
                ImGui.Text($"Item Level: {profit.Recipe.ItemLevel}");
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
                : new Vector4(1f, 0.84f, 0f, 1f); // Gold
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
                                   new Vector4(1f, 0.84f, 0.5f, 1f);
                
                var icon = velocity >= 50 ? "🔥" :
                          velocity >= 10 ? "📈" :
                          velocity >= 1 ? "➡️" : "🐌";
                
                ImGui.TextColored(velocityColor, $"{icon} {velocity:F1}/d");
                
                if (profit.IsStale)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "⏰"); // Clock icon for stale data
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Data is stale (from cache). API might be unreachable.");
                    }
                }
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
                // Full market data available - show chart button
                if (ImGui.Button($"📈##{profit.Recipe.RecipeId}"))
                {
                    if (ImGui.GetIO().KeyShift)
                    {
                        plugin.ChartWindow.IsOpen = true;
                        plugin.ChartWindow.AddComparisonData(profit.MarketData, profit.Recipe.ItemName);
                    }
                    else
                    {
                        plugin.ChartWindow.SetMarketData(profit.MarketData, profit.Recipe.ItemName);
                    }
                }
                
                if (ImGui.BeginPopupContextItem($"ChartContext_{profit.Recipe.RecipeId}"))
                {
                    if (ImGui.Selectable("Open Chart"))
                    {
                        plugin.ChartWindow.SetMarketData(profit.MarketData, profit.Recipe.ItemName);
                    }
                    if (ImGui.Selectable("Add to Comparison"))
                    {
                        plugin.ChartWindow.IsOpen = true;
                        plugin.ChartWindow.AddComparisonData(profit.MarketData, profit.Recipe.ItemName);
                    }
                    ImGui.EndPopup();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("View Price History\n(Shift+Click to add to comparison)\n(Right-click for options)");
                }
            }
            else
            {
                // Cached data without market data - show fetch button
                if (ImGui.Button($"📊##{profit.Recipe.RecipeId}"))
                {
                    _ = FetchAndShowChartAsync(profit.Recipe.ResultItemId, profit.Recipe.ItemName);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Fetch & View Chart");
                }
            }
        }
    }
    
    private void DrawSortableHeader(string columnName)
    {
        var isSorted = sortColumn == columnName;
        var displayName = columnName;
        
        if (isSorted)
        {
            displayName += sortAscending ? " ▲" : " ▼"; // Up/down arrows
        }
        
        if (ImGui.Selectable(displayName, isSorted))
        {
            if (sortColumn == columnName)
            {
                // Toggle direction
                sortAscending = !sortAscending;
            }
            else
            {
                // New column
                sortColumn = columnName;
                sortAscending = false; // Default to descending (highest first)
            }
            ApplyFilters(); // Re-sort
        }
        
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip($"Click to sort by {columnName}");
        }
    }
    
    private async Task FetchAndShowChartAsync(uint itemId, string itemName)
    {
        try
        {
            // Get world name
            var worldName = plugin.Configuration.PreferredWorld;
            if (string.IsNullOrEmpty(worldName) || worldName.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var currentWorld = Plugin.PlayerState.CurrentWorld;
                    if (currentWorld.Value.RowId != 0)
                    {
                        worldName = currentWorld.Value.Name.ToString();
                    }
                }
                catch { }
            }
            
            if (string.IsNullOrEmpty(worldName) || worldName == "Auto")
            {
                Plugin.Log.Warning("Unable to determine world name for chart");
                return;
            }
            
            // Fetch market data
            var marketData = await plugin.UniversalisService.GetMarketDataAsync(worldName, itemId);
            if (marketData != null)
            {
                plugin.ChartWindow.SetMarketData(marketData, itemName);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Failed to fetch chart data for item {itemId}");
        }
    }
    
    private async Task LoadCachedDataAsync()
    {
        if (isLoading) return;
        
        isLoading = true;
        
        try
        {
            Plugin.Log.Information("Loading cached profit data...");
            
            // Load from database cache (last 24 hours)
            await Task.Run(() =>
            {
                // Load more data for dashboard
                profitResults = plugin.ProfitService.LoadCachedProfits(24, 5000, 0);
            });
            
            if (profitResults.Any())
            {
                Plugin.Log.Information($"Loaded {profitResults.Count} cached profit calculations");
                // Set last refresh to the oldest calculation time
                lastRefresh = profitResults.Min(p => p.CalculatedAt);
                ApplyFilters();
            }
            else
            {
                Plugin.Log.Information("No cached data found");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error loading cached data");
        }
        finally
        {
            isLoading = false;
        }
    }
    
    private CancellationTokenSource? refreshCts;

    private async Task RefreshDataAsync()
    {
        if (isLoading) return;
        
        isLoading = true;
        profitResults.Clear();
        filteredResults.Clear();
        
        // Cancel previous refresh if any
        refreshCts?.Cancel();
        refreshCts = new CancellationTokenSource();
        
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
            
            // Get recipes to analyze - use filter settings if available, otherwise get recent level recipes
            var recipes = plugin.RecipeService.GetRecipesByLevel(minLevel, maxLevel).Take(500).ToList();
            
            Plugin.Log.Information($"Analyzing {recipes.Count} recipes for {worldName}...");
            
            // Stream results as they come in
            await foreach (var profit in plugin.ProfitService.CalculateProfitsStreamAsync(recipes, worldName, refreshCts.Token))
            {
                profitResults.Add(profit);
                
                // Only re-apply filters periodically to avoid UI stutter on every item
                // or just do it if list is small enough
                if (profitResults.Count % 10 == 0) 
                {
                    ApplyFilters(); 
                }
            }
            
            Plugin.Log.Information($"Loaded {profitResults.Count} profit calculations");
            
            lastRefresh = DateTime.UtcNow;
            ApplyFilters(); // Final filter application
        }
        catch (OperationCanceledException)
        {
            Plugin.Log.Information("Refresh cancelled");
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
            refreshCts?.Dispose();
            refreshCts = null;
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
            
            // Item Level filter
            if (p.Recipe.ItemLevel < minItemLevel || p.Recipe.ItemLevel > maxItemLevel)
                return false;

            // Profitable only
            if (showOnlyProfitable && p.RawProfit <= 0)
                return false;

            // Min profit filter
            if (p.RawProfit < minProfit)
                return false;
            
            // Category Filters
            if (p.Recipe != null)
            {
                if (!plugin.Configuration.FilterIncludeCombat && p.Recipe.MainCategory == ItemMainCategory.Combat) return false;
                if (!plugin.Configuration.FilterIncludeCraftingGathering && 
                    (p.Recipe.MainCategory == ItemMainCategory.Crafting || p.Recipe.MainCategory == ItemMainCategory.Gathering)) return false;
                if (!plugin.Configuration.FilterIncludeFurniture && p.Recipe.MainCategory == ItemMainCategory.Furniture) return false;
                if (!plugin.Configuration.FilterIncludeConsumables && p.Recipe.MainCategory == ItemMainCategory.Consumable) return false;
                if (!plugin.Configuration.FilterIncludeMaterials && p.Recipe.MainCategory == ItemMainCategory.Material) return false;
            }

            return true;
        }).ToList();
        
        // Reset page (not used anymore but kept for logic structure if needed later)
        // currentPage = 1;
        
        // Apply column-based sorting if active
        if (sortColumn != null)
        {
            filteredResults = sortColumn switch
            {
                "Item" => sortAscending 
                    ? filteredResults.OrderBy(p => p.Recipe.ItemName).ToList()
                    : filteredResults.OrderByDescending(p => p.Recipe.ItemName).ToList(),
                "Class" => sortAscending
                    ? filteredResults.OrderBy(p => p.Recipe.CraftingClassName).ToList()
                    : filteredResults.OrderByDescending(p => p.Recipe.CraftingClassName).ToList(),
                "Profit" => sortAscending
                    ? filteredResults.OrderBy(p => p.RawProfit).ToList()
                    : filteredResults.OrderByDescending(p => p.RawProfit).ToList(),
                "Margin" => sortAscending
                    ? filteredResults.OrderBy(p => p.ProfitMargin).ToList()
                    : filteredResults.OrderByDescending(p => p.ProfitMargin).ToList(),
                "Gil/Hr" => sortAscending
                    ? filteredResults.OrderBy(p => p.GilPerHour).ToList()
                    : filteredResults.OrderByDescending(p => p.GilPerHour).ToList(),
                "Demand" => sortAscending
                    ? filteredResults.OrderBy(p => p.MarketData?.SaleVelocity ?? 0).ToList()
                    : filteredResults.OrderByDescending(p => p.MarketData?.SaleVelocity ?? 0).ToList(),
                "Risk" => sortAscending
                    ? filteredResults.OrderBy(p => p.RiskScore).ToList()
                    : filteredResults.OrderByDescending(p => p.RiskScore).ToList(),
                "Score" => sortAscending
                    ? filteredResults.OrderBy(p => p.RecommendationScore).ToList()
                    : filteredResults.OrderByDescending(p => p.RecommendationScore).ToList(),
                _ => filteredResults
            };
        }
        else
        {
            // Default sort by currentSort enum
            filteredResults = currentSort switch
            {
                SortMode.HighestProfit => filteredResults.OrderByDescending(p => p.RawProfit).ToList(),
                SortMode.HighestMargin => filteredResults.OrderByDescending(p => p.ProfitMargin).ToList(),
                SortMode.BestGilPerHour => filteredResults.OrderByDescending(p => p.GilPerHour).ToList(),
                SortMode.FastestSelling => filteredResults.OrderByDescending(p => p.MarketData?.SaleVelocity ?? 0).ToList(),
                SortMode.LowestCompetition => filteredResults.OrderBy(p => p.MarketData?.CurrentListings ?? int.MaxValue).ToList(),
                SortMode.RecommendationScore => filteredResults.OrderByDescending(p => p.RecommendationScore).ToList(),
                SortMode.BestEfficiency => filteredResults.OrderByDescending(p => p.EfficiencyScore).ToList(),
                _ => filteredResults
            };
        }
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

    private void CopyReportToClipboard()
    {
        if (!profitResults.Any())
        {
             return;
        }

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("✨ Aurum Market Report ✨");
            sb.AppendLine($"Generated: {DateTime.Now:g}");
            sb.AppendLine($"Found: {filteredResults.Count} opportunities");
            sb.AppendLine("----------------------------------------");

            // Top 5 items
            var topItems = filteredResults.Take(5);
            foreach (var p in topItems)
            {
                var profitK = p.RawProfit >= 1000 ? $"{p.RawProfit / 1000.0:F1}k" : p.RawProfit.ToString();
                sb.AppendLine($"🔹 {p.Recipe.ItemName} ({p.Recipe.CraftingClassName})");
                sb.AppendLine($"   Profit: {profitK} gil | Margin: {p.ProfitMargin:F0}%");
                sb.AppendLine($"   Velocity: {p.MarketData?.SaleVelocity:F1}/day");
                sb.AppendLine("");
            }
            
            if (filteredResults.Count > 5)
            {
                sb.AppendLine($"...and {filteredResults.Count - 5} more items.");
            }
            
            sb.AppendLine("----------------------------------------");
            sb.AppendLine("Download Aurum: https://github.com/Dicklesworthstone/Aurum");
            
            ImGui.SetClipboardText(sb.ToString());
            Plugin.Log.Information("Copied market report to clipboard");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to copy report to clipboard");
        }
    }
}
