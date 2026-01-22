# Known Issues

This document tracks known issues, limitations, and planned improvements for Aurum.

## Core Services

### ItemPriorityService
- **Categories**: Currently relies on placeholder logic for category scoring. Needs refinement to use specific ItemCategory or UI Category IDs from game data.
  - *Location*: `Aurum/Services/ItemPriorityService.cs` (ScoreCategory method)

### UniversalisService
- **Synchronous Blocking**: The `GetMarketData` method blocks the thread, which is suboptimal for the game loop. Needs full async propagation.
  - *Location*: `Aurum/Services/UniversalisService.cs` (GetMarketData method)
- **World ID Mapping**: `GetMarketData` currently relies on hardcoded fallbacks or basic ID-to-string conversion if the world map isn't fully populated.
  - *Location*: `Aurum/Services/UniversalisService.cs` (GetMarketData method)

### ShoppingListService
- **Vendor Data**: Currently uses a hardcoded fallback list for vendor items. Needs to properly parse `GilShop` and `GilShopItem` excel sheets to support all vendor items dynamically.
  - *Location*: `Aurum/Services/ShoppingListService.cs` (Initialize method)

### DebugWindow
- **Selection Logic**: The Debug Window currently lacks implementation for specific item selection logic within its debug views.
  - *Location*: `Aurum/Windows/DebugWindow.cs` (Draw method)

## UI & UX
- **Async Loading**: Some UI components may freeze momentarily when fetching fresh data if not fully decoupled from the main thread.
- **Cross-World**: Cross-world market analysis is available but may be slower than single-world due to aggregation overhead.

## Data Accuracy
- **Market Lag**: Data from Universalis is user-contributed and may lag behind real-time in-game prices, especially for low-velocity items.
- **Sales History**: Recent sales history assumes the `WorldName` field is always populated correctly by the API, but older uploads might miss this context.

## Integration
- **Teamcraft**: Direct integration for export/import is planned but not yet implemented (Tracked in `Aurum-8ic.4.4`).
