# Aurum - Market Demand & Risk Analysis Design

## 🎯 Problem Statement

**High profit margins don't guarantee profit if no one buys your items.**

Key risks:
1. **Low demand** - Item doesn't sell, gil is locked in inventory
2. **Market saturation** - Too many listings, prices will crash
3. **Slow velocity** - Takes weeks to sell, opportunity cost
4. **Competition flooding** - Many players see same "profitable" item, flood market

## 📊 Demand Analysis Metrics

### 1. Sale Velocity (from Universalis)
**What:** Number of sales per day
**Formula:** `sales_count / time_period_days`
**Interpretation:**
- `> 50/day` = Very High Demand (consumables)
- `10-50/day` = High Demand (popular gear)
- `5-10/day` = Medium Demand (steady market)
- `1-5/day` = Low Demand (niche items)
- `< 1/day` = Very Low Demand (risky!)

### 2. Supply/Demand Ratio
**What:** Current listings vs daily sales
**Formula:** `current_listings / avg_daily_sales`
**Interpretation:**
- `< 1.0` = Undersupplied (GOOD - high demand, low stock)
- `1.0-3.0` = Balanced (OKAY - normal market)
- `3.0-7.0` = Oversupplied (WARNING - many competitors)
- `> 7.0` = Saturated (BAD - market crash risk)

### 3. Price Stability
**What:** How volatile prices are
**Formula:** `stddev(recent_prices) / avg_price`
**Interpretation:**
- `< 0.1` = Stable (predictable profit)
- `0.1-0.3` = Moderate (some risk)
- `> 0.3` = Volatile (high risk)

### 4. Time to Sell (Estimated)
**What:** How long until your item sells
**Formula:** `current_listings / avg_daily_sales`
**Interpretation:**
- `< 1 day` = Instant (excellent)
- `1-3 days` = Fast (good)
- `3-7 days` = Moderate (okay)
- `> 7 days` = Slow (poor liquidity)

### 5. Market Momentum
**What:** Is demand increasing or decreasing?
**Formula:** Compare recent 3-day velocity vs 7-day average
**Interpretation:**
- `> +20%` = Rising demand (good time to craft)
- `-20% to +20%` = Stable
- `< -20%` = Falling demand (avoid!)

## 🎲 Risk Scoring System

### Overall Risk Score (0-100)
Lower = safer, Higher = riskier

```
Risk Score = (
    (Supply/Demand Ratio * 25) +      // 0-25 points
    (Price Volatility * 25) +          // 0-25 points
    (Velocity Penalty * 25) +          // 0-25 points
    (Competition Factor * 25)          // 0-25 points
)
```

