using System;
using System.Collections.Generic;
using System.Linq;
using Aurum.Models;

namespace Aurum.Services;

public static class HousingOpportunityService
{
    public const int DefaultMaxHousingRecipes = 250;

    public static List<RecipeData> SelectHousingRecipes(IEnumerable<RecipeData> recipes, int maxRecipes = DefaultMaxHousingRecipes)
    {
        var limit = Math.Max(1, maxRecipes);

        return recipes
            .Where(IsHousingRecipe)
            .OrderByDescending(r => r.ClassJobLevel)
            .ThenByDescending(r => r.RecipeLevel)
            .ThenBy(r => r.ItemName)
            .Take(limit)
            .ToList();
    }

    public static bool IsHousingRecipe(RecipeData recipe)
    {
        return recipe.MainCategory == ItemMainCategory.Furniture
            || ItemCategoryClassifier.IsHousingItemUiCategory(recipe.ItemCategory);
    }

    public static bool PassesHousingBoomFilter(ProfitCalculation profit)
    {
        return IsHousingRecipe(profit.Recipe)
            && profit.RawProfit > 0
            && profit.IsDataComplete;
    }

    public static float GetHousingBoomScore(ProfitCalculation profit)
    {
        var market = profit.MarketData;
        var saleVelocity = Math.Max(0, market?.SaleVelocity ?? 0);
        var recentSales = Math.Max(0, market?.RecentSales ?? 0);
        var currentListings = Math.Max(0, market?.CurrentListings ?? 0);
        var scarcityMultiplier = currentListings == 0
            ? 2.5f
            : Math.Clamp(8f / currentListings, 0.5f, 2.5f);

        var profitScore = MathF.Log10(Math.Max(0, profit.RawProfit) + 1) * 18f;
        var velocityScore = MathF.Log10(saleVelocity + 1) * 35f;
        var recentSalesScore = MathF.Log10(recentSales + 1) * 12f;
        var recommendationScore = profit.RecommendationScore * 0.45f;
        var riskPenalty = profit.RiskScore * 0.25f;

        return (profitScore + velocityScore + recentSalesScore + recommendationScore) * scarcityMultiplier - riskPenalty;
    }
}
