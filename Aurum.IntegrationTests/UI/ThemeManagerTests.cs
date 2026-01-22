using Aurum.Utils;
using System.Numerics;
using Xunit;
using Aurum;

namespace Aurum.IntegrationTests.UI;

public class ThemeManagerTests
{
    // Now we can test the color retrieval logic directly

    [Fact]
    public void ThemeEnum_ContainsHighContrast()
    {
        Assert.Contains(Theme.HighContrast, Enum.GetValues<Theme>());
    }

    [Theory]
    [InlineData(Theme.Dark)]
    [InlineData(Theme.Light)]
    [InlineData(Theme.HighContrast)]
    [InlineData(Theme.Default)]
    public void GetThemeColors_ReturnsColorsForDefinedThemes(Theme theme)
    {
        // Act
        var colors = ThemeManager.GetThemeColors(theme);

        // Assert
        // Just verify alpha is non-zero to ensure we got something displayable
        Assert.NotEqual(0f, colors.TitleBg.W);
        Assert.NotEqual(0f, colors.TitleBgActive.W);
        Assert.NotEqual(0f, colors.TitleBgCollapsed.W);
    }

    [Fact]
    public void HighContrastTheme_UsesBlackAndHighVisColors()
    {
        // Act
        var colors = ThemeManager.GetThemeColors(Theme.HighContrast);

        // Assert
        // Background should be very dark (Black)
        Assert.True(colors.TitleBg.X < 0.2f && colors.TitleBg.Y < 0.2f && colors.TitleBg.Z < 0.2f);
        
        // Active should be high visibility (Red in current implementation)
        Assert.True(colors.TitleBgActive.X > 0.7f && colors.TitleBgActive.Y < 0.3f);
    }
}
