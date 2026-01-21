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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace Aurum.Windows;

public enum ChartType
{
    Line,
    Bar,
    Candle
}

public class ChartWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private MarketData? currentData;
    private string itemName = "Unknown Item";
    private int selectedTimeRange = 30; // Default 30 days, -1 for All
    private ChartType selectedChartType = ChartType.Candle;
    private bool showVolumeChart = true;
    private bool useLogScale = false;
    private bool normalizePrices = false;
    private bool shouldFitHistory = true;
    private bool shouldFitDistribution = true;
    private bool showColorCodedSupplyDemand = true;

    private class ChartSeries
    {
        public MarketData Data { get; init; } = null!;
        public string Name { get; init; } = string.Empty;
        public Vector4 Color { get; set; }
        public bool IsVisible { get; set; } = true;
    }

    private readonly List<ChartSeries> comparisonSeries = new();
    
    private bool isFetchingExtendedData = false;
    private DateTime lastExtendedDataCheck = DateTime.MinValue;
    
    private readonly Vector4[] SeriesColors = new[]
    {
        new Vector4(0.2f, 0.7f, 1f, 1f),    // Soft Blue (Primary)
        new Vector4(1f, 0.84f, 0f, 1f),    // Soft Gold
        new Vector4(0.3f, 0.8f, 0.5f, 1f),  // Soft Green
        new Vector4(1f, 0.75f, 0.2f, 1f),   // Soft Gold
        new Vector4(0.75f, 0.4f, 1f, 1f),   // Soft Purple
        new Vector4(1f, 0.55f, 0.2f, 1f),   // Soft Orange
        new Vector4(0.4f, 0.85f, 1f, 1f),   // Light Blue
        new Vector4(0.85f, 0.85f, 0.3f, 1f) // Muted Yellow
    };

    public ChartWindow(Plugin plugin) : base("Price History Chart##AurumCharts", ImGuiWindowFlags.None)
    {
        this.plugin = plugin;
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        
        // Override Dalamud's default red title bar with gold
        BgAlpha = 0.9f;
        RespectCloseHotkey = true;
    }
    
    public override void PreDraw()
    {
        // Override title bar colors to gold
        ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.4f, 0.3f, 0f, 0.8f)); // Dark gold
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.7f, 0.55f, 0f, 0.9f)); // Muted gold
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, new Vector4(0.4f, 0.3f, 0f, 0.5f)); // Faded gold
    }
    
    public override void PostDraw()
    {
        // Pop the 3 style colors we pushed
        ImGui.PopStyleColor(3);
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
            Color = SeriesColors[0],
            IsVisible = true
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

    public override void OnClose()
    {
        // Clear comparison series when window is closed so it starts fresh next time
        comparisonSeries.Clear();
        currentData = null;
        base.OnClose();
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
            Color = SeriesColors[colorIndex],
            IsVisible = true
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
        
        // Chart Type Selector
        ImGui.Separator();
        ImGui.Text("Chart Type:");
        ImGui.SameLine();
        
        if (ImGui.RadioButton("Line", selectedChartType == ChartType.Line)) selectedChartType = ChartType.Line;
        ImGui.SameLine();
        if (ImGui.RadioButton("Bar", selectedChartType == ChartType.Bar)) selectedChartType = ChartType.Bar;
        ImGui.SameLine();
        if (ImGui.RadioButton("Candle", selectedChartType == ChartType.Candle)) selectedChartType = ChartType.Candle;
        
        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();
        ImGui.TextDisabled("|");
        ImGui.SameLine();
        
        // Time Range Selector
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
        if (ImGui.RadioButton("All Time", selectedTimeRange == -1)) 
        {
            selectedTimeRange = -1;
            CheckAndFetchExtendedData();
        }

        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();
        if (ImGui.Button("Fit Zoom"))
        {
            shouldFitHistory = true;
            shouldFitDistribution = true;
        }
        
        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();
        ImGui.Checkbox("Show Volume", ref showVolumeChart);
        
        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();
        ImGui.Checkbox("Log Scale", ref useLogScale);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Use logarithmic scale for Y-axis (helpful when comparing items with different price ranges)");
        }

        // Price Normalization Checkbox
        if (comparisonSeries.Count > 1)
        {
            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();
            ImGui.Checkbox("Normalize Prices", ref normalizePrices);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Normalize prices to percentage change (0% = Start Price). Helpful for comparing trends between expensive and cheap items.");
            }
        }
        
        // Show comparison series management
        if (comparisonSeries.Count > 1)
        {
            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), $"Comparing {comparisonSeries.Count} items");
            
            ImGui.Indent();
            ChartSeries? toRemove = null;
            foreach (var series in comparisonSeries)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, series.Color);
                bool isVisible = series.IsVisible;
                if (ImGui.Checkbox($"##Vis_{series.Name}", ref isVisible))
                {
                    series.IsVisible = isVisible;
                }
                ImGui.SameLine();
                // If it's the primary series (first one), indicate it with a bullet or similar distinction if needed.
                // But for now, just the name.
                ImGui.PopStyleColor();
                ImGui.Text(series.Name);

                if (series != comparisonSeries[0])
                {
                    ImGui.SameLine();
                    ImGui.PushID(series.Name);
                    if (ImGui.SmallButton("Remove"))
                    {
                        toRemove = series;
                    }
                    ImGui.PopID();
                }
            }
            if (toRemove != null)
            {
                comparisonSeries.Remove(toRemove);
            }
            ImGui.Unindent();
        }

        ImGui.Separator();

        if (currentData.RecentHistory == null || !currentData.RecentHistory.Any())
        {
            ImGui.Text("No history data available for chart.");
        }
        else
        {
            // Export Controls
            var cursorPos = ImGui.GetCursorPosX();
            var availWidth = ImGui.GetContentRegionAvail().X;
            var buttonWidth = 100f;
            var padding = 10f; // Match padding from dashboard
            ImGui.SetCursorPosX(cursorPos + availWidth - buttonWidth - padding);
            if (ImGui.Button("📤 Export", new Vector2(buttonWidth, 0)))
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

                if (ImGui.Selectable("Save Chart as PNG"))
                {
                    SaveChartAsPng();
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

        var windowSize = ImGui.GetContentRegionAvail();
        
        // Check if we need extended data for this time range
        if (selectedTimeRange >= 90 || selectedTimeRange == -1)
        {
            CheckAndFetchExtendedData();
        }
        
        // Calculate heights for main chart and volume chart
        float mainChartHeight = showVolumeChart ? windowSize.Y * 0.55f : windowSize.Y * 0.8f;
        float volumeChartHeight = windowSize.Y * 0.25f;
        
        // Main price chart (no volume overlay)
        if (ImPlot.BeginPlot("##PriceHistoryChart", new Vector2(-1, mainChartHeight), ImPlotFlags.None))
        {
            // Setup axes with cleaner styling (single Y-axis for price only)
            string yLabel = normalizePrices ? "Price Change (%)" : "Price (Gil)";
            ImPlot.SetupAxes("Time", yLabel, ImPlotAxisFlags.None, ImPlotAxisFlags.AutoFit | ImPlotAxisFlags.RangeFit);
            ImPlot.SetupAxisScale(ImAxis.X1, ImPlotScale.Time);
            
            // Apply logarithmic scale if enabled (useful for comparing items with different price ranges)
            if (useLogScale && !normalizePrices)
            {
                ImPlot.SetupAxisScale(ImAxis.Y1, ImPlotScale.Log10);
                // For log scale, constrain to positive values only (log(0) is undefined)
                ImPlot.SetupAxisLimitsConstraints(ImAxis.Y1, 1, double.PositiveInfinity);
            }
            else
            {
                // For linear scale, allow starting from 0 (or -100% for normalized)
                double minLimit = normalizePrices ? -100 : 0;
                ImPlot.SetupAxisLimitsConstraints(ImAxis.Y1, minLimit, double.PositiveInfinity);
            }

            if (normalizePrices)
            {
                ImPlot.SetupAxisFormat(ImAxis.Y1, "%.0f%%");
            }
            else
            {
                ImPlot.SetupAxisFormat(ImAxis.Y1, "%.0f");
            }
            
            ImPlot.SetupAxisFormat(ImAxis.X1, "%m/%d");
            
            // Enable legend with better positioning and styling
            ImPlot.SetupLegend(ImPlotLocation.NorthWest, ImPlotLegendFlags.Outside | ImPlotLegendFlags.Horizontal);

            // Draw all series with the same chart type
            // First draw comparison series (non-primary), then primary on top
            // Iterate backwards so primary (index 0) is drawn last (on top)
            for (int i = comparisonSeries.Count - 1; i >= 1; i--)
            {
                if (comparisonSeries[i].IsVisible)
                {
                    DrawSeries(comparisonSeries[i], isPrimary: false, chartType: selectedChartType);
                }
            }
            
            // Draw primary series on top with selected chart type
            if (comparisonSeries.Count > 0 && comparisonSeries[0].IsVisible)
            {
                DrawSeries(comparisonSeries[0], isPrimary: true, chartType: selectedChartType);
            }
            
            // Crosshair with improved tooltip
            if (ImPlot.IsPlotHovered())
            {
                var mouse = ImPlot.GetPlotMousePos();
                var vLine = new[] { mouse.X };
                ImPlot.SetNextLineStyle(new Vector4(0.7f, 0.7f, 0.7f, 0.4f), 1.0f);
                ImPlot.PlotInfLines("##VCrosshair", ref vLine[0], 1);

                ImGui.BeginTooltip();
                var date = DateTimeOffset.FromUnixTimeSeconds((long)mouse.X).ToLocalTime();
                ImGui.TextColored(new Vector4(0.5f, 0.85f, 1f, 1f), $"📅 {date:MMM dd, yyyy HH:mm}");
                ImGui.Spacing();

                if (normalizePrices)
                    ImGui.TextColored(new Vector4(0.4f, 0.9f, 0.6f, 1f), $"📈 {mouse.Y:F1}%");
                else
                    ImGui.TextColored(new Vector4(0.4f, 0.9f, 0.6f, 1f), $"💰 {mouse.Y:N0} Gil");
                    
                ImGui.EndTooltip();
            }

            // Get X-axis limits BEFORE ending the plot
            var mainXLimits = ImPlot.GetPlotLimits(ImAxis.X1);

            ImPlot.EndPlot();
            
            // Separate volume chart below
            if (showVolumeChart)
            {
                DrawVolumeChart(volumeChartHeight, mainXLimits);
            }
        }
        else if (showVolumeChart)
        {
            // If main chart didn't render, use default limits
            var defaultLimits = new ImPlotRect();
            DrawVolumeChart(volumeChartHeight, defaultLimits);
        }

        // Supply/Demand Analysis
        DrawSupplyDemandAnalysis();

        // New Color Coded S/D Analysis
        if (showColorCodedSupplyDemand)
        {
            DrawColorCodedSupplyDemand();
        }
    }

    private void DrawColorCodedSupplyDemand()
    {
        if (currentData?.RecentHistory == null) return;
        
        var history = currentData.RecentHistory.OrderBy(h => h.Timestamp).ToList();
        var snapshots = currentData.HistorySnapshots?.OrderBy(h => h.Timestamp).ToList() ?? new List<MarketSnapshot>();
        
        if (!history.Any()) return;
        
        // 1. Calculate Daily Sales (full history)
        var dailySales = history
            .GroupBy(x => x.Timestamp.Date)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            
        // 2. Prepare Date Range (Start from earliest history to ensure MA is calculated correctly)
        var minDate = history.Min(x => x.Timestamp).Date;
        var maxDate = DateTime.UtcNow.Date;
        
        if (snapshots.Any())
        {
            var minSnap = snapshots.Min(x => x.Timestamp).Date;
            if (minSnap < minDate) minDate = minSnap;
        }
        
        // 3. Continuous date iteration to calculate metrics
        var allDates = new List<DateTime>();
        for (var d = minDate; d <= maxDate; d = d.AddDays(1))
        {
            allDates.Add(d);
        }

        // Helpers for interpolation
        var rawSupply = snapshots
            .GroupBy(s => s.Timestamp.Date)
            .ToDictionary(g => g.Key, g => g.Last().ListingCount);
            
        int lastSupply = rawSupply.TryGetValue(minDate, out int initialSupply) ? initialSupply : 0;
        
        var timePoints = new List<double>();
        var ratioPoints = new List<double>();
        
        foreach (var date in allDates)
        {
            // Interpolate Supply (Carry Forward)
            if (rawSupply.TryGetValue(date, out int supply))
            {
                lastSupply = supply;
            }
            
            // Calculate 7-day Moving Average of Sales
            double sumSales = 0;
            for (int i = 0; i < 7; i++)
            {
                var lookbackDate = date.AddDays(-i);
                if (dailySales.TryGetValue(lookbackDate, out long s))
                {
                    sumSales += s;
                }
            }
            double avgDailySales = sumSales / 7.0;

            // Only add point if it falls within selected range (but we needed prior data for MA)
            if (selectedTimeRange > 0)
            {
                 var cutoff = DateTime.UtcNow.Date.AddDays(-selectedTimeRange);
                 if (date < cutoff) continue;
            }

            // Calculate Ratio (Days to Clear)
            // If avg sales is super low (< 0.1/day), treat as "infinite" (cap at 60)
            // Use 0.1 to avoid division by zero or massive numbers for 0 sales
            double ratio = avgDailySales > 0.1 ? (double)lastSupply / avgDailySales : 60.0;
            
            if (ratio > 60) ratio = 60; // Hard cap for visualization
            
            timePoints.Add((double)new DateTimeOffset(date).ToUnixTimeSeconds());
            ratioPoints.Add(ratio);
        }
        
        if (timePoints.Count == 0) return;
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Supply/Demand Ratio (Days to Clear) - 7-Day Moving Average");
        
        if (ImPlot.BeginPlot("##SDRatioChart", new Vector2(-1, 200), ImPlotFlags.None))
        {
            ImPlot.SetupAxes("Date", "Ratio (Listings/DailySales)", ImPlotAxisFlags.None, ImPlotAxisFlags.AutoFit);
            ImPlot.SetupAxisScale(ImAxis.X1, ImPlotScale.Time);
            
            // Align X-axis limits with main chart if possible, but here we just autofit or use calculated range
            if (selectedTimeRange > 0)
            {
                 // ensure we see the whole range
                 // ImPlot.SetupAxisLimits(ImAxis.X1, ...); 
                 // AutoFit usually works fine
            }
            
            var xs = timePoints.ToArray();
            var ys = ratioPoints.ToArray();
            
            // Separate into color buckets
            // Green: < 7 days (Healthy)
            // Yellow: 7 - 30 days (Slower)
            // Red: > 30 days (Stagnant)
            
            var greenX = new List<double>(); var greenY = new List<double>();
            var yellowX = new List<double>(); var yellowY = new List<double>();
            var redX = new List<double>(); var redY = new List<double>();
            
            for(int i=0; i<xs.Length; i++)
            {
                if (ys[i] < 7) { greenX.Add(xs[i]); greenY.Add(ys[i]); }
                else if (ys[i] < 30) { yellowX.Add(xs[i]); yellowY.Add(ys[i]); }
                else { redX.Add(xs[i]); redY.Add(ys[i]); }
            }
            
            double barWidth = 86400 * 0.8;
            
            if (greenX.Any())
            {
                var gX = greenX.ToArray(); var gY = greenY.ToArray();
                ImPlot.SetNextFillStyle(new Vector4(0.2f, 0.8f, 0.4f, 0.7f));
                ImPlot.PlotBars("Healthy (<7d)", ref gX[0], ref gY[0], gX.Length, barWidth);
            }
            
            if (yellowX.Any())
            {
                var yX = yellowX.ToArray(); var yY = yellowY.ToArray();
                ImPlot.SetNextFillStyle(new Vector4(1f, 0.84f, 0f, 0.7f));
                ImPlot.PlotBars("Moderate (7-30d)", ref yX[0], ref yY[0], yX.Length, barWidth);
            }
            
            if (redX.Any())
            {
                var rX = redX.ToArray(); var rY = redY.ToArray();
                ImPlot.SetNextFillStyle(new Vector4(1f, 0.3f, 0.3f, 0.7f));
                ImPlot.PlotBars("Stagnant (>30d)", ref rX[0], ref rY[0], rX.Length, barWidth);
            }
            
            // Add a horizontal line at 30 days and 7 days for reference
            var plotLimits = ImPlot.GetPlotLimits(ImAxis.X1);
            double xMin = plotLimits.X.Min;
            double xMax = plotLimits.X.Max;
            
            // Fallback if limits are invalid (e.g. before first render)
            if (xMin >= xMax || double.IsNaN(xMin)) 
            {
                 xMin = xs.First();
                 xMax = xs.Last();
            }
            
            // Draw thresholds lines
            ImPlot.SetNextLineStyle(new Vector4(1f, 0.84f, 0f, 0.5f));
            double[] y7 = { 7, 7 };
            double[] xRange = { xMin, xMax };
            ImPlot.PlotLine("##Threshold7", ref xRange[0], ref y7[0], 2);
            
            ImPlot.SetNextLineStyle(new Vector4(1f, 0.3f, 0.3f, 0.5f));
            double[] y30 = { 30, 30 };
            ImPlot.PlotLine("##Threshold30", ref xRange[0], ref y30[0], 2);

            ImPlot.EndPlot();
        }
    }

    private void DrawVolumeChart(float height, ImPlotRect mainXLimits)
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

        if (ImPlot.BeginPlot("##VolumeChart", new Vector2(-1, height), ImPlotFlags.NoLegend | ImPlotFlags.NoMouseText))
        {
            ImPlot.SetupAxes("Time", "Volume", ImPlotAxisFlags.NoTickLabels, ImPlotAxisFlags.AutoFit);
            ImPlot.SetupAxisScale(ImAxis.X1, ImPlotScale.Time);
            
            // Link X-axis to main chart for synchronized zoom/pan
            ImPlot.SetupAxisLimits(ImAxis.X1, mainXLimits.X.Min, mainXLimits.X.Max, ImPlotCond.Always);
            
            // Draw Daily Sales Bars
            var dailySales = historyList
                .GroupBy(x => x.Timestamp.Date)
                .OrderBy(g => g.Key)
                .Select(g => new { Date = g.Key, Volume = g.Sum(x => x.Quantity) })
                .ToList();

            if (dailySales.Any())
            {
                var barXs = dailySales.Select(x => (double)new DateTimeOffset(x.Date).ToUnixTimeSeconds()).ToArray();
                var barYs = dailySales.Select(x => (double)x.Volume).ToArray();
                double barWidth = 86400 * 0.75;

                ImPlot.SetNextFillStyle(new Vector4(0.3f, 0.75f, 1f, 0.6f));
                ImPlot.PlotBars("Daily Sales", ref barXs[0], ref barYs[0], barXs.Length, barWidth);
            }
            
            // Draw Supply line overlay
            if (snapshotList.Any())
            {
                var snapXs = snapshotList.Select(x => (double)new DateTimeOffset(x.Timestamp).ToUnixTimeSeconds()).ToArray();
                var snapYs = snapshotList.Select(x => (double)x.ListingCount).ToArray();
                
                ImPlot.SetNextLineStyle(new Vector4(0.9f, 0.5f, 1f, 0.8f), 2.0f);
                ImPlot.PlotLine("Supply", ref snapXs[0], ref snapYs[0], snapXs.Length);
            }

            ImPlot.EndPlot();
        }
    }

    private void DrawSeries(ChartSeries series, bool isPrimary, ChartType chartType)
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

        ImPlot.SetAxes(ImAxis.X1, ImAxis.Y1);

        // Draw based on chart type
        if (chartType == ChartType.Candle)
        {
            DrawCandlestickChart(series, historyList);
        }
        else if (chartType == ChartType.Bar)
        {
            DrawBarChart(series, historyList);
        }
        else // Line chart
        {
            DrawLineChart(series, historyList);
            
            // HQ markers only on line charts for primary series
            if (isPrimary)
            {
                DrawHQMarkers(historyList);
            }
        }
    }

    private void DrawCandlestickChart(ChartSeries series, List<SaleRecord> historyList)
    {
        // Group into time buckets for candlesticks - smaller buckets to show more data
        var bucketSize = selectedTimeRange switch
            {
                1 => TimeSpan.FromMinutes(30),   // 30-min candles for 1 day
                7 => TimeSpan.FromHours(2),      // 2-hour candles for 7 days
                30 => TimeSpan.FromHours(6),     // 6-hour candles for 30 days
                90 => TimeSpan.FromHours(12),    // 12-hour candles for 90 days
                _ => TimeSpan.FromDays(1)        // Daily for all time
            };

        // Determine base price for normalization
        // Use the first available price in the visible range or first history item
        double basePrice = 1.0;
        if (normalizePrices && historyList.Any())
        {
            basePrice = (double)historyList.First().PricePerUnit;
            if (basePrice <= 0) basePrice = 1.0;
        }

        var candles = historyList
            .GroupBy(h => new DateTime((long)(h.Timestamp.Ticks / bucketSize.Ticks) * bucketSize.Ticks))
            .Select(g => new
            {
                Time = g.Key,
                Open = NormalizeIfNeeded(g.First().PricePerUnit, basePrice),
                High = NormalizeIfNeeded(g.Max(x => x.PricePerUnit), basePrice),
                Low = NormalizeIfNeeded(g.Min(x => x.PricePerUnit), basePrice),
                Close = NormalizeIfNeeded(g.Last().PricePerUnit, basePrice),
                Volume = g.Sum(x => x.Quantity)
            })
            .OrderBy(c => c.Time)
            .ToList();

        if (candles.Any())
        {
            var bullColor = new Vector4(0.2f, 0.8f, 0.4f, 1.0f);   // Bright green for up
            var bearColor = new Vector4(0.8f, 0.6f, 0f, 1.0f);  // Darker gold for down
            var wickColor = new Vector4(0.6f, 0.6f, 0.6f, 0.9f);   // Gray for wicks
            var syntheticWickColor = new Vector4(0.5f, 0.5f, 0.5f, 0.4f); // Faded for synthetic
            var interpolateColor = new Vector4(0.5f, 0.5f, 0.5f, 0.4f); // Faded gray for gaps
            var candleWidth = bucketSize.TotalSeconds * 0.7; // 70% of time bucket

            // Draw interpolation line connecting candle closes (helps see trend through gaps)
            var closeXs = candles.Select(c => (double)new DateTimeOffset(c.Time).ToUnixTimeSeconds()).ToArray();
            var closeYs = candles.Select(c => (double)c.Close).ToArray();
            
            // Use series color for the line so it appears in the legend with the correct color
            ImPlot.SetNextLineStyle(series.Color, 2.0f);
            ImPlot.PlotLine(series.Name, ref closeXs[0], ref closeYs[0], closeXs.Length);

            foreach (var candle in candles)
            {
                var time = (double)new DateTimeOffset(candle.Time).ToUnixTimeSeconds();
                var isBull = candle.Close > candle.Open;  // Strictly greater for green
                var bodyColor = isBull ? bullColor : bearColor;

                // Generate synthetic wick if we don't have real high/low data
                // Add padding to make wicks visible even on flat candles
                var wickHigh = candle.High;
                var wickLow = candle.Low;
                var isSyntheticWick = false;
                
                // If candle is completely flat (same open/high/low/close), add synthetic wick
                if (Math.Abs(wickHigh - wickLow) < (normalizePrices ? 0.01 : 1)) // Adjust epsilon for normalized vs raw
                {
                    var wickSize = Math.Max(normalizePrices ? 0.5 : 5u, Math.Abs(candle.Close) * 0.03); // 3% wick for visibility
                    wickHigh = candle.Close + wickSize;
                    wickLow = candle.Close - wickSize;
                    isSyntheticWick = true;
                }

                // Always draw wick (high-low line) - use dotted style for synthetic wicks
                var wickX = new[] { time, time };
                var wickY = new[] { (double)wickLow, (double)wickHigh };
                
                if (isSyntheticWick)
                {
                    // Dotted/dashed line for synthetic wicks
                    ImPlot.SetNextLineStyle(syntheticWickColor, 2.0f);
                }
                else
                {
                    // Solid line for real wicks
                    ImPlot.SetNextLineStyle(wickColor, 1.5f);
                }
                ImPlot.PlotLine($"##wick{time}", ref wickX[0], ref wickY[0], 2);

                // Draw body (open to close)
                var bodyX = new[] { time, time };
                var bodyY = new[] { (double)candle.Open, (double)candle.Close };
                
                // Keep bodies narrow - scale with time period
                var bodyThickness = selectedTimeRange switch
                {
                    1 => 4f,   // Very thin for intraday
                    7 => 6f,   // Thin for weekly
                    30 => 8f,  // Medium for monthly
                    90 => 10f, // Slightly thicker for quarterly
                    _ => 12f   // Thicker for long term
                };
                
                // If open == close (flat candle), we still draw the line - it will appear as a horizontal dash
                ImPlot.SetNextLineStyle(bodyColor, bodyThickness);
                ImPlot.PlotLine($"##body{time}", ref bodyX[0], ref bodyY[0], 2);
            }
        }
    }

    private void DrawBarChart(ChartSeries series, List<SaleRecord> historyList)
    {
        // Group sales by time buckets for bars
        var bucketSize = selectedTimeRange switch
        {
            1 => TimeSpan.FromMinutes(30),
            7 => TimeSpan.FromHours(2),
            30 => TimeSpan.FromHours(6),
            90 => TimeSpan.FromDays(1),
            _ => TimeSpan.FromDays(1)
        };
        
        // Determine base price for normalization
        double basePrice = 1.0;
        if (normalizePrices && historyList.Any())
        {
            basePrice = (double)historyList.First().PricePerUnit;
            if (basePrice <= 0) basePrice = 1.0;
        }

        var bars = historyList
            .GroupBy(h => new DateTime((long)(h.Timestamp.Ticks / bucketSize.Ticks) * bucketSize.Ticks))
            .Select(g => new
            {
                Time = g.Key,
                AvgPrice = NormalizeIfNeeded(g.Average(x => x.PricePerUnit), basePrice)
            })
            .OrderBy(c => c.Time)
            .ToList();

        if (bars.Any())
        {
            var barXs = bars.Select(b => (double)new DateTimeOffset(b.Time).ToUnixTimeSeconds()).ToArray();
            var barYs = bars.Select(b => (double)b.AvgPrice).ToArray();
            double barWidth = bucketSize.TotalSeconds * 0.7;

            // Use series color with some transparency
            var color = series.Color;
            color.W = 0.7f; 

            ImPlot.SetNextFillStyle(color);
            ImPlot.PlotBars(series.Name, ref barXs[0], ref barYs[0], barXs.Length, barWidth);
        }
    }

    private void DrawLineChart(ChartSeries series, List<SaleRecord> historyList)
    {
        // Determine base price for normalization
        double basePrice = 1.0;
        if (normalizePrices && historyList.Any())
        {
            basePrice = (double)historyList.First().PricePerUnit;
            if (basePrice <= 0) basePrice = 1.0;
        }
    
        var xs = historyList.Select(x => (double)new DateTimeOffset(x.Timestamp).ToUnixTimeSeconds()).ToArray();
        var ys = historyList.Select(x => NormalizeIfNeeded(x.PricePerUnit, basePrice)).ToArray();
        
        if (xs.Length > 0)
        {
            ImPlot.SetNextLineStyle(series.Color, 2.0f);
            ImPlot.PlotLine($"{series.Name}", ref xs[0], ref ys[0], xs.Length);
        }
    }

    private void DrawHQMarkers(List<SaleRecord> historyList)
    {
        // Determine base price for normalization
        double basePrice = 1.0;
        if (normalizePrices && historyList.Any())
        {
            basePrice = (double)historyList.First().PricePerUnit;
            if (basePrice <= 0) basePrice = 1.0;
        }

        var hqItems = historyList.Where(x => x.IsHQ).ToList();
        if (hqItems.Any())
        {
            var hqXs = hqItems.Select(x => (double)new DateTimeOffset(x.Timestamp).ToUnixTimeSeconds()).ToArray();
            var hqYs = hqItems.Select(x => NormalizeIfNeeded(x.PricePerUnit, basePrice)).ToArray();
            
            ImPlot.SetNextMarkerStyle(ImPlotMarker.Diamond, 5, new Vector4(1f, 0.85f, 0.1f, 0.9f), 1.0f, new Vector4(1f, 0.65f, 0f, 0.7f));
            ImPlot.PlotScatter("HQ", ref hqXs[0], ref hqYs[0], hqXs.Length);
        }
    }
    
    private double NormalizeIfNeeded(double price, double basePrice)
    {
        if (!normalizePrices) return price;
        return ((price - basePrice) / basePrice) * 100.0;
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
            if (ratio > 90) ratioColor = new Vector4(1f, 0.84f, 0f, 1f); // Dead
            
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
        int bucketCount = 20;
        double range = maxPrice - minPrice;
        if (range <= 0) range = 100; // Single price point
        double bucketSize = Math.Max(1, range / bucketCount);

        // Arrays for histogram data
        // We use bucketCount + 1 to catch edge cases
        var plotX = new double[bucketCount + 1];
        var countsNQ = new double[bucketCount + 1];
        var countsHQ = new double[bucketCount + 1];
        var countsTotal = new double[bucketCount + 1];
        
        // Initialize bucket centers
        for (int i = 0; i <= bucketCount; i++)
        {
            // Bar should be centered in the bucket
            // Bucket i spans [min + i*size, min + (i+1)*size]
            plotX[i] = minPrice + (i * bucketSize) + (bucketSize * 0.5);
        }

        // Fill counts
        foreach (var listing in listings)
        {
            int bucketIndex = (int)((listing.PricePerUnit - minPrice) / bucketSize);
            // Clamp to valid range
            if (bucketIndex < 0) bucketIndex = 0;
            if (bucketIndex > bucketCount) bucketIndex = bucketCount;
            
            if (listing.IsHQ)
                countsHQ[bucketIndex] += listing.Quantity;
            else
                countsNQ[bucketIndex] += listing.Quantity;
                
            countsTotal[bucketIndex] += listing.Quantity;
        }

        if (shouldFitDistribution)
        {
            ImPlot.SetNextAxesToFit();
            shouldFitDistribution = false;
        }

        if (ImPlot.BeginPlot("##PriceDistributionChart", new Vector2(-1, 400), ImPlotFlags.None))
        {
            ImPlot.SetupAxes("Price (Gil)", "Volume (Qty)", ImPlotAxisFlags.None, ImPlotAxisFlags.None);
            ImPlot.SetupAxisFormat(ImAxis.X1, "%'.0f");
            ImPlot.SetupLegend(ImPlotLocation.NorthEast);
            
            // Stacked Bar Chart Implementation
            // Strategy: Draw Total (HQ color) first, then NQ (NQ color) on top (covering the bottom part).
            // Visual result: Bottom = NQ (Blue), Top = HQ (Gold).

            // Calculate Outlier Threshold using IQR
            var prices = listings.Select(x => (double)x.PricePerUnit).ToList();
            double outlierThreshold = double.MaxValue;

            if (prices.Count >= 4)
            {
                int q1Idx = prices.Count / 4;
                int q3Idx = (prices.Count * 3) / 4;
                double q1 = prices[q1Idx];
                double q3 = prices[q3Idx];
                double iqr = q3 - q1;
                // If IQR is 0 (all prices same), use a multiplier fallback
                if (iqr <= 0) iqr = q3 * 0.5;
                
                outlierThreshold = q3 + (1.5 * iqr);
            }

            // Fallback / Sanity check
            if (outlierThreshold <= minPrice || outlierThreshold == double.MaxValue)
            {
                 outlierThreshold = minPrice * 3.0;
            }

            // Split counts into Normal and Outlier
            var countsTotalNormal = new double[bucketCount + 1];
            var countsNQNormal = new double[bucketCount + 1];
            var countsTotalOutlier = new double[bucketCount + 1];
            var countsNQOutlier = new double[bucketCount + 1];
            
            // Re-distribute counts
            foreach (var listing in listings)
            {
                int bucketIndex = (int)((listing.PricePerUnit - minPrice) / bucketSize);
                if (bucketIndex < 0) bucketIndex = 0;
                if (bucketIndex > bucketCount) bucketIndex = bucketCount;
                
                bool isOutlier = listing.PricePerUnit > outlierThreshold;
                
                if (isOutlier)
                {
                    countsTotalOutlier[bucketIndex] += listing.Quantity;
                    if (!listing.IsHQ) countsNQOutlier[bucketIndex] += listing.Quantity;
                }
                else
                {
                    countsTotalNormal[bucketIndex] += listing.Quantity;
                    if (!listing.IsHQ) countsNQNormal[bucketIndex] += listing.Quantity;
                }
            }
            
            // Draw Normal (Total then NQ)
            ImPlot.SetNextFillStyle(new Vector4(1f, 0.84f, 0f, 0.8f)); // Soft Gold for HQ
            ImPlot.PlotBars("HQ Volume", ref plotX[0], ref countsTotalNormal[0], bucketCount + 1, bucketSize);
            
            ImPlot.SetNextFillStyle(new Vector4(0.2f, 0.7f, 1f, 0.8f)); // Soft Blue for NQ
            ImPlot.PlotBars("NQ Volume", ref plotX[0], ref countsNQNormal[0], bucketCount + 1, bucketSize);

            // Draw Outliers (Total then NQ)
            if (countsTotalOutlier.Sum() > 0)
            {
                ImPlot.SetNextFillStyle(new Vector4(1f, 0.3f, 0.3f, 0.8f)); // Red for Outlier HQ
                ImPlot.PlotBars("Outlier HQ", ref plotX[0], ref countsTotalOutlier[0], bucketCount + 1, bucketSize);

                ImPlot.SetNextFillStyle(new Vector4(0.8f, 0.1f, 0.1f, 0.8f)); // Dark Red for Outlier NQ
                ImPlot.PlotBars("Outlier NQ", ref plotX[0], ref countsNQOutlier[0], bucketCount + 1, bucketSize);
            }

            // Highlight Current Min Price (Market Price) with cleaner line
            double currentMin = (double)currentData.MinPrice;
            ImPlot.SetNextLineStyle(new Vector4(0.25f, 0.85f, 0.45f, 0.9f), 2.5f);
            double[] minLineX = { currentMin, currentMin };
            double[] minLineY = { 0, countsTotal.Max() * 1.1 }; // Go a bit above max
            ImPlot.PlotLine("Market Price", ref minLineX[0], ref minLineY[0], 2);
            
            // Add Annotation for Market Price
            ImPlot.PlotText($"Market Price: {currentMin:N0} gil", currentMin, minLineY[1] * 0.95, new Vector2(0, -10), ImPlotTextFlags.None);

            // Highlight Outliers Threshold
            if (maxPrice > outlierThreshold)
            {
                 ImPlot.SetNextLineStyle(new Vector4(1f, 0.4f, 0.4f, 0.7f), 2.0f);
                 double[] outlierX = { outlierThreshold, outlierThreshold };
                 ImPlot.PlotLine("Outlier Threshold", ref outlierX[0], ref minLineY[0], 2);
            }
            
            ImPlot.EndPlot();
        }

        // Summary stats
        ImGui.Separator();
        ImGui.Text($"Min Price: {minPrice:N0}");
        ImGui.SameLine(200);
        ImGui.Text($"Max Price: {maxPrice:N0}");
        ImGui.SameLine(400);
        ImGui.Text($"Total Listed: {listings.Sum(x => x.Quantity):N0} (NQ: {countsNQ.Sum():N0}, HQ: {countsHQ.Sum():N0})");
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
    
    private void SaveChartAsPng()
    {
        if (currentData?.RecentHistory == null) return;

        try
        {
            var history = currentData.RecentHistory.OrderBy(h => h.Timestamp).ToList();
            if (!history.Any()) return;

            // Dimensions
            int width = 1200;
            int height = 800;
            int margin = 60;
            int chartWidth = width - (2 * margin);
            int chartHeight = height - (2 * margin);

            using (var image = new Image<Rgba32>(width, height))
            {
                // Fill background
                image.Mutate(x => x.Fill(Color.FromRgb(30, 30, 30)));

                // Calculate scales
                var minTime = history.Min(h => h.Timestamp).Ticks;
                var maxTime = history.Max(h => h.Timestamp).Ticks;
                var timeRange = maxTime - minTime;

                var minPrice = (float)history.Min(h => h.PricePerUnit);
                var maxPrice = (float)history.Max(h => h.PricePerUnit);
                // Add some padding to price range
                minPrice = Math.Max(0, minPrice * 0.9f);
                maxPrice *= 1.1f;
                var priceRange = maxPrice - minPrice;

                if (timeRange <= 0) timeRange = 1;
                if (priceRange <= 0) priceRange = 100;

                // Draw Grid Lines & Axes
                var gridColor = Color.FromRgba(255, 255, 255, 30);
                var axisColor = Color.White;
                
                // Draw Axes
                image.Mutate(ctx => {
                    // Y-Axis
                    var yStart = new PointF(margin, margin);
                    var yEnd = new PointF(margin, height - margin);
                    ctx.DrawLine(axisColor, 2, yStart, yEnd);

                    // X-Axis
                    var xStart = new PointF(margin, height - margin);
                    var xEnd = new PointF(width - margin, height - margin);
                    ctx.DrawLine(axisColor, 2, xStart, xEnd);
                });

                // Plot Data Points
                var points = new List<PointF>();
                foreach (var entry in history)
                {
                    float x = margin + ((float)(entry.Timestamp.Ticks - minTime) / timeRange) * chartWidth;
                    float y = (height - margin) - ((entry.PricePerUnit - minPrice) / priceRange) * chartHeight;
                    points.Add(new PointF(x, y));
                }

                if (points.Count > 1)
                {
                    // Draw Line
                    image.Mutate(x => x.DrawLine(Color.FromRgb(100, 200, 255), 2, points.ToArray()));
                    
                    // Draw Area (optional, simplified)
                    // To do a filled area, we'd need to close the polygon loop down to the x-axis
                }

                // Add Title
                // Note: Text drawing requires fonts, which might be tricky without bundling a font file.
                // For a first pass, we'll skip text or use simple primitives if needed.
                // However, users usually want context.
                // We can try to use a system font if available, but ImageSharp.Drawing needs a Font object.
                // Given the constraints, let's just save the graph lines for now.
                
                // Save
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var sanitizedName = string.Join("_", itemName.Split(Path.GetInvalidFileNameChars()));
                var filePath = Path.Combine(documentsPath, $"Aurum_Chart_{sanitizedName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                
                image.SaveAsPng(filePath);
                
                Plugin.Log.Information($"Chart saved to {filePath}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to save chart as PNG");
        }
    }
    
    private void CheckAndFetchExtendedData()
    {
        // Don't check too frequently (once every 3 seconds max)
        if (isFetchingExtendedData || (DateTime.UtcNow - lastExtendedDataCheck).TotalSeconds < 3)
            return;
            
        lastExtendedDataCheck = DateTime.UtcNow;
        
        if (currentData == null)
            return;
        
        // Check if we have snapshots data
        var hasSnapshots = currentData.HistorySnapshots != null && currentData.HistorySnapshots.Any();
        var hasHistory = currentData.RecentHistory != null && currentData.RecentHistory.Any();
        
        if (!hasHistory)
            return;
        
        // Always try to load snapshots if we don't have them
        if (!hasSnapshots)
        {
            Plugin.Log.Information($"No snapshot data available for {itemName}, fetching...");
            _ = FetchExtendedHistoricalDataAsync();
            return;
        }
        
        // Check data coverage
        var oldestHistory = currentData.RecentHistory?.Any() == true ? currentData.RecentHistory.Min(h => h.Timestamp) : DateTime.UtcNow;
        var oldestSnapshot = currentData.HistorySnapshots?.Any() == true ? currentData.HistorySnapshots.Min(s => s.Timestamp) : DateTime.UtcNow;
        var oldestData = oldestHistory < oldestSnapshot ? oldestHistory : oldestSnapshot;
        var dataAge = DateTime.UtcNow - oldestData;
        
        Plugin.Log.Debug($"Data age for {itemName}: {dataAge.TotalDays:F1} days (history: {currentData.RecentHistory?.Count ?? 0}, snapshots: {currentData.HistorySnapshots?.Count ?? 0})");
        
        // If viewing 90+ days or all time, try to get at least 90 days of data
        if ((selectedTimeRange >= 90 || selectedTimeRange == -1) && dataAge.TotalDays < 85)
        {
            Plugin.Log.Information($"Need more data for {itemName} (only {dataAge.TotalDays:F1} days available)");
            _ = FetchExtendedHistoricalDataAsync();
        }
    }
    
    private async System.Threading.Tasks.Task FetchExtendedHistoricalDataAsync()
    {
        if (isFetchingExtendedData || currentData == null) return;
        
        isFetchingExtendedData = true;
        
        try
        {
            Plugin.Log.Information($"Fetching extended historical data for {itemName} (Item ID: {currentData.ItemId})...");
            
            // Get world ID
            var worldId = plugin.UniversalisService.GetWorldIdByName(currentData.WorldName);
            if (worldId == 0)
            {
                Plugin.Log.Warning($"Could not determine world ID for {currentData.WorldName}");
                return;
            }
            
            Plugin.Log.Information($"Loading snapshots for item {currentData.ItemId} on world {currentData.WorldName} (ID: {worldId})");
            
            // Load all available snapshots from database (up to 1 year)
            var snapshots = await System.Threading.Tasks.Task.Run(() => 
                plugin.DatabaseService?.GetMarketSnapshots((int)currentData.ItemId, worldId, DateTime.UtcNow.AddDays(-365))
            );
            
            if (snapshots != null && snapshots.Any())
            {
                currentData.HistorySnapshots = snapshots;
                var oldest = snapshots.Min(s => s.Timestamp);
                var newest = snapshots.Max(s => s.Timestamp);
                Plugin.Log.Information($"Loaded {snapshots.Count} historical snapshots from cache (range: {oldest:d} to {newest:d})");
            }
            else
            {
                Plugin.Log.Warning($"No historical snapshots found in database for item {currentData.ItemId}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to fetch extended historical data");
        }
        finally
        {
            isFetchingExtendedData = false;
        }
    }
}
