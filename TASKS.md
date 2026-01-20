# Aurum Development Task List

**Last Updated:** 2026-01-20 21:09:08  
**Current Version:** 0.0.0.2  
**Production Readiness:** 60%

---

## ✅ COMPLETED TASKS

### Phase 1: Foundation & Bug Fixes
- [x] **Plugin infrastructure** - Loads, auto-reloads, service initialization
- [x] **Recipe loading** - 13,393 recipes from game data
- [x] **Fix JSON parsing** - Handle double prices from Universalis API
- [x] **Fix file logging** - Tail Dalamud log to aurum.log
- [x] **Universalis API integration** - 100% success rate on API calls
- [x] **Profit calculation engine** - Recursive ingredient tree resolution
- [x] **Market analysis** - Risk scoring, demand metrics
- [x] **Health check system** - Auto-run diagnostics on load
- [x] **DEV_MODE** - Auto-test on plugin load
- [x] **Integration tests** - Standalone test project
- [x] **Progress tracking** - Evidence-based PROGRESS.md

**Evidence:** All confirmed working in aurum.log (21:09:08), 6/6 API calls successful, profit calculation working

---

## 🔴 CRITICAL PRIORITY (Before Public Release)

### Database & Caching System
- [x] **Design SQLite database schema**
  - [x] Market data table (item_id, world, timestamp, price, listings, sales)
  - [x] Price history table (item_id, world, timestamp, price, quantity, is_sale)
  - [x] Recipe cache table (recipe_id, last_analyzed, profit_snapshot)
  - [x] Item metadata table (item_id, item_level, patch_added, category)
  - [x] API request log table (endpoint, timestamp, response_time, success)
  - [x] Add indexes for performance (item_id+world, timestamp ranges)

- [x] **Implement DatabaseService**
  - [x] Initialize SQLite database on plugin load
  - [x] Create tables if not exist
  - [x] Add migration system for schema updates
  - [x] Implement CRUD operations for all tables
  - [x] Add connection pooling for performance
  - [x] Implement database vacuum/cleanup routine

- [x] **Integrate database with UniversalisService**
  - [x] Check database before making API calls
  - [x] Store API responses in database
  - [x] Implement cache expiration logic (configurable TTL)
  - [x] Add database query methods (get latest price, get history, etc.)
  - [x] Implement bulk insert for batch operations

- [x] **Price history tracking**
  - [x] Store every API response with timestamp
  - [x] Track price changes over time
  - [x] Calculate price volatility metrics
  - [x] Identify price trends (rising, falling, stable)
  - [x] Store sale velocity history

**Impact:** Reduces API calls by 90%+, enables historical analysis

---

### Rate Limiting & API Etiquette
- [x] **Implement RateLimiter class**
  - [x] Token bucket algorithm (configurable requests/minute)
  - [ ] Per-endpoint rate limits
  - [ ] Global API request counter
  - [ ] Request queue with priority levels
  - [ ] Exponential backoff on failures
  - [ ] Respect HTTP 429 (Too Many Requests) responses

