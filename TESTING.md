# Self-Service Testing Guide

## Overview

Aurum now has built-in health checks and integration tests that run automatically and can be triggered manually. This allows you to verify the plugin is working without constantly checking logs.

## Automatic Health Checks

Every time the plugin loads, it automatically runs a comprehensive health check and logs the results to the Dalamud log.

**What it checks:**
- ✓ Plugin version and build time
- ✓ All services initialized correctly
- ✓ RecipeService loaded recipes (with count per class)
- ✓ UniversalisService ready for API calls
- ✓ Configuration values

**Where to find it:**
Open Dalamud log (`/xllog`) and look for:
```
========================================
AURUM HEALTH CHECK
========================================
✓ Plugin Version: 0.0.0.2
✓ Build Time: 2026-01-20 20:54:01
...
========================================
HEALTH CHECK COMPLETE
========================================
```

## Manual Testing Commands

### `/aurum health`
Runs the health check on demand without reloading the plugin.

**Use this to:**
- Verify the plugin is working after making changes
- Check recipe counts per crafting class
- Confirm services are initialized

### `/aurum test`
Runs a full integration test that:
1. Fetches real market data from Universalis API
2. Calculates profit for a sample level 90 recipe
3. Tests market analysis and risk scoring

**Output includes:**
```
========================================
RUNNING INTEGRATION TEST
========================================
Test 1: Fetching market data for Darksteel Ore (ID: 5114)
  ✓ Current Listings: 25
  ✓ Current Avg Price: 452
  ✓ Recent Sales: 5
  ✓ Sale Velocity: 2.50 sales/day

Test 2: Calculating profit for level 90 recipe
  Testing recipe: Rarefied Chondrite Saw
  ✓ Raw Profit: 12500 gil
  ✓ Profit Margin: 45.2%
  ✓ Risk Score: 35
  ✓ Recommendation Score: 78
========================================
INTEGRATION TEST COMPLETE
========================================
```

### `/aurum`
Opens the main dashboard window (default behavior).

### `/aurum config`
Opens the configuration window.

## What To Check After Code Changes

1. **After modifying RecipeService:**
   - Run `/aurum health`
   - Check "Total Recipes" count (should be ~5000-6000)
   - Check "Recipes by Class" breakdown

2. **After modifying UniversalisService:**
   - Run `/aurum test`
   - Verify "Test 1" fetches market data successfully
   - Check for JSON deserialization errors

3. **After modifying ProfitService or MarketAnalysisService:**
   - Run `/aurum test`
   - Verify "Test 2" calculates profit successfully
   - Check Risk Score and Recommendation Score values

4. **After modifying UI:**
   - Run `/aurum` to open the window
   - Click "Refresh" button
   - Verify data loads and displays correctly

## Interpreting Health Check Results

### Good Health Check
```
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
```

### Problem Indicators

**No Recipes Loaded:**
```
  Total Recipes: 0
  ✗ WARNING: No recipes loaded!
```
→ Check CraftType IDs or recipe loading logic

**Service NULL:**
```
  ✗ RecipeService: NULL
```
→ Check service initialization in Plugin.cs

**Integration Test Failures:**
```
  ✗ Failed to fetch market data
```
→ Check UniversalisService API call or JSON parsing

```
  ✗ Failed to calculate profit
```
→ Check ProfitService calculation logic or dependencies

## Build Time Verification

The health check shows the DLL build time:
```
✓ Build Time: 2026-01-20 20:54:01
```

If this doesn't match your latest build, the plugin hasn't reloaded. In that case:
1. Unload the plugin (`/xlplugins` → Unload)
2. Wait 2 seconds
3. Rebuild (`dotnet build`)
4. Reload the plugin

With auto-reload enabled, you should see the build time update automatically after each build.

## Integration Test Coverage

The `/aurum test` command covers:

**API Integration:**
- ✓ Universalis API connectivity
- ✓ JSON deserialization (listings, history, prices)
- ✓ Error handling for API failures

**Data Processing:**
- ✓ Market data parsing
- ✓ Sale velocity calculation
- ✓ Recipe lookup by level

**Profit Calculation:**
- ✓ Ingredient cost calculation
- ✓ Market board price fetching
- ✓ Profit margin calculation
- ✓ Risk score calculation
- ✓ Recommendation score calculation

## Logs Location

All test results are written to the Dalamud log:
- Open with `/xllog` command in-game
- Or check `%APPDATA%\XIVLauncher\dalamud.log`

## Tips for Faster Iteration

1. **Keep the log open** - Use `/xllog` and keep the window open to see results immediately
2. **Use health check first** - Run `/aurum health` before testing to verify basics
3. **Test incrementally** - After small changes, use `/aurum test` to verify
4. **Check build time** - Verify the build time in health check matches your latest build

## Example Workflow

```bash
# 1. Make code changes
# 2. Build
dotnet build Aurum.sln

# 3. Wait 1-2 seconds for auto-reload
# 4. In-game, run health check
/aurum health

# 5. If health check passes, run integration test
/aurum test

# 6. If integration test passes, test UI
/aurum
```

This gives you confidence that everything works without needing manual verification!
