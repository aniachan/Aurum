using System;
using System.Collections.Generic;
using System.Linq;
using Aurum.Models;
using Aurum.Services.Filtering;
using Aurum.Windows;
using Xunit;

namespace Aurum.IntegrationTests.UI;

/// <summary>
/// Integration tests for the Advanced Filter Window functionality
/// </summary>
public class FilterWindowIntegrationTests
{
    private readonly ItemFilterService _filterService;
    private readonly Configuration _configuration;



    public class MockConfiguration : Configuration
    {
        public new void Save()
        {
            // Do nothing for tests, or simulate in-memory
        }
    }

    public FilterWindowIntegrationTests()
    {
        _configuration = new MockConfiguration();
        _filterService = new ItemFilterService(_configuration);
    }

    private RecipeData CreateMockRecipe(string name, int itemLevel, int recipeLevel, string job, EquipSlot slot, ItemMainCategory category)
    {
        return new RecipeData
        {
            ItemName = name,
            ItemLevel = itemLevel,
            RecipeLevel = recipeLevel,
            CraftingClassName = job,
            EquipSlot = slot,
            MainCategory = category,
            IsDyeable = true,
            IsCollectable = false,
            MateriaSlotCount = 2,
            Rarity = 1
        };
    }

    private ProfitCalculation CreateMockProfitItem(RecipeData recipe, int profit, float margin, float velocity)
    {
        return new ProfitCalculation
        {
            Recipe = recipe,
            IsDataComplete = true,
            RawProfit = profit,
            ProfitMargin = margin,
            ROI = margin,
            MarketData = new MarketData
            {
                SaleVelocity = velocity,
                Trend = PriceTrend.Stable
            }
        };
    }

