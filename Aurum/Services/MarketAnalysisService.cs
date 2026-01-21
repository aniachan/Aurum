using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Aurum.Models;

namespace Aurum.Services;

/// <summary>
/// Service for analyzing market data and calculating demand metrics
/// </summary>
public class MarketAnalysisService
{
    private readonly IPluginLog log;
    private readonly Configuration config;
    
    public MarketAnalysisService(IPluginLog log, Configuration config)
    {
        this.log = log;
        this.config = config;
    }
    
    /// <summary>
    /// Analyze market data and calculate all demand metrics
    /// </summary>
    public void AnalyzeMarket(MarketData marketData)
    {
        if (marketData.RecentHistory == null || !marketData.RecentHistory.Any())
        {
            // No sales history - mark as high risk
            marketData.SaleVelocity = 0;
            marketData.SupplyDemandRatio = float.MaxValue;
            marketData.EstimatedSellTimeDays = float.MaxValue;
            marketData.RiskScore = 100;
            marketData.RiskLevel = RiskLevel.VeryHigh;
            marketData.Warnings.Add(new MarketWarningInfo
            {
                Type = MarketWarning.StaleMarket,
                Message = "No recent sales data available",
                Details = "This item has no sales history. Demand is unknown.",
                Level = WarningLevel.Danger
            });
            return;
        }
        
        // Calculate metrics
        CalculateSaleVelocity(marketData);
        CalculateSupplyDemandRatio(marketData);
        CalculatePriceVolatility(marketData);
        CalculateMarketMomentum(marketData);
        CalculatePriceTrend(marketData);
        CalculatePriceDistribution(marketData);
        
        // Generate risk score
        CalculateRiskScore(marketData);
        
        // Generate warnings
        GenerateWarnings(marketData);
        
        // Calculate recommendation score
        CalculateRecommendationScore(marketData);
        
        // Calculate quantity recommendations
        CalculateQuantityRecommendations(marketData);
    }
    
    /// <summary>
    /// Calculate sales per day
    /// </summary>
    private void CalculateSaleVelocity(MarketData marketData)
    {
        var history = marketData.RecentHistory;
        if (!history.Any())
        {
            marketData.SaleVelocity = 0;
            return;
        }
        
        var oldestSale = history.Min(h => h.Timestamp);
        var newestSale = history.Max(h => h.Timestamp);
        var timeSpan = (newestSale - oldestSale).TotalDays;
        
        if (timeSpan < 0.01) // Less than ~15 minutes
            timeSpan = 0.1; // Assume 2.4 hours for very recent sales
        
        var totalQuantity = history.Sum(h => h.Quantity);
        marketData.SaleVelocity = (float)(totalQuantity / timeSpan);
        
        log.Debug($"Item {marketData.ItemId}: {totalQuantity} sales over {timeSpan:F1} days = {marketData.SaleVelocity:F2}/day");
    }
    
    /// <summary>
    /// Calculate supply vs demand ratio
    /// </summary>
    private void CalculateSupplyDemandRatio(MarketData marketData)
    {
        if (marketData.SaleVelocity < 0.01f)
        {
            marketData.SupplyDemandRatio = float.MaxValue;
            marketData.EstimatedSellTimeDays = float.MaxValue;
            return;
        }
        
        marketData.SupplyDemandRatio = marketData.CurrentListings / marketData.SaleVelocity;
        marketData.EstimatedSellTimeDays = marketData.SupplyDemandRatio;
    }
    
    /// <summary>
    /// Calculate price volatility (standard deviation / mean)
    /// </summary>
    private void CalculatePriceVolatility(MarketData marketData)
    {
        var prices = marketData.RecentHistory.Select(h => (double)h.PricePerUnit).ToList();
        
        if (prices.Count < 2)
        {
            marketData.PriceVolatility = 0;
            return;
        }
        
        var mean = prices.Average();
        var variance = prices.Sum(p => Math.Pow(p - mean, 2)) / prices.Count;
        var stdDev = Math.Sqrt(variance);
        
        marketData.PriceVolatility = mean > 0 ? (float)(stdDev / mean) : 0;
    }
    
