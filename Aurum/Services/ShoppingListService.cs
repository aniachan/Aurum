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
        
        try 
        {
            // TODO: Load vendor data from GilShop/GilShopItem sheets.
            // Currently, the schema for connecting Items to Prices via GilShop is complex and requires
            // further investigation into the sheet structure (GilShop vs GilShopItem).
            // For the MVP, we are using a fallback list of common vendor items.
            
            /* 
            var gilShopSheet = dataManager.GetExcelSheet<GilShop>();
            if (gilShopSheet != null)
            {
                foreach (var shop in gilShopSheet)
                {
                    // Implementation for parsing GilShop to populate vendorItemPrices
                    // will go here in a future update.
                }
            }
            */
            
            // Fallback: Load common crafting materials sold by vendors
            LoadCommonVendorItems();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to load vendor data");
        }

        isInitialized = true;
    }

    private void LoadCommonVendorItems()
    {
        // Common crafting materials sold by vendors
        // This is a manual stopgap until we get the sheet loading perfect
        // Distilled Water
        vendorItemPrices[930] = 4;
        // Rock Salt
        vendorItemPrices[931] = 3;
        // Iron Ore
        vendorItemPrices[5111] = 18;
        // Copper Ore
        vendorItemPrices[5106] = 2;
        // Maple Log
        vendorItemPrices[5361] = 9;
        // Ash Log
        vendorItemPrices[5364] = 18;
        // Cotton Boll
        vendorItemPrices[5344] = 10;
        // Hemp
        vendorItemPrices[5491] = 10;
        // Cinnamon
        vendorItemPrices[4819] = 4;
        // Garlean Garlic
        vendorItemPrices[4820] = 5;
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

            // Check if vendor purchasable
            bool hasVendorPrice = vendorItemPrices.TryGetValue(itemId, out var vendorPrice);
            
            // Always get MB price for comparison
            var marketData = universalisService.GetMarketData(itemId);
            int mbPrice = (marketData != null && marketData.MinPrice > 0) ? (int)marketData.MinPrice : int.MaxValue;
            
            if (hasVendorPrice)
            {
                // If we have both, choose the cheaper one
                if (mbPrice < vendorPrice)
                {
                    item.SourceType = MaterialSourceType.MarketBoard;
                    item.AveragePricePerUnit = mbPrice;
                }
                else
                {
                    item.SourceType = MaterialSourceType.Vendor;
                    item.AveragePricePerUnit = (int)vendorPrice;
                }
            }
            else
            {
                // Only MB available
                item.SourceType = MaterialSourceType.MarketBoard;
                item.AveragePricePerUnit = (mbPrice == int.MaxValue) ? 0 : mbPrice;
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
