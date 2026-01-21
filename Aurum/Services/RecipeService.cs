using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Aurum.Models;

namespace Aurum.Services;

/// <summary>
/// Service for loading and querying crafting recipes from game data
/// </summary>
public class RecipeService
{
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;
    
    private Dictionary<uint, RecipeData> recipeCache = new();
    private Dictionary<uint, string> itemNameCache = new();
    private Dictionary<uint, uint> itemIconCache = new();
    
    // Indices for fast lookups without full loading
    private Dictionary<uint, List<uint>> itemResultIndex = new();
    private Dictionary<uint, List<uint>> jobIndex = new();
    private Dictionary<int, List<uint>> levelIndex = new();
    
    private bool isInitialized = false;
    
    // Crafting class IDs from CraftType sheet (NOT job IDs)
    // CraftType indices: 0=CRP, 1=BSM, 2=ARM, 3=GSM, 4=LTW, 5=WVR, 6=ALC, 7=CUL
    private static readonly uint[] CraftingJobIds = { 0, 1, 2, 3, 4, 5, 6, 7 };
    private static readonly Dictionary<uint, string> JobNames = new()
    {
        { 0, "CRP" },   // Carpenter
        { 1, "BSM" },   // Blacksmith
        { 2, "ARM" },  // Armorer
        { 3, "GSM" },  // Goldsmith
        { 4, "LTW" },  // Leatherworker
        { 5, "WVR" },  // Weaver
        { 6, "ALC" },  // Alchemist
        { 7, "CUL" }   // Culinarian
    };
    
    public RecipeService(IDataManager dataManager, IPluginLog log)
    {
        this.dataManager = dataManager;
        this.log = log;
    }
    
    /// <summary>
    /// Initialize the service by loading all recipes
    /// </summary>
    public void Initialize()
    {
        if (isInitialized)
            return;
        
        log.Info("Initializing RecipeService...");
        
        try
        {
            LoadItemNames();
            LoadRecipes();
            isInitialized = true;
            log.Info($"RecipeService initialized with {recipeCache.Count} recipes");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to initialize RecipeService");
        }
    }
    
    /// <summary>
    /// Load all item names and icons for quick lookup
    /// </summary>
    private void LoadItemNames()
    {
        var itemSheet = dataManager.GetExcelSheet<Item>();
        if (itemSheet == null)
        {
            log.Error("Failed to load Item sheet");
            return;
        }
        
        foreach (var item in itemSheet)
        {
            if (item.RowId == 0)
                continue;
            
            itemNameCache[item.RowId] = item.Name.ExtractText();
            itemIconCache[item.RowId] = item.Icon;
        }
        
        log.Info($"Loaded {itemNameCache.Count} item names");
    }
    
    /// <summary>
    /// Load all crafting recipes from game data
    /// </summary>
    private void LoadRecipes()
    {
        var recipeSheet = dataManager.GetExcelSheet<Recipe>();
        if (recipeSheet == null)
        {
            log.Error("Failed to load Recipe sheet");
            return;
        }
        
        int totalRecipes = 0;
        int skippedNonCrafting = 0;
        int skippedNoResult = 0;
        
        // Clear indices
        itemResultIndex.Clear();
        jobIndex.Clear();
        levelIndex.Clear();
        
        foreach (var recipe in recipeSheet)
        {
            totalRecipes++;
            
            if (recipe.RowId == 0)
                continue;
            
            // Skip non-crafting recipes
            if (!CraftingJobIds.Contains(recipe.CraftType.RowId))
            {
                skippedNonCrafting++;
                continue;
            }
            
            // Skip recipes with no result
            if (recipe.ItemResult.RowId == 0)
            {
                skippedNoResult++;
                continue;
            }
            
            // Build indices
            var recipeId = recipe.RowId;
            var resultItemId = recipe.ItemResult.RowId;
            var jobType = recipe.CraftType.RowId;
            var level = (int)recipe.RecipeLevelTable.Value.ClassJobLevel;
            
            // Index by Result Item
            if (!itemResultIndex.ContainsKey(resultItemId))
                itemResultIndex[resultItemId] = new List<uint>();
            itemResultIndex[resultItemId].Add(recipeId);
            
            // Index by Job
            if (!jobIndex.ContainsKey(jobType))
                jobIndex[jobType] = new List<uint>();
            jobIndex[jobType].Add(recipeId);
            
            // Index by Level
            if (!levelIndex.ContainsKey(level))
                levelIndex[level] = new List<uint>();
            levelIndex[level].Add(recipeId);
        }
        
        log.Info($"Indexed {totalRecipes} recipes (skipped {skippedNonCrafting} non-crafting, {skippedNoResult} no-result). Recipes will be loaded on demand.");
    }
    