    /// <summary>
    /// Calculate if demand is increasing or decreasing
    /// </summary>
    private void CalculateMarketMomentum(MarketData marketData)
    {
        var history = marketData.RecentHistory.OrderBy(h => h.Timestamp).ToList();
        
        if (history.Count < 10)
        {
            marketData.MarketMomentum = 0;
            return;
        }
        
        // Split into recent (last 30%) and older (first 70%)
        var splitPoint = (int)(history.Count * 0.7);
        var olderSales = history.Take(splitPoint).ToList();
        var recentSales = history.Skip(splitPoint).ToList();
        
        if (!olderSales.Any() || !recentSales.Any())
        {
            marketData.MarketMomentum = 0;
            return;
        }
        
        var olderTimeSpan = (olderSales.Max(h => h.Timestamp) - olderSales.Min(h => h.Timestamp)).TotalDays;
        var recentTimeSpan = (recentSales.Max(h => h.Timestamp) - recentSales.Min(h => h.Timestamp)).TotalDays;
        
        if (olderTimeSpan < 0.01) olderTimeSpan = 0.1;
        if (recentTimeSpan < 0.01) recentTimeSpan = 0.1;
        
        var olderVelocity = olderSales.Sum(h => h.Quantity) / olderTimeSpan;
        var recentVelocity = recentSales.Sum(h => h.Quantity) / recentTimeSpan;
        
        if (olderVelocity < 0.01)
        {
            marketData.MarketMomentum = recentVelocity > 0.01 ? 1.0f : 0f;
        }
        else
        {
            marketData.MarketMomentum = (float)((recentVelocity - olderVelocity) / olderVelocity);
        }
        
        // Clamp to -1 to 1
        marketData.MarketMomentum = Math.Clamp(marketData.MarketMomentum, -1f, 1f);
    }

    /// <summary>
    /// Analyze price trend (rising, falling, stable)
    /// </summary>
    private void CalculatePriceTrend(MarketData marketData)
    {
        if (marketData.RecentHistory.Count < 5)
        {
            marketData.Trend = PriceTrend.Unknown;
            return;
        }

        // If volatility is extremely high, it's just volatile
        if (marketData.PriceVolatility > 0.4f)
        {
            marketData.Trend = PriceTrend.Volatile;
            return;
        }

        // Simple linear regression or moving average comparison
        // Let's compare recent average vs older average
        var history = marketData.RecentHistory.OrderBy(h => h.Timestamp).ToList();
        var splitIndex = history.Count / 2;
        
        var olderHalf = history.Take(splitIndex).ToList();
        var newerHalf = history.Skip(splitIndex).ToList();
        
        if (!olderHalf.Any() || !newerHalf.Any())
        {
            marketData.Trend = PriceTrend.Unknown;
            return;
        }
        
        var oldAvg = olderHalf.Average(h => h.PricePerUnit);
        var newAvg = newerHalf.Average(h => h.PricePerUnit);
        
        var change = (newAvg - oldAvg) / oldAvg;
        
        if (change > 0.10) // > 10% increase
        {
            marketData.Trend = PriceTrend.Rising;
        }
        else if (change < -0.10) // > 10% decrease
        {
            marketData.Trend = PriceTrend.Falling;
        }
        else
        {
            marketData.Trend = PriceTrend.Stable;
        }
        
        log.Debug($"Item {marketData.ItemId} Trend: {marketData.Trend} (Change: {change:P1})");
    }
    
    /// <summary>
    /// Analyze price distribution of current listings
    /// </summary>
    private void CalculatePriceDistribution(MarketData marketData)
    {
        marketData.PriceDistribution.Clear();
        
        foreach (var listing in marketData.Listings)
        {
            var price = listing.PricePerUnit;
            if (marketData.PriceDistribution.ContainsKey(price))
            {
                marketData.PriceDistribution[price]++;
            }
            else
            {
                marketData.PriceDistribution[price] = 1;
            }
        }
        
        // Estimate where you'd rank if you list at average price
        var avgPrice = marketData.CurrentAveragePriceNQ > 0 ? marketData.CurrentAveragePriceNQ : marketData.MinPrice;
        marketData.YourCompetitorRank = marketData.Listings.Count(l => l.PricePerUnit < avgPrice) + 1;
    }
    
