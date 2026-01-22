using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Aurum.Models;
using Aurum.Utils;

namespace Aurum.Services.Community;

/// <summary>
/// Service to aggregate and sync market trends with community data
/// </summary>
public class CommunitySyncService
{
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly PrivacyService privacyService;
    private readonly MarketAnalysisService analysisService;
    
    // In-memory buffer for trends to avoid excessive DB writes or API calls
    private readonly List<TrendRecord> trendBuffer = new();
    private readonly object bufferLock = new();
    
    // Mock community API endpoint (placeholder for future implementation)
    private const string COMMUNITY_API_URL = "https://api.aurum-app.com/community/v1";

    public CommunitySyncService(
        IPluginLog log, 
        Configuration config, 
        PrivacyService privacyService,
        MarketAnalysisService analysisService)
    {
        this.log = log;
        this.config = config;
        this.privacyService = privacyService;
        this.analysisService = analysisService;
    }

    /// <summary>
    /// Represents a single trend data point for aggregation
    /// </summary>
    public class TrendRecord
    {
        public uint ItemId { get; set; }
        public string WorldName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public float PriceVolatility { get; set; }
        public float SaleVelocity { get; set; }
        public float SupplyDemandRatio { get; set; }
        public PriceTrend TrendDirection { get; set; }
        public int RiskScore { get; set; }
    }

    /// <summary>
    /// Record a market trend from a fresh analysis
    /// </summary>
    public void RecordTrend(MarketData marketData)
    {
        // Respect user privacy - only record if sync is allowed
        if (!config.AllowCommunityDataSync) return;
        
        // Skip if data is stale or invalid
        if (marketData.IsCachedData || marketData.RecentHistory.Count == 0) return;

        var record = new TrendRecord
        {
            ItemId = marketData.ItemId,
            WorldName = marketData.WorldName,
            Timestamp = DateTime.UtcNow,
            PriceVolatility = marketData.PriceVolatility,
            SaleVelocity = marketData.SaleVelocity,
            SupplyDemandRatio = marketData.SupplyDemandRatio,
            TrendDirection = marketData.Trend,
            RiskScore = marketData.RiskScore
        };

        lock (bufferLock)
        {
            trendBuffer.Add(record);
            
            // If buffer gets too large, trigger processing (in a real app, this would push to API)
            if (trendBuffer.Count >= 100)
            {
                ProcessBuffer();
            }
        }
    }

    // Local aggregation state
    private LocalMarketStats currentStats = new();
    public LocalMarketStats CurrentStats => currentStats;

    /// <summary>
    /// Process buffered trends (locally aggregate for now)
    /// </summary>
    public void ProcessBuffer()
    {
        List<TrendRecord> batch;
        lock (bufferLock)
        {
            if (!trendBuffer.Any()) return;
            batch = new List<TrendRecord>(trendBuffer);
            trendBuffer.Clear();
        }

        Task.Run(async () => 
        {
            try
            {
                // In the future, this will POST to the community API
                // await UploadTrendsAsync(batch);
                
                // Update local aggregation
                UpdateLocalStats(batch);
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to process community trend buffer");
            }
        });
    }

    /// <summary>
    /// Updates the local market statistics with the new batch of data
    /// </summary>
    private void UpdateLocalStats(List<TrendRecord> records)
    {
        var stats = new LocalMarketStats
        {
            LastUpdated = DateTime.UtcNow,
            TotalItemsAnalyzed = records.Count
        };

        // 1. Trend Distribution
        stats.TrendDistribution = records
            .GroupBy(r => r.TrendDirection)
            .ToDictionary(g => g.Key, g => g.Count());

        // 2. Average Volatility
        stats.AverageVolatility = records.Any() ? records.Average(r => r.PriceVolatility) : 0;

        // 3. Highlights - High Volatility
        stats.TopVolatileItems = records
            .OrderByDescending(r => r.PriceVolatility)
            .Take(5)
            .Select(r => new MarketHighlight 
            { 
                ItemId = r.ItemId, 
                WorldName = r.WorldName, 
                Value = r.PriceVolatility, 
                Label = $"{r.PriceVolatility:P0} volatility" 
            })
            .ToList();

        // 4. Highlights - High Demand (Velocity > 1 + Undersupplied)
        stats.TopDemandItems = records
            .Where(r => r.SaleVelocity > 1.0f && r.SupplyDemandRatio < 1.0f)
            .OrderByDescending(r => r.SaleVelocity)
            .Take(5)
            .Select(r => new MarketHighlight 
            { 
                ItemId = r.ItemId, 
                WorldName = r.WorldName, 
                Value = r.SaleVelocity, 
                Label = $"{r.SaleVelocity:F1} sales/day" 
            })
            .ToList();

        // Atomic update of the exposed stats
        this.currentStats = stats;
        
        log.Debug($"Market Stats Updated: {stats.TotalItemsAnalyzed} items processed. Avg Volatility: {stats.AverageVolatility:P1}");
    }

    /// <summary>
    /// Get aggregated community insights (mock implementation)
    /// </summary>
    public async Task<string> GetCommunityInsightsAsync(uint itemId)
    {
        if (!config.AllowCommunityDataSync) return "Community sync disabled.";

        // This would fetch from the API. For now, return a placeholder.
        return "Community data not yet available (Server unavailable).";
    }
}
