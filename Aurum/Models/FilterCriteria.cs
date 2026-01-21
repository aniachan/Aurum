using System.Collections.Generic;

namespace Aurum.Models;

/// <summary>
/// Criteria for filtering market items and crafting recipes
/// </summary>
public class FilterCriteria
{
    // Profit Metrics
    public int MinProfitAmount { get; set; } = 0;
    public float MinProfitMargin { get; set; } = 0;
    public float MinROI { get; set; } = 0;
    
    // Demand Metrics
    public float MinSaleVelocity { get; set; } = 0; // Sales per day
    public float MaxSupplyDemandRatio { get; set; } = float.MaxValue;
    public float MaxEstimatedSellTimeDays { get; set; } = float.MaxValue;
    
    // Risk Metrics
    public int MaxRiskScore { get; set; } = 100;
    public HashSet<RiskLevel> AllowedRiskLevels { get; set; } = new() 
    { 
        RiskLevel.Low, 
        RiskLevel.Medium, 
        RiskLevel.High, 
        RiskLevel.VeryHigh 
    };
    
    // Market Health
    public bool ExcludeStaleMarkets { get; set; } = true;
    public bool ExcludeMarketCrashRisks { get; set; } = false;
    public bool OnlyRisingTrends { get; set; } = false;
    
    // Job/Level (Future expansion)
    public int MaxJobLevel { get; set; } = int.MaxValue;
}

public enum SortStrategy
{
    RecommendationScore, // Default smart scoring
    ProfitAmount,
    ProfitMargin,
    ROI,
    SaleVelocity,
    LowRiskFirst
}
