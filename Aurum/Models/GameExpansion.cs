namespace Aurum.Models;

/// <summary>
/// FFXIV expansions for filtering content
/// </summary>
public enum GameExpansion
{
    ARealmReborn = 0,      // 2.x (Patch 2.0-2.55)
    Heavensward = 1,       // 3.x (Patch 3.0-3.5x)
    Stormblood = 2,        // 4.x (Patch 4.0-4.5x)
    Shadowbringers = 3,    // 5.x (Patch 5.0-5.5x)
    Endwalker = 4,         // 6.x (Patch 6.0-6.5x)
    Dawntrail = 5          // 7.x (Patch 7.0+)
}

public static class GameExpansionExtensions
{
    /// <summary>
    /// Get the patch range for an expansion (major version)
    /// </summary>
    public static (int minPatch, int maxPatch) GetPatchRange(this GameExpansion expansion)
    {
        return expansion switch
        {
            GameExpansion.ARealmReborn => (20000, 29999),    // 2.0000 - 2.9999
            GameExpansion.Heavensward => (30000, 39999),     // 3.0000 - 3.9999
            GameExpansion.Stormblood => (40000, 49999),      // 4.0000 - 4.9999
            GameExpansion.Shadowbringers => (50000, 59999),  // 5.0000 - 5.9999
            GameExpansion.Endwalker => (60000, 69999),       // 6.0000 - 6.9999
            GameExpansion.Dawntrail => (70000, 79999),       // 7.0000 - 7.9999
            _ => (0, int.MaxValue)
        };
    }

    /// <summary>
    /// Get display name for expansion
    /// </summary>
    public static string GetDisplayName(this GameExpansion expansion)
    {
        return expansion switch
        {
            GameExpansion.ARealmReborn => "A Realm Reborn (2.x)",
            GameExpansion.Heavensward => "Heavensward (3.x)",
            GameExpansion.Stormblood => "Stormblood (4.x)",
            GameExpansion.Shadowbringers => "Shadowbringers (5.x)",
            GameExpansion.Endwalker => "Endwalker (6.x)",
            GameExpansion.Dawntrail => "Dawntrail (7.x)",
            _ => "Unknown"
        };
    }
}
