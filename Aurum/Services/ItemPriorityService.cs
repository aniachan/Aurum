using System;
using System.Collections.Generic;
using System.Linq;
using Aurum.Models;
using Dalamud.Plugin.Services;

namespace Aurum.Services;

/// <summary>
/// Service responsible for calculating item priority scores to determine 
/// which items should be updated most frequently.
/// </summary>
public class ItemPriorityService
{
    private readonly IPluginLog log;
    private readonly Configuration configuration;
    
    // Weight constants - now using config
    // private const float WEIGHT_RECIPE_LEVEL = 0.3f;
    // private const float WEIGHT_MARKET_VELOCITY = 0.3f;
    // private const float WEIGHT_PROFIT_POTENTIAL = 0.2f;
    // private const float WEIGHT_CATEGORY = 0.1f;
    // private const float WEIGHT_USER_PREFERENCE = 0.1f;

    // Level thresholds (Endwalker)
    private const int CURRENT_MAX_LEVEL = 90;
    private const int RELEVANT_RECIPE_LEVEL_MIN = 500; // EW crafted gear starts around here

    public ItemPriorityService(IPluginLog log, Configuration configuration)
    {
        this.log = log;
        this.configuration = configuration;
    }

    /// <summary>
    /// Calculates a priority score (0-100) for a recipe based on static data and last known market data.
    /// </summary>
    public int CalculatePriority(RecipeData recipe, MarketData? lastKnownMarketData)
    {
        float score = 0;

        // 1. Static Heuristics (based on recipe data only)
        score += ScoreRecipeLevel(recipe) * configuration.WeightRecipeLevel;
        score += ScoreCategory(recipe) * configuration.WeightCategory;
        score += ScoreUserPreference(recipe) * configuration.WeightUserPreference;

        // 2. Dynamic Heuristics (based on market data if available)
        if (lastKnownMarketData != null)
        {
            score += ScoreMarketVelocity(lastKnownMarketData) * configuration.WeightMarketVelocity;
            score += ScoreProfitPotential(lastKnownMarketData) * configuration.WeightProfitPotential;
        }
        else
        {
            // If no market data, boost score slightly for high-level items to encourage initial fetch
            if (IsCurrentExpansion(recipe))
            {
                score += 20; 
            }
        }

        // Clamp to 0-100
        return Math.Clamp((int)score, 0, 100);
    }

    private bool IsCurrentExpansion(RecipeData recipe)
    {
        return recipe.ClassJobLevel >= 80; // Endwalker + Shadowbringers late game
    }

    private float ScoreRecipeLevel(RecipeData recipe)
    {
        // Max score for current cap items
        if (recipe.ClassJobLevel >= CURRENT_MAX_LEVEL)
            return 100f;
        
        // High score for leveling gear (80-89)
        if (recipe.ClassJobLevel >= 80)
            return 80f;

        // Medium score for previous expansion (70-79)
        if (recipe.ClassJobLevel >= 70)
            return 40f;

        // Low score for old content
        return 10f;
    }

    private float ScoreCategory(RecipeData recipe)
    {
        // TODO: Refine categories with specific IDs if available
        // For now, we might rely on UI category names if we had them mapped, 
        // but RecipeData mainly has raw IDs. 
        // We can infer some things from the RecipeLevelTable or ItemCategory if we mapped them.
        
        // Placeholder logic:
        // Expert recipes often mean endgame gear/food -> High priority
        if (recipe.IsExpert) return 100f;
        
        return 50f; // Default neutral score
    }

    private float ScoreUserPreference(RecipeData recipe)
    {
        if (configuration.RecentSearches.Any(term => recipe.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return 100f;
        }
        return 0f;
    }

    private float ScoreMarketVelocity(MarketData marketData)
    {
        // Velocity is items sold per day
        // > 10/day = Very High (100)
        // > 5/day = High (80)
        // > 1/day = Medium (50)
        // < 1/day = Low (10)
        
        if (marketData.SaleVelocity >= 10) return 100f;
        if (marketData.SaleVelocity >= 5) return 80f;
        if (marketData.SaleVelocity >= 1) return 50f;
        if (marketData.SaleVelocity > 0.1) return 20f;
        
        return 0f; // Dead item
    }

    private float ScoreProfitPotential(MarketData marketData)
    {
        // Higher price *usually* means higher potential profit (gross), though not always net.
        // We assume items selling for 100k+ are worth watching more than items selling for 100g.
        
        long avgPrice = marketData.CurrentAveragePrice;

        if (avgPrice >= 100000) return 100f;
        if (avgPrice >= 50000) return 80f;
        if (avgPrice >= 10000) return 60f;
        if (avgPrice >= 1000) return 40f;
        
        return 10f;
    }

    /// <summary>
    /// Returns true if the item is worth refreshing based on its last update time and priority.
    /// Higher priority items have shorter refresh intervals.
    /// </summary>
    public bool ShouldRefresh(int priorityScore, DateTime lastUpdateUtc)
    {
        TimeSpan age = DateTime.UtcNow - lastUpdateUtc;

        // High priority (80-100): Refresh every 30 mins
        if (priorityScore >= 80) return age.TotalMinutes >= 30;

        // Medium priority (50-79): Refresh every 2 hours
        if (priorityScore >= 50) return age.TotalHours >= 2;

        // Low priority (20-49): Refresh every 6 hours
        if (priorityScore >= 20) return age.TotalHours >= 6;

        // Very low priority (<20): Refresh every 24 hours
        return age.TotalHours >= 24;
    }

    /// <summary>
    /// Sorts a list of recipes by their priority score in descending order, optionally limiting the result.
    /// </summary>
    /// <param name="recipes">The list of recipes to sort.</param>
    /// <param name="getMarketData">A function to retrieve the last known market data for a recipe (can return null).</param>
    /// <param name="limit">Optional limit to return only the top N items. If 0 or less, returns all.</param>
    /// <returns>A new list of recipes sorted by priority.</returns>
    public List<RecipeData> SortRecipesByPriority(IEnumerable<RecipeData> recipes, Func<RecipeData, MarketData?> getMarketData, int limit = 0)
    {
        var query = recipes
            .Select(r => new { Recipe = r, Score = CalculatePriority(r, getMarketData(r)) })
            .OrderByDescending(x => x.Score)
            .Select(x => x.Recipe);

        if (limit > 0)
        {
            query = query.Take(limit);
        }

        return query.ToList();
    }
}
