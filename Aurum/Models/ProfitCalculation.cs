using System;
using System.Collections.Generic;

namespace Aurum.Models;

/// <summary>
/// Cost calculation modes
/// </summary>
public enum CostMode
{
    MarketBoard,    // Use current MB prices
    Vendor,         // Use vendor prices where available
    Cheapest,       // Use whichever is cheaper (MB vs Vendor)
    SelfGathered    // Assume zero cost for gatherable items
}

/// <summary>
/// Represents the calculated cost breakdown for an ingredient
/// </summary>
public class IngredientCost
{
    public uint ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public uint UnitCost { get; set; }
    public uint TotalCost { get; set; }
    public bool IsHQ { get; set; }
    public CostSource Source { get; set; }
    
    // If this is a sub-recipe, track its breakdown
    public List<IngredientCost>? SubIngredients { get; set; }
}

public enum CostSource
{
    MarketBoard,
    Vendor,
    SelfGathered,
    SubRecipe,
    Unknown
}

/// <summary>
/// Complete ingredient tree with costs resolved
/// </summary>
public class IngredientTree
{
    public uint RootRecipeId { get; set; }
    public uint ResultItemId { get; set; }
    public List<IngredientCost> FlatIngredientList { get; set; } = new();  // All ingredients, flattened
    public List<IngredientCost> RootIngredients { get; set; } = new();     // Top-level with sub-trees
    public uint TotalCost { get; set; }
    public CostMode CalculationMode { get; set; }
}

/// <summary>
/// Complete profit calculation result for a recipe
/// </summary>
public class ProfitCalculation
{
    // Recipe info
    public RecipeData Recipe { get; set; } = null!;
    
    // Market data
    public MarketData? MarketData { get; set; }
    
    // Cost breakdown
    public IngredientTree IngredientTree { get; set; } = null!;
    public uint TotalCraftCost { get; set; }
    
    // Sale price
    public uint ExpectedSalePrice { get; set; }  // What you'd list it for
    public uint MarketBoardTax { get; set; }     // 5% tax
    public uint NetSalePrice { get; set; }       // After tax
    
    // Profit metrics
    public int RawProfit { get; set; }           // Net sale - cost (can be negative!) - Renamed back to RawProfit due to extensive usage
    public float ProfitMargin { get; set; }      // profit / sale_price * 100
    public int GilPerHour { get; set; }          // profit / craft_time
    
    // Convenience property for new code preferring "NetProfit"
    public int NetProfit { get => RawProfit; set => RawProfit = value; }
    public float ROI { get; set; }               // profit / cost * 100
    
    // Demand-adjusted scoring
    public int ProfitScore { get; set; }         // 0-100 based on profit alone
    public int DemandScore { get; set; }         // 0-100 based on market analysis
    public int RecommendationScore { get; set; } // 0-100 weighted score
    
    // Risk assessment
    public RiskLevel RiskLevel { get; set; }
    public int RiskScore { get; set; }
    public List<MarketWarningInfo> Warnings { get; set; } = new();
    
    // Recommendations
    public int RecommendedQuantity { get; set; }
    public int MaxSafeQuantity { get; set; }
    public float EstimatedSellTimeDays { get; set; }
    
    // Metadata
    public DateTime CalculatedAt { get; set; }
    public CostMode CostMode { get; set; }
    public bool IsDataComplete { get; set; }     // False if missing market data
}
