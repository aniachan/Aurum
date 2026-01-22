using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Aurum.Models;
using Aurum.Services;
using Aurum.Services.Filtering;
using Aurum.Utils;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace Aurum.Windows;

public class FilterWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly ItemFilterService filterService;
    
    // UI State
    private string presetName = "";
    private string selectedPresetId = "";
    
    public FilterWindow(Plugin plugin) 
        : base("Advanced Filters##AurumFilterWindow", ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
        this.filterService = new ItemFilterService(plugin.Configuration);
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 500),
            MaximumSize = new Vector2(800, 1000)
        };
        
        // Hide window by default
        IsOpen = false;
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("FilterTabs"))
        {
            if (ImGui.BeginTabItem("Current Filter"))
            {
                DrawCurrentFilterTab();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Presets"))
            {
                DrawPresetsTab();
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
    }

    private void DrawCurrentFilterTab()
    {
        var criteria = filterService.CurrentCriteria;
        bool changed = false;
        
        ImGui.Spacing();
        ImGui.TextDisabled("Configure advanced filtering options below.");
        ImGui.Separator();
        
        // Basic Filters Section
        if (ImGui.CollapsingHeader("Basic Requirements", ImGuiTreeNodeFlags.DefaultOpen))
        {
            // Level Range
            int minLvl = criteria.MinLevel;
            int maxLvl = criteria.MaxLevel;
            ImGui.Text("Level Range:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("##MinLevel", ref minLvl))
            {
                criteria.MinLevel = Math.Clamp(minLvl, 1, 100);
                changed = true;
            }
            ImGui.SameLine();
            ImGui.Text("-");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("##MaxLevel", ref maxLvl))
            {
                criteria.MaxLevel = Math.Clamp(maxLvl, 1, 100);
                changed = true;
            }

            // Item Level Range
            int minILvl = criteria.MinItemLevel;
            int maxILvl = criteria.MaxItemLevel;
            ImGui.Text("Item Level: ");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("##MinItemLevel", ref minILvl))
            {
                criteria.MinItemLevel = Math.Max(1, minILvl);
                changed = true;
            }
            ImGui.SameLine();
            ImGui.Text("-");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("##MaxItemLevel", ref maxILvl))
            {
                criteria.MaxItemLevel = Math.Max(1, maxILvl);
                changed = true;
            }
        }
        
        // Market & Profit Section
        if (ImGui.CollapsingHeader("Market & Profit", ImGuiTreeNodeFlags.DefaultOpen))
        {
            // Min Profit
            int minProfit = criteria.MinProfit;
            if (ImGui.InputInt("Min Profit (Gil)", ref minProfit, 1000))
            {
                criteria.MinProfit = Math.Max(0, minProfit);
                changed = true;
            }
            
            // Min ROI
            float minRoi = criteria.MinRoi;
            if (ImGui.SliderFloat("Min ROI %", ref minRoi, 0f, 200f, "%.0f%%"))
            {
                criteria.MinRoi = minRoi;
                changed = true;
            }
            
            // Min Velocity
            float minVelocity = criteria.MinVelocity;
            if (ImGui.SliderFloat("Min Sales/Day", ref minVelocity, 0f, 50f, "%.1f"))
            {
                criteria.MinVelocity = minVelocity;
                changed = true;
            }
            
            // Profitable Only
            bool profitableOnly = criteria.ProfitableOnly;
            if (ImGui.Checkbox("Show Only Profitable Items", ref profitableOnly))
            {
                criteria.ProfitableOnly = profitableOnly;
                changed = true;
            }
        }
        
        // Categories Section
        if (ImGui.CollapsingHeader("Categories"))
        {
            // Job Classes
            if (ImGui.TreeNode("Jobs / Classes"))
            {
                // This would be populated with checkboxes for each crafter class
                // For now, placeholder
                ImGui.TextDisabled("Class selection coming soon...");
                ImGui.TreePop();
            }
            
            // Item Categories
            if (ImGui.TreeNode("Item Types"))
            {
                // This would be populated with checkboxes for item categories
                // For now, placeholder
                ImGui.TextDisabled("Category selection coming soon...");
                ImGui.TreePop();
            }
        }
        
        ImGui.Separator();
        
        // Action Buttons
        if (ImGui.Button("Apply Filter"))
        {
            // Signal to dashboard to refresh with new filter
            // plugin.DashboardWindow.ApplyAdvancedFilter(criteria);
            IsOpen = false;
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Reset"))
        {
            filterService.Reset();
            changed = true;
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Save as Preset"))
        {
            ImGui.OpenPopup("SavePresetPopup");
        }
        
        // Save Preset Popup
        if (ImGui.BeginPopup("SavePresetPopup"))
        {
            ImGui.Text("Preset Name:");
            ImGui.InputText("##PresetName", ref presetName, 64);
            
            if (ImGui.Button("Save"))
            {
                if (!string.IsNullOrWhiteSpace(presetName))
                {
                    filterService.SavePreset(presetName, criteria);
                    presetName = "";
                    ImGui.CloseCurrentPopup();
                }
            }
            ImGui.EndPopup();
        }

        if (changed)
        {
            // Auto-update if desired, or just wait for Apply button
        }
    }

    private void DrawPresetsTab()
    {
        var presets = filterService.GetPresets();
        
        if (!presets.Any())
        {
            ImGui.TextDisabled("No presets saved.");
            return;
        }
        
        ImGui.BeginChild("PresetsList", new Vector2(0, -40), true);
        
        foreach (var preset in presets)
        {
            bool isSelected = selectedPresetId == preset.Id;
            if (ImGui.Selectable($"{preset.Name}##{preset.Id}", isSelected))
            {
                selectedPresetId = preset.Id;
                filterService.LoadPreset(preset.Id);
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"Level: {preset.Criteria.MinLevel}-{preset.Criteria.MaxLevel}\nProfit: >{preset.Criteria.MinProfit:N0}");
            }
        }
        
        ImGui.EndChild();
        
        if (ImGui.Button("Load Selected") && !string.IsNullOrEmpty(selectedPresetId))
        {
            filterService.LoadPreset(selectedPresetId);
            ImGui.BeginTabItem("Current Filter"); // Switch tab?
        }
        
        ImGui.SameLine();
        
        if (ImGui.Button("Delete") && !string.IsNullOrEmpty(selectedPresetId))
        {
            filterService.DeletePreset(selectedPresetId);
            selectedPresetId = "";
        }
    }

    public void Dispose()
    {
        // Cleanup resources
    }
}