    /// <summary>
    /// Calculate overall risk score (0-100)
    /// </summary>
    private void CalculateRiskScore(MarketData marketData)
    {
        float riskScore = 0;
        
        // Supply/Demand contribution (0-25 points)
        float sdRatio = Math.Min(marketData.SupplyDemandRatio, 20f);
        riskScore += (sdRatio / 20f) * 25f;
        
        // Price volatility contribution (0-25 points)
        float volatility = Math.Min(marketData.PriceVolatility, 0.5f);
        riskScore += (volatility / 0.5f) * 25f;
        
        // Velocity contribution (0-25 points) - lower velocity = higher risk
        float velocityScore = marketData.SaleVelocity < 1f ? 25f :
                             marketData.SaleVelocity < 5f ? 15f :
                             marketData.SaleVelocity < 20f ? 5f : 0f;
        riskScore += velocityScore;
        
        // Competition contribution (0-25 points)
        float competitionScore = marketData.CurrentListings > 100 ? 25f :
                                marketData.CurrentListings > 50 ? 15f :
                                marketData.CurrentListings > 20 ? 5f : 0f;
        riskScore += competitionScore;
        
        marketData.RiskScore = (int)Math.Clamp(riskScore, 0, 100);
        
        // Assign risk level
        marketData.RiskLevel = marketData.RiskScore switch
        {
            < 25 => RiskLevel.Low,
            < 50 => RiskLevel.Medium,
            < 75 => RiskLevel.High,
            _ => RiskLevel.VeryHigh
        };

        // Generate detailed risk analysis
        marketData.RiskAnalysis = BuildRiskAnalysis(marketData, sdRatio, volatility, velocityScore, competitionScore);
    }
    