    /// <summary>
    /// Helper to determine the main category from Item data
    /// </summary>
    private ItemMainCategory DetermineMainCategory(Item item)
    {
        var uiCategoryId = item.ItemUICategory.RowId;

        // 1. Weapons / Tools
        if (uiCategoryId >= 1 && uiCategoryId <= 11) return ItemMainCategory.Combat; // Combat Weapons
        if (uiCategoryId >= 84 && uiCategoryId <= 89) return ItemMainCategory.Combat; // New Jobs (RPR, SGE, VPR, PCT) - expanded range for safety
        
        // DoH/DoL Tools
        // 12=CRP, 13=BSM, 14=ARM, 15=GSM, 16=LTW, 17=WVR, 18=ALC, 19=CUL
        // 20=MIN, 21=BTN, 22=FSH
        // 23=CRP_OFF ... 30=FSH_OFF
        if (uiCategoryId >= 12 && uiCategoryId <= 33) return ItemMainCategory.Crafting; // Tools (Crafting & Gathering)

        // 2. Armor & Accessories
        // 34=Head, 35=Body, 36=Legs, 37=Hands, 38=Feet, 39=Waist(RIP)
        // 40=Neck, 41=Ears, 42=Wrists, 43=Rings
        if ((uiCategoryId >= 34 && uiCategoryId <= 39) || (uiCategoryId >= 40 && uiCategoryId <= 43))
        {
            // Check ClassJobCategory to see if it's for DoH/DoL
            // This is a heuristic: If the ClassJobCategory includes CRA(31) or GAT(32) it's likely non-combat gear
            // We'll peek at the ClassJobCategory if available
            // Lumina Item -> ClassJobCategory -> Name or IsCrafting/Gathering
            
            // Note: We need to access ClassJobCategory.
            // Since we passed the whole Item object, we can try to inspect it.
            var cjc = item.ClassJobCategory.Value;
            if (cjc.RowId != 0)
            {
                // CRP=9, BSM=10 ... CUL=16. MIN=17, BTN=18, FSH=19
                // But ClassJobCategory is a set of bools for each job.
                
                // Fast check: If it has stats for CP (Crafting Points) or GP (Gathering Points), it's definitely Crafting/Gathering
                // BaseParam[0] is usually Main Attribute, but CP/GP can be anywhere.
                // Actually, let's check the Equippable Jobs.
                
                // If it's equippable by DoW/DoM, it's Combat.
                // If equippable ONLY by DoH/DoL, it's Crafting.
                
                bool isDoW = cjc.GLA || cjc.PGL || cjc.MRD || cjc.LNC || cjc.ARC || cjc.ROG || cjc.THM || cjc.ACN;
                // ... check all combat jobs ...
                // Shortcuts:
                // ClassJobCategory has boolean properties for every job.
                
                // Heuristic: If it gives CP or GP, it's for us.
                // Param 11 = CP, Param 10 = GP ? Need to verify param IDs.
                // Let's stick to the Category Name if possible? "Disciples of the Hand"
                
                if (cjc.Name.ExtractText().Contains("Disciple of the Hand") || cjc.Name.ExtractText().Contains("Disciple of the Land") || cjc.Name.ExtractText().Contains("Crafter") || cjc.Name.ExtractText().Contains("Gatherer"))
                {
                    return ItemMainCategory.Crafting; // Or Gathering, grouped for this task
                }
                
                // Fallback: Check if it's strictly for DoH/DoL
                if (!isDoW && (cjc.CRP || cjc.MIN)) // simple spot check
                {
                   return ItemMainCategory.Crafting;
                }
            }

            return ItemMainCategory.Combat; // Default to combat for armor/accessories
        }

        // 3. Consumables
        // 44=Medicine, 45=Ingredient, 46=Meal, 47=Seafood
        if (uiCategoryId == 44 || uiCategoryId == 46 || uiCategoryId == 47) return ItemMainCategory.Consumable;
        
        // 4. Materials
        // 48-60ish are materials
        if (uiCategoryId >= 48 && uiCategoryId <= 60) return ItemMainCategory.Material;

        return ItemMainCategory.Other;
    }
    
    /// <summary>
    /// Helper to determine the main category from UI category ID
    
