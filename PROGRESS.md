# Aurum Plugin - Development Progress

**Last Updated:** 2026-01-20 21:06 (Build time from logs)  
**Evidence Source:** Dalamud log + Build outputs

---

## ✅ CONFIRMED WORKING (With Evidence)

### Plugin Infrastructure
- ✅ **Plugin loads successfully** - Evidence: `[INF] [LocalPlugin] Finished loading Aurum` (21:06:10)
- ✅ **Auto-reload on build** - Evidence: Plugin reloaded at 21:06:10, build completed at 21:06:05
- ✅ **Service initialization** - Evidence: All services marked OK in health check
- ✅ **Version tracking** - Evidence: `✓ Plugin Version: 0.0.0.2`

### Recipe Loading
- ✅ **RecipeService loads recipes** - Evidence: `RecipeService initialized with 13393 recipes`
- ✅ **Recipe breakdown by class:**
  - ALC: 1561
  - ARM: 1419
  - BSM: 1911
  - CRP: 1528
  - CUL: 1037
  - GSM: 2189
  - LTW: 1823
  - WVR: 1925
- ✅ **Item database loaded** - Evidence: `Total Items: 50900`
- ✅ **Recipe queries work** - Evidence: `✓ Sample Level 90 Recipe: Chondrite Saw (ID: 5166)`

### Health Check System
- ✅ **Health check runs automatically on load** - Evidence: Log shows health check at 21:06:10
- ✅ **All services report OK**:
  - CacheService: OK
  - RecipeService: OK
  - UniversalisService: OK
  - MarketAnalysisService: OK
  - ProfitService: OK

### DEV_MODE Testing
- ✅ **DEV_MODE enabled** - Evidence: `DEV MODE ENABLED - Auto-running tests`
- ✅ **Auto-test delay works** - Evidence: 2 second delay between init and test start
- ✅ **Integration test executes** - Evidence: Test sequence ran completely

### API Communication
- ✅ **HTTP requests work** - Evidence: API URLs logged successfully
  - `https://universalis.app/api/v2/Gilgamesh/5114?listings=20&entries=50`
  - `https://universalis.app/api/v2/Gilgamesh/35383?listings=20&entries=50`
- ✅ **Network connectivity confirmed** - Evidence: Responses received (even if parsing failed)

---

## ❌ CONFIRMED BROKEN (With Evidence)

### JSON Parsing Errors
- ❌ **Price field type mismatch** - Evidence:
  ```
  System.Text.Json.JsonException: The JSON value could not be converted to System.UInt32. 
  Path: $.currentAveragePriceNQ | LineNumber: 0 | BytePositionInLine: 13051
  ```
  - **Root Cause:** API returns `"currentAveragePriceNQ":472.05` (double) but model expects `uint`
  - **Affected Fields:**
    - `currentAveragePriceNQ`
    - `currentAveragePriceHQ`
    - Likely: `minPrice`, `maxPrice`, `averagePrice` variants

- ❌ **Market data fetch fails** - Evidence: `✗ Failed to fetch market data` (both test items)
- ❌ **Profit calculation returns zeros** - Evidence:
  ```
  ✓ Raw Profit: 0 gil
  ✓ Profit Margin: 0,0%
  ✓ Risk Score: 100
  ✓ Recommendation Score: 0
  ```
  - Caused by market data fetch failure

### File Logging Not Working
- ❌ **aurum.log is empty** - Evidence: File contains only header, no actual logs
  - File created: `2026-01-20 21:06:06`
  - File content: Only 5 lines (header + empty)
  - **Root Cause:** FileLogger created but logs go through Dalamud IPluginLog, not intercepted

---

## ⚠️ UNKNOWN / NOT TESTED (No Evidence)

### UI Components
- ⚠️ **Dashboard window** - No evidence in logs (not opened during test)
- ⚠️ **Config window** - No evidence
- ⚠️ **Profit table population** - Cannot test until API parsing fixed
- ⚠️ **ImGui rendering** - Not tested

### Advanced Features
- ⚠️ **Market analysis calculations** - Cannot verify until data loads
- ⚠️ **Demand scoring** - No test data
- ⚠️ **Risk assessment** - Returns default value (100) when no data
- ⚠️ **Ingredient tree resolution** - Not tested
- ⚠️ **Vendor price lookup** - Not tested

### Commands
- ⚠️ `/aurum` - Not tested
- ⚠️ `/aurum config` - Not tested  
- ⚠️ `/aurum health` - Not tested (auto-run works, manual unknown)
- ⚠️ `/aurum test` - Not tested manually
- ⚠️ `/aurum log` - Not tested

---

## 🚧 MISSING / TODO (Identified Issues)

### Critical Bugs to Fix
1. **JSON parsing error** - Change price fields from `uint` to `double` in UniversalisItemResponse
2. **File logging** - FileLogger not intercepting log calls

### Missing Features (Requested by User)
3. **Local database for price history** - Not implemented
   - Purpose: Cache historical data, reduce API calls
   - Requirement: Don't crush Universalis API
   
4. **Rate limiting** - Not implemented
   - No API throttling
   - No request queuing
   - Risk of API abuse

### Technical Debt
5. **Error handling** - JSON exceptions crash data fetches
6. **Retry logic** - No retry on API failures
7. **Batch optimization** - Not tuned for performance
8. **Cache invalidation** - Simple time-based only

---

## 📊 Statistics (From Logs)

| Metric | Value | Source |
|--------|-------|--------|
| Total Recipes | 13,393 | RecipeService |
| Total Items | 50,900 | RecipeService |
| Plugin Version | 0.0.0.2 | Manifest |
| Build Time | 2026-01-20 21:06:05 | DLL metadata |
| Services Initialized | 5/5 (100%) | Health check |
| API Requests Made | 2 | Integration test |
| API Requests Successful | 0 (0%) | Error logs |
| Integration Tests Passed | 0/2 (0%) | Test results |

---

## 🔍 Evidence-Based Conclusions

### What Works
The **plugin infrastructure is solid**:
- Loads correctly
- Auto-reloads on build
- Services initialize
- Recipe data loads perfectly
- Health checks run
- DEV_MODE works as intended
- Network requests execute

### What's Broken
The **data layer has two critical bugs**:
1. JSON model type mismatch (prices are doubles, not uints)
2. File logging not wired up correctly

### Impact
- **Cannot fetch market data** (JSON parsing fails)
- **Cannot calculate profits** (depends on market data)
- **Cannot debug easily** (file log empty)
- **Cannot use the plugin** (core functionality broken)

### Next Steps (Priority Order)
1. ✅ Fix JSON parsing (5 min fix)
2. ✅ Fix file logging (investigate why logs don't write)
3. ✅ Test with working data
4. 🔄 Design local database schema
5. 🔄 Implement rate limiting
6. 🔄 Implement database caching

---

## 📝 Notes

- All evidence from Dalamud log dated 2026-01-20 21:06:06 to 21:06:12
- No assumptions made - every claim has log evidence
- Missing features identified from user request in conversation
- Statistics are exact counts from log output
