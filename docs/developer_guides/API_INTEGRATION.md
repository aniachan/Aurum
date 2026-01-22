# API Integration Guide

This guide describes how to integrate with Aurum's various services and APIs. It is intended for developers who want to extend Aurum or understand its internal data flow.

## Core Services

Aurum's functionality is divided into several core services managed by the `ServiceManager` (implicitly via `Plugin.cs` initialization).

### 1. UniversalisService
**Purpose:** Handles all communication with the Universalis API.
**Location:** `Aurum/Services/UniversalisService.cs`

- **Key Methods:**
    - `GetMarketData(uint worldId, uint itemId)`: Fetches current market data.
    - `GetMarketDataBatch(uint worldId, List<uint> itemIds)`: Fetches data for multiple items (optimized).
    - `GetHistory(uint worldId, uint itemId)`: Fetches sales history.

**Usage Example:**
```csharp
var marketData = await _universalisService.GetMarketData(worldId, itemId);
if (marketData != null) {
    var minPrice = marketData.MinPrice;
}
```

### 2. ProfitService
**Purpose:** Calculates profit margins, costs, and risk scores.
**Location:** `Aurum/Services/ProfitService.cs`

- **Key Methods:**
    - `CalculateProfit(Recipe recipe)`: Returns a `ProfitCalculation` object containing detailed cost breakdown.
    - `CalculateRisk(MarketData data)`: Returns a risk score (0-100).

**Data Model (ProfitCalculation):**
- `Cost`: Total material cost.
- `Revenue`: Expected selling price.
- `Profit`: Revenue - Cost.
- `Margin`: (Profit / Cost) * 100.

### 3. DatabaseService
**Purpose:** Manages local SQLite storage for caching.
**Location:** `Aurum/Services/DatabaseService.cs`

**Schema Access:**
- The database schema is defined in `docs/DATABASE_SCHEMA.md`.
- Use `GetConnection()` to access the raw SQLite connection if needed (discouraged; use service methods).

### 4. RecipeService
**Purpose:** Interfaces with Lumina to read game data.
**Location:** `Aurum/Services/RecipeService.cs`

- **Key Methods:**
    - `GetRecipesByLevel(int min, int max)`
    - `GetRecipesByJob(ClassJob job)`

## Integration Patterns

### Adding a New Data Source
If you want to add a new price source (e.g., a different website):
1. Create a service implementing `IMarketDataSource` (interface to be defined if multiple sources supported).
2. Register it in `Plugin.cs`.
3. Update `ProfitService` to use the new source.

### Event Handling
Aurum uses a basic event system for updates.
- **`MarketDataUpdated`**: Fired when new data arrives from Universalis.
- **`ConfigChanged`**: Fired when the user modifies settings.

## Rate Limiting
All API calls must go through the `RateLimiter` to avoid bans.
- **Default Limit:** 25 requests/second (internal burst limit).
- **Service:** `Aurum/Services/RateLimiter.cs`

## Best Practices
1. **Always Cache:** Never call `UniversalisService` directly in a render loop (ImGui). Use the cached data in `MarketDataPool` or the local DB.
2. **Async/Await:** All I/O operations must be asynchronous to prevent freezing the game client.
3. **Error Handling:** Network calls can fail. Always wrap API calls in `try/catch` blocks and log errors using `Service<Log>`.
