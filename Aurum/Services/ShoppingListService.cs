using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Plugin.Services;
using Aurum.Models;
using Lumina.Excel.Sheets;

namespace Aurum.Services;

/// <summary>
/// Service for generating shopping lists from recipes
/// </summary>
public class ShoppingListService
{
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;
    private readonly RecipeService recipeService;
    private readonly UniversalisService universalisService;
    
    // Vendor item cache
    private Dictionary<uint, uint> vendorItemPrices = new();
    private bool isInitialized = false;

    public ShoppingListService(
        IDataManager dataManager, 
        IPluginLog log, 
        RecipeService recipeService,
        UniversalisService universalisService)
    {
        this.dataManager = dataManager;
        this.log = log;
        this.recipeService = recipeService;
        this.universalisService = universalisService;
    }

    public void Initialize()
    {
        if (isInitialized) return;
        
        // TODO: In a real implementation, we would load vendor data from sheets
        // For now we'll start empty and rely on future enhancements
        isInitialized = true;
    }

    /// <summary>
    /// Generate a shopping list for a set of recipes to craft
    /// </summary>
    public ShoppingList GenerateShoppingList(List<CraftingTarget> targets)
    {
        var shoppingList = new ShoppingList
        {
            Targets = targets
        };

        var rawMaterials = new Dictionary<uint, int>();

        // 1. Break down all recipes into raw materials
        foreach (var target in targets)
        {
            var recipe = recipeService.GetRecipe(target.RecipeId);
            if (recipe == null)
            {
                log.Warning($"Could not find recipe {target.RecipeId} for shopping list");
                continue;
            }

            AddIngredientsRecursive(recipe, target.AmountToCraft, rawMaterials);
        }

        // 2. Convert to shopping list items and find best sources
        foreach (var (itemId, amount) in rawMaterials)
        {
            var item = new ShoppingListItem
            {
                ItemId = itemId,
                ItemName = recipeService.GetItemName(itemId),
                IconId = recipeService.GetItemIcon(itemId),
                AmountNeeded = amount,
                SourceType = MaterialSourceType.MarketBoard // Default to MB
            };

            // Check if vendor purchasable (mock logic for now as we don't have full vendor sheets loaded yet)
            // In future: Check GilShopItem sheet
            if (vendorItemPrices.TryGetValue(itemId, out var vendorPrice))
            {
                item.SourceType = MaterialSourceType.Vendor;
                item.AveragePricePerUnit = (int)vendorPrice;
            }
            else
            {
                // Get MB price estimate
                // We'll use a rough estimate if we don't have fresh data
                // In a real flow, we might want to trigger a price update here
                var marketData = universalisService.GetMarketData(itemId);
                if (marketData != null && marketData.MinPrice > 0)
                {
                    item.AveragePricePerUnit = (int)marketData.MinPrice;
                }
            }

            shoppingList.Items.Add(item);
        }

        // 3. Calculate totals
        shoppingList.TotalEstimatedCost = shoppingList.Items.Sum(i => i.TotalCost);

        return shoppingList;
    }

    private void AddIngredientsRecursive(RecipeData recipe, int quantity, Dictionary<uint, int> totals)
    {
        foreach (var ingredient in recipe.Ingredients)
        {
            // If we have a sub-recipe and we decide to craft it (recursive)
            // For now, let's assume we buy all base materials. 
            // A more advanced version would ask "do you want to craft intermediates?"
            // For this version (Aurum-8ic.3.1), we just list immediate ingredients or base mats?
            // The requirement implies "Shopping List", usually meaning base materials.
            // But if an intermediate is cheaper to buy than craft, we should buy it.
            // For MVP: Flatten to immediate ingredients. Users often buy intermediates.
            
            // Check if this ingredient has a sub-recipe and recursively break it down
            if (ingredient.SubRecipeId.HasValue)
            {
                var subRecipe = recipeService.GetRecipe(ingredient.SubRecipeId.Value);
                if (subRecipe != null)
                {
                    // Recursively add ingredients for the sub-recipe
                    AddIngredientsRecursive(subRecipe, quantity * ingredient.AmountNeeded, totals);
                    continue; // Skip adding the intermediate item itself to the shopping list
                }
            }
            
            if (totals.ContainsKey(ingredient.ItemId))
            {
                totals[ingredient.ItemId] += ingredient.AmountNeeded * quantity;
            }
            else
            {
                totals[ingredient.ItemId] = ingredient.AmountNeeded * quantity;
            }
        }
        
        foreach (var crystal in recipe.Crystals)
        {
             if (totals.ContainsKey(crystal.ItemId))
            {
                totals[crystal.ItemId] += crystal.AmountNeeded * quantity;
            }
            else
            {
                totals[crystal.ItemId] = crystal.AmountNeeded * quantity;
            }
        }
    }
}
