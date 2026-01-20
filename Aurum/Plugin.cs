using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System;
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
        
        var stats = RecipeService.GetStats();
        Log.Information($"Loaded {stats.TotalRecipes} recipes from game data");

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
        if (args.Trim().Equals("config", StringComparison.OrdinalIgnoreCase))
        {
            ToggleConfigUi();
        }
        else
        {
            // Default: toggle main window
            ToggleMainUi();
        }
    }
    
    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => DashboardWindow.Toggle();
}
