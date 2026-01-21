using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Aurum.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    // We give this window a constant ID using ###.
    // This allows for labels to be dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("A Wonderful Configuration Window###With a constant ID")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(300, 150);
        SizeCondition = ImGuiCond.Always;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        // Flags must be added or removed before Draw() is being called, or they won't apply
        if (configuration.IsConfigWindowMovable)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
    }

    public override void Draw()
    {
        // Can't ref a property, so use a local copy
        var uiScale = configuration.UIScale;
        if (ImGui.SliderFloat("UI Scale", ref uiScale, 0.5f, 2.0f))
        {
            configuration.UIScale = uiScale;
            // Can save immediately on change if you don't want to provide a "Save and Close" button
            configuration.Save();
        }

        var movable = configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Movable Config Window", ref movable))
        {
            configuration.IsConfigWindowMovable = movable;
            configuration.Save();
        }
        
        var enableCharts = configuration.EnableAnimatedCharts;
        if (ImGui.Checkbox("Enable Animated Charts", ref enableCharts))
        {
            configuration.EnableAnimatedCharts = enableCharts;
            configuration.Save();
        }

        var cacheDuration = configuration.MarketDataCacheDurationSeconds;
        if (ImGui.InputInt("Market Data Cache Duration (Seconds)", ref cacheDuration))
        {
            configuration.MarketDataCacheDurationSeconds = cacheDuration;
            configuration.Save();
        }
        
        ImGui.Separator();
        ImGui.Text("Database Settings");
        
        var dbSize = Aurum.Plugin.Instance?.DatabaseService?.GetDatabaseSize() ?? 0;
        ImGui.Text($"Database Size: {dbSize / 1024.0 / 1024.0:F2} MB");
        
        if (ImGui.Button("Optimize Database (VACUUM)"))
        {
            Aurum.Plugin.Instance?.DatabaseService?.Vacuum();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Reclaims unused space and defragments the database file.\nMay take a few seconds.");
        }

        ImGui.Separator();
        ImGui.Text("API Usage Statistics");
        var limiter = Aurum.Plugin.Instance?.RateLimiter;
        if (limiter != null)
        {
            ImGui.Text($"Requests (Last Minute): {limiter.RequestsLastMinute}");
            ImGui.Text($"Requests (Last Hour): {limiter.RequestsLastHour}");
            ImGui.Text($"Requests (Today): {limiter.RequestsToday}");
            ImGui.Text($"Total Requests: {limiter.TotalRequests}");
            ImGui.Text($"Rate Limited: {limiter.RateLimitedRequests}");
            ImGui.Text($"Errors/Retries: {limiter.TotalErrors} / {limiter.TotalRetries}");
        }
    }
}
