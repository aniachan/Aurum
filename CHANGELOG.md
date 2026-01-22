# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### ⚠️ Migration Notes & Breaking Changes
- **Database Schema v5**: The local database (`aurum.db`) will automatically migrate to schema version 5 on startup to support `gil_per_hour` caching.
    - **Important**: Once migrated, the database **cannot be used with older versions** of Aurum. If you need to downgrade, you must delete `aurum.db` to reset the cache.
- **UniversalisService Constructor**: The constructor now requires `IDataManager` to support world/DC resolution.
    - *Impact*: Custom integrations or unit tests instantiating `UniversalisService` manually must be updated to provide this dependency.
- **Cross-World Data**: `MarketListing` and `SaleRecord` models now include a `WorldName` property.
    - *Impact*: Data fetched may now come from multiple worlds. Consumers should check `WorldName` rather than assuming all data belongs to the currently logged-in world.

### Added
- **Cross-World Market Analysis**: Added support for fetching and analyzing data across the entire Data Center.
- **Offline Mode**: Added `WorkOffline` configuration to disable all network requests (uses cached data only).
- **Profit Analysis Improvements**:
    - Added `EfficiencyScore` (profit/cost ratio).
    - Added `GatheringTimeSeconds` calculation for self-gathered materials.
    - Added `AlternativeSuggestions` to recommend better items to craft.
- **Performance**: Implemented `CalculateProfitsStreamAsync` for streaming results in `ProfitService`.

### Documentation
- Detailed contributing guidelines.
- API integration guide for developers.
- Database schema diagram.
- Architecture overview.
- User guide updates:
    - Configuration section.
    - Market warnings section.
    - Understanding risk scores.
    - Dashboard usage.

## [0.0.0.2] - 2026-01-22

### Added
- Initial project structure and documentation.
