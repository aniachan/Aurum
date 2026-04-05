using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Lumina.Excel.Sheets;

namespace Aurum.Models;

/// <summary>
/// Represents a consolidated shopping list for crafting multiple items
/// </summary>
public class ShoppingList
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ShoppingListItem> Items { get; set; } = new();
    public int TotalEstimatedCost { get; set; }
    
    // Items we are crafting that generated this list
    public List<CraftingTarget> Targets { get; set; } = new();

    // Optimal crafting order to achieve the targets
    public List<CraftingStep> CraftingSteps { get; set; } = new();

    public string ToClipboardString()
    {
        var rng = new Random();
        var al = new ArtisanList
        {
            ID = (uint) rng.Next(0,65535),
            Name = $"Aurum - {CreatedAt}"
        };

        foreach (var step in CraftingSteps)
        {
            var r = new ArtisanListRecipe
            {
                ID = step.RecipeId,
                Quantity = (uint) step.Quantity
            };

            al.Recipes.Add(r);
            al.ExpandedList.Add(step.RecipeId);
        }

        return JsonSerializer.Serialize(al);
    }

    public string ToCsvString()
    {
        var sb = new System.Text.StringBuilder();
        // Header
        sb.AppendLine("Item Name,Quantity,Source Type,Vendor Location,Est. Cost (Gil)");
        
        foreach (var item in Items.OrderBy(i => i.SourceType).ThenBy(i => i.ItemName))
        {
            // Escape quotes if necessary, though simple item names usually don't have them
            var safeName = item.ItemName.Contains(",") ? $"\"{item.ItemName}\"" : item.ItemName;
            var safeLocation = item.VendorLocation.Contains(",") ? $"\"{item.VendorLocation}\"" : item.VendorLocation;
            
            sb.AppendLine($"{safeName},{item.AmountNeeded},{item.SourceType},{safeLocation},{item.TotalCost}");
        }
        
        sb.AppendLine($",,,TOTAL,{TotalEstimatedCost}");
        
        return sb.ToString();
    }

    public string ToTeamcraftJson()
    {
        var sb = new System.Text.StringBuilder();
        // Simple JSON format for Teamcraft import
        sb.Append($"{{\"name\":\"Aurum List {DateTime.Now:MM-dd HH:mm}\",\"items\":[");
        
        var items = Items.OrderBy(i => i.SourceType).ThenBy(i => i.ItemName).ToList();
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            // 0 for recipe means it might try to auto-detect or just treat as item
            sb.Append($"{{\"id\":{item.ItemId},\"amount\":{item.AmountNeeded},\"scrip\":0,\"hq\":false}}");
            if (i < items.Count - 1) sb.Append(",");
        }
        
        sb.Append("]}");
        return sb.ToString();
    }
}

public class CraftingTarget
{
    public uint RecipeId { get; set; }
    public uint ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int AmountToCraft { get; set; }
}

public class ShoppingListItem
{
    public uint ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public uint IconId { get; set; }
    public int AmountNeeded { get; set; }
    
    // Cost analysis
    public int AveragePricePerUnit { get; set; }
    public int TotalCost => AmountNeeded * AveragePricePerUnit;
    
    // Sourcing
    public MaterialSourceType SourceType { get; set; }
    public string VendorLocation { get; set; } = string.Empty; // If vendor purchasable
    public string CheapestWorld { get; set; } = string.Empty;
}

public enum MaterialSourceType
{
    MarketBoard,
    Vendor,
    Gathering,
    Craftable
}

public class CraftingStep
{
    public int StepIndex { get; set; }
    public uint ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public uint IconId { get; set; }
    public uint RecipeId { get; set; }
    public int Quantity { get; set; }
    
    // Efficiency metrics
    public int BatchSize { get; set; }     // e.g. yields 3 per craft
    public int TotalCrafts { get; set; }   // Number of crafting actions
    
    // Ingredients needed for THIS step (not full recursive tree)
    public List<ShoppingListItem> Ingredients { get; set; } = new();
}
