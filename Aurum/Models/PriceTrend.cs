namespace Aurum.Models;

/// <summary>
/// General direction of price movement
/// </summary>
public enum PriceTrend
{
    Stable,     // Price is relatively flat
    Rising,     // Price is trending up
    Falling,    // Price is trending down
    Volatile,   // Price is fluctuating wildly
    Unknown     // Insufficient data
}
