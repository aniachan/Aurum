# Architecture Overview

Aurum is a profit calculator and market analysis plugin for Final Fantasy XIV, built on the Dalamud plugin framework. It helps players find profitable crafting opportunities by analyzing market board data from Universalis.

## Core Architecture

Aurum follows a service-oriented architecture with a clear separation of concerns:

- **Plugin Entry Point (`Plugin.cs`)**: Initializes services, registers commands, and manages the window system.
- **Service Layer**: Handles business logic, data fetching, and calculations.
- **Data Layer**: Manages local data persistence using SQLite.
- **UI Layer**: Implements the user interface using ImGui (via Dalamud).

### Data Flow

1. **User Request**: User opens the dashboard or requests a refresh.
2. **Recipe Service**: Fetches crafting recipes based on filters (level, job, etc.).
3. **Universalis Service**: Fetches market data (prices, sales history) for the recipe items and their ingredients.
4. **Profit Service**: Calculates production costs and potential profit based on market data.
5. **Item Priority Service**: Ranks items based on profitability, demand, and user preferences.
6. **UI Display**: Results are displayed in the Dashboard or Shopping List windows.

## Key Components

### Services

*   **`UniversalisService`**: Interacts with the Universalis API to fetch market data. Handles caching and rate limiting.
*   **`RecipeService`**: Retrieves recipe information from the game data (Lumina).
*   **`ProfitService`**: The core calculation engine. Computes net profit, profit margin, and risk scores.
*   **`MarketAnalysisService`**: Analyzes market trends (velocity, volatility) to determine item demand and risk.
*   **`DatabaseService`**: Manages the local SQLite database for caching market data and historical trends.
*   **`RefreshService`**: Orchestrates background data refreshes to keep market data up-to-date.
*   **`ShoppingListService`**: Manages the user's shopping list for crafting materials.

### Data Models

*   **`MarketData`**: Represents market board info (min price, average price, velocity) for a specific item.
*   **`RecipeData`**: Contains recipe details (ingredients, level, job).
*   **`ProfitCalculation`**: Stores the results of a profit analysis (cost, revenue, profit, margin).
*   **`Configuration`**: Persists user settings (filters, risk tolerance, UI preferences).

### User Interface

*   **`DashboardWindow`**: The main view, showing a list of profitable crafting opportunities.
*   **`DetailWindow`**: Shows detailed breakdown for a specific item (profit calc, price history, ingredient costs).
*   **`ShoppingListWindow`**: Displays the aggregated list of materials needed for selected recipes.
*   **`ConfigWindow`**: Allows users to customize plugin settings.

## Database Schema

Aurum uses SQLite (`aurum.db`) for local storage. Key tables include:

*   **`MarketData`**: Caches the latest market snapshot for items to reduce API calls.
*   **`PriceHistory`**: Stores historical price points and sales for trend analysis.
*   **`RecipeCache`**: Caches profit calculations to speed up dashboard rendering.
*   **`ItemPriorities`**: Stores calculated priority scores for items.
*   **`VelocityHistory`**: Tracks sales velocity over time.

## External Dependencies

*   **Dalamud**: The plugin framework providing hooks into the game client.
*   **Lumina**: Library for reading FFXIV game data (Excel sheets).
*   **Universalis API**: Third-party API for market board data.
*   **SQLite**: Embedded database for local caching.

## Performance Considerations

*   **Caching**: Aggressive caching (RAM and DB) is used to minimize Universalis API requests and improve responsiveness.
*   **Rate Limiting**: `RateLimiter` ensures the plugin respects Universalis API limits.
*   **Background Processing**: Heavy calculations and data fetches are performed asynchronously to avoid freezing the game client.