    private string BuildRiskAnalysis(MarketData marketData, float sdScore, float volScore, float velScore, float compScore)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Risk Score Breakdown:");
        sb.AppendLine($"- Supply/Demand: {sdScore:F0}/25 (Ratio: {marketData.SupplyDemandRatio:F1})");
        sb.AppendLine($"- Volatility: {volScore:F0}/25 ({(marketData.PriceVolatility*100):F1}%)");
        sb.AppendLine($"- Velocity: {velScore:F0}/25 ({marketData.SaleVelocity:F1}/day)");
        sb.AppendLine($"- Competition: {compScore:F0}/25 ({marketData.CurrentListings} listings)");
        return sb.ToString();
    }
    
    /// <summary>
    /// Generate market warnings based on conditions
    /// </summary>
    private void GenerateWarnings(MarketData marketData)
    {
        marketData.Warnings.Clear();
        
        // Market crash risk
        if (marketData.SupplyDemandRatio > 5.0f)
        {
            marketData.Warnings.Add(new MarketWarningInfo
            {
                Type = MarketWarning.MarketCrashRisk,
                Message = "Market oversupplied",
                Details = $"{marketData.CurrentListings} listings but only {marketData.SaleVelocity:F1} sales/day. Supply will last {marketData.EstimatedSellTimeDays:F1} days.",
                Level = WarningLevel.Danger
            });
        }
        
        // Low demand
        if (marketData.SaleVelocity < 1.0f && marketData.SaleVelocity > 0)
        {
            marketData.Warnings.Add(new MarketWarningInfo
            {
                Type = MarketWarning.LowDemand,
                Message = "Low sales volume",
                Details = $"Only {marketData.SaleVelocity:F2} sales per day. This item sells slowly.",
                Level = WarningLevel.Warning
            });
        }
        
        // Price war (high volatility + falling momentum)
        if (marketData.PriceVolatility > 0.3f && marketData.MarketMomentum < -0.2f)
        {
            marketData.Warnings.Add(new MarketWarningInfo
            {
                Type = MarketWarning.PriceWarActive,
                Message = "Price war detected",
                Details = $"Prices are volatile ({marketData.PriceVolatility:P0}) and demand is falling ({marketData.MarketMomentum:P0}).",
                Level = WarningLevel.Danger
            });
        }
        
        // Stale market
        if (marketData.LastSaleTime.HasValue)
        {
            var hoursSinceLastSale = (DateTime.UtcNow - marketData.LastSaleTime.Value).TotalHours;
            if (hoursSinceLastSale > 48)
            {
                marketData.Warnings.Add(new MarketWarningInfo
                {
                    Type = MarketWarning.StaleMarket,
                    Message = "No recent sales",
                    Details = $"Last sale was {hoursSinceLastSale / 24:F1} days ago. Market may be dead.",
                    Level = WarningLevel.Danger
                });
            }
        }
        
        // High competition
        if (marketData.CurrentListings > 50 && marketData.PriceDistribution.Any())
        {
            var mostCommonPrice = marketData.PriceDistribution.OrderByDescending(kvp => kvp.Value).First();
            if (mostCommonPrice.Value > 20)
            {
                marketData.Warnings.Add(new MarketWarningInfo
                {
                    Type = MarketWarning.HighCompetition,
                    Message = "Many sellers at same price",
                    Details = $"{mostCommonPrice.Value} sellers listing at {mostCommonPrice.Key:N0} gil. Hard to stand out.",
                    Level = WarningLevel.Warning
                });
            }
        }
    }
    
    /// <summary>
    /// Calculate weighted recommendation score (0-100)
    /// </summary>
    private void CalculateRecommendationScore(MarketData marketData)
    {
        // This will be enhanced by ProfitService to include profit metrics
        // For now, just score based on demand factors
        
        float score = 0;
        
        // Velocity score (0-25 points)
        float velocityScore = marketData.SaleVelocity >= 50 ? 25f :
                             marketData.SaleVelocity >= 10 ? 20f :
                             marketData.SaleVelocity >= 5 ? 15f :
                             marketData.SaleVelocity >= 1 ? 10f : 5f;
        score += velocityScore;
        
        // Supply/Demand score (0-20 points)
        float sdScore = marketData.SupplyDemandRatio < 1.0f ? 20f :
                       marketData.SupplyDemandRatio < 3.0f ? 15f :
                       marketData.SupplyDemandRatio < 7.0f ? 10f : 5f;
        score += sdScore;
        
        // Stability score (0-15 points)
        float stabilityScore = marketData.PriceVolatility < 0.1f ? 15f :
                              marketData.PriceVolatility < 0.3f ? 10f : 5f;
        score += stabilityScore;
        
        // Momentum score (0-10 points)
        float momentumScore = marketData.MarketMomentum > 0.2f ? 10f :
                             marketData.MarketMomentum > -0.2f ? 7f : 3f;
        score += momentumScore;
        
        marketData.RecommendationScore = (int)Math.Clamp(score, 0, 100);
    }
    
    /// <summary>
    /// Calculate safe quantities to craft
    /// </summary>
    private void CalculateQuantityRecommendations(MarketData marketData)
    {
        if (marketData.SaleVelocity < 0.1f)
        {
            marketData.RecommendedQuantity = 1;
            marketData.MaxSafeQuantity = 1;
            return;
        }
        
        // Recommended: 1-2 days worth of sales
        var dailyDemand = marketData.SaleVelocity;
        marketData.RecommendedQuantity = Math.Max(1, (int)(dailyDemand * 1.5f));
        
        // Max safe: 5 days worth, or until supply ratio hits 7.0
        var currentSupplyDays = marketData.CurrentListings / dailyDemand;
        var daysUntilOversupply = Math.Max(0, 7.0f - currentSupplyDays);
        marketData.MaxSafeQuantity = Math.Max(1, (int)(dailyDemand * Math.Min(5, daysUntilOversupply)));
        
        // Clamp based on risk level
        if (marketData.RiskLevel == RiskLevel.High)
        {
            marketData.RecommendedQuantity = Math.Min(marketData.RecommendedQuantity, 3);
            marketData.MaxSafeQuantity = Math.Min(marketData.MaxSafeQuantity, 5);
        }
        else if (marketData.RiskLevel == RiskLevel.VeryHigh)
        {
            marketData.RecommendedQuantity = 1;
            marketData.MaxSafeQuantity = 1;
        }
    }
}
