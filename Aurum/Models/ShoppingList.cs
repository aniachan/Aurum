using System;
using System.Collections.Generic;
using System.Linq;

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

    public string ToClipboardString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Aurum Shopping List");
        sb.AppendLine($"Created: {CreatedAt}");
        sb.AppendLine();
        
        sb.AppendLine("=== Materials Needed ===");
        foreach (var item in Items.OrderBy(i => i.SourceType).ThenBy(i => i.ItemName))
        {
            sb.AppendLine($"[ ] {item.AmountNeeded}x {item.ItemName} ({item.SourceType}) - approx. {item.TotalCost:N0} gil");
        }
        
        sb.AppendLine();
        sb.AppendLine($"Total Estimated Cost: {TotalEstimatedCost:N0} gil");
        
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
}

public enum MaterialSourceType
{
    MarketBoard,
    Vendor,
    Gathering,
    Craftable
}
