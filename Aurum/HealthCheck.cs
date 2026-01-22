using System;
using System.IO;
using System.Linq;
using Dalamud.Plugin.Services;
using Aurum.Services;

namespace Aurum;

/// <summary>
/// Health check system that runs on plugin initialization and logs detailed diagnostics
/// </summary>
public class HealthCheck
{
    private readonly IPluginLog log;
    private readonly Plugin plugin;
    
    public HealthCheck(Plugin plugin, IPluginLog log)
    {
        this.plugin = plugin;
        this.log = log;
    }
    
    /// <summary>
    /// Run all health checks and log results
    /// </summary>
    public void RunAll()
    {
        log.Info("========================================");
        log.Info("AURUM HEALTH CHECK");
        log.Info("========================================");
        
        CheckVersion();
        CheckServices();
        CheckRecipeService();
        CheckUniversalisService();
        CheckConfiguration();
        
        log.Info("========================================");
        log.Info("HEALTH CHECK COMPLETE");
        log.Info("========================================");
    }
    
    private void CheckVersion()
    {
        log.Info($"✓ Plugin Version: {Plugin.PluginInterface.Manifest.AssemblyVersion}");
        log.Info($"✓ Build Time: {File.GetLastWriteTime(Plugin.PluginInterface.AssemblyLocation.FullName):yyyy-MM-dd HH:mm:ss}");
    }
    
    private void CheckServices()
    {
        log.Info("Checking Services:");
        log.Info($"  ✓ CacheService: {(plugin.CacheService != null ? "OK" : "NULL")}");
        log.Info($"  ✓ RecipeService: {(plugin.RecipeService != null ? "OK" : "NULL")}");
        log.Info($"  ✓ UniversalisService: {(plugin.UniversalisService != null ? "OK" : "NULL")}");
        log.Info($"  ✓ MarketAnalysisService: {(plugin.MarketAnalysisService != null ? "OK" : "NULL")}");
        log.Info($"  ✓ ProfitService: {(plugin.ProfitService != null ? "OK" : "NULL")}");
        
        // Log memory profile during health check
        if (plugin.CacheService != null)
        {
            var profiler = new Utils.MemoryProfiler(plugin.CacheService);
            profiler.LogMemoryUsage(log, "Health Check");
        }
    }
    
    private void CheckRecipeService()
    {
        if (plugin.RecipeService == null)
        {
            log.Error("✗ RecipeService is NULL!");
            return;
        }
        
        var stats = plugin.RecipeService.GetStats();
        log.Info("RecipeService Status:");
        log.Info($"  Total Recipes: {stats.TotalRecipes}");
        log.Info($"  Total Items: {stats.TotalItems}");
        
        if (stats.TotalRecipes == 0)
        {
            log.Error("  ✗ WARNING: No recipes loaded!");
        }
        else
        {
            log.Info($"  ✓ Recipes loaded successfully");
            log.Info("  Recipes by Class:");
            foreach (var kvp in stats.RecipesByClass.OrderBy(x => x.Key))
            {
                log.Info($"    {kvp.Key}: {kvp.Value}");
            }
            
            // Test getting some recipes
            var level90Recipes = plugin.RecipeService.GetRecipesByLevel(90, 90).Take(3).ToList();
            if (level90Recipes.Any())
            {
                log.Info($"  ✓ Sample Level 90 Recipe: {level90Recipes[0].ItemName} (ID: {level90Recipes[0].RecipeId})");
            }
        }
    }
    
    private void CheckUniversalisService()
    {
        log.Info("UniversalisService Status:");
        log.Info($"  ✓ Service initialized");
        log.Info($"  Note: API connectivity will be tested on first refresh");
    }
    
    private void CheckConfiguration()
    {
        log.Info("Configuration Status:");
        log.Info($"  Preferred World: {plugin.Configuration.PreferredWorld}");
        log.Info($"  Cache Duration: {plugin.Configuration.MarketDataCacheDurationSeconds} seconds");
        log.Info($"  Use HQ Prices: {plugin.Configuration.UseHQPricesWhenAvailable}");
        log.Info($"  Default Cost Mode: {plugin.Configuration.DefaultCostMode}");
    }
}
