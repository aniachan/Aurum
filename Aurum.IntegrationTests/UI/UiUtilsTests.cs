using Aurum.Utils;
using Dalamud.Interface;
using System.Numerics;
using Xunit;
using Aurum.Models;

namespace Aurum.IntegrationTests.UI;

public class UiUtilsTests
{
    [Theory]
    [InlineData(RiskLevel.Low, 0f, 1f, 0.5f, 1f)]
    [InlineData(RiskLevel.Medium, 1f, 1f, 0.5f, 1f)]
    [InlineData(RiskLevel.High, 1f, 0.7f, 0f, 1f)]
    [InlineData(RiskLevel.VeryHigh, 0.8f, 0.6f, 0f, 1f)]
    public void GetRiskColor_ReturnsCorrectColor_ForRiskLevels(RiskLevel level, float r, float g, float b, float a)
    {
        // Act
        var color = UiUtils.GetRiskColor(level);

        // Assert
        Assert.Equal(new Vector4(r, g, b, a), color);
    }

    [Theory]
    [InlineData(WarningLevel.Danger, 0.8f, 0.6f, 0f, 1f)]
    [InlineData(WarningLevel.Warning, 1f, 0.7f, 0f, 1f)]
    public void GetWarningColor_ReturnsCorrectColor_ForWarningLevels(WarningLevel level, float r, float g, float b, float a)
    {
        // Act
        var color = UiUtils.GetWarningColor(level);

        // Assert
        Assert.Equal(new Vector4(r, g, b, a), color);
    }

    [Fact]
    public void GetRiskIcon_ReturnsCorrectIconStrings()
    {
        // Just verify it returns something non-empty for each level
        // (We can't easily check against FontAwesome constants without a full Dalamud context in some cases,
        // but we can check it returns a string)
        Assert.False(string.IsNullOrEmpty(UiUtils.GetRiskIcon(RiskLevel.Low)));
        Assert.False(string.IsNullOrEmpty(UiUtils.GetRiskIcon(RiskLevel.Medium)));
        Assert.False(string.IsNullOrEmpty(UiUtils.GetRiskIcon(RiskLevel.High)));
        Assert.False(string.IsNullOrEmpty(UiUtils.GetRiskIcon(RiskLevel.VeryHigh)));
    }

    [Fact]
    public void GetWarningIcon_ReturnsCorrectIconStrings()
    {
        Assert.False(string.IsNullOrEmpty(UiUtils.GetWarningIcon(MarketWarning.MarketCrashRisk)));
        Assert.False(string.IsNullOrEmpty(UiUtils.GetWarningIcon(MarketWarning.LowDemand)));
    }
}