### Risk Categories
- **0-25**: 🟢 Low Risk (Safe bet)
- **25-50**: 🟡 Medium Risk (Calculated risk)
- **50-75**: 🟠 High Risk (Caution advised)
- **75-100**: 🔴 Very High Risk (Avoid unless you know what you're doing)

## 🎯 Recommendation Algorithm

### Weighted Score Formula
Combines profit with demand to find **true opportunities**

```
Recommendation Score = 
    (Profit Margin * 0.30) +           // 30% weight - still important!
    (Sale Velocity Score * 0.25) +     // 25% weight - demand matters
    (Supply/Demand Score * 0.20) +     // 20% weight - competition
    (Price Stability Score * 0.15) +   // 15% weight - predictability
    (Market Momentum * 0.10)           // 10% weight - trend
```

### Example Comparison

**Item A: Grade 8 Tincture**
- Profit: 45K (85/100)
- Velocity: 120/day (100/100) ← consumable, high demand
- Supply/Demand: 0.8 (95/100) ← undersupplied
- Stability: 0.12 (80/100) ← stable prices
- Momentum: +15% (75/100) ← growing
- **Score: 90/100** ✅ EXCELLENT OPPORTUNITY

**Item B: Replica Gear**
- Profit: 85K (95/100) ← Higher profit!
- Velocity: 2/day (20/100) ← very slow
- Supply/Demand: 8.5 (10/100) ← way too many listings
- Stability: 0.45 (40/100) ← volatile
- Momentum: -30% (25/100) ← declining
- **Score: 41/100** ⚠️ POOR OPPORTUNITY (despite high profit!)

## 🚨 Warning System

### Red Flags (Show warnings to user)

1. **"Market Crash Risk"**
   - Trigger: Supply/Demand > 5.0
   - Message: "⚠️ Too many listings! Market may crash if you add more."

2. **"Low Demand"**
   - Trigger: Velocity < 1/day
   - Message: "⚠️ This item rarely sells. Could take weeks to sell."

3. **"Price War Active"**
   - Trigger: Price dropped >20% in last 24h
   - Message: "⚠️ Prices falling rapidly! Sellers undercutting aggressively."

4. **"Stale Market"**
   - Trigger: No sales in 48+ hours
   - Message: "⚠️ Dead market. Last sale was 2+ days ago."

5. **"High Competition"**
   - Trigger: 20+ listings at same price point
   - Message: "⚠️ Many sellers at this price. Hard to stand out."

6. **"Oversupply Expected"**
   - Trigger: Item appears on many "high profit" lists (requires tracking)
   - Message: "⚠️ Popular craft alert! Others may flood market."

## 📱 UI Enhancements for Demand

### Dashboard List View - Additional Columns
```
┌────┬───────────┬────────┬────────┬──────────┬─────────┬─────────┐
│Icon│Item       │Profit  │Demand  │Risk      │Sell Time│Score    │
├────┼───────────┼────────┼────────┼──────────┼─────────┼─────────┤
│🧪  │Tincture   │45K     │🔥120/d │🟢 Low    │<1 day   │⭐⭐⭐⭐⭐│
│    │           │68.3%   │▂▃▅▇█   │Risk: 18  │         │90/100   │
├────┼───────────┼────────┼────────┼──────────┼─────────┼─────────┤
│🛡️  │Replica    │85K     │🐌2/d   │🔴 High   │8 days   │⭐⭐     │
│    │           │72.5%   │▂▁▁▁▁   │Risk: 78  │⚠️       │41/100   │
└────┴───────────┴────────┴────────┴──────────┴─────────┴─────────┘
```

### Detail Window - Demand Analysis Panel
```
┌─────────────────────────────────────────┐
│ 📊 Market Health Analysis               │
├─────────────────────────────────────────┤
│ Overall Risk: 🟢 LOW (18/100)          │
│                                          │
│ ✅ High demand (120 sales/day)          │
│ ✅ Undersupplied (0.8 ratio)            │
│ ✅ Stable prices (±8% variance)         │
│ ✅ Growing market (+15% trend)          │
│                                          │
│ Current Listings: 96                    │
│ Your Position: #8 if you list at 112K  │
│ Expected Sell Time: 19 hours            │
│                                          │
│ Competition Analysis:                   │
│ ├─ 15 sellers at 115K-120K             │
│ ├─ 32 sellers at 110K-115K ← You here  │
│ └─ 49 sellers at 100K-110K             │
│                                          │
│ Recommendation: ✅ SAFE TO CRAFT        │
│ "High demand, low risk. Good choice!"   │
└─────────────────────────────────────────┘
```

### Smart Quantity Recommendation
```
┌─────────────────────────────────────────┐
│ 🎯 Quantity Recommendation              │
├─────────────────────────────────────────┤
│ Based on market analysis:               │
│                                          │
│ Recommended: 5 items                    │
│ Max safe: 12 items                      │
│                                          │
│ Why?                                     │
│ • Market absorbs ~120/day               │
│ • 96 current listings = 0.8 days supply│
│ • Your 5 items = 4% of daily demand    │
│ • Estimated sell time: 1-2 days        │
│                                          │
│ ⚠️ Crafting >12 may oversupply market  │
└─────────────────────────────────────────┘
```

## 🧮 Implementation Data Structures

### Enhanced MarketData Model
```csharp
public class MarketAnalysis
{
    // Raw Universalis data
    public int CurrentListings { get; set; }
    public decimal AveragePriceNQ { get; set; }
    public decimal AveragePriceHQ { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public List<Sale> RecentHistory { get; set; }
    
    // Calculated demand metrics
    public decimal SaleVelocity { get; set; }           // sales per day
    public decimal SupplyDemandRatio { get; set; }       // listings / daily_sales
    public decimal PriceVolatility { get; set; }         // stddev / avg
    public decimal EstimatedSellTimeDays { get; set; }   // listings / velocity
    public decimal MarketMomentum { get; set; }          // % change in velocity
    
    // Risk assessment
    public int RiskScore { get; set; }                   // 0-100
    public RiskLevel RiskLevel { get; set; }             // Low/Med/High/VeryHigh
    public List<MarketWarning> Warnings { get; set; }    // Active red flags
    
    // Recommendation
    public int RecommendationScore { get; set; }         // 0-100
    public int RecommendedQuantity { get; set; }         // Safe amount to craft
    public int MaxSafeQuantity { get; set; }             // Upper limit
    
    // Competition
    public Dictionary<int, int> PriceDistribution { get; set; }  // price -> count
    public int YourCompetitorRank { get; set; }          // Where you'd place
    
    // Timestamps
    public DateTime LastUpdated { get; set; }
    public DateTime LastSale { get; set; }
}

public enum RiskLevel
{
    Low,        // 0-25
    Medium,     // 25-50
    High,       // 50-75
    VeryHigh    // 75-100
}

public enum MarketWarning
{
    MarketCrashRisk,
    LowDemand,
    PriceWarActive,
    StaleMarket,
    HighCompetition,
    OversupplyExpected
}
```

## 🔄 Real-World Scenarios

### Scenario 1: Daily Consumable (Food)
- High velocity (100+ sales/day)
- Many listings (200+) but they sell fast
- Supply/Demand: 2.0 (balanced)
- **Verdict:** Safe to craft in bulk, high turnover

### Scenario 2: Raid Food (Patch Day)
- Extreme velocity (500+ sales/day)
- Listings sold out constantly
- Supply/Demand: 0.3 (undersupplied!)
- **Verdict:** GOLDMINE - craft as much as you can

### Scenario 3: Glamour Gear (Niche)
- Low velocity (1-2 sales/week)
- Few listings (5-10)
- Supply/Demand: 25 (oversupplied)
- **Verdict:** AVOID - takes months to sell

### Scenario 4: Housing Item (Patch Week)
- Medium velocity (10/day) but declining
- Many listings (150+) all undercutting
- Supply/Demand: 15 (saturated)
- **Verdict:** TOO LATE - market already crashed

## 🎓 Educational Features

### Market Wisdom Tooltips
Show educational popups to help users learn:

- "Why is this risky?" → Explain the metrics
- "What's a good velocity?" → Show benchmarks
- "How to avoid market crashes?" → Best practices

### Historical Examples
"On Jan 15, this item had 200 listings and crashed from 80K to 20K in 48 hours"

## 🚀 Advanced Features (Future)

1. **Predictive Analytics**
   - ML model to predict price trends
   - Patch day surge predictions
   - Seasonal demand patterns

2. **Portfolio Theory**
   - Diversification suggestions
   - Don't put all gil in one item
   - Balance high-risk/high-reward with stable income

3. **Alerts**
   - "Market opening up!" (listings dropping)
   - "Demand spike!" (velocity increasing)
   - "Price crash imminent" (oversupply detected)

4. **Crowd Intelligence**
   - Track what other Aurum users are viewing
   - Warn if many users targeting same item
   - Reduce oversupply risks from plugin users

---

## ✅ Implementation Priority

**Phase 1 (Core):**
- Sale velocity calculation
- Supply/Demand ratio
- Risk score
- Basic warnings

**Phase 2 (Enhanced):**
- Market momentum
- Price stability
- Quantity recommendations
- Competition analysis

**Phase 3 (Advanced):**
- Predictive models
- Portfolio analysis
- Crowd intelligence
- Educational features

---

This demand analysis turns Aurum from a "profit calculator" into a **smart market advisor**!