    /// <summary>
    /// Convert Lumina Recipe to our RecipeData model
    /// </summary>
    private RecipeData ConvertToRecipeData(Recipe recipe)
    {
        var jobId = recipe.CraftType.RowId;
        
        var recipeData = new RecipeData
        {
            RecipeId = recipe.RowId,
            ResultItemId = recipe.ItemResult.RowId,
            ItemName = GetItemName(recipe.ItemResult.RowId),
            IconId = GetItemIcon(recipe.ItemResult.RowId),
            CraftingClassJobId = jobId,
            CraftingClassName = JobNames.GetValueOrDefault(jobId, "Unknown"),
            RecipeLevel = (int)recipe.RecipeLevelTable.RowId,
            ClassJobLevel = (int)recipe.RecipeLevelTable.Value.ClassJobLevel,
            Difficulty = (int)recipe.RecipeLevelTable.Value.Difficulty,
            Quality = (int)recipe.RecipeLevelTable.Value.Quality,
            Durability = (int)recipe.RecipeLevelTable.Value.Durability,
            ResultAmount = (int)recipe.AmountResult,
            CanBeHQ = recipe.CanHq,
            IsExpert = recipe.IsExpert,
            IsSpecialist = recipe.IsSpecializationRequired,
            RecipeLevelTable = recipe.RecipeLevelTable.RowId,
            ItemCategory = recipe.ItemResult.Value.ItemUICategory.RowId,
            MainCategory = DetermineMainCategory(recipe.ItemResult.Value)
        };
        
        // Load ingredients (materials)
        for (int i = 0; i < recipe.Ingredient.Count && i < recipe.AmountIngredient.Count; i++)
        {
            var ingredientRef = recipe.Ingredient[i];
            var amount = recipe.AmountIngredient[i];
            
            if (ingredientRef.RowId > 0 && amount > 0)
            {
                var ingredient = new RecipeIngredient
                {
                    ItemId = ingredientRef.RowId,
                    ItemName = GetItemName(ingredientRef.RowId),
                    AmountNeeded = (int)amount,
                    IsHQ = false
                };
                
                // Check if this ingredient is itself craftable
                ingredient.SubRecipeId = FindRecipeForItem(ingredientRef.RowId);
                
                recipeData.Ingredients.Add(ingredient);
            }
        }
        
        return recipeData;
    }
    
    /// <summary>
    /// Find if an item has a recipe
    /// </summary>
    private uint? FindRecipeForItem(uint itemId)
    {
        // Use index first
        if (itemResultIndex.TryGetValue(itemId, out var recipeIds) && recipeIds.Count > 0)
        {
            return recipeIds[0];
        }
        
        return null;
    }
    
    /// <summary>
    /// Get all loaded recipes
    /// </summary>
    public IEnumerable<RecipeData> GetAllRecipes()
    {
        if (!isInitialized)
            Initialize();
            
        // For backwards compatibility, or mass operations, we might need to load all
        // But for performance, we should avoid this where possible
        
        // Return cached + load missing from indices
        // This is heavy!
        
        // Strategy: Iterate all indices and load if missing
        foreach (var list in itemResultIndex.Values)
        {
            foreach (var recipeId in list)
            {
                if (!recipeCache.ContainsKey(recipeId))
                {
                    LoadRecipe(recipeId);
                }
            }
        }
        
        return recipeCache.Values;
    }
    
    /// <summary>
    /// Load a specific recipe by ID into cache
    /// </summary>
    private RecipeData? LoadRecipe(uint recipeId)
    {
        if (recipeCache.TryGetValue(recipeId, out var cached))
            return cached;

        var recipeSheet = dataManager.GetExcelSheet<Recipe>();
        var recipe = recipeSheet?.GetRow(recipeId);
        
        if (recipe == null)
            return null;
            
        try
        {
            var data = ConvertToRecipeData(recipe.Value);
            recipeCache[recipeId] = data;
            return data;
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to load recipe {recipeId}");
            return null;
        }
    }
    
    /// <summary>
    /// Get recipe by ID
    /// </summary>
    public RecipeData? GetRecipe(uint recipeId)
    {
        if (!isInitialized)
            Initialize();
        
        return LoadRecipe(recipeId);
    }
    
