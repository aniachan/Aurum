using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System;
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
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/aurum";

    public Configuration Configuration { get; init; }

    // Services
    public CacheService CacheService { get; init; }
    public DatabaseService DatabaseService { get; init; }
    public RecipeService RecipeService { get; init; }
    public UniversalisService UniversalisService { get; init; }
    public MarketAnalysisService MarketAnalysisService { get; init; }
    public RateLimiter RateLimiter { get; init; }
    public RequestQueue RequestQueue { get; init; }
    public ProfitService ProfitService { get; init; }
    public ShoppingListService ShoppingListService { get; init; }
    public RefreshService RefreshService { get; init; }

    public static Plugin? Instance { get; private set; }

    public readonly WindowSystem WindowSystem = new("Aurum");
    private ConfigWindow ConfigWindow { get; init; }
    private DashboardWindow DashboardWindow { get; init; }
    public DetailWindow DetailWindow { get; init; }
    public FilterWindow FilterWindow { get; init; }
    public ShoppingListWindow ShoppingListWindow { get; init; }

    public Plugin()
    {
        Instance = this;
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        var pluginDir = PluginInterface.AssemblyLocation.DirectoryName ?? System.IO.Directory.GetCurrentDirectory();

        Log.Information("Initializing Aurum services...");

        CacheService = new CacheService(Configuration);
        DatabaseService = new DatabaseService(Log, pluginDir);
        MarketAnalysisService = new MarketAnalysisService(Log, Configuration);

        DatabaseService.CheckAndRunVacuum(
            Configuration.DatabaseVacuumFrequencyDays,
            Configuration.LastDatabaseVacuum,
            (completedAt) =>
            {
                Configuration.LastDatabaseVacuum = completedAt;
                Configuration.Save();
            }
        );

        RequestQueue = new RequestQueue();
        RateLimiter = new RateLimiter(Log, Configuration, ChatGui, DatabaseService);
        RecipeService = new RecipeService(DataManager, Log, Configuration);
        UniversalisService = new UniversalisService(Log, CacheService, DatabaseService, RateLimiter, Configuration, DataManager);
        ProfitService = new ProfitService(Log, Configuration, RecipeService, UniversalisService, MarketAnalysisService);
        ShoppingListService = new ShoppingListService(DataManager, Log, RecipeService, UniversalisService);
        RefreshService = new RefreshService(Log, Configuration, UniversalisService, DatabaseService, RecipeService, ClientState);

        RecipeService.Initialize();
        ShoppingListService.Initialize();

        ConfigWindow = new ConfigWindow(this);
        DashboardWindow = new DashboardWindow(this);
        DetailWindow = new DetailWindow(this);
        FilterWindow = new FilterWindow(this);
        ShoppingListWindow = new ShoppingListWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(DashboardWindow);
        WindowSystem.AddWindow(DetailWindow);
        WindowSystem.AddWindow(FilterWindow);
        WindowSystem.AddWindow(ShoppingListWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Aurum crafting profit calculator"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information($"Aurum v{PluginInterface.Manifest.AssemblyVersion} initialized successfully!");
    }

    public void Dispose()
    {
        RefreshService?.Dispose();
        UniversalisService?.Dispose();
        DatabaseService?.Dispose();
        RateLimiter?.Dispose();

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        DashboardWindow.Dispose();
        DetailWindow.Dispose();
        FilterWindow.Dispose();
        ShoppingListWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);

        Log.Information("Aurum disposed");
    }

    private void OnCommand(string command, string args)
    {
        var argsTrimmed = args.Trim().ToLower();
        if (argsTrimmed == "config")
            ToggleConfigUi();
        else
            ToggleMainUi();
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => DashboardWindow.Toggle();
}
