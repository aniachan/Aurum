using System.Collections.Generic;
using System.Linq;
using Aurum.Models;
using Aurum.Services;
using Xunit;

namespace Aurum.Tests;

public class HousingOpportunityServiceTests
{
    [Fact]
    public void IsHousingRecipe_UsesItemUiCategoryAsFallback()
    {
        var recipe = new RecipeData
        {
            ItemName = "Outdoor Bench",
            ItemCategory = 76,
            MainCategory = ItemMainCategory.Other
        };

        Assert.True(HousingOpportunityService.IsHousingRecipe(recipe));
    }

    [Fact]
    public void PassesHousingBoomFilter_RequiresHousingProfitAndCompleteData()
    {
        Assert.True(HousingOpportunityService.PassesHousingBoomFilter(CreateProfit(ItemMainCategory.Furniture, 10_000, true)));
        Assert.False(HousingOpportunityService.PassesHousingBoomFilter(CreateProfit(ItemMainCategory.Material, 10_000, true)));
        Assert.False(HousingOpportunityService.PassesHousingBoomFilter(CreateProfit(ItemMainCategory.Furniture, 0, true)));
        Assert.False(HousingOpportunityService.PassesHousingBoomFilter(CreateProfit(ItemMainCategory.Furniture, 10_000, false)));
    }

    [Fact]
    public void HousingBoomRanking_PrefersFastSellingLowCompetitionFurniture()
    {
        var slowCrowded = CreateProfit(ItemMainCategory.Furniture, 25_000, true, saleVelocity: 0.2f, currentListings: 40, recentSales: 1);
        var fastScarce = CreateProfit(ItemMainCategory.Furniture, 15_000, true, saleVelocity: 8f, currentListings: 2, recentSales: 20);

        var ranked = new[] { slowCrowded, fastScarce }
            .OrderByDescending(HousingOpportunityService.GetHousingBoomScore)
            .ToList();

        Assert.Same(fastScarce, ranked[0]);
    }

    private static ProfitCalculation CreateProfit(
        ItemMainCategory category,
        int rawProfit,
        bool isDataComplete,
        float saleVelocity = 1,
        int currentListings = 5,
        int recentSales = 5)
    {
        return new ProfitCalculation
        {
            Recipe = new RecipeData
            {
                ItemName = "Test Item",
                MainCategory = category,
                ItemCategory = category == ItemMainCategory.Furniture ? 57u : 45u
            },
            RawProfit = rawProfit,
            IsDataComplete = isDataComplete,
            RecommendationScore = 50,
            RiskScore = 25,
            MarketData = new MarketData
            {
                SaleVelocity = saleVelocity,
                CurrentListings = currentListings,
                RecentHistory = Enumerable.Range(0, recentSales).Select(i => new SaleRecord()).ToList()
            }
        };
    }
}
