using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Aurum.Utils;

public static class ThemeManager
{
    public static void PushWindowStyles(Theme theme)
    {
        var colors = GetThemeColors(theme);
        ImGui.PushStyleColor(ImGuiCol.TitleBg, colors.TitleBg);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, colors.TitleBgActive);
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, colors.TitleBgCollapsed);
    }

    public static void PopWindowStyles()
    {
        ImGui.PopStyleColor(3);
    }

    public static ThemeColors GetThemeColors(Theme theme)
    {
        return theme switch
        {
            Theme.Dark => new ThemeColors(
                new Vector4(0.15f, 0.15f, 0.17f, 0.95f),   // Dark slate/charcoal
                new Vector4(0.25f, 0.35f, 0.45f, 1.0f),    // Muted blue-grey active
                new Vector4(0.1f, 0.1f, 0.12f, 0.7f)       // Darker collapsed
            ),
            Theme.Light => new ThemeColors(
                new Vector4(0.7f, 0.7f, 0.7f, 1.0f),       // Light grey
                new Vector4(0.4f, 0.6f, 0.9f, 1.0f),       // Blue active
                new Vector4(0.6f, 0.6f, 0.6f, 0.8f)        // Grey collapsed
            ),
             Theme.HighContrast => new ThemeColors(
                new Vector4(0.1f, 0.1f, 0.1f, 1.0f),       // Black
                new Vector4(0.8f, 0.0f, 0.0f, 1.0f),       // High vis Red active (or Blue) - let's go with a strong Blue for accessibility usually, but let's stick to simple contrast
                new Vector4(0.0f, 0.0f, 0.0f, 1.0f)        // Black collapsed
            ),
            _ => new ThemeColors( // Default (Gold)
                new Vector4(0.4f, 0.3f, 0f, 0.8f),
                new Vector4(0.7f, 0.55f, 0f, 0.9f),
                new Vector4(0.4f, 0.3f, 0f, 0.5f)
            )
        };
    }

    public record struct ThemeColors(Vector4 TitleBg, Vector4 TitleBgActive, Vector4 TitleBgCollapsed);
}
