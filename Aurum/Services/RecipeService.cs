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
        int failed = 0;
        
        // Debug: Check first few recipes
        bool debugged = false;
        
        foreach (var recipe in recipeSheet)
        {
            totalRecipes++;
            
            if (!debugged && recipe.RowId > 0)
            {
                log.Info($"DEBUG First recipe: ID={recipe.RowId}, CraftType={recipe.CraftType.RowId}, ItemResult={recipe.ItemResult.RowId}");
                debugged = true;
            }
            
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
            
            try
            {
                var recipeData = ConvertToRecipeData(recipe);
                recipeCache[recipe.RowId] = recipeData;
            }
            catch (Exception ex)
            {
                failed++;
                if (failed <= 5) // Only log first 5 errors to avoid spam
                {
                    log.Error(ex, $"Failed to convert recipe {recipe.RowId}: {ex.Message}");
                }
            }
        }
        
        log.Info($"Loaded {recipeCache.Count} crafting recipes (scanned {totalRecipes}, skipped {skippedNonCrafting} non-crafting, {skippedNoResult} no-result, {failed} failed)");
    }
    
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
            RecipeLevelTable = recipe.RecipeLevelTable.RowId
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
        // This will be populated as we load recipes
        var recipe = recipeCache.Values.FirstOrDefault(r => r.ResultItemId == itemId);
        return recipe?.RecipeId;
    }
    
    /// <summary>
    /// Get all loaded recipes
    /// </summary>
    public IEnumerable<RecipeData> GetAllRecipes()
    {
        if (!isInitialized)
            Initialize();
        
        return recipeCache.Values;
    }
    
    /// <summary>
    /// Get recipe by ID
    /// </summary>
    public RecipeData? GetRecipe(uint recipeId)
    {
        if (!isInitialized)
            Initialize();
        
        return recipeCache.GetValueOrDefault(recipeId);
    }
    
    /// <summary>
    /// Get recipes that produce a specific item
    /// </summary>
    public IEnumerable<RecipeData> GetRecipesForItem(uint itemId)
    {
        if (!isInitialized)
            Initialize();
        
        return recipeCache.Values.Where(r => r.ResultItemId == itemId);
    }
    
    /// <summary>
    /// Get recipes for a specific crafting class
    /// </summary>
    public IEnumerable<RecipeData> GetRecipesByClass(string className)
    {
        if (!isInitialized)
            Initialize();
        
        return recipeCache.Values.Where(r => 
            r.CraftingClassName.Equals(className, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Get recipes within a level range
    /// </summary>
    public IEnumerable<RecipeData> GetRecipesByLevel(int minLevel, int maxLevel)
    {
        if (!isInitialized)
            Initialize();
        
        return recipeCache.Values.Where(r => 
            r.ClassJobLevel >= minLevel && r.ClassJobLevel <= maxLevel);
    }
    
    /// <summary>
    /// Search recipes by name
    /// </summary>
    public IEnumerable<RecipeData> SearchRecipes(string searchTerm)
    {
        if (!isInitialized)
            Initialize();
        
        if (string.IsNullOrWhiteSpace(searchTerm))
            return recipeCache.Values;
        
        var term = searchTerm.ToLowerInvariant();
        return recipeCache.Values.Where(r => 
            r.ItemName.ToLowerInvariant().Contains(term));
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
        
        return recipeCache.Values.Any(r => r.ResultItemId == itemId);
    }
    
    /// <summary>
    /// Get statistics
    /// </summary>
    public RecipeServiceStats GetStats()
    {
        if (!isInitialized)
            Initialize();
        
        return new RecipeServiceStats
        {
            TotalRecipes = recipeCache.Count,
            TotalItems = itemNameCache.Count,
            RecipesByClass = JobNames.Values.ToDictionary(
                className => className,
                className => recipeCache.Values.Count(r => r.CraftingClassName == className)
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
