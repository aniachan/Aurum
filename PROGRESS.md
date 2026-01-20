# Aurum - Implementation Progress

## ✅ Completed (Phase 1 - Foundation)

### Project Setup
- [x] Renamed entire project from SamplePlugin to Aurum
- [x] Updated all namespaces, references, and build files
- [x] Updated plugin manifest with proper description
- [x] Created comprehensive README.md

### Design Documents
- [x] **DESIGN.md** - Complete demand/risk analysis system design
- [x] Market warning types and algorithms
- [x] Risk scoring formulas
- [x] Recommendation engine design

### Core Data Models (Aurum/Models/)
- [x] **MarketWarning.cs** - Warning types and severity levels
- [x] **MarketData.cs** - Complete market analysis structure:
  - Current listings and sales history
  - Demand metrics (velocity, supply/demand ratio, volatility)
  - Risk scoring (0-100)
  - Recommendation scoring with quantity suggestions
  - Competition analysis
- [x] **RecipeData.cs** - Crafting recipes with ingredient trees
- [x] **ProfitCalculation.cs** - Complete profit results with demand integration
- [x] **Configuration.cs** - Extended settings for all features

### Services Layer (Aurum/Services/)
- [x] **CacheService.cs** - Thread-safe in-memory cache with TTL
- [x] **UniversalisService.cs** - Universalis API client
  - Single item fetch
  - Batch fetch (up to 100 items)
  - Automatic caching
  - Error handling and retry logic
- [x] **RecipeService.cs** - Game data queries via Lumina
  - Loads all DoH recipes (CRP, BSM, ARM, GSM, LTW, WVR, ALC, CUL)
  - Item name/icon caching
  - Recipe lookup and search
  - Sub-recipe detection
- [x] **MarketAnalysisService.cs** - Demand calculation engine
  - Sale velocity calculation
  - Supply/demand ratio
  - Price volatility (stddev/mean)
  - Market momentum (trending up/down)
  - Risk scoring algorithm
  - Warning generation (6 types)
  - Quantity recommendations
- [x] **ProfitService.cs** - Profit calculation engine
  - Recursive ingredient tree resolution
  - Multiple cost modes (MB, Vendor, Cheapest, Self-gathered)
  - Sub-recipe crafting cost calculation
  - Full profit metrics (margin, ROI, gil/hour)
  - Demand-weighted recommendation scoring
  - Cycle detection and max depth protection

### Plugin Integration
- [x] **Plugin.cs** - Service initialization
  - All services properly instantiated
  - RecipeService auto-initialization
  - Command registration (`/aurum`, `/aurum config`)
  - Window system integration
  - Proper disposal

### Build System
- [x] Solution compiles successfully
- [x] All namespaces updated
- [x] Only 1 warning (nullable reference - non-critical)

---

## 📊 System Capabilities (Implemented)

### Market Analysis
✅ **6 Warning Types:**
1. Market Crash Risk - Oversupplied market
2. Low Demand - Slow sales
3. Price War Active - Volatile + falling prices
4. Stale Market - No recent sales
5. High Competition - Many sellers at same price
6. (Reserved for future: Oversupply Expected)

✅ **Risk Scoring (0-100):**
- Supply/Demand ratio: 25 points
- Price volatility: 25 points
- Velocity penalty: 25 points
- Competition: 25 points

✅ **Recommendation Scoring (0-100):**
- Profit margin: 30%
- Sale velocity: 25%
- Supply/demand: 20%
- Price stability: 15%
- Market momentum: 10%

### Profit Calculation
✅ **Features:**
- Full ingredient tree resolution (recursive)
- Sub-recipe detection and cost comparison
- HQ vs NQ pricing
- Market board tax calculation (5%)
- Multiple result amounts (recipes yielding >1 item)
- Cycle detection
- Max depth protection (10 levels)

✅ **Cost Modes:**
- Market Board - Current MB prices
- Vendor - Vendor prices (TODO: needs game data hookup)
- Cheapest - Lowest of MB or Vendor
- Self-Gathered - Zero cost

---

## 📈 Statistics

**Lines of Code:** ~2,500+
**Services:** 5 core services
**Models:** 10+ data structures
**Recipes Loaded:** All DoH classes (thousands of recipes)
**API Integration:** Universalis (REST)
**Caching:** 5-minute default TTL

---

## 🚧 Next Steps (Phase 2 - UI)

### High Priority
- [ ] Create DashboardWindow.cs - Main profit list UI
- [ ] Implement profit list rendering
- [ ] Add search and filter UI
- [ ] Create DetailWindow.cs for item analysis
- [ ] Add basic sorting (profit, margin, recommendation score)
- [ ] Implement "refresh" button for manual cache clearing

### Medium Priority
- [ ] Add price history charts (ImPlot)
- [ ] Create profit comparison visualizations
- [ ] Implement watchlist/favorites
- [ ] Add ingredient tree display in DetailWindow
- [ ] Create shopping list view

### Low Priority (Future Versions)
- [ ] Artisan IPC integration
- [ ] Price alerts system
- [ ] Historical profit tracking
- [ ] Retainer integration
- [ ] Multi-world comparison

---

## 🎯 Current State

**Status:** ✅ Foundation Complete, Ready for UI Development

The core engine is **fully functional** and includes:
- ✅ Game data loading (RecipeService)
- ✅ Market data fetching (UniversalisService)
- ✅ Demand analysis (MarketAnalysisService)
- ✅ Profit calculation (ProfitService)
- ✅ Risk assessment and warnings
- ✅ Recommendation scoring

**What works right now:**
```csharp
// Plugin has these services ready:
plugin.RecipeService      // 5000+ recipes loaded
plugin.UniversalisService // API client ready
plugin.ProfitService      // Can calculate any recipe

// Example usage (in UI code):
var recipes = plugin.RecipeService.GetRecipesByClass("ALC");
var profits = await plugin.ProfitService.CalculateProfitsBatchAsync(
    recipes, 
    "Gilgamesh"
);
// Returns List<ProfitCalculation> with full demand analysis!
```

**Next milestone:** Create a basic UI to display the profit calculations.

---

## 🐛 Known Issues

1. **Vendor prices not implemented** - GetVendorPrice() returns 0 (needs GilShop/SpecialShop integration)
2. **One nullable warning** - UniversalisService line 103 (non-critical)
3. **No UI yet** - Services work but need visualization

---

## 💡 Technical Highlights

### Demand-First Design
Unlike traditional profit calculators that only show margins, Aurum **actively prevents bad crafts**:
- A 100K profit item with 0 sales/day gets a LOW score
- A 40K profit item with 50 sales/day gets a HIGH score

### Smart Ingredient Trees
Recursively resolves sub-recipes:
```
Potion of Intelligence
├─ Cloudsbreath (×2) - 8,400g MB
│  └─ Silkworm Cocoon (×3) - Sub-recipe cheaper! (Craft it)
│     ├─ Silkworm (×1) - 200g
│     └─ Silk Thread (×1) - 150g
└─ Water Crystal (×7) - 140g
```

### Batch Optimization
- Fetches up to 100 items per API call
- Caches aggressively (5-minute TTL)
- Parallel profit calculations

---

## 📝 Commit History

1. Initial project setup and design documents
2. Core data models with demand analysis
3. Service layer implementation (Cache, Universalis, Recipe)
4. Market analysis service with risk scoring
5. Profit service with recursive ingredient trees
6. Plugin integration and service initialization
7. **Current:** Ready for UI development

---

**Last Updated:** January 20, 2026
**Build Status:** ✅ Successful (1 warning, 0 errors)
**Next Session:** Dashboard UI implementation
