using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Aurum.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    // We give this window a constant ID using ###.
    // This allows for labels to be dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("Aurum Configuration###AurumConfig")
    {
        Flags = ImGuiWindowFlags.NoCollapse;

        Size = new Vector2(500, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 400),
            MaximumSize = new Vector2(800, 1000)
        };

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        // Override title bar colors to gold
        ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.4f, 0.3f, 0f, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.7f, 0.55f, 0f, 0.9f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, new Vector4(0.4f, 0.3f, 0f, 0.5f));
        
        // Flags must be added or removed before Draw() is being called, or they won't apply
        if (configuration.IsConfigWindowMovable)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
    }
    
    public override void PostDraw()
    {
        ImGui.PopStyleColor(3);
    }

    public override void Draw()
    {
        // Can't ref a property, so use a local copy
        var uiScale = configuration.UIScale;
        if (ImGui.SliderFloat("UI Scale", ref uiScale, 0.5f, 2.0f))
        {
            configuration.UIScale = uiScale;
            // Can save immediately on change if you don't want to provide a "Save and Close" button
            configuration.Save();
        }

        var movable = configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Movable Config Window", ref movable))
        {
            configuration.IsConfigWindowMovable = movable;
            configuration.Save();
        }
        
        var enableCharts = configuration.EnableAnimatedCharts;
        if (ImGui.Checkbox("Enable Animated Charts", ref enableCharts))
        {
            configuration.EnableAnimatedCharts = enableCharts;
            configuration.Save();
        }

        var rowsPerPage = configuration.RowsPerPage;
        if (ImGui.InputInt("Rows Per Page", ref rowsPerPage))
        {
             // Clamp to reasonable values
            rowsPerPage = Math.Clamp(rowsPerPage, 5, 200);
            configuration.RowsPerPage = rowsPerPage;
            configuration.Save();
        }
        
        var theme = configuration.ColorTheme;
        if (ImGui.BeginCombo("Color Theme", theme.ToString()))
        {
            foreach (var t in Enum.GetValues<Theme>())
            {
                if (ImGui.Selectable(t.ToString(), theme == t))
                {
                    configuration.ColorTheme = t;
                    configuration.Save();
                }
            }
            ImGui.EndCombo();
        }
        
        if (ImGui.TreeNode("Column Visibility"))
        {
            var columns = new[] { "Item", "Class", "Profit", "Margin", "Gil/Hr", "Demand", "Risk", "Score", "Actions" };
            foreach (var col in columns)
            {
                bool isVisible = !configuration.HiddenColumns.Contains(col);
                if (ImGui.Checkbox(col, ref isVisible))
                {
                    if (isVisible)
                        configuration.HiddenColumns.Remove(col);
                    else
                        configuration.HiddenColumns.Add(col);
                    configuration.Save();
                }
            }
            ImGui.TreePop();
        }

        ImGui.Separator();
        ImGui.Text("API Settings");

        var preferredWorld = configuration.PreferredWorld;
        if (ImGui.InputText("Preferred World", ref preferredWorld, 32))
        {
            configuration.PreferredWorld = preferredWorld;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Leave as 'Auto' to use your current world, or specify a world name.");
        }

        var rememberWorld = configuration.RememberLastWorld;
        if (ImGui.Checkbox("Remember Last World Selection", ref rememberWorld))
        {
            configuration.RememberLastWorld = rememberWorld;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("If enabled, the dashboard will remember the last world you refreshed data for,\noverriding 'Auto' until you change it back.");
        }

        var cacheDurationMinutes = configuration.MarketDataCacheDurationSeconds / 60;
        if (ImGui.SliderInt("Market Data Cache Duration (Minutes)", ref cacheDurationMinutes, 5, 1440))
        {
            configuration.MarketDataCacheDurationSeconds = cacheDurationMinutes * 60;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            var ts = TimeSpan.FromMinutes(cacheDurationMinutes);
            ImGui.SetTooltip(ts.Hours > 0 
                ? $"Cache will expire after {ts.Hours} hours and {ts.Minutes} minutes" 
                : $"Cache will expire after {ts.Minutes} minutes");
        }

        if (ImGui.Button("Clear Market Data Cache"))
        {
            Aurum.Plugin.Instance?.CacheService?.Clear();
        }
        
        var cacheStats = Aurum.Plugin.Instance?.CacheService?.GetStats();
        if (cacheStats != null)
        {
             ImGui.SameLine();
             ImGui.TextDisabled($"({cacheStats.ActiveEntries} entries)");
        }
        
        var maxConcurrent = configuration.MaxConcurrentApiRequests;
        if (ImGui.SliderInt("Max Concurrent API Requests", ref maxConcurrent, 1, 20))
        {
            configuration.MaxConcurrentApiRequests = maxConcurrent;
            configuration.Save();
        }

        var rateLimit = configuration.ApiRateLimitPerMinute;
        if (ImGui.InputInt("API Rate Limit (Requests/Minute)", ref rateLimit))
        {
            configuration.ApiRateLimitPerMinute = rateLimit;
            configuration.Save();
        }
        
        ImGui.Separator();
        ImGui.Text("Filtering & Sorting");

        var minProfit = configuration.MinimumProfitFilter;
        if (ImGui.InputInt("Minimum Profit (Gil)", ref minProfit))
        {
            configuration.MinimumProfitFilter = minProfit;
            configuration.Save();
        }

        var showProfitable = configuration.ShowOnlyProfitableItems;
        if (ImGui.Checkbox("Show Only Profitable Items", ref showProfitable))
        {
            configuration.ShowOnlyProfitableItems = showProfitable;
            configuration.Save();
        }

        ImGui.Separator();
        ImGui.Text("Category Filters");

        var includeCombat = configuration.FilterIncludeCombat;
        if (ImGui.Checkbox("Combat Gear", ref includeCombat))
        {
            configuration.FilterIncludeCombat = includeCombat;
            configuration.Save();
        }

        var includeCraftGather = configuration.FilterIncludeCraftingGathering;
        if (ImGui.Checkbox("Crafting & Gathering Gear", ref includeCraftGather))
        {
            configuration.FilterIncludeCraftingGathering = includeCraftGather;
            configuration.Save();
        }
        
        var includeFurniture = configuration.FilterIncludeFurniture;
        if (ImGui.Checkbox("Furniture & Housing", ref includeFurniture))
        {
            configuration.FilterIncludeFurniture = includeFurniture;
            configuration.Save();
        }

        var includeConsumables = configuration.FilterIncludeConsumables;
        if (ImGui.Checkbox("Consumables (Food/Potions)", ref includeConsumables))
        {
            configuration.FilterIncludeConsumables = includeConsumables;
            configuration.Save();
        }

        var includeMaterials = configuration.FilterIncludeMaterials;
        if (ImGui.Checkbox("Crafting Materials", ref includeMaterials))
        {
            configuration.FilterIncludeMaterials = includeMaterials;
            configuration.Save();
        }

        ImGui.Separator();
        
        // Sort Mode
        var sortMode = configuration.DefaultSortMode;
        if (ImGui.BeginCombo("Default Sort Mode", sortMode.ToString()))
        {
            foreach (var mode in Enum.GetValues<SortMode>())
            {
                if (ImGui.Selectable(mode.ToString(), sortMode == mode))
                {
                    configuration.DefaultSortMode = mode;
                    configuration.Save();
                }
            }
            ImGui.EndCombo();
        }

        // Risk Level
        var riskLevel = configuration.MaxAcceptableRisk;
        if (ImGui.BeginCombo("Max Acceptable Risk", riskLevel.ToString()))
        {
            foreach (var level in Enum.GetValues<Models.RiskLevel>())
            {
                if (ImGui.Selectable(level.ToString(), riskLevel == level))
                {
                    configuration.MaxAcceptableRisk = level;
                    configuration.Save();
                }
            }
            ImGui.EndCombo();
        }

        var showHighRisk = configuration.ShowHighRiskItems;
        if (ImGui.Checkbox("Show High Risk Items", ref showHighRisk))
        {
            configuration.ShowHighRiskItems = showHighRisk;
            configuration.Save();
        }

        var topItems = configuration.TopItemsToFetch;
        if (ImGui.SliderInt("Max Items to Fetch per Cycle", ref topItems, 10, 100))
        {
            configuration.TopItemsToFetch = topItems;
            configuration.Save();
        }

        var maxTracked = configuration.MaxItemsToTrack;
        if (ImGui.InputInt("Max Items to Track", ref maxTracked))
        {
            // Set reasonable limits (e.g., min 100, max 10000)
            maxTracked = Math.Clamp(maxTracked, 100, 10000);
            configuration.MaxItemsToTrack = maxTracked;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Maximum number of items to keep in the priority tracking system.\nOlder/lower priority items may be removed when this limit is reached.");
        }

        ImGui.Separator();
        ImGui.Text("Database Settings");
        
        var dbSize = Aurum.Plugin.Instance?.DatabaseService?.GetDatabaseSize() ?? 0;
        ImGui.Text($"Database Size: {dbSize / 1024.0 / 1024.0:F2} MB");
        
        if (ImGui.Button("Optimize Database (VACUUM)"))
        {
            Aurum.Plugin.Instance?.DatabaseService?.Vacuum();
            // Update last vacuum time
            configuration.LastDatabaseVacuum = DateTime.UtcNow;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Reclaims unused space and defragments the database file.\nMay take a few seconds.");
        }
        
        var vacuumFreq = configuration.DatabaseVacuumFrequencyDays;
        if (ImGui.InputInt("Auto-Optimize Frequency (Days)", ref vacuumFreq))
        {
            configuration.DatabaseVacuumFrequencyDays = Math.Max(0, vacuumFreq); // Ensure non-negative
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("How often to automatically run database optimization.\nSet to 0 to disable.");
        }

        if (configuration.LastDatabaseVacuum != DateTime.MinValue)
        {
            ImGui.TextDisabled($"Last optimized: {configuration.LastDatabaseVacuum.ToLocalTime()}");
        }

        ImGui.Separator();
        ImGui.Text("API Usage Statistics");
        var limiter = Aurum.Plugin.Instance?.RateLimiter;
        if (limiter != null)
        {
            ImGui.Text($"Requests (Last Minute): {limiter.RequestsLastMinute}");
            ImGui.Text($"Requests (Last Hour): {limiter.RequestsLastHour}");
            ImGui.Text($"Requests (Today): {limiter.RequestsToday}");
            ImGui.Text($"Total Requests: {limiter.TotalRequests}");
            ImGui.Text($"Rate Limited: {limiter.RateLimitedRequests}");
            ImGui.Text($"Errors/Retries: {limiter.TotalErrors} / {limiter.TotalRetries}");
        }
    }
}
