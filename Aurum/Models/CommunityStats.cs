using System;
using System.Collections.Generic;

namespace Aurum.Models
{
    public class LocalMarketStats
    {
        public DateTime LastUpdated { get; set; }
        public int TotalItemsAnalyzed { get; set; }
        
        // Market Health
        public Dictionary<PriceTrend, int> TrendDistribution { get; set; } = new();
        public double AverageVolatility { get; set; }
        
        // Top Lists
        public List<MarketHighlight> TopVolatileItems { get; set; } = new();
        public List<MarketHighlight> TopDemandItems { get; set; } = new();
        public List<MarketHighlight> TopOpportunities { get; set; } = new();
    }

    public class MarketHighlight
    {
        public uint ItemId { get; set; }
        public string WorldName { get; set; } = string.Empty;
        public float Value { get; set; } // Context dependent (volatility, velocity, score)
        public string Label { get; set; } = string.Empty;
    }
}
