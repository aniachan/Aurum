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
            // Currently, the schema for connecting Items to Prices via GilShop is complex.
            // Based on investigation, GilShopItem is a Subrow sheet keyed by GilShop.RowId.
            
            // First, get the main sheets
            var gilShopSheet = dataManager.GetExcelSheet<GilShop>();
            var gilShopItemSheet = dataManager.GetSubrowExcelSheet<GilShopItem>();
            
            if (gilShopSheet != null && gilShopItemSheet != null)
            {
                foreach (var shop in gilShopSheet)
                {
                    // GetRow throws ArgumentOutOfRangeException if the rowId doesn't exist in the subrow sheet.
                    // Not every GilShop entry has a corresponding GilShopItem row, so we guard here.
                    if (!gilShopItemSheet.HasRow(shop.RowId)) continue;
                    var shopItems = gilShopItemSheet.GetRow(shop.RowId);

                    // SubrowCollection is a struct or non-nullable? Checking count instead.
                    if (shopItems.Count > 0)
                    {
                        foreach (var shopItem in shopItems)
                        {
                            // shopItem is a GilShopItem
                            // It has an Item property which is a RowRef<Item>
                            
                            var item = shopItem.Item.Value;
                            // Value might be nullable or we just check if RowId is valid
                            if (item.RowId != 0)
                            {
                                // Store the price. For GilShops, the price is the Item's PriceMid (usually).
                                // Or PriceLow? Usually PriceMid is the vendor sell price.
                                // NOTE: Some items cannot be sold by vendors, but GilShops sell them to YOU.
                                // The price you buy it for is usually PriceMid.
                                
                                // Let's verify this assumption.
                                if (item.PriceMid > 0)
                                {
                                    // We only care about the cheapest price if it appears in multiple shops?
                                    // Actually, vendor prices are usually global fixed constants.
                                    vendorItemPrices[item.RowId] = item.PriceMid;
                                }
                            }
                        }
                    }
                }
            }
            
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
        var intermediatePool = new Dictionary<uint, int>(); // Track pooled intermediates needed

        // 1. First pass: Collect all intermediate needs
        // We do this because multiple recipes might need the SAME intermediate,
        // and we can batch craft that intermediate to save materials (if it yields > 1).
        
        // This is now handled by ProcessWithPooling called below.
        
        return ProcessWithPooling(targets);
    }
    
    private ShoppingList ProcessWithPooling(List<CraftingTarget> targets)
    {
         var shoppingList = new ShoppingList
        {
            Targets = targets
        };

         // Pool of items we need. 
         // Key: ItemId. Value: Amount.
         var itemPool = new Dictionary<uint, int>();
         
         // We also need to know IF an item is craftable and WHICH recipe to use.
         // Since multiple sources might ask for the same item, we need to be consistent.
         // We'll map ItemId -> RecipeId (if we decide to craft it).
         var itemRecipeMap = new Dictionary<uint, uint>();

        // 1. Initial breakdown
        foreach (var target in targets)
        {
            var recipe = recipeService.GetRecipe(target.RecipeId);
            if (recipe == null) continue;
            int crafts = (int)Math.Ceiling((double)target.AmountToCraft / recipe.ResultAmount);
            
            // Add the final product to our plan too, so it gets sequenced
            itemRecipeMap[target.ItemId] = target.RecipeId;
            AddToPool(itemPool, target.ItemId, target.AmountToCraft);

            // We don't add ingredients here immediately if we want to sequence the final product too.
            // But the current logic assumes itemPool only contains things we need to ACQUIRE (buy or craft).
            // The existing logic removes items from pool as they are processed.
            
            // Wait, if we put the TARGETS in the pool, the loop below will process them (break them down).
            // That is exactly what we want for ordering!
        }
        
        // 2. Iterative processing with step recording
        var steps = new List<CraftingStep>();
        
        // We need to keep processing until NO craftable items remain in the pool.
        bool changed = true;
        while (changed)
        {
            changed = false;
            // Find items in pool that have a recipe mapped
            var craftableItems = itemPool.Keys.Where(k => itemRecipeMap.ContainsKey(k)).ToList();
            
            if (craftableItems.Count == 0) break;

            // Build dependency graph of items in pool.
            // Key: ItemId. Value: List of Ingredients.
            
            var currentPool = itemPool.Where(kv => itemRecipeMap.ContainsKey(kv.Key)).ToList();
            
            // Build a mini-graph of the current pool to find "Leafs" (items that don't depend on other craftables in pool)
            // Actually, for crafting order (execution), we want to do the opposite of breakdown?
            // "Breakdown" order is Top-Down (Final Product -> Ingredients).
            // "Crafting" order is Bottom-Up (Ingredients -> Final Product).
            
            // If we process Top-Down here (which we do to find raw materials), we are generating the steps in REVERSE order.
            // So: Final Product depends on Intermediate. We process Final Product first (break it down).
            // Then we process Intermediate (break it down).
            // The execution order should be: Craft Intermediate -> Craft Final Product.
            // So if we record steps as we break them down, we just need to REVERSE the list at the end!
            
            // ... logic for finding what to process next (Top-Down) ...
            
            var dependencies = new Dictionary<uint, HashSet<uint>>();
            foreach (var kv in currentPool)
            {
                 var rid = itemRecipeMap[kv.Key];
                 var r = recipeService.GetRecipe(rid);
                 var deps = new HashSet<uint>();
                 if (r != null) {
                     foreach(var ing in r.Ingredients) if(itemPool.ContainsKey(ing.ItemId)) deps.Add(ing.ItemId);
                 }
                 dependencies[kv.Key] = deps;
            }
            
            // Find items that are NOT ingredients of any OTHER item currently in the pool (Roots of dependency tree)
            // This allows us to aggregate demand before breaking down.
            
            var referencedByOthers = new HashSet<uint>();
            foreach(var kv in dependencies)
            {
                foreach(var dep in kv.Value)
                {
                    if (itemRecipeMap.ContainsKey(dep)) // Only care if the dependency is also a craftable we intend to break down
                        referencedByOthers.Add(dep);
                }
            }
            
            var toProcess = currentPool.Where(kv => !referencedByOthers.Contains(kv.Key)).ToList();
            
            if (toProcess.Count == 0 && currentPool.Count > 0)
            {
                 // Cycle detected or complex dependency? Fallback to processing first one.
                 toProcess.Add(currentPool.First());
            }
            
            foreach (var item in toProcess)
            {
                changed = true;
                uint itemId = item.Key;
                int amountNeeded = item.Value;
                uint recipeId = itemRecipeMap[itemId];
                
                // Record this step (Top-Down)
                var recipe = recipeService.GetRecipe(recipeId);
                if (recipe != null)
                {
                    int crafts = (int)Math.Ceiling((double)amountNeeded / recipe.ResultAmount);
                    
                    var step = new CraftingStep
                    {
                        ItemId = itemId,
                        ItemName = recipe.ItemName, // Or recipeService.GetItemName(itemId)
                        IconId = recipe.IconId,
                        RecipeId = recipeId,
                        Quantity = amountNeeded,
                        BatchSize = recipe.ResultAmount,
                        TotalCrafts = crafts
                    };
                    
                    // Capture immediate ingredients for this step (for display)
                    foreach (var ing in recipe.Ingredients)
                    {
                        step.Ingredients.Add(new ShoppingListItem 
                        {
                            ItemId = ing.ItemId,
                            ItemName = ing.ItemName,
                            AmountNeeded = ing.AmountNeeded * crafts
                        });
                    }
                     foreach (var cry in recipe.Crystals)
                    {
                        step.Ingredients.Add(new ShoppingListItem 
                        {
                            ItemId = cry.ItemId,
                            ItemName = cry.ItemName, // Crystals usually have names
                            AmountNeeded = cry.AmountNeeded * crafts
                        });
                    }
                    
                    steps.Add(step);
                    
                    // Remove from pool (we are replacing it with ingredients)
                    itemPool.Remove(itemId);
                    itemRecipeMap.Remove(itemId); 
                    
                    AddImmediateIngredients(recipe, crafts, itemPool, itemRecipeMap);
                }
                else
                {
                    // Recipe not found? Just remove it to prevent infinite loop
                    itemPool.Remove(itemId);
                    itemRecipeMap.Remove(itemId);
                }
            }
        }
        
        // Reverse steps to get Execution Order (Bottom-Up)
        steps.Reverse();
        
        // Assign step indices
        for (int i = 0; i < steps.Count; i++)
        {
            steps[i].StepIndex = i + 1;
        }
        
        shoppingList.CraftingSteps = steps;
        
        // Convert leftover pool to shopping list
        foreach (var (itemId, amount) in itemPool)
        {
             var item = new ShoppingListItem
            {
                ItemId = itemId,
                ItemName = recipeService.GetItemName(itemId),
                IconId = recipeService.GetItemIcon(itemId),
                AmountNeeded = amount,
                SourceType = MaterialSourceType.MarketBoard
            };
            
            // Existing logic for pricing...
            bool hasVendorPrice = vendorItemPrices.TryGetValue(itemId, out var vendorPrice);
            var marketData = universalisService.GetMarketData(itemId);
            int mbPrice = (marketData != null && marketData.MinPrice > 0) ? (int)marketData.MinPrice : int.MaxValue;
            
            if (hasVendorPrice && (mbPrice >= vendorPrice || mbPrice == int.MaxValue))
            {
                item.SourceType = MaterialSourceType.Vendor;
                item.AveragePricePerUnit = (int)vendorPrice;
            }
            else
            {
                item.SourceType = MaterialSourceType.MarketBoard;
                item.AveragePricePerUnit = (mbPrice == int.MaxValue) ? 0 : mbPrice;
                if (marketData != null && marketData.Listings.Any())
                {
                    // Find cheapest world from listings
                    var cheapestListing = marketData.Listings
                        .Where(l => !l.IsHQ) // Prefer NQ for materials usually
                        .OrderBy(l => l.PricePerUnit)
                        .FirstOrDefault();
                    
                    if (cheapestListing == null)
                    {
                         cheapestListing = marketData.Listings.OrderBy(l => l.PricePerUnit).FirstOrDefault();
                    }

                    if (cheapestListing != null)
                    {
                        item.CheapestWorld = cheapestListing.WorldName;
                    }
                }
            }
            
            shoppingList.Items.Add(item);
        }
        
        shoppingList.TotalEstimatedCost = shoppingList.Items.Sum(i => i.TotalCost);
        return shoppingList;
    }
    
    private void AddToPool(Dictionary<uint, int> pool, uint itemId, int amount)
    {
        if (pool.ContainsKey(itemId)) pool[itemId] += amount;
        else pool[itemId] = amount;
    }
    
    private void AddImmediateIngredients(RecipeData recipe, int crafts, Dictionary<uint, int> pool, Dictionary<uint, uint>? recipeMap = null)
    {
        foreach (var ing in recipe.Ingredients)
        {
            AddToPool(pool, ing.ItemId, ing.AmountNeeded * crafts);
            if (ing.SubRecipeId.HasValue && recipeMap != null)
            {
                recipeMap[ing.ItemId] = ing.SubRecipeId.Value;
            }
        }
        foreach (var crystal in recipe.Crystals)
        {
             AddToPool(pool, crystal.ItemId, crystal.AmountNeeded * crafts);
        }
    }

    private void AddIngredientsRecursive(RecipeData recipe, int quantity, Dictionary<uint, int> totals)
    {
        // Legacy method, kept if needed or we can delete it.
        // Replacing with the new pooled logic means we don't use this anymore.
    }
}