    [Fact]
    public void FilterPreset_SaveAndLoad_ShouldPreserveCriteria()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            NameSearch = "TestItem",
            MinLevel = 50,
            MaxLevel = 90,
            MinProfitAmount = 50000,
            MinRoi = 25.5f,
            IncludedJobIds = new HashSet<string> { "BSM", "CRP" },
            IncludeCrafted = true,
            IncludeCombatGear = false
        };

        // Act
        _filterService.SavePreset("IntegrationTestPreset", criteria);
        
        // Assert: Verify saved to config
        var presets = _filterService.GetPresets();
        Assert.Contains(presets, p => p.Name == "IntegrationTestPreset");
        
        var savedPreset = presets.First(p => p.Name == "IntegrationTestPreset");
        
        // Act: Load
        _filterService.Reset(); // Clear current
        _filterService.LoadPreset(savedPreset.Id);
        
        // Assert: Verify loaded values
        Assert.Equal("TestItem", _filterService.CurrentCriteria.NameSearch);
        Assert.Equal(50, _filterService.CurrentCriteria.MinLevel);
        Assert.Equal(90, _filterService.CurrentCriteria.MaxLevel);
        Assert.Equal(50000, _filterService.CurrentCriteria.MinProfitAmount);
        Assert.Equal(25.5f, _filterService.CurrentCriteria.MinRoi);
        Assert.Contains("BSM", _filterService.CurrentCriteria.IncludedJobIds);
        Assert.Contains("CRP", _filterService.CurrentCriteria.IncludedJobIds);
        Assert.False(_filterService.CurrentCriteria.IncludeCombatGear);
    }

    [Fact]
    public void FilterCombination_ShouldFilterCorrectly()
    {
        // Arrange
        var recipe1 = CreateMockRecipe("Iron Sword", 20, 20, "BSM", EquipSlot.MainHand, ItemMainCategory.Combat);
        var item1 = CreateMockProfitItem(recipe1, 1000, 10, 5); // Low profit, combat

        var recipe2 = CreateMockRecipe("Cotton Tunic", 25, 25, "WVR", EquipSlot.Body, ItemMainCategory.Crafting);
        var item2 = CreateMockProfitItem(recipe2, 50000, 50, 10); // High profit, crafting gear

        var recipe3 = CreateMockRecipe("Mythril Ingot", 40, 40, "BSM", EquipSlot.None, ItemMainCategory.Material);
        var item3 = CreateMockProfitItem(recipe3, 2000, 15, 20); // Med profit, material

        var items = new List<ProfitCalculation> { item1, item2, item3 };

        // Act 1: Filter by Profit only
        var criteriaProfit = new FilterCriteria { MinProfitAmount = 10000 };
        var resultsProfit = _filterService.FilterItems(items, criteriaProfit);
        Assert.Single(resultsProfit);
        Assert.Equal("Cotton Tunic", resultsProfit[0].Recipe?.ItemName);

        // Act 2: Filter by Job (BSM)
        var criteriaJob = new FilterCriteria { IncludedJobIds = new HashSet<string> { "BSM" } };
        var resultsJob = _filterService.FilterItems(items, criteriaJob);
        Assert.Equal(2, resultsJob.Count); // Sword and Ingot
        Assert.Contains(resultsJob, i => i.Recipe?.ItemName == "Iron Sword");
        Assert.Contains(resultsJob, i => i.Recipe?.ItemName == "Mythril Ingot");

        // Act 3: Filter by Category (Combat)
        var criteriaCombat = new FilterCriteria { IncludeCombatGear = true, IncludeCraftingGatheringGear = false, IncludeMaterials = false };
        // Note: Default criteria usually enables all categories, so we must explicitly disable others or ensure logic handles "only this"
        // Let's check default state of criteria:
        // Usually boolean flags for categories are inclusive. If we want ONLY combat, we might need to disable others if they default to true?
        // Let's assume the filter logic is "PassesRecipeFilter" checks "if (!IncludeCombat && isCombat) fail".
        // So if we want ONLY combat, we turn OFF others.
        
        criteriaCombat.IncludeCombatGear = true;
        criteriaCombat.IncludeCraftingGatheringGear = false;
        criteriaCombat.IncludeMaterials = false;
        criteriaCombat.IncludeFurniture = false;
        criteriaCombat.IncludeConsumables = false;
        
        var resultsCombat = _filterService.FilterItems(items, criteriaCombat);
        Assert.Single(resultsCombat);
        Assert.Equal("Iron Sword", resultsCombat[0].Recipe?.ItemName);
    }
    
    [Fact]
    public void FilterPreset_Delete_ShouldRemovePreset()
    {
        // Arrange
        var criteria = new FilterCriteria { NameSearch = "ToDelete" };
        _filterService.SavePreset("DeleteMe", criteria);
        var presets = _filterService.GetPresets();
        var id = presets.First(p => p.Name == "DeleteMe").Id;
        
        // Act
        _filterService.DeletePreset(id);
        
        // Assert
        Assert.DoesNotContain(_filterService.GetPresets(), p => p.Id == id);
    }

    [Fact]
    public void ComplexFilter_UniversalisIntegration_Simulation()
    {
        // Simulate the flow where FilterWindow settings affect Universalis data processing
        // Since we can't easily mock the full UniversalisService with HTTP here, 
        // we verify that the FilterService correctly handles the criteria that would be used by Universalis.
        
        // Arrange: A "high velocity" filter
        var criteria = new FilterCriteria 
        { 
            MinSaleVelocity = 10.0f,
            MaxRiskScore = 20
        };
        
        var lowVelItem = CreateMockProfitItem(CreateMockRecipe("Slow", 50, 50, "ALC", EquipSlot.None, ItemMainCategory.Consumable), 50000, 50, 2);
        lowVelItem.RiskScore = 10;
        
        var highRiskItem = CreateMockProfitItem(CreateMockRecipe("Risky", 50, 50, "ALC", EquipSlot.None, ItemMainCategory.Consumable), 50000, 50, 15);
        highRiskItem.RiskScore = 80;
        
        var goodItem = CreateMockProfitItem(CreateMockRecipe("Good", 50, 50, "ALC", EquipSlot.None, ItemMainCategory.Consumable), 50000, 50, 15);
        goodItem.RiskScore = 10;
        
        var items = new List<ProfitCalculation> { lowVelItem, highRiskItem, goodItem };
        
        // Act
        var results = _filterService.FilterItems(items, criteria);
        
        // Assert
        Assert.Single(results);
        Assert.Equal("Good", results[0].Recipe?.ItemName);
    }
}
