# Aurum - User Guide

Aurum helps you make smarter crafting decisions by analyzing market data. This guide explains how to use the dashboard effectively.

## 🌟 The Dashboard

The dashboard is your main hub for finding profitable crafts. Open it by typing `/aurum` in chat.

### Main Interface

The dashboard consists of several key areas:

1. **Search Bar**: Filter recipes by name.
2. **Filters**: Narrow down results by Job, Level, and Risk tolerance.
3. **Data Grid**: The list of recipes matching your criteria.
4. **Detail Panel**: In-depth analysis of the selected item.

### Understanding the Data Grid

Each row in the grid represents a crafting recipe with key metrics:

*   **Item**: Name of the item.
*   **Profit**: Expected profit per item (Market Price - Crafting Cost).
*   **Margin**: Profit as a percentage of cost. Higher is better.
*   **Velocity**: How fast the item sells (sales per day).
*   **Risk**: A color-coded risk assessment (Green = Low, Yellow = Medium, Red = High).
*   **Score**: Aurum's overall recommendation score (0-100).

### 🛡️ Understanding Risk Scores

Aurum assigns a Risk Score (0-100) to every item to help you avoid bad investments. The score is calculated based on market volatility, competition, and demand stability.

**Risk Levels:**
*   **Low (0-24)**: Safe bets. Consistent demand and stable prices.
*   **Medium (25-49)**: Good opportunities but require monitoring.
*   **High (50-74)**: Risky. Prices fluctuate wildly or demand is dropping.
*   **Very High (75-100)**: Dangerous. Likely to result in a loss due to market crashes or zero demand.

### ⚠️ Reading Market Warnings

Aurum analyzes market conditions and displays specific warnings when it detects potential issues. These appear as icons in the grid or detailed messages in the panel.

*   **Market Crash Risk** (Danger): Prices are trending down rapidly. Entering this market now may result in selling below cost.
*   **Price War Active** (Danger): Sellers are aggressively undercutting each other. Expect low margins and high frustration.
*   **Low Demand** (Warning): The item sells very slowly. You might be holding stock for weeks.
*   **Stale Market** (Warning): The item hasn't sold in a significant amount of time. Data may be outdated or the item is dead.
*   **High Competition** (Warning): There are many distinct sellers. It will be hard to keep your listing the cheapest.
*   **Oversupply Expected** (Warning): The number of listings is very high compared to the daily sales volume. Prices are likely to drop.
*   **API Unreachable** (Info): Cannot fetch fresh data from Universalis. Prices shown may be old.

### 🔍 Finding Profitable Crafts

1.  **Set your Job**: Select your crafter class (e.g., Alchemist, Culinarian) from the dropdown.
2.  **Filter by Level**: Choose a level range appropriate for your character.
3.  **Sort by Score**: Click the "Score" column header to see the best recommendations first.
4.  **Check Risk**: Look for items with Low or Medium risk. Avoid High risk items unless you know the market well.
5.  **Verify Velocity**: Ensure the item has a decent sale velocity (e.g., > 1 sale/day) so you aren't stuck with stock.

## 📊 Detail Panel Analysis

Clicking an item reveals the Detail Panel with advanced metrics:

### Charts
*   **Price History**: View price trends over the last week.
*   **Sales Volume**: See how many items are sold daily.

### Market Indicators
*   **Supply/Demand Ratio**:
    *   `< 1.0`: Undersupplied (Great opportunity!)
    *   `1.0 - 3.0`: Balanced
    *   `> 3.0`: Oversupplied (Competition is high)
*   **Competition**: Number of other sellers listing the item.

### Recommendations
Aurum provides a clear "Buy", "Sell", or "Avoid" recommendation based on its analysis. It also suggests a **Safe Quantity** to craft to avoid flooding the market.

## ⚙️ Configuration

Access settings via `/aurum config` or the cog icon on the dashboard.

*   **Data Source**: Choose between Universalis (default) or other providers if available.
*   **Refresh Rate**: How often market data is updated.
*   **Risk Tolerance**: Hide items above a certain risk level automatically.
*   **UI Scale**: Adjust the size of the interface.

## ❓ Troubleshooting

*   **"No Data"**: Ensure you have visited the market board recently or that Universalis has data for your world.
*   **Plugin not showing**: Check `/xlplugins` to ensure Aurum is enabled.

---
*Happy Crafting!*
