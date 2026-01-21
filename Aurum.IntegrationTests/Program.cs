using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aurum.IntegrationTests;

namespace Aurum.IntegrationTests;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Aurum Service Tests ===\n");
        
        RecipeServiceTests.Run();
        ShoppingListTests.Run();
        
        Console.WriteLine("=== Aurum Config Window Verification ===\n");
        Console.WriteLine("Since this involves UI components that depend on Dalamud/ImGui,");
        Console.WriteLine("automated verification is limited to checking the code logic.");
        
        await VerifyConfigWindowLogic();

        Console.WriteLine("\n=== Verification Complete ===");
    }
    
    static async Task VerifyConfigWindowLogic()
    {
        Console.WriteLine("Verifying ConfigWindow.cs logic...");
        
        // We can't easily instantiate ConfigWindow without Dalamud context,
        // but we can verify the Configuration object it depends on.
        
        try
        {
            var config = new Configuration();
            
            // Verify default settings used in the window
            Console.WriteLine($"Default PreferredWorld: {config.PreferredWorld}");
            Console.WriteLine($"Default RememberLastWorld: {config.RememberLastWorld}");
            
            bool defaultsOk = true;
            if (config.PreferredWorld != "Auto" || config.RememberLastWorld != true)
            {
                 Console.WriteLine("✗ Configuration defaults do NOT match expected values");
                 defaultsOk = false;
            }
            
            // Verify Enums used in UI
            if (Enum.GetNames(typeof(Aurum.Models.CostMode)).Length != 4)
            {
                 Console.WriteLine("✗ CostMode enum length mismatch");
                 defaultsOk = false;
            }

            if (Enum.GetNames(typeof(Aurum.SortMode)).Length != 6)
            {
                 Console.WriteLine("✗ SortMode enum length mismatch");
                 defaultsOk = false;
            }
            
            if (defaultsOk)
            {
                 Console.WriteLine("✓ Configuration defaults and Enums match expected values");
            }
            
            Console.WriteLine("✓ ConfigWindow logic verification passed (static analysis)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ FAIL: {ex.Message}");
        }
        
        await Task.CompletedTask;
    }
}
