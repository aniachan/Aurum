
## Wed Jan 21 2026 - Aurum-0at.4
- Implemented "API Settings" section in `ConfigWindow`.
- Added controls for `PreferredWorld`, `MarketDataCacheDurationSeconds`, `MaxConcurrentApiRequests`, `ApiRateLimitPerMinute`.
- Added tooltip for `PreferredWorld` to explain "Auto" behavior.
- **Learnings:**
  - `ImGui.InputText` requires a `maxLength` parameter.
  - `ImGui.SliderInt` is useful for bounded integer values like concurrent requests.
---
