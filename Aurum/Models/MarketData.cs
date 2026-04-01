using System;
using System.Collections.Generic;
using System.Linq;

namespace Aurum.Models;

/// <summary>
/// Represents a single sale from market board history
/// </summary>
public class SaleRecord
{
    public uint ItemId { get; set; }
    public bool IsHQ { get; set; }
    public uint PricePerUnit { get; set; }
    public uint Quantity { get; set; }
    public DateTime Timestamp { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public bool OnMannequin { get; set; }
    public string WorldName { get; set; } = string.Empty;
}

/// <summary>
/// Represents a historical snapshot of market state (not a sale)
/// </summary>
public class MarketSnapshot
{
    public DateTime Timestamp { get; set; }
    public uint MinPrice { get; set; }
    public int ListingCount { get; set; }
}

/// <summary>
/// Represents a current market board listing
/// </summary>
public class MarketListing
{
    public uint ItemId { get; set; }
    public bool IsHQ { get; set; }
    public uint PricePerUnit { get; set; }
    public uint Quantity { get; set; }
    public uint Total { get; set; }
    public string RetainerName { get; set; } = string.Empty;
    public string RetainerCity { get; set; } = string.Empty;
    public DateTime ListingTime { get; set; }
    public uint Materia { get; set; }
    public bool OnMannequin { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public string WorldName { get; set; } = string.Empty;
}

/// <summary>
/// Market risk level classification
/// </summary>
public enum RiskLevel
{
    Low,        // 0-25: Safe bet
    Medium,     // 25-50: Calculated risk
    High,       // 50-75: Caution advised
    VeryHigh    // 75-100: Avoid unless you know what you're doing
}

/// <summary>
/// Comprehensive market analysis data from Universalis API
/// </summary>
public class MarketData
{
    // Basic item info
    public uint ItemId { get; set; }
    public string WorldName { get; set; } = string.Empty;
    public DateTime LastUploadTime { get; set; }
    
    // Current listings
    public List<MarketListing> Listings { get; set; } = new();
    public int CurrentListings { get; set; }
    public uint MinPrice { get; set; }
    public uint MaxPrice { get; set; }
    public uint CurrentAveragePriceNQ { get; set; }
    public uint CurrentAveragePriceHQ { get; set; }
    
    // Recent sales history
    public List<SaleRecord> RecentHistory { get; set; } = new();
    public uint AveragePriceNQ { get; set; }
    public uint AveragePriceHQ { get; set; }
    public uint MinPriceNQ { get; set; }
    public uint MinPriceHQ { get; set; }
    public uint MaxPriceNQ { get; set; }
    public uint MaxPriceHQ { get; set; }
    
    // Computed convenience properties
    public uint CurrentAveragePrice => CurrentAveragePriceHQ > 0 ? CurrentAveragePriceHQ : CurrentAveragePriceNQ;
    public uint CurrentMinPrice => MinPriceHQ > 0 ? MinPriceHQ : MinPriceNQ;
    public uint LastSalePrice => RecentHistory.Any() ? RecentHistory.First().PricePerUnit : 0;
    public int RecentSales => RecentHistory.Count;
    public List<SaleRecord> SaleHistory => RecentHistory; // Alias for compatibility
    
    // Historical Snapshots (for supply/demand over time)
    // These are loaded on demand usually, but we can store them here for chart convenience
    public List<MarketSnapshot> HistorySnapshots { get; set; } = new();

    // Demand metrics (calculated)
    public float SaleVelocity { get; set; }              // sales per day
    public float SupplyDemandRatio { get; set; }         // listings / daily_sales
    public float PriceVolatility { get; set; }           // stddev / avg (0-1)
    public float EstimatedSellTimeDays { get; set; }     // listings / velocity
    public float MarketMomentum { get; set; }            // % change in velocity (-1 to 1)
    
    // Additional metrics for Supply/Demand analysis
    public float SalesPerDay { get; set; }               // Explicit Sales Per Day
    public int ListingsCount { get; set; }               // Explicit Listings Count for history tracking
    public float DemandRatio { get; set; }               // Sales / Listings ratio

    public PriceTrend Trend { get; set; }                // Rising, Falling, Stable, Volatile
    
    // Risk assessment
    public int RiskScore { get; set; }                   // 0-100
    public RiskLevel RiskLevel { get; set; }
    public List<MarketWarningInfo> Warnings { get; set; } = new();
    public string RiskAnalysis { get; set; } = string.Empty; // Detailed breakdown of risk factors
    
    // Recommendation scoring
    public int RecommendationScore { get; set; }         // 0-100 (weighted with demand)
    public int RecommendedQuantity { get; set; }         // Safe amount to craft
    public int MaxSafeQuantity { get; set; }             // Upper limit before oversupply
    public string Recommendation { get; set; } = string.Empty; // Text recommendation
    
    // Competition analysis
    public Dictionary<uint, int> PriceDistribution { get; set; } = new();  // price -> count
    public int YourCompetitorRank { get; set; }          // Where you'd place in listings
    
    // Peak Demand Analysis
    public List<DayOfWeek> BestDaysToSell { get; set; } = new();
    public List<int> BestHoursToSell { get; set; } = new(); // 0-23
    public string PeakDemandAnalysis { get; set; } = string.Empty;

    // Seasonal Analysis
    public bool IsSeasonal { get; set; }
    public string SeasonalTrend { get; set; } = string.Empty;

    // Freshness tracking
    public DateTime CachedAt { get; set; }
    public DateTime? LastSaleTime { get; set; }
    public bool IsStale => (DateTime.UtcNow - CachedAt).TotalMinutes > 5;
    public bool IsCachedData { get; set; } = false; // Flag to indicate if this is cached data returned because API was unreachable
}
