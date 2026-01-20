using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System;
using System.Linq;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Aurum.Windows;
using Aurum.Services;

namespace Aurum;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/aurum";
    private const string ConfigCommandName = "/aurum config";
    private const string HealthCheckCommand = "/aurum health";

    public Configuration Configuration { get; init; }
    
    // Services
    public CacheService CacheService { get; init; }
    public RecipeService RecipeService { get; init; }
    public UniversalisService UniversalisService { get; init; }
    public MarketAnalysisService MarketAnalysisService { get; init; }
    public ProfitService ProfitService { get; init; }

    public readonly WindowSystem WindowSystem = new("Aurum");
    private ConfigWindow ConfigWindow { get; init; }
    private DashboardWindow DashboardWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // Initialize services
        Log.Information("Initializing Aurum services...");
        
        CacheService = new CacheService(Configuration);
        RecipeService = new RecipeService(DataManager, Log);
        UniversalisService = new UniversalisService(Log, CacheService);
        MarketAnalysisService = new MarketAnalysisService(Log, Configuration);
        ProfitService = new ProfitService(Log, Configuration, RecipeService, UniversalisService, MarketAnalysisService);
        
        // Initialize recipe service asynchronously
        RecipeService.Initialize();
        
        // Run health check
        var healthCheck = new HealthCheck(this, Log);
        healthCheck.RunAll();

        // Initialize windows
        ConfigWindow = new ConfigWindow(this);
        DashboardWindow = new DashboardWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(DashboardWindow);

        // Register commands
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Aurum crafting profit calculator"
        });

        // Tell the UI system that we want our windows to be drawn through the window system
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // This adds a button to the plugin installer entry of this plugin which allows
        // toggling the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        // Adds another button doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information($"Aurum v{PluginInterface.Manifest.AssemblyVersion} (DEBUG BUILD) initialized successfully!");
    }

    public void Dispose()
    {
        // Dispose services
        UniversalisService?.Dispose();
        
        // Unregister all actions to not leak anything during disposal of plugin
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        DashboardWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        
        Log.Information("Aurum disposed");
    }

    private void OnCommand(string command, string args)
    {
        // Handle subcommands
        var argsTrimmed = args.Trim().ToLower();
        
        if (argsTrimmed == "config")
        {
            ToggleConfigUi();
        }
        else if (argsTrimmed == "health")
        {
            var healthCheck = new HealthCheck(this, Log);
            healthCheck.RunAll();
            Log.Information("Health check complete. Check Dalamud log for details.");
        }
        else if (argsTrimmed == "test")
        {
            _ = RunIntegrationTest();
        }
        else
        {
            // Default: toggle main window
            ToggleMainUi();
        }
    }
    
    private async System.Threading.Tasks.Task RunIntegrationTest()
    {
        Log.Information("========================================");
        Log.Information("RUNNING INTEGRATION TEST");
        Log.Information("========================================");
        
        try
        {
            // Test 1: Fetch market data for a known item
            Log.Information("Test 1: Fetching market data for Darksteel Ore (ID: 5114)");
            var marketData = await UniversalisService.GetMarketDataAsync("Gilgamesh", 5114);
            
            if (marketData != null)
            {
                Log.Information($"  ✓ Current Listings: {marketData.CurrentListings}");
                Log.Information($"  ✓ Current Avg Price: {marketData.CurrentAveragePrice}");
                Log.Information($"  ✓ Recent Sales: {marketData.RecentSales}");
                Log.Information($"  ✓ Sale Velocity: {marketData.SaleVelocity:F2} sales/day");
            }
            else
            {
                Log.Error("  ✗ Failed to fetch market data");
            }
            
            // Test 2: Calculate profit for a recipe
            Log.Information("Test 2: Calculating profit for level 90 recipe");
            var recipes = RecipeService.GetRecipesByLevel(90, 90).Take(1).ToList();
            if (recipes.Any())
            {
                var recipe = recipes[0];
                Log.Information($"  Testing recipe: {recipe.ItemName}");
                
                var profit = await ProfitService.CalculateProfitAsync(recipe, "Gilgamesh");
                if (profit != null)
                {
                    Log.Information($"  ✓ Raw Profit: {profit.RawProfit} gil");
                    Log.Information($"  ✓ Profit Margin: {profit.ProfitMargin:F1}%");
                    Log.Information($"  ✓ Risk Score: {profit.RiskScore}");
                    Log.Information($"  ✓ Recommendation Score: {profit.RecommendationScore}");
                }
                else
                {
                    Log.Error("  ✗ Failed to calculate profit");
                }
            }
            else
            {
                Log.Error("  ✗ No level 90 recipes found");
            }
            
            Log.Information("========================================");
            Log.Information("INTEGRATION TEST COMPLETE");
            Log.Information("========================================");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Integration test failed");
        }
    }
    
    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => DashboardWindow.Toggle();
}
