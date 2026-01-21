using System.Numerics;
using Aurum.Models;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;

namespace Aurum.Utils;

public static class UiUtils
{
    public static Vector4 GetRiskColor(RiskLevel level)
    {
        return level switch
        {
            RiskLevel.Low => new Vector4(0f, 1f, 0.5f, 1f),      // Green
            RiskLevel.Medium => new Vector4(1f, 1f, 0.5f, 1f),   // Yellow-Green
            RiskLevel.High => new Vector4(1f, 0.7f, 0f, 1f),     // Orange
            RiskLevel.VeryHigh => new Vector4(0.8f, 0.6f, 0f, 1f), // Darker gold
            _ => new Vector4(0.8f, 0.8f, 0.8f, 1f)               // Grey
        };
    }

    public static Vector4 GetWarningColor(WarningLevel level)
    {
        return level switch
        {
            WarningLevel.Danger => new Vector4(0.8f, 0.6f, 0f, 1f),   // Darker gold
            WarningLevel.Warning => new Vector4(1f, 0.7f, 0f, 1f),    // Orange
            _ => new Vector4(0.8f, 0.8f, 1f, 1f)                      // Light Blue/White
        };
    }

    public static string GetRiskIcon(RiskLevel level)
    {
        return level switch
        {
            RiskLevel.Low => FontAwesomeIcon.CheckCircle.ToIconString(),
            RiskLevel.Medium => FontAwesomeIcon.ExclamationTriangle.ToIconString(),
            RiskLevel.High => FontAwesomeIcon.Radiation.ToIconString(),
            RiskLevel.VeryHigh => FontAwesomeIcon.SkullCrossbones.ToIconString(),
            _ => FontAwesomeIcon.QuestionCircle.ToIconString()
        };
    }

    public static string GetWarningIcon(MarketWarning type)
    {
        return type switch
        {
            MarketWarning.MarketCrashRisk => FontAwesomeIcon.ChartLine.ToIconString(), // Should be going down, but ChartLine is generic enough
            MarketWarning.LowDemand => FontAwesomeIcon.Walking.ToIconString(),
            MarketWarning.PriceWarActive => FontAwesomeIcon.Gavel.ToIconString(), // Or something indicating conflict
            MarketWarning.StaleMarket => FontAwesomeIcon.HourglassEnd.ToIconString(),
            MarketWarning.HighCompetition => FontAwesomeIcon.Users.ToIconString(),
            MarketWarning.OversupplyExpected => FontAwesomeIcon.BoxOpen.ToIconString(),
            MarketWarning.ApiUnreachable => FontAwesomeIcon.Wifi.ToIconString(), // Using Wifi icon (strikethrough usually implies it's off)
            _ => FontAwesomeIcon.ExclamationCircle.ToIconString()
        };
    }

    public static void DrawRiskBadge(RiskLevel level)
    {
        var color = GetRiskColor(level);
        var icon = GetRiskIcon(level);
        
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(color, icon);
        ImGui.PopFont();
        ImGui.SameLine();
        ImGui.TextColored(color, level.ToString());
    }
}
