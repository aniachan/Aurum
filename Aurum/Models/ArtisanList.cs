// Model for Artisan-compatible list exports

using System.Collections.Generic;

namespace Aurum.Models;

public class ArtisanList
{
    public bool SkipLiteral { get; set; } = false;
    public uint RepairPercent { get; set; } = 50;
    public bool AddAsQuickSynth { get; set; } = false;
    public bool TidyAfter { get; set; } = true;
    public bool OnlyRestockNonCrafted { get; set; } = false;
    public uint ID { get; set; } = 0;
    public string Name { get; set; } = "";

    public List<ArtisanListRecipe> Recipes { get; set; } = new();
    public List<uint> ExpandedList { get; set;} = [];
    public bool SkipIfEnough { get; set; } = false;
    public bool Materia { get; set; } = false;
    public bool Repair { get; set; } = true;
}

public class ArtisanListRecipe
{
    public uint ID { get; set; }
    public uint Quantity { get; set; }

    public ArtisanListItemOptions ListItemOptions { get; set; } = new();
}

public class ArtisanListItemOptions
{
    public bool NQOnly { get; set; } = false;
    public bool Skipping { get; set; } = false;
}