    /// <summary>
    /// Get recipes that produce a specific item
    /// </summary>
    public IEnumerable<RecipeData> GetRecipesForItem(uint itemId)
    {
        if (!isInitialized)
            Initialize();
        
        if (itemResultIndex.TryGetValue(itemId, out var recipeIds))
        {
            foreach (var id in recipeIds)
            {
                var recipe = LoadRecipe(id);
                if (recipe != null)
                    yield return recipe;
            }
        }
    }
    
    /// <summary>
    /// Get recipes for a specific crafting class
    /// </summary>
    public IEnumerable<RecipeData> GetRecipesByClass(string className)
    {
        if (!isInitialized)
            Initialize();
            
        // Find job ID from name
        var jobEntry = JobNames.FirstOrDefault(x => x.Value.Equals(className, StringComparison.OrdinalIgnoreCase));
        if (jobEntry.Value == null) // KeyValuePair default is null value? No, struct.
            yield break;
            
        // Check if className is valid
        if (!JobNames.Values.Contains(className))
             yield break;
             
        var jobId = JobNames.First(x => x.Value == className).Key;
        
        if (jobIndex.TryGetValue(jobId, out var recipeIds))
        {
            foreach (var id in recipeIds)
            {
                var recipe = LoadRecipe(id);
                if (recipe != null)
                    yield return recipe;
            }
        }
    }
    
    /// <summary>
    /// Get recipes within a level range
    /// </summary>
    public IEnumerable<RecipeData> GetRecipesByLevel(int minLevel, int maxLevel)
    {
        if (!isInitialized)
            Initialize();
        
        // Iterate levels in range
        for (int lvl = minLevel; lvl <= maxLevel; lvl++)
        {
            if (levelIndex.TryGetValue(lvl, out var recipeIds))
            {
                foreach (var id in recipeIds)
                {
                    var recipe = LoadRecipe(id);
                    if (recipe != null)
                        yield return recipe;
                }
            }
        }
    }
    
    /// <summary>
    /// Search recipes by name
    /// </summary>
    public IEnumerable<RecipeData> SearchRecipes(string searchTerm)
    {
        if (!isInitialized)
            Initialize();
        
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            foreach (var r in GetAllRecipes())
                yield return r;
            yield break;
        }
        
        // Search is tricky with lazy loading because we don't have names loaded
        // BUT we have Item Names loaded in itemNameCache!
        // So we can search items first, then find recipes for those items
        
        var term = searchTerm.ToLowerInvariant();
        
        // Find items matching name
        var matchingItemIds = itemNameCache
            .Where(kvp => kvp.Value.ToLowerInvariant().Contains(term))
            .Select(kvp => kvp.Key);
            
        foreach (var itemId in matchingItemIds)
        {
            if (itemResultIndex.TryGetValue(itemId, out var recipeIds))
            {
                foreach (var id in recipeIds)
                {
                    var recipe = LoadRecipe(id);
                    if (recipe != null)
                        yield return recipe;
                }
            }
        }
    }
    
    /// <summary>
    /// Get item name by ID
    /// </summary>
    public string GetItemName(uint itemId)
    {
        return itemNameCache.GetValueOrDefault(itemId, $"Unknown Item #{itemId}");
    }
    
    /// <summary>
    /// Get item icon by ID
    /// </summary>
    public uint GetItemIcon(uint itemId)
    {
        itemIconCache.TryGetValue(itemId, out var icon);
        return icon;
    }
    
    /// <summary>
    /// Check if an item is craftable
    /// </summary>
    public bool IsCraftable(uint itemId)
    {
        if (!isInitialized)
            Initialize();
        
        return itemResultIndex.ContainsKey(itemId);
    }
    
    /// <summary>
    /// Get statistics
    /// </summary>
    public RecipeServiceStats GetStats()
    {
        if (!isInitialized)
            Initialize();
        
        // Count total recipes from indices
        int totalRecipes = jobIndex.Values.Sum(list => list.Count);
        
        return new RecipeServiceStats
        {
            TotalRecipes = totalRecipes,
            TotalItems = itemNameCache.Count,
            RecipesByClass = JobNames.Values.ToDictionary(
                className => className,
                className => 
                {
                    // Find job ID
                    var entry = JobNames.FirstOrDefault(x => x.Value == className);
                    if (entry.Value != null && jobIndex.ContainsKey(entry.Key))
                        return jobIndex[entry.Key].Count;
                    return 0;
                }
            )
        };
    }
}

public class RecipeServiceStats
{
    public int TotalRecipes { get; set; }
    public int TotalItems { get; set; }
    public Dictionary<string, int> RecipesByClass { get; set; } = new();
}
