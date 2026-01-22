# Database Schema

Aurum uses a local SQLite database (`aurum.db`) to cache market data, track history, and store analysis results. This persistence layer is critical for reducing API calls to Universalis and providing a responsive user experience.

## Entity Relationship Diagram

```mermaid
erDiagram
    MarketData {
        int item_id PK "Composite PK"
        int world_id PK "Composite PK"
        int last_updated
        int min_price
        real average_price
        int listing_count
        real velocity
        text current_listings_json "JSON"
        text recent_sales_json "JSON"
        real sales_per_day
        real demand_ratio
    }

    PriceHistory {
        int id PK
        int item_id FK
        int world_id FK
        int timestamp
        int price
        int quantity
        boolean is_sale "true=sale, false=snapshot"
    }

    VelocityHistory {
        int item_id PK "Composite PK"
        int world_id PK "Composite PK"
        int timestamp PK "Composite PK"
        real velocity
    }

    RecipeCache {
        int recipe_id PK
        int item_id
        int last_analyzed
        int profit_snapshot
        real margin_snapshot
        int risk_score
        int recommendation_score
        int gil_per_hour
        text ingredients_json "JSON"
    }

    ItemMetadata {
        int item_id PK
        text name
        int item_level
        int category_id
        boolean can_be_hq
        boolean is_marketable
    }

    ItemPriorities {
        int item_id PK
        int priority_score
        int last_calculated
    }

    ApiRequestLog {
        int id PK
        text endpoint
        int timestamp
        int response_time_ms
        int status_code
        boolean success
    }

    SchemaVersion {
        int version PK
        int applied_at
    }

    MarketData ||--o{ PriceHistory : "history"
    MarketData ||--o{ VelocityHistory : "velocity trend"
    ItemMetadata ||--o{ MarketData : "market info"
    ItemMetadata ||--o{ RecipeCache : "recipe result"
    ItemMetadata ||--o{ ItemPriorities : "priority"
```

## Tables Detail

### MarketData
Stores the latest snapshot of market board data for an item on a specific world. This is the primary cache for Universalis data.
- **item_id**: The unique ID of the item (GameData).
- **world_id**: The ID of the world (server).
- **last_updated**: Unix timestamp of when this data was fetched from Universalis.
- **current_listings_json**: JSON array of current listings (prices, retainers).
- **recent_sales_json**: JSON array of recent sales history.

### PriceHistory
Tracks granular price points over time. Used for generating charts and analyzing trends.
- **is_sale**:
  - `true`: Represents a completed transaction (sale).
  - `false`: Represents a snapshot of the minimum listing price at a point in time.

### VelocityHistory
Tracks the "sale velocity" (sales per day) metric over time. This helps identify if an item is becoming more or less popular.

### RecipeCache
Stores the results of profit calculations. Since calculating profit involves traversing ingredient trees and market data, this cache prevents re-calculation for every frame/refresh.
- **profit_snapshot**: Net profit calculated at `last_analyzed`.
- **risk_score**: Calculated risk metric (0-100).
- **recommendation_score**: overall score combining profit, velocity, and risk.

### ItemMetadata
Static or semi-static data about items (Name, Level, Category). Used to avoid constant lookups in Lumina/GameData for basic info.

### ItemPriorities
Stores the "priority score" for items, which determines their order in the dashboard. High priority items are refreshed more frequently.

### ApiRequestLog
Logs outgoing HTTP requests to Universalis. Used for debugging and verifying rate limiter behavior.

### SchemaVersion
Tracks applied database migrations. Used by `DatabaseService` to automatically apply schema updates on startup.
