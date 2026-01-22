# Contributing to Aurum

Thank you for your interest in contributing to Aurum! This guide will help you set up your development environment and understand the project structure.

## 🛠️ Development Setup

1. **Prerequisites**
   - Visual Studio 2022 or JetBrains Rider
   - .NET 10.0 SDK
   - FINAL FANTASY XIV (for live testing)
   - [Dalamud](https://github.com/goatcorp/Dalamud) (via XIVLauncher)

2. **Build Instructions**
   ```bash
   git clone https://github.com/yourusername/Aurum.git
   dotnet build Aurum.sln --configuration Debug
   ```

3. **Installation for Development**
   - Build the project.
   - Open Dalamud Settings (in-game via `/xlsettings`).
   - Go to the **Experimental** tab.
   - Under **Dev Plugin Locations**, add the path to your build output:
     `.../Aurum/Aurum/bin/Debug/Aurum.dll`
   - Aurum should appear in the Plugin Installer under "Dev Tools".

## 🐛 Debugging & Dev Mode

### File Logging
The plugin writes detailed logs to a file for easier debugging (bypassing the transient in-game console).
- **Location:** `Aurum/bin/Debug/aurum.log`
- **Contains:** Initialization, API calls, error stacks, integration test results.

### DEV_MODE
In `Plugin.cs`, there is a `DEV_MODE` constant:
```csharp
private const bool DEV_MODE = true;
```
When enabled (default in Debug), the plugin automatically runs a suite of tests on load:
1. Waits 2 seconds for services.
2. Runs Health Checks.
3. Runs Integration Tests (Universalis API, Profit Calculation).
4. Logs results to `aurum.log`.

To disable this auto-testing loop, set `DEV_MODE = false`.

### Debug Commands
- `/aurum health` - Run manual health check.
- `/aurum test` - Run integration tests manually.
- `/aurum log` - Print the location of the log file.

## ✅ Manual Testing Checklist

Before submitting a PR, please verify the following:

### 1. Startup & Basic UI
- [ ] Plugin loads without errors.
- [ ] `/aurum` opens the dashboard.
- [ ] `/aurum config` opens settings.

### 2. Dashboard & Data
- [ ] **Search:** Can find items by name (e.g., "Tincture").
- [ ] **Filtering:** Class and Level filters work.
- [ ] **Sorting:** Sort by Profit, Demand, and Score.
- [ ] **Risk:** Items show correct risk colors (Green/Yellow/Red).

### 3. Detail Analysis
- [ ] Clicking an item opens the Detail Window.
- [ ] Price history chart renders.
- [ ] Market Analysis metrics (Velocity, S/D Ratio) look reasonable.

### 4. Stability
- [ ] No crashes when disconnected from the internet (API handling).
- [ ] Settings persist after restarting the game.

## 🏗️ Project Structure

- **Aurum/** - Main plugin logic.
  - **Models/** - Data structures (MarketData, RecipeData).
  - **Services/** - Core logic (Universalis, Profit, Analysis).
  - **Windows/** - ImGui UI classes.
- **Aurum.IntegrationTests/** - Standalone console tests for the API.

## 🤝 Code Style
- Follow standard C# coding conventions.
- Use `file-scoped namespaces`.
- Prefer `var` when type is obvious.
- Keep UI logic separate from Service logic where possible.

## 📚 Community Guidelines
For detailed contributing guidelines, including bug reporting and PR process, see [docs/community/CONTRIBUTING_GUIDELINES.md](docs/community/CONTRIBUTING_GUIDELINES.md).
