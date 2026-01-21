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
            if (!criteria.IncludeCraftingGatheringGear && item.Recipe.MainCategory == ItemMainCategory.Crafting) return false;
            if (!criteria.IncludeFurniture && item.Recipe.MainCategory == ItemMainCategory.Furniture) return false;
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
