# Development Mode Guide

## File Logging

All plugin logs are now automatically written to a file next to the DLL for easy debugging:

**Log File Location:** `Aurum\bin\Debug\aurum.log`

The log file contains:
- All plugin initialization messages
- Health check results
- Integration test results
- API calls and responses
- Error messages with full stack traces

You can read this file directly without needing to check the Dalamud log window.

## DEV_MODE Auto-Testing

**Location:** `Plugin.cs` line 18

When `DEV_MODE = true` (currently enabled), the plugin automatically runs tests on load:

1. **2 second delay** - Waits for services to initialize
2. **Health Check** - Verifies all services are working
3. **Integration Test** - Tests Universalis API and profit calculation
4. **Log File** - All results written to `aurum.log`

### What Gets Tested Automatically:
- Recipe loading (should show ~5000-6000 recipes)
- Universalis API market data fetch
- Profit calculation for a level 90 recipe
- Service initialization status

### Viewing Test Results:

**Option 1: Read the log file**
```
C:\Users\nixx\Projects\xiv\Aurum\Aurum\bin\Debug\aurum.log
```

**Option 2: Use the /aurum log command**
```
/aurum log
```
This prints the log file path to the Dalamud log.

**Option 3: Check Dalamud log**
Press `/xllog` in-game to view the Dalamud log window.

## Available Commands

- `/aurum` - Open dashboard
- `/aurum config` - Open config window
- `/aurum health` - Run health check manually
- `/aurum test` - Run integration test manually
- `/aurum log` - Show log file location

## Workflow for Development

1. Make code changes
2. Build: `dotnet build Aurum.sln`
3. Plugin auto-reloads (you have this enabled)
4. Tests run automatically (2 second delay)
5. Read `aurum.log` to see results
6. Iterate!

## Disabling DEV_MODE

When you're ready to ship or want to disable auto-testing:

Edit `Plugin.cs` line 18:
```csharp
private const bool DEV_MODE = false;  // Changed from true
```

This will disable automatic test running but keep the file logging and manual commands.

## What to Look For in aurum.log

### Successful Run Example:
```
[21:04:10.123] [INFO] Aurum v0.0.0.2 initialized successfully!
[21:04:10.124] [INFO] DEV MODE ENABLED - Auto-running tests
[21:04:12.456] [INFO] Starting DEV MODE automated tests...
[21:04:12.789] [INFO] Test 1: Fetching market data for Darksteel Ore (ID: 5114)
[21:04:13.012] [INFO]   ✓ Current Listings: 25
[21:04:13.013] [INFO]   ✓ Current Avg Price: 452
[21:04:13.234] [INFO] Test 2: Calculating profit for level 90 recipe
[21:04:13.567] [INFO]   ✓ Raw Profit: 12500 gil
[21:04:13.568] [INFO]   ✓ Profit Margin: 45.2%
[21:04:13.569] [INFO] DEV MODE tests complete!
```

### Error Example:
```
[21:04:12.789] [ERROR] Failed to fetch market data
[21:04:12.790] [ERROR] Exception: HttpRequestException: No such host is known
[21:04:12.791] [ERROR] Stack trace: ...
```

## Notes

- The log file is **overwritten** on each plugin load (not appended)
- Log file path is printed at plugin initialization
- All Dalamud log calls also go to the file automatically
- File logging happens even if DEV_MODE is disabled
