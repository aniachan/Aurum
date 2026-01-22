# Aurum - FFXIV Crafting Profit Calculator

<p align="center">
  <img src="Data/logo.png" alt="Aurum" width="128"/>
</p>

<p align="center">
  <strong>Smart crafting profit analysis with real-time market demand intelligence</strong>
</p>

---

## 🎯 What is Aurum?

**Aurum** is a professional-grade Dalamud plugin that helps FFXIV crafters make smarter, more profitable decisions. Unlike simple profit calculators that only show "high margin = good," Aurum analyzes **real market demand** to warn you about items that won't sell, preventing you from wasting time and gil on bad crafts.

### Key Features

✨ **Demand-Aware Profit Analysis**
- Calculates full ingredient trees (all sub-components)
- Integrates real-time market data from Universalis API
- Weighs profit against actual market demand

🚨 **Market Risk Warnings**
- **Market Crash Risk** - Too many listings, prices may drop
- **Low Demand** - Item rarely sells, could take weeks
- **Price Wars** - Sellers undercutting aggressively
- **Stale Markets** - No recent sales, dead market
- **High Competition** - Many sellers at same price
- **Oversupply Risk** - Too many crafters targeting this item

📊 **Professional Stock-Trading UI**
- Real-time price charts and trends
- Supply/demand visualizations
- Sale velocity indicators
- Risk scoring (0-100)
- Smart quantity recommendations

🎯 **Recommendation Engine**
Scores recipes 0-100 based on:
- 30% Profit margin
- 25% Sale velocity (demand)
- 20% Supply/demand ratio
- 15% Price stability
- 10% Market momentum

### Example

Instead of showing just:
```
Grade 8 Tincture: 85,000g profit ✨
```

Aurum shows:
```
Grade 8 Tincture
Profit: 85,000g (72.5%)
Demand: 🐌 2 sales/day
Risk: 🔴 Very High (78/100)
Score: ⭐⭐ 41/100
⚠️ Market oversupplied - 150 listings, only 2 sales/day
⚠️ Could take 75 days to sell
Recommendation: ❌ AVOID - Try something else!
```

---

## 🚀 Installation

### Prerequisites
- FINAL FANTASY XIV with Dalamud installed
- XIVLauncher (with Dalamud enabled)

### From Plugin Installer (Coming Soon)
1. Open Dalamud Plugin Installer (`/xlplugins`)
2. Search for "Aurum"
3. Click Install

### Manual Development Install
1. Clone this repository
2. Build the solution: `dotnet build Aurum.sln --configuration Release`
3. Add `Aurum/bin/x64/Release/Aurum.dll` to Dalamud Dev Plugin Locations
4. Enable in Plugin Installer under Dev Tools

---

## 📖 Usage

### Commands
- `/aurum` - Open main dashboard
- `/aurum config` - Open settings

See the [User Guide](docs/USER_GUIDE.md) for detailed usage instructions.

---

## 🏗️ Architecture

### Project Structure
```
Aurum/
├── Models/           # Data structures
│   ├── MarketData.cs        # Market analysis data
│   ├── RecipeData.cs        # Crafting recipes
│   ├── ProfitCalculation.cs # Profit results
│   └── MarketWarning.cs     # Warning types
├── Services/         # Business logic
│   ├── RecipeService.cs         # Game data queries
│   ├── UniversalisService.cs    # Market API client
│   ├── MarketAnalysisService.cs # Demand calculations
│   ├── ProfitService.cs         # Profit engine
│   └── CacheService.cs          # Data caching
├── Windows/          # UI components
│   ├── DashboardWindow.cs   # Main UI
│   └── ConfigWindow.cs      # Settings
└── Configuration.cs  # Plugin settings
```

See [ARCHITECTURE.md](docs/ARCHITECTURE.md) for more details.

### Technologies
- **.NET 10.0** with C# 12
- **Dalamud SDK 14.0.1** for game integration
- **Lumina** for game data access
- **ImGui** for UI rendering
- **Universalis API** for market data

---

## 🧠 How It Works

### Demand Analysis Algorithm

1. **Sale Velocity Calculation**
   ```
   velocity = total_sales / time_period_days
   ```

2. **Supply/Demand Ratio**
   ```
   ratio = current_listings / daily_sales
   < 1.0  = Undersupplied (GOOD)
   1-3    = Balanced
   3-7    = Oversupplied (WARNING)
   > 7    = Saturated (BAD)
   ```

3. **Risk Scoring (0-100)**
   ```
   Risk = (Supply/Demand * 25) +
          (Price Volatility * 25) +
          (Velocity Penalty * 25) +
          (Competition * 25)
   ```

4. **Recommendation Score (0-100)**
   ```
   Score = (Profit Margin * 30%) +
           (Sale Velocity * 25%) +
           (Supply/Demand * 20%) +
           (Price Stability * 15%) +
           (Market Momentum * 10%)
   ```

### Quantity Recommendations
```
Recommended = 1-2 days of demand
Max Safe = Until supply ratio hits 7.0 (oversupply threshold)
```

---

## 🤝 Contributing

Contributions are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for details.

### Development Setup
1. Install .NET 10 SDK
2. Clone the repo
3. Open `Aurum.sln` in Visual Studio 2022 or Rider
4. Build and test

---

## 📜 License

This project is licensed under the AGPL-3.0 License - see [LICENSE.md](LICENSE.md)

---

## 🙏 Acknowledgments

- **Universalis** for providing the market board API
- **Dalamud Team** for the plugin framework
- **goatcorp** for the sample plugin template
- **FFXIV Community** for feedback and testing

---

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/yourusername/Aurum/issues)
- **Wiki**: [Documentation](https://github.com/yourusername/Aurum/wiki)

---

<p align="center">
  <strong>Made with 💰 for the FFXIV crafting community</strong>
</p>
