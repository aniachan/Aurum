namespace Aurum.Models;

/// <summary>
/// Types of market warnings that can be displayed to users
/// </summary>
public enum MarketWarning
{
    None,
    MarketCrashRisk,
    LowDemand,
    PriceWarActive,
    StaleMarket,
    HighCompetition,
    OversupplyExpected,
    ApiUnreachable
}

/// <summary>
/// Details about a specific market warning
/// </summary>
public class MarketWarningInfo
{
    public MarketWarning Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public WarningLevel Level { get; set; }
}

public enum WarningLevel
{
    Info,
    Warning,
    Danger
}
