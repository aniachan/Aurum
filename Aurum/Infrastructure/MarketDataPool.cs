using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Aurum.Models;

namespace Aurum.Infrastructure;

/// <summary>
/// Specialized pool for MarketData objects to reduce GC pressure during large batch updates.
/// </summary>
public static class MarketDataPool
{
    private static readonly ObjectPool<MarketData> _pool = new(() => new MarketData());
    
    // We also need to pool the sub-objects to be truly effective
    // However, MarketListing and SaleRecord are often created in bulk, so pooling them individually
    // might add more overhead than savings due to tracking complexity.
    // For now, let's focus on the heavy MarketData container.
    
    /// <summary>
    /// Gets a clear MarketData instance.
    /// </summary>
    public static MarketData Get()
    {
        var data = _pool.Get();
        Reset(data);
        return data;
    }

    /// <summary>
    /// Returns a MarketData instance to the pool.
    /// </summary>
    public static void Return(MarketData data)
    {
        // Don't hold onto massive lists
        data.Listings.Clear();
        data.RecentHistory.Clear();
        data.HistorySnapshots.Clear();
        data.Warnings.Clear();
        data.PriceDistribution.Clear();
        data.BestDaysToSell.Clear();
        data.BestHoursToSell.Clear();
        data.AlternativeSuggestions.Clear();
        
        _pool.Return(data);
    }

    private static void Reset(MarketData data)
    {
        // Reset primitives
        data.ItemId = 0;
        data.WorldName = string.Empty;
        data.LastUploadTime = default;
        
        // Lists are cleared on Return, so we just ensure they are not null
        if (data.Listings == null) data.Listings = new();
        if (data.RecentHistory == null) data.RecentHistory = new();
        if (data.HistorySnapshots == null) data.HistorySnapshots = new();
        if (data.Warnings == null) data.Warnings = new();
        if (data.PriceDistribution == null) data.PriceDistribution = new();
        if (data.BestDaysToSell == null) data.BestDaysToSell = new();
        if (data.BestHoursToSell == null) data.BestHoursToSell = new();
        if (data.AlternativeSuggestions == null) data.AlternativeSuggestions = new();

        data.CurrentListings = 0;
        data.MinPrice = 0;
        data.MaxPrice = 0;
        data.CurrentAveragePriceNQ = 0;
        data.CurrentAveragePriceHQ = 0;
        data.AveragePriceNQ = 0;
        data.AveragePriceHQ = 0;
        data.MinPriceNQ = 0;
        data.MinPriceHQ = 0;
        data.MaxPriceNQ = 0;
        data.MaxPriceHQ = 0;
        
        data.SaleVelocity = 0;
        data.SupplyDemandRatio = 0;
        data.PriceVolatility = 0;
        data.EstimatedSellTimeDays = 0;
        data.MarketMomentum = 0;
        
        data.SalesPerDay = 0;
        data.ListingsCount = 0;
        data.DemandRatio = 0;
        
        data.Trend = PriceTrend.Stable; // Default?
        data.RiskScore = 0;
        data.RiskLevel = RiskLevel.Low;
        data.RiskAnalysis = string.Empty;
        
        data.RecommendationScore = 0;
        data.RecommendedQuantity = 0;
        data.MaxSafeQuantity = 0;
        data.Recommendation = string.Empty;
        
        data.YourCompetitorRank = 0;
        data.PeakDemandAnalysis = string.Empty;
        
        data.CachedAt = default;
        data.LastSaleTime = null;
        data.IsCachedData = false;
    }
}
