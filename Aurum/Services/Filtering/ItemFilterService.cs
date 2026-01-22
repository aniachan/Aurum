using System;
using System.Collections.Generic;
using System.Linq;
using Aurum.Models;

namespace Aurum.Services.Filtering;

/// <summary>
/// Service for filtering and sorting item opportunities based on complex criteria
/// </summary>
public class ItemFilterService
{
    private readonly Configuration _configuration;
    
    // Default criteria state
    public FilterCriteria CurrentCriteria { get; private set; } = new FilterCriteria();

    public ItemFilterService(Configuration configuration)
    {
        _configuration = configuration;
    }

    public void Reset()
    {
        CurrentCriteria = new FilterCriteria();
    }
    
    // Preset Management - In-memory for now, could be persisted to Configuration later
    private Dictionary<string, (string Name, FilterCriteria Criteria)> _presets = new();

    public void SavePreset(string name, FilterCriteria criteria)
    {
        string id = Guid.NewGuid().ToString("N");
        // Deep copy needed if FilterCriteria were complex reference type, but properties are mostly value types or simple.
        // For safety, let's just serialize/deserialize or manual copy if needed.
        // For now, assuming direct copy is fine or shallow copy.
        // Actually, let's do a manual memberwise clone approach via JSON or similar if needed.
        // For this prototype, we'll just store the reference, which is risky if UI mutates it.
        // Better:
        var copy = new FilterCriteria 
        {
            NameSearch = criteria.NameSearch,
            MinJobLevel = criteria.MinJobLevel,
            MaxJobLevel = criteria.MaxJobLevel,
            MinItemLevel = criteria.MinItemLevel,
            MaxItemLevel = criteria.MaxItemLevel,
            MinProfitAmount = criteria.MinProfitAmount,
            MinROI = criteria.MinROI,
            MinSaleVelocity = criteria.MinSaleVelocity,
            IncludedEquipSlots = new HashSet<EquipSlot>(criteria.IncludedEquipSlots),
            IncludedJobIds = new HashSet<string>(criteria.IncludedJobIds),
            
            // Additional properties
            IsDyeableOnly = criteria.IsDyeableOnly,
            IsCollectableOnly = criteria.IsCollectableOnly,
            MinMateriaSlots = criteria.MinMateriaSlots,
            MinRarity = criteria.MinRarity,
            MaxRarity = criteria.MaxRarity,
            ExcludeUnique = criteria.ExcludeUnique,
            ExcludeUntradable = criteria.ExcludeUntradable
        };
        _presets[id] = (name, copy);
    }

    public List<(string Id, string Name, FilterCriteria Criteria)> GetPresets()
    {
        return _presets.Select(kv => (kv.Key, kv.Value.Name, kv.Value.Criteria)).ToList();
    }

    public void LoadPreset(string id)
    {
        if (_presets.TryGetValue(id, out var preset))
        {
            // Clone back to CurrentCriteria
            CurrentCriteria = new FilterCriteria
            {
                NameSearch = preset.Criteria.NameSearch,
                MinJobLevel = preset.Criteria.MinJobLevel,
                MaxJobLevel = preset.Criteria.MaxJobLevel,
                MinItemLevel = preset.Criteria.MinItemLevel,
                MaxItemLevel = preset.Criteria.MaxItemLevel,
                MinProfitAmount = preset.Criteria.MinProfitAmount,
                MinROI = preset.Criteria.MinROI,
                MinSaleVelocity = preset.Criteria.MinSaleVelocity,
                IncludedEquipSlots = new HashSet<EquipSlot>(preset.Criteria.IncludedEquipSlots),
                IncludedJobIds = new HashSet<string>(preset.Criteria.IncludedJobIds),
                
                // Additional properties
                IsDyeableOnly = preset.Criteria.IsDyeableOnly,
                IsCollectableOnly = preset.Criteria.IsCollectableOnly,
                MinMateriaSlots = preset.Criteria.MinMateriaSlots,
                MinRarity = preset.Criteria.MinRarity,
                MaxRarity = preset.Criteria.MaxRarity,
                ExcludeUnique = preset.Criteria.ExcludeUnique,
                ExcludeUntradable = preset.Criteria.ExcludeUntradable
            };
        }
    }

    public void DeletePreset(string id)
    {
        _presets.Remove(id);
    }

    /// <summary>
    /// Filter a list of profit calculations based on criteria
    /// </summary>
    public List<ProfitCalculation> FilterItems(IEnumerable<ProfitCalculation> items, FilterCriteria criteria)
    {
        return items.Where(item => PassesFilter(item, criteria)).ToList();
    }

    /// <summary>
    /// Check if a single item passes the filter criteria
    /// </summary>
    public bool PassesFilter(ProfitCalculation item, FilterCriteria criteria)
    {
        // 1. Data Integrity Check
        if (!item.IsDataComplete) return false;

        // 1.5 Name Filter - Check on Recipe
        if (item.Recipe != null && !PassesRecipeFilter(item.Recipe, criteria))
             return false;

        // 2. Profit Metrics
        if (item.RawProfit < criteria.MinProfitAmount) return false;
        if (item.ProfitMargin < criteria.MinProfitMargin) return false;
        if (item.ROI < criteria.MinROI) return false;

        // 3. Demand Metrics & Market Health (MarketData checks)
        if (item.MarketData != null)
        {
            if (!PassesMarketFilter(item.MarketData, criteria))
                return false;
        }

        // 4. Risk Metrics
        if (item.RiskScore > criteria.MaxRiskScore) return false;
        if (!criteria.AllowedRiskLevels.Contains(item.RiskLevel)) return false;
        
        // 7. Favorites
        if (criteria.OnlyFavorites)
        {
            if (_configuration == null || !_configuration.FavoriteItems.Contains(item.ItemId)) return false;
        }

        return true;
    }