- [x] **Request queue system**
  - [x] FIFO queue for pending requests
  - [x] Request deduplication (don't fetch same item twice)
  - [ ] Batch request optimization (group items for batch API)
  - [x] Priority queue (user-requested > background)
  - [x] Cancel queued requests on plugin unload

- [ ] **API usage monitoring**
  - [ ] Track requests per minute/hour/day
  - [ ] Log API errors and retry attempts
  - [ ] Display API usage stats in config window
  - [ ] Alert user if approaching rate limits
  - [ ] Implement graceful degradation (use cache only)

**Impact:** Prevents API abuse, respectful to Universalis infrastructure

---

### Smart Item Filtering & Selection
- [x] **Design intelligent item filtering algorithm**
  - [x] **Item level heuristics**
    - [x] Prioritize current expansion items (lvl 90+)
    - [x] Boost latest patch items (higher relevance)
    - [x] Deprioritize obsolete gear (5+ patches old)
    - [x] Consider item level range for player's crafters
  
  - [x] **Market activity heuristics**
    - [x] Check sale velocity (high velocity = track)
    - [x] Check listing count (active market = track)
    - [x] Check price stability (volatile = track more often)
    - [x] Consider world transfer trends
  
  - [x] **Recipe value heuristics**
    - [x] High-value crafts (>100k gil) = higher priority
    - [x] Popular intermediate materials = track
    - [x] Leve turn-ins = track
    - [x] Collectables = track
    - [x] Glamour items = track (always relevant)
  
  - [ ] **Category-based filtering**
    - [ ] Combat gear (main hand, armor, accessories)
    - [ ] Crafting/gathering gear
    - [ ] Consumables (food, potions)
    - [ ] Furniture (housing items)
    - [ ] Materials (intermediate crafts)
  
  - [ ] **User preference integration**
    - [ ] Track user's favorite items
    - [ ] Learn from user's search history
    - [ ] Respect user's level range filter
    - [ ] Remember user's world selection

- [x] **Implement ItemPriorityService**
  - [x] Score items based on combined heuristics (0-100)
  - [ ] Sort recipes by priority score
  - [ ] Limit API fetches to top N items (configurable)
  - [x] Update priority scores based on new data
  - [x] Cache priority scores in database

- [x] **Implement background refresh system**
  - [x] Refresh high-priority items every 5 minutes (via RefreshService)
  - [x] Refresh medium-priority items every 30 minutes
  - [x] Refresh low-priority items every 6 hours
  - [ ] Skip items with no market activity for 30 days
  - [x] Respect rate limits during background refresh (UniversalisService handles this)

**Impact:** Reduces unnecessary API calls by 80%, focuses on relevant items

---

### Batch Optimization
- [x] **Implement batch request system**
  - [x] Group items by world for batch API call
  - [x] Use Universalis batch endpoint (/api/v2/{world}/{ids})
  - [x] Max 100 items per batch (API limit)
  - [ ] Split large requests into multiple batches
  - [ ] Parallel batch processing with rate limiting

- [ ] **Request deduplication**
  - [ ] Track in-flight requests (don't duplicate)
  - [ ] Merge duplicate requests from UI
  - [ ] Return cached results for recent requests
  - [ ] Implement request coalescing (wait 100ms for more)

**Impact:** 10x faster for large recipe lists

---

## 🟡 HIGH PRIORITY (User Experience)

### UI Components - Dashboard Window
- [ ] **Profit table display**
  - [ ] Test table rendering with real data
  - [ ] Verify sorting by profit/margin/risk
  - [ ] Add filter controls (level, class, profit range)
  - [ ] Implement pagination (50 items per page)
  - [ ] Add row highlighting (green=profit, red=loss)
  - [ ] Add tooltips on hover (show warnings)

- [ ] **Risk indicators**
  - [ ] Color-coded risk badges (low/medium/high/very high)
  - [ ] Warning icons for market issues
  - [ ] Tooltip explanations for risk factors
  - [ ] Risk score breakdown (hover to see details)

- [ ] **Market warnings display**
  - [ ] Show top 3 warnings per item
  - [ ] Icon+text format for each warning
  - [ ] Color coding (yellow=caution, red=danger)
  - [ ] Expandable details on click

- [ ] **Action buttons**
  - [ ] "Refresh" button (re-fetch market data)
  - [ ] "Export" button (CSV export)
  - [ ] "Filter" button (open filter panel)
  - [ ] Loading spinner during refresh

- [ ] **Status indicators**
  - [ ] Show last refresh timestamp
  - [ ] Show number of items analyzed
  - [ ] Show API request count/limit
  - [ ] Show cache hit rate

**Testing Required:** Open `/aurum` and verify all elements render

---

### UI Components - Price History Charts (ImPlot)
- [ ] **Design chart window**
  - [ ] Create new ImGui window for charts
  - [ ] Integrate ImPlot library (already in Dalamud)
  - [ ] Add window open/close button in dashboard

- [ ] **Price history chart**
  - [ ] Line chart showing price over time
  - [ ] Dual Y-axis (listings + price)
  - [ ] Time range selector (1d, 7d, 30d, all)
  - [ ] Zoom and pan controls
  - [ ] Crosshair with value tooltip

- [ ] **Sale velocity chart**
  - [ ] Bar chart showing sales per day
  - [ ] Moving average trend line
  - [ ] Highlight unusual spikes

- [ ] **Supply/demand chart**
  - [ ] Stacked area chart (listings vs sales)
  - [ ] Show supply/demand ratio over time
  - [ ] Color code over/undersupplied periods

- [ ] **Price distribution chart**
  - [ ] Histogram of listing prices
  - [ ] Show price outliers
  - [ ] Highlight current market price

- [ ] **Multi-item comparison**
  - [ ] Overlay multiple items on same chart
  - [ ] Legend with item names and colors
  - [ ] Toggle items on/off
  - [ ] Normalize prices for comparison

- [ ] **Chart export**
  - [ ] Save chart as PNG
  - [ ] Export data as CSV
  - [ ] Copy chart to clipboard

**Dependencies:** ImPlot integration, database price history

---

### UI Components - Detail Window
- [ ] **Create DetailWindow.cs**
  - [ ] Deep-dive view for single item
  - [ ] Open from dashboard table (click row)
  - [ ] Show all market data fields

- [ ] **Item information panel**
  - [ ] Item icon and name
  - [ ] Item level, category
  - [ ] Recipe details (ingredients, yield)
  - [ ] Link to Garland Tools/Teamcraft

- [ ] **Current market snapshot**
  - [ ] Current listings (price, quantity, retainer)
  - [ ] Recent sales (price, quantity, buyer)
  - [ ] Min/max/average prices
  - [ ] Market board URL (Universalis)

- [ ] **Profit breakdown**
  - [ ] Sale price (current avg)
  - [ ] Ingredient costs (itemized)
  - [ ] Crystal costs
  - [ ] Vendor sale price
  - [ ] Net profit calculation
  - [ ] Profit margin percentage

- [ ] **Risk analysis panel**
  - [ ] Risk score with explanation
  - [ ] All market warnings listed
  - [ ] Recommendation (craft/don't craft)
  - [ ] Suggested pricing

- [ ] **Ingredient tree view**
  - [ ] Expandable tree of all ingredients
  - [ ] Show sub-recipes recursively
  - [ ] Indicate craftable vs purchasable
  - [ ] Show costs at each level

**Testing Required:** Click item in dashboard to open detail window

---

### UI Components - Config Window
- [ ] **Test existing config window**
  - [ ] Open `/aurum config` and verify rendering
  - [ ] Test all settings save/load correctly

- [ ] **Add database settings**
  - [ ] Cache expiration time (slider, 5min-24hr)
  - [ ] Database vacuum frequency
  - [ ] Clear cache button
  - [ ] Show database size

- [ ] **Add filtering settings**
  - [ ] Item level range (min/max)
  - [ ] Enable/disable item categories
  - [ ] Priority weights (sliders)
  - [ ] Max items to track (limit)

- [ ] **Add API settings**
  - [ ] Rate limit (requests/minute)
  - [ ] Request timeout (seconds)
  - [ ] Batch size (items per request)
  - [ ] Show API usage stats

- [ ] **Add UI settings**
  - [ ] Table rows per page
  - [ ] Font size
  - [ ] Color theme
  - [ ] Show/hide columns

**Testing Required:** Verify all settings persist across reloads

---

## 🟢 MEDIUM PRIORITY (Polish & Features)

### Advanced Profit Analysis
- [ ] **Opportunity cost calculation**
  - [ ] Compare profit vs time to craft
  - [ ] Factor in gathering time for materials
  - [ ] Show gil/hour metric
  - [ ] Rank by efficiency

- [ ] **Market timing recommendations**
  - [ ] Identify best time to sell (peak demand)
  - [ ] Warn about market saturation
  - [ ] Suggest alternative items
  - [ ] Seasonal trends (if data available)

- [ ] **Shopping list generation**
  - [ ] Generate ingredient shopping list
  - [ ] Optimize for cheapest source (MB vs vendor)
  - [ ] Show total shopping cost
  - [ ] Export to CSV/clipboard

- [ ] **Crafting queue optimizer**
  - [ ] Suggest optimal crafting order
  - [ ] Batch similar items
  - [ ] Minimize material waste
  - [ ] Integrate with Teamcraft (optional)

---

### Error Handling & Resilience
- [x] **Implement retry logic**
  - [x] Retry failed API calls (max 3 attempts)
  - [x] Exponential backoff (1s, 2s, 4s)
  - [x] Log retry attempts
  - [x] Give up and use cache after max retries

- [ ] **Graceful degradation**
  - [ ] Show cached data when API unavailable
  - [ ] Display staleness indicator
  - [ ] Continue working offline
  - [ ] Queue requests for when API returns

- [ ] **User-friendly error messages**
  - [ ] Replace technical errors with plain English
  - [ ] Suggest solutions (check internet, try later)
  - [ ] Add error reporting button
  - [ ] Log errors to file for debugging

---

### Performance Optimization
- [ ] **Parallel processing**
  - [ ] Fetch multiple items concurrently (with rate limit)
  - [ ] Process profit calculations in parallel
  - [ ] Use Task.WhenAll for batches

- [ ] **Memory optimization**
  - [ ] Implement object pooling for MarketData
  - [ ] Clear old cache entries (LRU eviction)
  - [ ] Limit in-memory recipe count
  - [ ] Profile memory usage

- [ ] **Lazy loading**
  - [ ] Load recipes on-demand (not all 13k at startup)
  - [ ] Paginate database queries
  - [ ] Stream large result sets
  - [ ] Defer UI rendering until visible

---

## 🔵 LOW PRIORITY (Nice to Have)

### Multi-World Analysis
- [ ] **Cross-world price comparison**
  - [ ] Fetch data for all worlds in data center
  - [ ] Show cheapest world for buying
  - [ ] Show highest profit world for selling
  - [ ] Factor in world transfer costs

- [ ] **World transfer arbitrage**
  - [ ] Identify items with large price differences
  - [ ] Calculate profit after transfer fee
  - [ ] Warn about transfer cooldowns
  - [ ] Track historical arbitrage opportunities

---

### Social Features
- [ ] **Share profit calculations**
  - [ ] Export to clipboard (text format)
  - [ ] Generate shareable link
  - [ ] Screenshot entire dashboard

- [ ] **Community data sync** (optional, future)
  - [ ] Share priority scores anonymously
  - [ ] Aggregate market trends
  - [ ] Warn about price manipulation
  - [ ] Respect user privacy

---

### Testing & QA
- [ ] **Unit tests**
  - [ ] Test profit calculation edge cases
  - [ ] Test market analysis algorithms
  - [ ] Test rate limiter logic
  - [ ] Test database operations

- [ ] **Integration tests**
  - [ ] Test full profit calculation pipeline
  - [ ] Test database caching flow
  - [ ] Test API failure scenarios
  - [ ] Test batch request handling

- [ ] **UI tests**
  - [ ] Manual testing checklist
  - [ ] Screenshot comparison
  - [ ] Accessibility testing
  - [ ] Performance profiling

---

### Documentation
- [ ] Clean up old documentation generated by the AI process
- [ ] **User guide**
  - [ ] How to use the dashboard
  - [ ] Understanding risk scores
  - [ ] Reading market warnings
  - [ ] Configuring settings

- [ ] **Developer docs**
  - [ ] Architecture overview
  - [ ] Database schema diagram
  - [ ] API integration guide
  - [ ] Contributing guidelines

- [ ] **Changelog**
  - [ ] Track all changes by version
  - [ ] Migration notes for breaking changes
  - [ ] Known issues list

---

## 📊 Task Summary Statistics

| Category | Total | Completed | In Progress | Not Started |
|----------|-------|-----------|-------------|-------------|
| **CRITICAL** | 27 | 0 | 0 | 27 |
| **HIGH** | 35 | 0 | 0 | 35 |
| **MEDIUM** | 14 | 0 | 0 | 14 |
| **LOW** | 8 | 0 | 0 | 8 |
| **COMPLETED** | 11 | 11 | 0 | 0 |
| **TOTAL** | 95 | 11 | 0 | 84 |

**Overall Completion:** 11.6% (11/95 tasks)  
**Critical Path Completion:** 0% (0/27 tasks)  
**Production Ready:** NO (critical tasks incomplete)

---

## 🎯 Recommended Implementation Order

### Sprint 1: Database Foundation (CRITICAL)
1. Design and implement SQLite database schema
2. Create DatabaseService with CRUD operations
3. Integrate database caching with UniversalisService
4. Test cache hit/miss scenarios

**Goal:** 90% reduction in API calls

### Sprint 2: Smart Filtering & Rate Limiting (CRITICAL)
1. Implement item priority scoring algorithm
2. Create RateLimiter with token bucket
3. Build request queue system
4. Implement batch request optimization

**Goal:** Respectful API usage, focus on relevant items

### Sprint 3: UI Polish & Charts (HIGH)
1. Test and fix dashboard table rendering
2. Integrate ImPlot for price history charts
3. Create DetailWindow for item deep-dive
4. Add chart export functionality

**Goal:** Rich visual analysis tools

### Sprint 4: Advanced Features (MEDIUM)
1. Implement opportunity cost calculations
2. Add shopping list generation
3. Improve error handling and resilience
4. Performance optimization pass

**Goal:** Production-ready user experience

### Sprint 5: Polish & Release (LOW)
1. Multi-world analysis features
2. Social sharing capabilities
3. Comprehensive testing
4. Documentation and user guide

**Goal:** Public release candidate

---

## 📝 Notes

- **Current blocker:** No database = API abuse risk
- **Quick win:** Implement rate limiter first (protect API immediately)
- **High value:** Smart filtering will dramatically improve UX
- **User delight:** Price history charts provide massive insight
- **Evidence-based:** All completed tasks verified in aurum.log

**Next session priority:** Start Sprint 1 (Database Foundation)
