using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Aurum.Models;
using Aurum.Utils;

namespace Aurum.Windows;

/// <summary>
/// Window for managing bulk market data downloads by expansion
/// </summary>
public class DataManagerWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    
    // UI State
    private GameExpansion selectedExpansion = GameExpansion.Dawntrail;
    private bool isDownloading = false;
    private int downloadProgress = 0;
    private int downloadTotal = 0;
    private string downloadStatus = "";
    private CancellationTokenSource? downloadCts;
    private DateTime lastDownloadTime = DateTime.MinValue;
    private string lastDownloadExpansion = "";
    
    public DataManagerWindow(Plugin plugin) 
        : base("Market Data Manager##AurumDataManager", ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 350),
            MaximumSize = new Vector2(800, 600)
        };
        
        IsOpen = false;
    }

    public override void Draw()
    {
        ImGui.TextWrapped("Download market data in bulk by expansion. Once downloaded, use filters in the Dashboard to analyze your cached data.");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        // Expansion Selection
        ImGui.Text("Select Expansion:");
        ImGui.SetNextItemWidth(300);
        
        if (ImGui.BeginCombo("##ExpansionSelect", selectedExpansion.GetDisplayName()))
        {
            foreach (GameExpansion exp in Enum.GetValues(typeof(GameExpansion)))
            {
                bool isSelected = selectedExpansion == exp;
                if (ImGui.Selectable(exp.GetDisplayName(), isSelected))
                {
                    selectedExpansion = exp;
                }
                
                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }
        
        ImGui.Spacing();
        
        // Item count preview
        var itemCount = GetExpansionItemCount(selectedExpansion);
        ImGui.TextDisabled($"Approximate items in this expansion: {itemCount:N0}");
        
        // Estimate time
        var estimatedMinutes = EstimateDownloadTime(itemCount);
        ImGui.TextDisabled($"Estimated download time: {estimatedMinutes} minutes");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Based on API rate limits and batch sizes.\nActual time may vary depending on network conditions.");
        }
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        // Download Button
        if (isDownloading)
        {
            ImGui.BeginDisabled();
            ImGui.Button("Downloading...", new Vector2(200, 40));
            ImGui.EndDisabled();
            
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(100, 40)))
            {
                downloadCts?.Cancel();
            }
        }
        else
        {
            if (ImGui.Button("Start Download", new Vector2(200, 40)))
            {
                _ = StartDownloadAsync();
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"Download all market data for {selectedExpansion.GetDisplayName()}\nThis will cache data in your local database for offline filtering.");
            }
        }
        
        ImGui.Spacing();
        
        // Progress Bar
        if (isDownloading || downloadProgress > 0)
        {
            ImGui.Text(downloadStatus);
            
            float progress = downloadTotal > 0 ? (float)downloadProgress / downloadTotal : 0f;
            ImGui.ProgressBar(progress, new Vector2(-1, 30), $"{downloadProgress}/{downloadTotal}");
            
            if (isDownloading)
            {
                ImGui.TextDisabled($"Please wait... This may take several minutes.");
                ImGui.TextDisabled($"You can minimize this window and continue using FFXIV.");
            }
        }
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        // Last Download Info
        if (lastDownloadTime > DateTime.MinValue)
        {
            var timeSince = DateTime.UtcNow - lastDownloadTime;
            ImGui.Text($"Last download: {lastDownloadExpansion}");
            ImGui.SameLine();
            ImGui.TextDisabled($"({FormatTimeAgo(timeSince)} ago)");
        }
        
        ImGui.Spacing();
        
        // Cache Stats
        var cacheStats = plugin.CacheService.GetStats();
        ImGui.Text("Current Cache:");
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.3f, 1f, 0.3f, 1f), $"{cacheStats.ActiveEntries:N0} items");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text("Database Cache Statistics");
            ImGui.Separator();
            ImGui.Text($"Active: {cacheStats.ActiveEntries:N0}");
            ImGui.Text($"Expired: {cacheStats.ExpiredEntries:N0}");
            ImGui.Text($"Total: {cacheStats.TotalEntries:N0}");
            ImGui.EndTooltip();
        }
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        // Help Text
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1f));
        ImGui.TextWrapped("💡 Tip: Download data for your current expansion first, then use the Dashboard filters to find profitable crafts without waiting for API calls.");
        ImGui.PopStyleColor();
    }
    
    private async Task StartDownloadAsync()
    {
        if (isDownloading) return;
        
        isDownloading = true;
        downloadProgress = 0;
        downloadTotal = 0;
        downloadStatus = "Initializing...";
        downloadCts = new CancellationTokenSource();
        
        try
        {
            Plugin.Log.Information($"Starting bulk download for {selectedExpansion.GetDisplayName()}");
            
            // Get world name
            var worldName = GetWorldName();
            if (string.IsNullOrEmpty(worldName))
            {
                downloadStatus = "❌ Error: Unable to determine world name. Please log in.";
                Plugin.Log.Warning("Unable to determine world name for download");
                return;
            }
            
            // Get all recipes for the expansion
            downloadStatus = $"Scanning {selectedExpansion.GetDisplayName()} recipes...";
            var recipes = plugin.RecipeService.GetRecipesByExpansion(selectedExpansion).ToList();
            
            if (recipes.Count == 0)
            {
                downloadStatus = $"❌ No recipes found for {selectedExpansion.GetDisplayName()}";
                Plugin.Log.Warning($"No recipes found for {selectedExpansion}");
                return;
            }
            
            downloadTotal = recipes.Count;
            downloadStatus = $"Downloading market data for {downloadTotal:N0} items...";
            Plugin.Log.Information($"Found {downloadTotal} recipes to download for {selectedExpansion.GetDisplayName()}");
            
            // Extract unique item IDs
            var itemIds = recipes.Select(r => r.ResultItemId).Distinct().ToList();
            Plugin.Log.Information($"Unique items to fetch: {itemIds.Count}");
            
            // Download in batches using the existing batch API
            var batchSize = Math.Min(plugin.Configuration.ApiBatchSize, 100);
            var batches = itemIds.Chunk(batchSize).ToList();
            
            Plugin.Log.Information($"Downloading in {batches.Count} batches of {batchSize}");
            
            int processedItems = 0;
            
            foreach (var batch in batches)
            {
                if (downloadCts.Token.IsCancellationRequested)
                {
                    downloadStatus = "❌ Download cancelled by user";
                    Plugin.Log.Information("Download cancelled by user");
                    return;
                }
                
                try
                {
                    // Fetch batch
                    var batchList = batch.ToList();
                    var results = await plugin.UniversalisService.GetMarketDataBatchAsync(worldName, batchList);
                    
                    processedItems += batchList.Count;
                    downloadProgress = processedItems;
                    
                    var successCount = results.Count;
                    downloadStatus = $"Downloading... {downloadProgress:N0}/{downloadTotal:N0} ({successCount}/{batchList.Count} in batch)";
                    
                    Plugin.Log.Debug($"Batch complete: {successCount}/{batchList.Count} items fetched successfully");
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, $"Error downloading batch at position {processedItems}");
                    // Continue with next batch even if this one fails
                    processedItems += batch.Count();
                    downloadProgress = processedItems;
                }
            }
            
            // Success!
            downloadStatus = $"✅ Download complete! {downloadProgress:N0} items cached.";
            lastDownloadTime = DateTime.UtcNow;
            lastDownloadExpansion = selectedExpansion.GetDisplayName();
            
            Plugin.Log.Information($"Bulk download complete: {downloadProgress}/{downloadTotal} items");
            
            // Show success message for a few seconds before resetting
            await Task.Delay(3000);
            if (!isDownloading) return; // Don't reset if user started another download
            
            downloadProgress = 0;
            downloadTotal = 0;
        }
        catch (OperationCanceledException)
        {
            downloadStatus = "❌ Download cancelled";
            Plugin.Log.Information("Download cancelled");
        }
        catch (Exception ex)
        {
            downloadStatus = $"❌ Error: {ErrorMessageUtils.GetUserFriendlyMessage(ex)}";
            Plugin.Log.Error(ex, "Error during bulk download");
        }
        finally
        {
            isDownloading = false;
            downloadCts?.Dispose();
            downloadCts = null;
        }
    }
    
    private string GetWorldName()
    {
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
            catch
            {
                return "";
            }
        }
        
        if (worldName == "Auto")
        {
            return "";
        }
        
        return worldName;
    }
    
    private int GetExpansionItemCount(GameExpansion expansion)
    {
        // Rough estimates based on typical expansion content
        return expansion switch
        {
            GameExpansion.ARealmReborn => 3500,
            GameExpansion.Heavensward => 2000,
            GameExpansion.Stormblood => 2200,
            GameExpansion.Shadowbringers => 2400,
            GameExpansion.Endwalker => 2600,
            GameExpansion.Dawntrail => 2800,
            _ => 2000
        };
    }
    
    private string EstimateDownloadTime(int itemCount)
    {
        // With batch size of 100 and rate limits:
        // - 20 requests/min = ~2000 items/min (with 100 item batches)
        // - But with delays and processing, more like 1000-1500 items/min
        
        var batchSize = Math.Min(plugin.Configuration.ApiBatchSize, 100);
        var requestsNeeded = (int)Math.Ceiling((double)itemCount / batchSize);
        var rateLimit = plugin.Configuration.ApiRateLimitPerMinute;
        
        var minutes = Math.Ceiling((double)requestsNeeded / rateLimit);
        
        if (minutes < 2)
            return "1-2";
        else if (minutes < 5)
            return "2-5";
        else if (minutes < 10)
            return "5-10";
        else
            return $"10-{(int)minutes}";
    }
    
    private string FormatTimeAgo(TimeSpan timeSpan)
    {
        if (timeSpan.TotalMinutes < 1)
            return "just now";
        else if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes}m";
        else if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours}h";
        else
            return $"{(int)timeSpan.TotalDays}d";
    }
    
    public void Dispose()
    {
        downloadCts?.Cancel();
        downloadCts?.Dispose();
    }
}
