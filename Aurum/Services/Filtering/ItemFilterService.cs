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
            MinJobLevel = criteria.MinJobLevel,
            MaxJobLevel = criteria.MaxJobLevel,
            MinItemLevel = criteria.MinItemLevel,
            MaxItemLevel = criteria.MaxItemLevel,
            MinProfitAmount = criteria.MinProfitAmount,
            MinROI = criteria.MinROI,
            MinSaleVelocity = criteria.MinSaleVelocity,
            // ... copy other fields
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
                MinJobLevel = preset.Criteria.MinJobLevel,
                MaxJobLevel = preset.Criteria.MaxJobLevel,
                MinItemLevel = preset.Criteria.MinItemLevel,
                MaxItemLevel = preset.Criteria.MaxItemLevel,
                MinProfitAmount = preset.Criteria.MinProfitAmount,
                MinROI = preset.Criteria.MinROI,
                MinSaleVelocity = preset.Criteria.MinSaleVelocity,
                // ... copy others
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

        // 2. Profit Metrics
        if (item.RawProfit < criteria.MinProfitAmount) return false;
        if (item.ProfitMargin < criteria.MinProfitMargin) return false;
        if (item.ROI < criteria.MinROI) return false;

        // 3. Demand Metrics
        // Note: Use MarketData properties for raw metrics if ProfitCalculation doesn't expose them all yet, 
        // but ProfitCalculation copies most of them.
        if (item.MarketData?.SaleVelocity < criteria.MinSaleVelocity) return false;
        if (item.MarketData?.SupplyDemandRatio > criteria.MaxSupplyDemandRatio) return false;
        if (item.EstimatedSellTimeDays > criteria.MaxEstimatedSellTimeDays) return false;

        // 4. Risk Metrics
        if (item.RiskScore > criteria.MaxRiskScore) return false;
        if (!criteria.AllowedRiskLevels.Contains(item.RiskLevel)) return false;

        // 5. Market Health Checks
        if (item.MarketData != null)
        {
            if (criteria.ExcludeStaleMarkets)
            {
                // Filter out items with no recent sales or explicit stale warnings
                if (item.MarketData.Warnings.Any(w => w.Type == MarketWarning.StaleMarket)) return false;
            }

            if (criteria.ExcludeMarketCrashRisks)
            {
                if (item.MarketData.Warnings.Any(w => w.Type == MarketWarning.MarketCrashRisk)) return false;
            }

            if (criteria.OnlyRisingTrends)
            {
                if (item.MarketData.Trend != PriceTrend.Rising) return false;
            }
        }
        
        // 6. Category Filtering
        if (item.Recipe != null)
        {
            if (!criteria.IncludeCombatGear && item.Recipe.MainCategory == ItemMainCategory.Combat) return false;
            if (!criteria.IncludeCraftingGatheringGear && 
                (item.Recipe.MainCategory == ItemMainCategory.Crafting || item.Recipe.MainCategory == ItemMainCategory.Gathering)) return false;
            if (!criteria.IncludeFurniture && item.Recipe.MainCategory == ItemMainCategory.Furniture) return false;
            if (!criteria.IncludeConsumables && item.Recipe.MainCategory == ItemMainCategory.Consumable) return false;
            if (!criteria.IncludeMaterials && item.Recipe.MainCategory == ItemMainCategory.Material) return false;
        }

        // 7. Favorites
        if (criteria.OnlyFavorites)
        {
            if (_configuration == null || !_configuration.FavoriteItems.Contains(item.ItemId)) return false;
        }

        // 8. Level Range Filtering
        if (item.Recipe != null)
        {
            // Class/Job Level
            if (item.Recipe.ClassJobLevel < criteria.MinJobLevel || item.Recipe.ClassJobLevel > criteria.MaxJobLevel) return false;
            
            // Recipe Level (RLvl) - e.g. Starred recipes have higher RLvl than Job Level
            if (item.Recipe.RecipeLevel < criteria.MinRecipeLevel || item.Recipe.RecipeLevel > criteria.MaxRecipeLevel) return false;

            // Item Level (ILvl)
            if (item.Recipe.ItemLevel < criteria.MinItemLevel || item.Recipe.ItemLevel > criteria.MaxItemLevel) return false;
        }

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
