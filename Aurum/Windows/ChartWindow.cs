using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.IO;
using System.Text;
using Dalamud.Interface.Windowing;
using Aurum.Models;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
// Implot is now imported through dalamud. Fancy charts can now be added.
using Dalamud.Bindings.ImPlot;

namespace Aurum.Windows;

public class ChartWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private MarketData? currentData;
    private string itemName = "Unknown Item";
    private int selectedTimeRange = 30; // Default 30 days, -1 for All
    private bool shouldFitHistory = true;
    private bool shouldFitDistribution = true;

    private class ChartSeries
    {
        public MarketData Data { get; init; } = null!;
        public string Name { get; init; } = string.Empty;
        public Vector4 Color { get; set; }
    }

    private readonly List<ChartSeries> comparisonSeries = new();
    
    private readonly Vector4[] SeriesColors = new[]
    {
        new Vector4(0f, 0.8f, 1f, 1f),   // Cyan (Primary)
        new Vector4(1f, 0.4f, 0.4f, 1f), // Red
        new Vector4(0.4f, 1f, 0.4f, 1f), // Green
        new Vector4(1f, 0.8f, 0f, 1f),   // Yellow
        new Vector4(0.8f, 0.4f, 1f, 1f), // Purple
        new Vector4(1f, 0.5f, 0f, 1f)    // Orange
    };

    public ChartWindow(Plugin plugin) : base("Price History Chart##AurumCharts", ImGuiWindowFlags.None)
    {
        this.plugin = plugin;
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void SetMarketData(MarketData data, string name)
    {
        currentData = data;
        itemName = name;
        
        comparisonSeries.Clear();
        comparisonSeries.Add(new ChartSeries 
        { 
            Data = data, 
            Name = name,
            Color = SeriesColors[0]
        });

        // If we have access to database service (via plugin), we could fetch historical snapshots here
        // Ideally, MarketData would already have this populated or we fetch it on demand.
        // For now, let's assume if HistorySnapshots is empty, we might need to fetch it? 
        // Or we just rely on what's passed in.
        // But ChartWindow doesn't have reference to DatabaseService directly, only Plugin.
        // Let's add a helper in Plugin or assume data is fully loaded.
        
        // Check if we can hydrate snapshots
        if (plugin?.DatabaseService != null && (data.HistorySnapshots == null || !data.HistorySnapshots.Any()))
        {
            // Fetch last 90 days of snapshots
            try 
            {
                // We need worldId, but MarketData usually only has WorldName string.
                // We'd need to resolve it or just skip if we can't easily.
                // Assuming currentData.WorldName is valid, but we need ID.
                // Actually DatabaseService.GetMarketSnapshots takes int worldId.
                // We don't have worldId readily available on MarketData object as public int property in all paths.
                // Wait, MarketData doesn't store WorldId, only WorldName.
                // This is a gap. We might need to look it up.
                // For now, let's skip auto-hydration in Window and rely on caller to hydrate.
            }
            catch {}
        }

        IsOpen = true;
    }

    public void AddComparisonData(MarketData data, string name)
    {
        if (comparisonSeries.Any(s => s.Data.ItemId == data.ItemId)) return;
        
        // If this is the first data being added, set it as current
        if (currentData == null)
        {
            SetMarketData(data, name);
            return;
        }
        
        int colorIndex = comparisonSeries.Count % SeriesColors.Length;
        comparisonSeries.Add(new ChartSeries
        {
            Data = data,
            Name = name,
            Color = SeriesColors[colorIndex]
        });
    }

    public override void Draw()
    {
        if (currentData == null)
        {
            ImGui.Text("No market data available.");
            return;
        }

        ImGui.Text($"Price History: {itemName}");
        
        // Time Range Selector
        ImGui.Separator();
        ImGui.Text("Time Range:");
        ImGui.SameLine();
        
        if (ImGui.RadioButton("1 Day", selectedTimeRange == 1)) selectedTimeRange = 1;
        ImGui.SameLine();
        if (ImGui.RadioButton("7 Days", selectedTimeRange == 7)) selectedTimeRange = 7;
        ImGui.SameLine();
        if (ImGui.RadioButton("30 Days", selectedTimeRange == 30)) selectedTimeRange = 30;
        ImGui.SameLine();
        if (ImGui.RadioButton("90 Days", selectedTimeRange == 90)) selectedTimeRange = 90;
        ImGui.SameLine();
        if (ImGui.RadioButton("All Time", selectedTimeRange == -1)) selectedTimeRange = -1;

        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();
        if (ImGui.Button("Fit Zoom"))
        {
            shouldFitHistory = true;
            shouldFitDistribution = true;
        }

        ImGui.Separator();

        if (currentData.RecentHistory == null || !currentData.RecentHistory.Any())
        {
            ImGui.Text("No history data available for chart.");
        }
        else
        {
            // Export Controls
            ImGui.SameLine(ImGui.GetWindowWidth() - 150);
            if (ImGui.Button("📤 Export"))
            {
                ImGui.OpenPopup("ExportPopup");
            }

            if (ImGui.BeginPopup("ExportPopup"))
            {
                if (ImGui.Selectable("Copy to Clipboard"))
                {
                    CopyChartDataToClipboard();
                }
                
                if (ImGui.Selectable("Save as CSV"))
                {
                    ExportChartDataToCsv();
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginTabBar("ChartTabs"))
            {
                if (ImGui.BeginTabItem("Price History"))
                {
                    DrawHistoryChart();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Price Distribution"))
                {
                    DrawDistributionChart();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }
        
        // Debug data dump
        if (ImGui.CollapsingHeader("Raw Data Debug"))
        {
            ImGui.Text($"Listings: {currentData.CurrentListings}");
            ImGui.Text($"Avg Price: {currentData.CurrentAveragePrice:F0}");
            
            if (currentData.RecentHistory != null)
            {
                ImGui.Text($"History Entries: {currentData.RecentHistory.Count}");
                
                if (ImGui.BeginTable("HistoryTableDebug", 3))
                {
                    ImGui.TableSetupColumn("Date");
                    ImGui.TableSetupColumn("Price");
                    ImGui.TableSetupColumn("Qty");
                    ImGui.TableHeadersRow();
    
                    foreach (var entry in currentData.RecentHistory.Take(10))
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text(entry.Timestamp.ToLocalTime().ToString("g"));
                        ImGui.TableNextColumn();
                        ImGui.Text(entry.PricePerUnit.ToString("N0"));
                        ImGui.TableNextColumn();
                        ImGui.Text(entry.Quantity.ToString());
                    }
                    ImGui.EndTable();
                }
            }
            else
            {
                ImGui.Text("History Entries: 0 (null)");
            }
        }
    }

    private void DrawHistoryChart()
    {
        if (currentData?.RecentHistory == null) return;

        if (shouldFitHistory)
        {
            ImPlot.SetNextAxesToFit();
            shouldFitHistory = false;
        }

        if (ImPlot.BeginPlot("Price History & Supply/Demand", new Vector2(-1, 400), ImPlotFlags.None))
        {
            // Setup axes
            ImPlot.SetupAxes("Date", "Price (Gil)", ImPlotAxisFlags.None, ImPlotAxisFlags.AutoFit);
            ImPlot.SetupAxisScale(ImAxis.X1, ImPlotScale.Time);
            
            // Setup Y2 axis for Volume / Listings
            ImPlot.SetupAxis(ImAxis.Y2, "Volume / Listings", ImPlotAxisFlags.AutoFit | ImPlotAxisFlags.AuxDefault);

            // Draw series in reverse order so the primary series (added first) is drawn last (on top)
            for (int i = comparisonSeries.Count - 1; i >= 0; i--)
            {
                DrawSeries(comparisonSeries[i]);
            }
            
            // Crosshair
            if (ImPlot.IsPlotHovered())
            {
                var mouse = ImPlot.GetPlotMousePos();
                ImPlot.TagX(mouse.X, new Vector4(1, 1, 1, 0.5f));
                ImPlot.TagY(mouse.Y, new Vector4(1, 1, 1, 0.5f));

                ImGui.BeginTooltip();
                var date = DateTimeOffset.FromUnixTimeSeconds((long)mouse.X).ToLocalTime();
                ImGui.Text($"Date: {date:g}");
                ImGui.Text($"Price: {mouse.Y:N0} Gil");
                ImGui.EndTooltip();
            }

            ImPlot.EndPlot();
        }

        // Only show detailed analysis for primary item
        DrawSupplyDemandAnalysis();
    }

    private void DrawSeries(ChartSeries series)
    {
        var data = series.Data;
        if (data.RecentHistory == null) return;

        var history = data.RecentHistory.OrderBy(h => h.Timestamp).AsEnumerable();
        var snapshots = data.HistorySnapshots?.OrderBy(h => h.Timestamp).AsEnumerable() ?? Enumerable.Empty<MarketSnapshot>();
        
        if (selectedTimeRange > 0)
        {
            var cutoff = DateTime.UtcNow.AddDays(-selectedTimeRange);
            history = history.Where(h => h.Timestamp >= cutoff);
            snapshots = snapshots.Where(h => h.Timestamp >= cutoff);
        }

        var historyList = history.ToList();
        var snapshotList = snapshots.ToList();

        if (!historyList.Any()) return;

        // ---------------------------------------------------------
        // PLOT 1 (Foreground): Price Line & Scatter on Y1 Axis
        // ---------------------------------------------------------
        ImPlot.SetAxes(ImAxis.X1, ImAxis.Y1);

        var xs = historyList.Select(x => (double)new DateTimeOffset(x.Timestamp.ToLocalTime()).ToUnixTimeSeconds()).ToArray();
        var ys = historyList.Select(x => (double)x.PricePerUnit).ToArray();
        
        ImPlot.SetNextLineStyle(series.Color, 2.0f);
        ImPlot.PlotLine($"{series.Name} Price", ref xs[0], ref ys[0], xs.Length);

        // Only show detailed scatter/bars for the primary item to avoid clutter
        if (series.Data == currentData)
        {
            ImPlot.SetNextMarkerStyle(ImPlotMarker.Circle, 4, new Vector4(1f, 1f, 1f, 0.8f), 1f, series.Color);
            ImPlot.PlotScatter($"{series.Name} Sales", ref xs[0], ref ys[0], xs.Length);

            var hqItems = historyList.Where(x => x.IsHQ).ToList();
            if (hqItems.Any())
            {
                var hqXs = hqItems.Select(x => (double)new DateTimeOffset(x.Timestamp.ToLocalTime()).ToUnixTimeSeconds()).ToArray();
                var hqYs = hqItems.Select(x => (double)x.PricePerUnit).ToArray();
                
                ImPlot.SetNextMarkerStyle(ImPlotMarker.Diamond, 6, new Vector4(1f, 0.8f, 0f, 1f), 1f, new Vector4(1f, 0.5f, 0f, 1f));
                ImPlot.PlotScatter("HQ Sales", ref hqXs[0], ref hqYs[0], hqXs.Length);
            }
            
            // Draw Volume/Listings for primary item only
            if (snapshotList.Any())
            {
                var snapXs = snapshotList.Select(x => (double)new DateTimeOffset(x.Timestamp.ToLocalTime()).ToUnixTimeSeconds()).ToArray();
                var snapYs = snapshotList.Select(x => (double)x.ListingCount).ToArray();
                
                ImPlot.SetAxes(ImAxis.X1, ImAxis.Y2);
                ImPlot.SetNextLineStyle(new Vector4(0.8f, 0.4f, 0.8f, 0.6f), 2.0f);
                ImPlot.PlotLine("Listings (Supply)", ref snapXs[0], ref snapYs[0], snapXs.Length);
                
                ImPlot.SetNextFillStyle(new Vector4(0.8f, 0.4f, 0.8f, 0.2f));
                ImPlot.PlotShaded("##SupplyArea", ref snapXs[0], ref snapYs[0], snapXs.Length, double.NegativeInfinity);
            }

            // Daily Sales Bars
            var dailySales = historyList
                .GroupBy(x => x.Timestamp.Date)
                .OrderBy(g => g.Key)
                .Select(g => new { Date = g.Key, Volume = g.Sum(x => x.Quantity) })
                .ToList();

            if (dailySales.Any())
            {
                var barXs = dailySales.Select(x => (double)new DateTimeOffset(x.Date).ToUnixTimeSeconds()).ToArray();
                var barYs = dailySales.Select(x => (double)x.Volume).ToArray();
                double barWidth = 86400 * 0.8;

                ImPlot.SetAxes(ImAxis.X1, ImAxis.Y2);
                ImPlot.SetNextFillStyle(new Vector4(0.5f, 0.5f, 0.5f, 0.25f)); 
                ImPlot.PlotBars("Daily Sales (Demand)", ref barXs[0], ref barYs[0], barXs.Length, barWidth);
            }
        }
    }

    private void DrawSupplyDemandAnalysis()
    {
        if (currentData?.RecentHistory == null) return;
        
        var history = currentData.RecentHistory.OrderBy(h => h.Timestamp).AsEnumerable();
        var snapshots = currentData.HistorySnapshots?.OrderBy(h => h.Timestamp).AsEnumerable() ?? Enumerable.Empty<MarketSnapshot>();
        
        if (selectedTimeRange > 0)
        {
            var cutoff = DateTime.UtcNow.AddDays(-selectedTimeRange);
            history = history.Where(h => h.Timestamp >= cutoff);
            snapshots = snapshots.Where(h => h.Timestamp >= cutoff);
        }

        var historyList = history.ToList();
        var snapshotList = snapshots.ToList();

        if (!historyList.Any())
        {
             ImGui.TextColored(new Vector4(1f, 0.5f, 0f, 1f), $"No sales found in the last {selectedTimeRange} days.");
             return;
        }

        // Aggregate data for Volume (Quantity) analysis
        var dailySales = historyList
            .GroupBy(x => x.Timestamp.Date)
            .OrderBy(g => g.Key)
            .Select(g => new { Date = g.Key, Volume = g.Sum(x => x.Quantity) })
            .ToList();

        // Supply/Demand Ratio Visualization
        if (snapshotList.Any() && dailySales.Any())
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Supply/Demand Analysis");
            
            // Simple gauge or ratio bar
            var lastSnapshot = snapshotList.Last();
            // Average daily sales over selected period
            var totalVolume = dailySales.Sum(x => x.Volume);
            var days = Math.Max(1, (dailySales.Max(x => x.Date) - dailySales.Min(x => x.Date)).TotalDays + 1);
            var avgDailyVolume = totalVolume / days;
            
            var ratio = avgDailyVolume > 0 ? lastSnapshot.ListingCount / avgDailyVolume : 999;
            
            ImGui.Text($"Current Listings: {lastSnapshot.ListingCount}");
            ImGui.SameLine(200);
            ImGui.Text($"Avg Daily Sales: {avgDailyVolume:F1}");
            
            ImGui.Text("S/D Ratio (Days to clear):");
            ImGui.SameLine();
            
            Vector4 ratioColor = new Vector4(0f, 1f, 0f, 1f); // Good
            if (ratio > 7) ratioColor = new Vector4(1f, 1f, 0f, 1f); // Slow
            if (ratio > 30) ratioColor = new Vector4(1f, 0.5f, 0f, 1f); // Very Slow
            if (ratio > 90) ratioColor = new Vector4(1f, 0f, 0f, 1f); // Dead
            
            ImGui.TextColored(ratioColor, $"{ratio:F1} days");
            
            // Progress bar for visual
            // 0 = instant, 30+ = full bar (bad)
            float progress = (float)Math.Clamp(ratio / 30.0, 0.0, 1.0);
            ImGui.ProgressBar(progress, new Vector2(-1, 20), "");
        }

        ImGui.Spacing();
        ImGui.Separator();
    }

    private void DrawDistributionChart()
    {
        if (currentData?.Listings == null || !currentData.Listings.Any())
        {
            ImGui.Text("No active listings available for distribution analysis.");
            return;
        }

        var listings = currentData.Listings.OrderBy(x => x.PricePerUnit).ToList();
        var minPrice = listings.First().PricePerUnit;
        var maxPrice = listings.Last().PricePerUnit;
        
        // Determine buckets
        // If spread is small, use exact prices. If large, bucketize.
        // Let's create roughly 20-30 buckets max.
        int bucketCount = 20;
        double range = maxPrice - minPrice;
        if (range == 0) range = 100; // Single price point
        double bucketSize = Math.Max(1, range / bucketCount);

        var buckets = new double[bucketCount + 1];
        var counts = new double[bucketCount + 1];
        
        // Initialize bucket X values (start of each bucket)
        for (int i = 0; i <= bucketCount; i++)
        {
            buckets[i] = minPrice + (i * bucketSize);
        }

        // Fill counts
        foreach (var listing in listings)
        {
            int bucketIndex = (int)((listing.PricePerUnit - minPrice) / bucketSize);
            if (bucketIndex < 0) bucketIndex = 0;
            if (bucketIndex > bucketCount) bucketIndex = bucketCount;
            counts[bucketIndex] += listing.Quantity; // Use Quantity for volume at price, or 1 for number of listings? Issue says "Histogram of listing prices" usually implies volume or count. Let's use Quantity (Depth).
        }

        if (shouldFitDistribution)
        {
            ImPlot.SetNextAxesToFit();
            shouldFitDistribution = false;
        }

        if (ImPlot.BeginPlot("Price Distribution (Market Depth)", new Vector2(-1, 400), ImPlotFlags.None))
        {
            ImPlot.SetupAxes("Price (Gil)", "Volume (Qty)", ImPlotAxisFlags.AutoFit, ImPlotAxisFlags.AutoFit);
            
            // Histogram
            ImPlot.SetNextFillStyle(new Vector4(0.4f, 0.7f, 0.9f, 0.5f));
            ImPlot.PlotBars("Volume at Price", ref buckets[0], ref counts[0], bucketCount + 1, bucketSize);

            // Highlight Current Min Price (Current Market Price)
            double currentMin = (double)currentData.MinPrice;
            ImPlot.SetNextLineStyle(new Vector4(0f, 1f, 0f, 1f), 2.0f);
            double[] minLineX = { currentMin, currentMin };
            double[] minLineY = { 0, counts.Max() * 1.1 }; // Go a bit above max
            ImPlot.PlotLine("Current Min Price", ref minLineX[0], ref minLineY[0], 2);

            // Highlight Outliers (Visual)
            // Simple logic: Highlight anything > 3x MinPrice as potential outlier
            double outlierThreshold = currentMin * 3.0;
            if (maxPrice > outlierThreshold)
            {
                 ImPlot.SetNextLineStyle(new Vector4(1f, 0f, 0f, 0.8f), 2.0f);
                 double[] outlierX = { outlierThreshold, outlierThreshold };
                 ImPlot.PlotLine("High Price Warning (>3x Min)", ref outlierX[0], ref minLineY[0], 2);
            }
            
            // Add NQ vs HQ distribution if useful later
            
            ImPlot.EndPlot();
        }

        // Summary stats
        ImGui.Separator();
        ImGui.Text($"Min Price: {minPrice:N0}");
        ImGui.SameLine(200);
        ImGui.Text($"Max Price: {maxPrice:N0}");
        ImGui.Text($"Total Listed Quantity: {listings.Sum(x => x.Quantity):N0}");
    }

    public void Dispose()
    {
        // Cleanup resources if needed
    }

    private void CopyChartDataToClipboard()
    {
        if (currentData?.RecentHistory == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("Date,Price,Quantity,HQ");

        foreach (var entry in currentData.RecentHistory.OrderBy(h => h.Timestamp))
        {
            sb.AppendLine($"{entry.Timestamp},{entry.PricePerUnit},{entry.Quantity},{entry.IsHQ}");
        }

        ImGui.SetClipboardText(sb.ToString());
    }

    private void ExportChartDataToCsv()
    {
        if (currentData?.RecentHistory == null) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Date,Price,Quantity,HQ");

            foreach (var entry in currentData.RecentHistory.OrderBy(h => h.Timestamp))
            {
                sb.AppendLine($"{entry.Timestamp},{entry.PricePerUnit},{entry.Quantity},{entry.IsHQ}");
            }

            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var sanitizedName = string.Join("_", itemName.Split(Path.GetInvalidFileNameChars()));
            var filePath = Path.Combine(documentsPath, $"Aurum_{sanitizedName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            File.WriteAllText(filePath, sb.ToString());
            
            // Optional: Notify user (could add a notification service later)
        }
        catch (Exception)
        {
            // Silently fail for now or log if logger available
        }
    }
}
