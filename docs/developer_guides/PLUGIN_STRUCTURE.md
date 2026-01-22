# Plugin Structure Guide

This guide details the internal structure of the Aurum plugin to help developers navigate the codebase effectively.

## Project Organization

The solution consists of the following key projects:

- **Aurum**: The main Dalamud plugin project.
- **Aurum.IntegrationTests**: Standalone console application for testing core services outside of the game client.

## Directory Structure (Aurum/)

The main plugin code is organized by function:

```
Aurum/
├── Attributes/         # Custom attributes (e.g., Command attributes)
├── Models/             # Data transfer objects and internal models
│   ├── Config/         # Configuration models
│   └── Market/         # Market and recipe data models
├── Services/           # Core business logic and service orchestration
├── Windows/            # UI implementation using ImGui
├── Utils/              # Helper functions and extensions
└── Plugin.cs           # Entry point and lifecycle management
```

## Key Components

### 1. Entry Point (`Plugin.cs`)
- Implements `IDalamudPlugin`.
- Initializes the `ServiceManager`.
- Registers slash commands (`/aurum`).
- Sets up the `WindowSystem` for UI rendering.
- Handles disposal and cleanup on plugin unload.

### 2. Service Layer (`Services/`)
Aurum uses a service-oriented architecture. Services are singletons managed by `ServiceManager`.

- **`ServiceManager.cs`**: Container for dependency injection-style service management.
- **`UniversalisService.cs`**: Handles external API communication with Universalis.
- **`ProfitService.cs`**: Contains the core logic for calculating crafting profits.
- **`DatabaseService.cs`**: Manages the local SQLite cache.
- **`Configuration.cs`**: Manages user settings and persistence.

### 3. UI Layer (`Windows/`)
The UI is built using Dalamud's ImGui wrappers.

- **`MainWindow.cs`**: The primary dashboard interface.
- **`ConfigWindow.cs`**: Settings interface.
- **`DetailWindow.cs`**: Detailed item analysis view.

### 4. Data Models (`Models/`)
- **`MarketData`**: Raw data from Universalis.
- **`RecipeData`**: Game data structure for recipes.
- **`ProfitCalculation`**: The result of analyzing a recipe against market data.

## Data Flow

1. **Initialization**: `Plugin.cs` loads, initializes `ServiceManager`, and restores `Configuration`.
2. **User Action**: User opens the dashboard (`MainWindow`).
3. **Data Request**: `MainWindow` requests data from `ProfitService`.
4. **Resolution**:
   - `ProfitService` checks `DatabaseService` for cached data.
   - If missing or stale, it requests fresh data from `UniversalisService`.
   - `UniversalisService` fetches from the API (respecting rate limits).
5. **Calculation**: `ProfitService` combines market data with recipe data to compute profit.
6. **Rendering**: The result is returned to `MainWindow` for display.

## Extending Aurum

### Adding a New Service
1. Create a new class in `Services/` (e.g., `MyNewService.cs`).
2. Register it in `ServiceManager.InitializeServices()`.
3. Add a public property in `ServiceManager` to expose it.

### Adding a New Window
1. Create a class inheriting from `Window` in `Windows/`.
2. Instantiate it in `Plugin.cs`.
3. Add it to the `WindowSystem`.
4. Add a method to toggle its visibility (usually linked to a command or button).

## Debugging
- Use `Service<Log>.Info()` for logging.
- Check `aurum.log` in the bin folder for detailed output.
- Enable `DEV_MODE` in `Plugin.cs` to run automated integration tests on startup.
