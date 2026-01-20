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
    
    // DEV MODE: Set to true to auto-run tests on plugin load
    private const bool DEV_MODE = true;

    public Configuration Configuration { get; init; }
    
    // File logger for easier debugging
    private FileLogger? FileLog { get; set; }
    
    // Services
    public CacheService CacheService { get; init; }
    public DatabaseService DatabaseService { get; init; }
    public RecipeService RecipeService { get; init; }
    public UniversalisService UniversalisService { get; init; }
    public MarketAnalysisService MarketAnalysisService { get; init; }
    public RateLimiter RateLimiter { get; init; }
    public ProfitService ProfitService { get; init; }

    public readonly WindowSystem WindowSystem = new("Aurum");
    private ConfigWindow ConfigWindow { get; init; }
    private DashboardWindow DashboardWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // Initialize file logging
        var pluginDir = PluginInterface.AssemblyLocation.DirectoryName ?? Directory.GetCurrentDirectory();
        FileLog = new FileLogger(Log, pluginDir);
        Log.Information($"Plugin directory: {pluginDir}");
        Log.Information($"Log file: {FileLog.GetLogFilePath()}");

        // Initialize services
        Log.Information("Initializing Aurum services...");
        
        CacheService = new CacheService(Configuration);
        DatabaseService = new DatabaseService(Log, pluginDir);
        RateLimiter = new RateLimiter(Log, Configuration);
        RecipeService = new RecipeService(DataManager, Log);
        UniversalisService = new UniversalisService(Log, CacheService, DatabaseService, RateLimiter, Configuration);
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

        Log.Information($"Aurum v{PluginInterface.Manifest.AssemblyVersion} initialized successfully!");
        
        // DEV MODE: Auto-run tests
        if (DEV_MODE)
        {
            Log.Information("========================================");
            Log.Information("DEV MODE ENABLED - Auto-running tests");
            Log.Information("========================================");
            _ = RunDevModeTests();
        }
    }

    public void Dispose()
    {
        // Dispose services
        UniversalisService?.Dispose();
        DatabaseService?.Dispose();
        RateLimiter?.Dispose();
        
        // Unregister all actions to not leak anything during disposal of plugin
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        DashboardWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        
        Log.Information("Aurum disposed");
        FileLog?.Dispose();
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
            Log.Information("Health check complete. Check log file for details.");
        }
        else if (argsTrimmed == "test")
        {
            _ = RunIntegrationTest();
        }
        else if (argsTrimmed == "log")
        {
            var logPath = FileLog?.GetLogFilePath() ?? "Unknown";
            Log.Information($"Log file location: {logPath}");
            Log.Information("You can read this file to see all plugin output.");
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
    
    private async System.Threading.Tasks.Task RunDevModeTests()
    {
        try
        {
            // Wait a bit for services to fully initialize
            await System.Threading.Tasks.Task.Delay(2000);
            
            Log.Information("Starting DEV MODE automated tests...");
            Log.Information("");
            
            // Run integration test
            await RunIntegrationTest();
            
            Log.Information("");
            Log.Information("DEV MODE tests complete!");
            Log.Information($"Full log available at: {FileLog?.GetLogFilePath()}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DEV MODE tests failed");
        }
    }
    
    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => DashboardWindow.Toggle();
}