    /// <summary>
    /// Check if a recipe matches the structural criteria (Level, Category, Job, etc.)
    /// This can be used to pre-filter recipes before fetching market data.
    /// </summary>
    public bool PassesRecipeFilter(RecipeData recipe, FilterCriteria criteria)
    {
        // Name Filter
        if (!string.IsNullOrWhiteSpace(criteria.NameSearch))
        {
            if (string.IsNullOrEmpty(recipe.ItemName)) return false;
            if (!recipe.ItemName.Contains(criteria.NameSearch, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        
        // Category Filtering
        if (!criteria.IncludeCombatGear && recipe.MainCategory == ItemMainCategory.Combat) return false;
        if (!criteria.IncludeCraftingGatheringGear && 
            (recipe.MainCategory == ItemMainCategory.Crafting || recipe.MainCategory == ItemMainCategory.Gathering)) return false;
        if (!criteria.IncludeFurniture && recipe.MainCategory == ItemMainCategory.Furniture) return false;
        if (!criteria.IncludeConsumables && recipe.MainCategory == ItemMainCategory.Consumable) return false;
        if (!criteria.IncludeMaterials && recipe.MainCategory == ItemMainCategory.Material) return false;
        
        // Additional Property Filtering
        if (criteria.IsDyeableOnly && !recipe.IsDyeable) return false;
        if (criteria.IsCollectableOnly && !recipe.IsCollectable) return false;
        if (recipe.MateriaSlotCount < criteria.MinMateriaSlots) return false;
        if (recipe.Rarity < criteria.MinRarity || recipe.Rarity > criteria.MaxRarity) return false;
        if (criteria.ExcludeUnique && recipe.IsUnique) return false;
        if (criteria.ExcludeUntradable && recipe.IsUntradable) return false;

        // Item Source Filtering (Crafted)
        // Since we are checking a Recipe, it IS crafted.
        if (!criteria.IncludeCrafted) return false;

        // Level Range Filtering
        // Class/Job Level
        if (recipe.ClassJobLevel < criteria.MinJobLevel || recipe.ClassJobLevel > criteria.MaxJobLevel) return false;
        
        // Recipe Level (RLvl)
        if (recipe.RecipeLevel < criteria.MinRecipeLevel || recipe.RecipeLevel > criteria.MaxRecipeLevel) return false;

        // Item Level (ILvl)
        if (recipe.ItemLevel < criteria.MinItemLevel || recipe.ItemLevel > criteria.MaxItemLevel) return false;
        
        // Equipment Slot Filtering
        if (recipe.EquipSlot != EquipSlot.None)
        {
            if (!criteria.IncludedEquipSlots.Contains(recipe.EquipSlot)) return false;
        }
        
        // Job/Class Filtering
        if (criteria.IncludedJobIds.Count > 0)
        {
            if (!criteria.IncludedJobIds.Contains(recipe.CraftingClassName)) return false;
        }

        return true;
    }

    /// <summary>
    /// Check if market data matches criteria (Velocity, Supply, Risk, etc.)
    /// </summary>
    public bool PassesMarketFilter(MarketData marketData, FilterCriteria criteria)
    {
        // Demand Metrics
        if (marketData.SaleVelocity < criteria.MinSaleVelocity) return false;
        if (marketData.SupplyDemandRatio > criteria.MaxSupplyDemandRatio) return false;
        // EstimatedSellTimeDays is calculated on ProfitCalculation usually, but might be derivable from MarketData
        // For now, we leave SellTime check in main PassesFilter as it uses ProfitCalculation property
        
        // Market Tradeability (Availability on market)
        if (!criteria.IncludeMarketTradeable) return false; // If we exclude tradeable, and this HAS market data, it's tradeable?

        // 5. Market Health Checks
        if (criteria.ExcludeStaleMarkets)
        {
            if (marketData.Warnings.Any(w => w.Type == MarketWarning.StaleMarket)) return false;
        }

        if (criteria.ExcludeMarketCrashRisks)
        {
            if (marketData.Warnings.Any(w => w.Type == MarketWarning.MarketCrashRisk)) return false;
        }

        if (criteria.OnlyRisingTrends)
        {
            if (marketData.Trend != PriceTrend.Rising) return false;
        }
        
        // Risk (Calculated on ProfitCalculation usually, but let's check what we can)
        // RiskScore is on ProfitCalculation.
        
        return true;
    }


    /// <summary>
    /// Sort items based on the selected strategy
    /// </summary>
    public List<ProfitCalculation> SortItems(IEnumerable<ProfitCalculation> items, SortStrategy strategy)
    {
        var query = items.AsQueryable();

        query = strategy switch
        {
            SortStrategy.RecommendationScore => query.OrderByDescending(i => i.RecommendationScore),
            SortStrategy.ProfitAmount => query.OrderByDescending(i => i.RawProfit),
            SortStrategy.ProfitMargin => query.OrderByDescending(i => i.ProfitMargin),
            SortStrategy.ROI => query.OrderByDescending(i => i.ROI),
            SortStrategy.SaleVelocity => query.OrderByDescending(i => i.MarketData != null ? i.MarketData.SaleVelocity : 0),
            SortStrategy.LowRiskFirst => query.OrderBy(i => i.RiskScore),
            _ => query.OrderByDescending(i => i.RecommendationScore)
        };

        return query.ToList();
    }
    
    /// <summary>
    /// Apply both filtering and sorting
    /// </summary>
    public List<ProfitCalculation> FilterAndSort(IEnumerable<ProfitCalculation> items, FilterCriteria criteria, SortStrategy strategy)
    {
        var filtered = FilterItems(items, criteria);
        return SortItems(filtered, strategy);
    }
}
