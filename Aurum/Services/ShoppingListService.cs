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
            
            foreach (var ing in recipe.Ingredients)
            {
                AddToPool(itemPool, ing.ItemId, ing.AmountNeeded * crafts);
                if (ing.SubRecipeId.HasValue)
                {
                    itemRecipeMap[ing.ItemId] = ing.SubRecipeId.Value;
                }
            }
             foreach (var crystal in recipe.Crystals)
            {
                 AddToPool(itemPool, crystal.ItemId, crystal.AmountNeeded * crafts);
            }
        }
        
        // 2. Iterative processing
        bool changed = true;
        while (changed)
        {
            changed = false;
            // Find items in pool that have a recipe mapped
            var craftableItems = itemPool.Keys.Where(k => itemRecipeMap.ContainsKey(k)).ToList();
            
            // We need to process them in topological order effectively.
            // But simply picking one, calculating crafts, adding ingredients, and removing it works
            // IF we don't have cycles (crafting trees usually DAGs).
            
            // However, we want to aggregate maximal amount before breaking down.
            // If we break down 'Iron Ingot' now, but later another branch adds more 'Iron Ingot',
            // we might have optimized prematurely (e.g. 2 needed now -> 1 craft (yield 3). Later +1 needed -> 0 extra crafts).
            // But if we processed 2, then 1, we might calculate 1 craft + 1 craft = 2 crafts.
            // If we processed 3 at once -> 1 craft.
            
            // So we need to peel from the "top" (items that are NOT ingredients of any other current item).
            // But identifying "top" is hard without full graph.
            
            // Heuristic: Just process everything currently in the pool that IS craftable?
            // No, if A needs B, and we have A and B in pool.
            // We should process A first, which adds more B. THEN process B.
            
            // So: Process items that are NOT ingredients of any OTHER item currently in the pool? Expensive.
            
            // Alternative: Recursion with Memoization?
            // No, we need global aggregation.
            
            // Let's use a "Tiered" approach?
            // Or just iterate until no changes, but only process "Leafs" of the current dependency graph?
            // Actually, "Roots" (items that no one else needs).
            // But "no one else needs" changes as we break things down.
            
            // Let's try a simpler approach:
            // 1. Expand EVERYTHING to a list of (ItemId, Depth).
            // 2. Sort by Depth descending?
            
            // Let's trust that the recipe data implies a hierarchy.
            // We can iterate: Pick an item. Does any OTHER item in the pool use this as ingredient?
            // If yes, defer. If no, process.
            
            // To do this efficiently:
            // Build dependency graph of items in pool.
            // Key: ItemId. Value: List of Ingredients.
            
            var currentPool = itemPool.Where(kv => itemRecipeMap.ContainsKey(kv.Key)).ToList();
            if (currentPool.Count == 0) break; // Nothing left to break down
            
            // Build a mini-graph of the current pool
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
            
            // Find items that are NOT dependencies of anyone else in the current "craftable" set
            // i.e. Indegree 0 within the subgraph of craftables.
            
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
                 // Cycle detected or logic error?
                 // Fallback: Process first
                 toProcess.Add(currentPool.First());
            }
            
            foreach (var item in toProcess)
            {
                changed = true;
                uint itemId = item.Key;
                int amountNeeded = item.Value;
                uint recipeId = itemRecipeMap[itemId];
                
                // Remove from pool (we are replacing it with ingredients)
                itemPool.Remove(itemId);
                itemRecipeMap.Remove(itemId); // No longer pending breakdown
                
                var recipe = recipeService.GetRecipe(recipeId);
                if (recipe != null)
                {
                    int crafts = (int)Math.Ceiling((double)amountNeeded / recipe.ResultAmount);
                    
                    AddImmediateIngredients(recipe, crafts, itemPool, itemRecipeMap);
                }
            }
        }
        
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
