using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace Aurum.Windows;

public class DebugWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public DebugWindow(Plugin plugin) : base("Aurum Debug Tools")
    {
        this.plugin = plugin;
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("DebugTabs"))
        {
            if (ImGui.BeginTabItem("General"))
            {
                DrawGeneralTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawGeneralTab()
    {
        ImGui.Text("Aurum Debug Interface");
        ImGui.Separator();
        
        ImGui.Text($"Plugin Version: {plugin.GetType().Assembly.GetName().Version}");
        
        if (ImGui.Button("Clear All Cache"))
        {
            plugin.CacheService.Clear();
        }
        
        // ActiveUploads is not available in UniversalisService based on error, removing for now
        // ImGui.Text($"Active Uploads: {plugin.UniversalisService.ActiveUploads}");
    }
}
