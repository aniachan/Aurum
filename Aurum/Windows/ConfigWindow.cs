using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Aurum.Utils;

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
        ThemeManager.PushWindowStyles(configuration.ColorTheme);
        ImGui.GetIO().FontGlobalScale = configuration.UIScale;
        
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
        ThemeManager.PopWindowStyles();
        ImGui.GetIO().FontGlobalScale = 1.0f;
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
            Aurum.Plugin.Instance?.RateLimiter?.UpdateConfiguration();
        }

        var batchSize = configuration.ApiBatchSize;
        if (ImGui.SliderInt("Batch Size (Items per Request)", ref batchSize, 10, 100))
        {
            configuration.ApiBatchSize = batchSize;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Number of items to fetch in a single API call.\nUniversalis supports up to 100, but lower values may improve responsiveness.");
        }

        var timeout = configuration.ApiRequestTimeoutSeconds;
        if (ImGui.SliderInt("API Request Timeout (Seconds)", ref timeout, 5, 60))
        {
            configuration.ApiRequestTimeoutSeconds = timeout;
            configuration.Save();
        }

        var workOffline = configuration.WorkOffline;
        if (ImGui.Checkbox("Work Offline (Disable API)", ref workOffline))
        {
            configuration.WorkOffline = workOffline;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("If enabled, no network requests will be made.\nThe plugin will only use cached data.");
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

        // Cross-World Settings
        ImGui.Separator();
        ImGui.Text("Cross-World Settings");
        
        var travelCost = configuration.CrossWorldTravelCost;
        if (ImGui.InputInt("Est. Travel Cost (Gil)", ref travelCost))
        {
            configuration.CrossWorldTravelCost = Math.Max(0, travelCost);
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Estimated gil cost to travel to another world (teleport fees).\nUsed to calculate net profit for cross-world arbitrage.");
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
            
            // Visual indicator for token bucket
            var currentTokens = (float)limiter.CurrentTokens;
            var maxTokens = (float)limiter.MaxTokens;
            
            ImGui.Spacing();
            ImGui.Text("API Token Bucket (Burst Capacity)");
            ImGui.ProgressBar(currentTokens / maxTokens, new Vector2(-1, 0), $"{currentTokens:F1} / {maxTokens:F0}");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Tokens replenish over time.\nRequests consume 1 token.\nEmpty bucket = Rate Limiting active.");
            }
        }
        
        ImGui.Separator();
        ImGui.Text("Priority Weights");
        ImGui.TextWrapped("Adjust how the recommendation engine prioritizes items. Total doesn't need to equal 100%.");

        var wRecipeLevel = configuration.WeightRecipeLevel;
        if (ImGui.SliderFloat("Recipe Level Weight", ref wRecipeLevel, 0f, 1f, "%.2f"))
        {
            configuration.WeightRecipeLevel = wRecipeLevel;
            configuration.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Higher level recipes get priority");

        var wMarketVelocity = configuration.WeightMarketVelocity;
        if (ImGui.SliderFloat("Market Velocity Weight", ref wMarketVelocity, 0f, 1f, "%.2f"))
        {
            configuration.WeightMarketVelocity = wMarketVelocity;
            configuration.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Items that sell often get priority");

        var wProfit = configuration.WeightProfitPotential;
        if (ImGui.SliderFloat("Profit Potential Weight", ref wProfit, 0f, 1f, "%.2f"))
        {
            configuration.WeightProfitPotential = wProfit;
            configuration.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Expensive items get priority");

        var wCategory = configuration.WeightCategory;
        if (ImGui.SliderFloat("Category Weight", ref wCategory, 0f, 1f, "%.2f"))
        {
            configuration.WeightCategory = wCategory;
            configuration.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Expert recipes get priority");

        var wUserPref = configuration.WeightUserPreference;
        if (ImGui.SliderFloat("User Preference Weight", ref wUserPref, 0f, 1f, "%.2f"))
        {
            configuration.WeightUserPreference = wUserPref;
            configuration.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Recently searched items get priority");
        
        if (ImGui.Button("Reset Weights to Defaults"))
        {
            configuration.WeightRecipeLevel = 0.3f;
            configuration.WeightMarketVelocity = 0.3f;
            configuration.WeightProfitPotential = 0.2f;
            configuration.WeightCategory = 0.1f;
            configuration.WeightUserPreference = 0.1f;
            configuration.Save();
        }
    }
}
