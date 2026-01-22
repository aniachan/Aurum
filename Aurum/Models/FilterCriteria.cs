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

    // Item Sources
    public bool IncludeCrafted { get; set; } = true;
    public bool IncludeGathered { get; set; } = true;
    public bool IncludeVendor { get; set; } = true;
    public bool IncludeMarketTradeable { get; set; } = true;
    public bool IncludeUntradeable { get; set; } = false; // Usually not useful for profit analysis, but maybe for gathering?

    // Categories
    public bool IncludeCombatGear { get; set; } = true;
    public bool IncludeCraftingGatheringGear { get; set; } = true;
    public bool IncludeFurniture { get; set; } = true;
    public bool IncludeConsumables { get; set; } = true;
    public bool IncludeMaterials { get; set; } = true;
    public bool OnlyFavorites { get; set; } = false;

    // Job/Level
    public int MinJobLevel { get; set; } = 1;
    public int MaxJobLevel { get; set; } = int.MaxValue;
    public int MinRecipeLevel { get; set; } = 1;
    public int MaxRecipeLevel { get; set; } = int.MaxValue;
    public int MinItemLevel { get; set; } = 1;
    public int MaxItemLevel { get; set; } = int.MaxValue;

    // Equipment Slots
    public HashSet<EquipSlot> IncludedEquipSlots { get; set; } = new()
    {
        EquipSlot.MainHand, EquipSlot.OffHand,
        EquipSlot.Head, EquipSlot.Body, EquipSlot.Hands, EquipSlot.Legs, EquipSlot.Feet,
        EquipSlot.Ears, EquipSlot.Neck, EquipSlot.Wrists, EquipSlot.Ring
    };
    
    // Jobs/Classes (by Abbreviation e.g. "CRP", "PLD")
    // If empty, all jobs are included. If populated, only these jobs are included.
    public HashSet<string> IncludedJobIds { get; set; } = new();

    // Helper properties for UI binding
    public int MinLevel { get => MinJobLevel; set => MinJobLevel = value; }
    public int MaxLevel { get => MaxJobLevel; set => MaxJobLevel = value; }
    public int MinProfit { get => MinProfitAmount; set => MinProfitAmount = value; }
    public float MinRoi { get => MinROI; set => MinROI = value; }
    public float MinVelocity { get => MinSaleVelocity; set => MinSaleVelocity = value; }
    public bool ProfitableOnly { get => MinProfitAmount > 0; set { if (value && MinProfitAmount <= 0) MinProfitAmount = 1; else if (!value && MinProfitAmount > 0) MinProfitAmount = 0; } }
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
