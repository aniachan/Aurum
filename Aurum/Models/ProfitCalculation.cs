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
    
    // Helper property to access ItemId consistently
    public uint ItemId => Recipe?.ResultItemId ?? 0;

    // Market data
    public MarketData? MarketData { get; set; }
    
    // Cost breakdown
    public IngredientTree IngredientTree { get; set; } = null!;
    public uint TotalCraftCost { get; set; }
    
    // Sale price
    public uint ExpectedSalePrice { get; set; }  // What you'd list it for
    public uint VendorPrice { get; set; }        // NPC sell price
    public uint MarketBoardTax { get; set; }     // 5% tax
    public uint NetSalePrice { get; set; }       // After tax
    
    // Profit metrics
    public int RawProfit { get; set; }           // Net sale - cost (can be negative!) - Renamed back to RawProfit due to extensive usage
    public int OpportunityCost { get; set; }     // Market value of ingredients (if sold directly)
    public int ProfitVsMaterials { get; set; }   // Net sale - OpportunityCost
    public float ProfitMargin { get; set; }      // profit / sale_price * 100
    public int GilPerHour { get; set; }          // profit / craft_time
    public int GatheringTimeSeconds { get; set; } // Estimated time to gather materials
    
    // Convenience property for new code preferring "NetProfit"
    public int NetProfit { get => RawProfit; set => RawProfit = value; }
    public float ROI { get; set; }               // profit / cost * 100
    
    // Demand-adjusted scoring
    public int ProfitScore { get; set; }         // 0-100 based on profit alone
    public float EfficiencyScore { get; set; }   // New metric: Profit per resource unit
    public int DemandScore { get; set; }         // 0-100 based on market analysis
    public int RecommendationScore { get; set; } // 0-100 weighted score
    
    // Risk assessment
    public RiskLevel RiskLevel { get; set; }
    public int RiskScore { get; set; }
    public List<MarketWarningInfo> Warnings { get; set; } = new();
    public string RiskAnalysis { get; set; } = string.Empty; // Detailed breakdown
    
    // Recommendations
    public int RecommendedQuantity { get; set; }
    public int MaxSafeQuantity { get; set; }
    public string Recommendation { get; set; } = string.Empty; // Text recommendation
    public float EstimatedSellTimeDays { get; set; }
    
    // Metadata
    public DateTime CalculatedAt { get; set; }
    public CostMode CostMode { get; set; }
    public bool IsDataComplete { get; set; }     // False if missing market data
    public bool IsStale { get; set; }            // True if data is from stale cache

    // Cross-World Analysis
    public string? BestWorldName { get; set; }
    public uint BestWorldPrice { get; set; }
    
    public string? CheapestWorldName { get; set; }
    public uint CheapestWorldPrice { get; set; }
    
    public int CrossWorldTravelCost { get; set; } // Estimated cost to visit the other world
    public int ArbitrageProfit { get; set; } // Potential profit from buying on CheapestWorld and selling on CurrentWorld
}
