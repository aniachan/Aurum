using System;
using System.Collections.Generic;
using System.Linq;

namespace Aurum.Models;

/// <summary>
/// Suggestion for an alternative item to craft/sell
/// </summary>
public class AlternativeItemSuggestion
{
    public uint OriginalItemId { get; set; }
    public uint SuggestedItemId { get; set; }
    public string SuggestedItemName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public float ScoreImprovement { get; set; } // How much better is the score?
    public int RiskDifference { get; set; } // Negative is safer
}
