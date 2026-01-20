using System;
using System.Collections.Generic;

namespace Aurum.Models;

/// <summary>
/// Represents a game recipe ingredient (can be sub-recipe or material)
/// </summary>
public class RecipeIngredient
{
    public uint ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int AmountNeeded { get; set; }
    public bool IsHQ { get; set; }
    
    // If this ingredient is itself a craftable item
    public uint? SubRecipeId { get; set; }
    public RecipeData? SubRecipe { get; set; }
}

/// <summary>
/// Represents a crafting recipe from game data
/// </summary>
public class RecipeData
{
    public uint RecipeId { get; set; }
    public uint ResultItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public uint IconId { get; set; }
    
    // Crafting requirements
    public uint CraftingClassJobId { get; set; }
    public string CraftingClassName { get; set; } = string.Empty;  // "CRP", "BSM", etc.
    public int RecipeLevel { get; set; }
    public int ClassJobLevel { get; set; }
    public int Difficulty { get; set; }
    public int Quality { get; set; }
    public int Durability { get; set; }
    
    // Result info
    public int ResultAmount { get; set; } = 1;  // Some recipes yield multiple items
    public bool CanBeHQ { get; set; }
    
    // Ingredients
    public List<RecipeIngredient> Ingredients { get; set; } = new();
    
    // Crystals
    public List<RecipeIngredient> Crystals { get; set; } = new();
    
    // Categories
    public uint RecipeLevelTable { get; set; }
    public uint ItemCategory { get; set; }
    public bool IsExpert { get; set; }
    public bool IsSpecialist { get; set; }
    
    // Estimated crafting time (for gil/hour calculations)
    public int EstimatedCraftTimeSeconds { get; set; } = 20;  // Default estimate
}
