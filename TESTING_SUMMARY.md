# Testing System Summary

## New In-Game Commands

- **`/aurum health`** - Run health check (shows recipe counts, service status, config)
- **`/aurum test`** - Run integration test (tests API, profit calc, market analysis)
- **`/aurum`** - Open dashboard (unchanged)
- **`/aurum config`** - Open config (unchanged)

## What Happens On Plugin Load

The plugin now automatically runs a health check and logs:
1. Plugin version + build time (verify you have latest code)
2. Service initialization status
3. Recipe counts per crafting class (CRP, BSM, ARM, etc.)
4. Sample recipe to verify loading works
5. Configuration values

## How To Use

### Quick Check (2 seconds)
```
/aurum health
```
Check the log - should see ~5000-6000 recipes loaded.

### Full Test (5-10 seconds)
```
/aurum test
```
Tests real API call + profit calculation. Should show market data and profit numbers.

### Visual Test
```
/aurum
```
Click "Refresh" button - should load profit data into the table.

## Benefits

✅ **No more guessing** - Health check tells you exactly what's working  
✅ **Fast iteration** - Test changes in seconds without asking for screenshots  
✅ **Self-documenting** - Logs show exactly what the plugin is doing  
✅ **Automatic** - Runs on every plugin load, catches issues immediately  

## Recent Fixes Applied

1. ✅ Fixed CraftType IDs (0-7 instead of 8-15)
2. ✅ Fixed JSON parsing for `retainerCity` (int instead of string)
3. ✅ Added city name mapping (1=Limsa, 2=Gridania, etc.)
4. ✅ Added comprehensive health check system
5. ✅ Added integration test command

## Expected Health Check Output

```
========================================
AURUM HEALTH CHECK
========================================
✓ Plugin Version: 0.0.0.2
✓ Build Time: 2026-01-20 20:54:01
Checking Services:
  ✓ CacheService: OK
  ✓ RecipeService: OK
  ✓ UniversalisService: OK
  ✓ MarketAnalysisService: OK
  ✓ ProfitService: OK
RecipeService Status:
  Total Recipes: 5432
  Total Items: 50900
  ✓ Recipes loaded successfully
  Recipes by Class:
    ALC: 678
    ARM: 654
    BSM: 712
    CRP: 689
    CUL: 734
    GSM: 598
    LTW: 687
    WVR: 680
  ✓ Sample Level 90 Recipe: Rarefied Chondrite Saw (ID: 35383)
========================================
HEALTH CHECK COMPLETE
========================================
```

## Next Time You Need To Verify Something

Just run `/aurum test` in-game and check the log. No more asking for screenshots!
