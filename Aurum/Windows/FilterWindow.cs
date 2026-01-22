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
        
        // Search Section
        if (ImGui.CollapsingHeader("Search", ImGuiTreeNodeFlags.DefaultOpen))
        {
            string nameSearch = criteria.NameSearch;
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Item Name:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.InputText("##NameSearch", ref nameSearch, 100))
            {
                criteria.NameSearch = nameSearch;
                changed = true;
            }
        }
        
        // Basic Filters Section
        if (ImGui.CollapsingHeader("Basic Requirements", ImGuiTreeNodeFlags.DefaultOpen))
        {
            // Level Range (Equip Level)
            int minLvl = criteria.MinLevel;
            int maxLvl = criteria.MaxLevel >= 100 ? 100 : criteria.MaxLevel; // Cap display at current level cap
            
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Equip Level:");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Required character level to equip the item.");
            
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            if (ImGui.DragInt("##MinLevel", ref minLvl, 0.5f, 1, 100))
            {
                // Ensure Min doesn't exceed Max (if Max is reasonable)
                int safeMax = maxLvl == 0 ? 100 : maxLvl;
                criteria.MinLevel = Math.Clamp(minLvl, 1, safeMax);
                changed = true;
            }
            
            ImGui.SameLine();
            ImGui.Text("-");
            ImGui.SameLine();
            
            ImGui.SetNextItemWidth(100);
            if (ImGui.DragInt("##MaxLevel", ref maxLvl, 0.5f, 1, 100))
            {
                // Ensure Max doesn't go below Min
                criteria.MaxLevel = Math.Clamp(maxLvl, minLvl, 100);
                changed = true;
            }

            // Item Level Range (iLvl)
            int minILvl = criteria.MinItemLevel;
            int maxILvl = criteria.MaxItemLevel >= 999 ? 999 : criteria.MaxItemLevel; // Cap display
            
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Item Level: ");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The item's distinct power level (iLvl).");
            
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            if (ImGui.DragInt("##MinItemLevel", ref minILvl, 1f, 1, 999))
            {
                int safeMax = maxILvl == 0 ? 999 : maxILvl;
                criteria.MinItemLevel = Math.Clamp(minILvl, 1, safeMax);
                changed = true;
            }
            
            ImGui.SameLine();
            ImGui.Text("-");
            ImGui.SameLine();
            
            ImGui.SetNextItemWidth(100);
            if (ImGui.DragInt("##MaxItemLevel", ref maxILvl, 1f, 1, 999))
            {
                criteria.MaxItemLevel = Math.Clamp(maxILvl, minILvl, 999);
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
                ImGui.TextDisabled("Select specific jobs to include:");
                
                var jobs = new[] { "CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL" };
                bool allJobs = criteria.IncludedJobIds.Count == 0;
                
                if (ImGui.Checkbox("All Jobs", ref allJobs))
                {
                    if (allJobs)
                        criteria.IncludedJobIds.Clear();
                    changed = true;
                }
                
                if (!allJobs)
                {
                    int cols = 4;
                    int i = 0;
                    foreach (var job in jobs)
                    {
                        if (i % cols != 0) ImGui.SameLine();
                        
                        bool isIncluded = criteria.IncludedJobIds.Contains(job);
                        if (ImGui.Checkbox(job, ref isIncluded))
                        {
                            if (isIncluded)
                                criteria.IncludedJobIds.Add(job);
                            else
                                criteria.IncludedJobIds.Remove(job);
                                
                            // If all deselected, maybe switch back to "All"? 
                            // Or leave empty as "None"? Let's leave as "None" if user explicitly unchecked all.
                            changed = true;
                        }
                        i++;
                    }
                }
                
                ImGui.TreePop();
            }

            // Equipment Slots
            if (ImGui.TreeNode("Equipment Slots"))
            {
                // Helper for slot checkbox
                void SlotCheckbox(string label, EquipSlot slot)
                {
                    bool included = criteria.IncludedEquipSlots.Contains(slot);
                    if (ImGui.Checkbox(label, ref included))
                    {
                        if (included) criteria.IncludedEquipSlots.Add(slot);
                        else criteria.IncludedEquipSlots.Remove(slot);
                        changed = true;
                    }
                }
                
                ImGui.TextDisabled("Filter by equipment slot:");
                
                // Left Side (Weapon/Shield/Head/Body/Hands/Waist)
                ImGui.BeginGroup();
                SlotCheckbox("Main Hand", EquipSlot.MainHand);
                SlotCheckbox("Head", EquipSlot.Head);
                SlotCheckbox("Body", EquipSlot.Body);
                SlotCheckbox("Hands", EquipSlot.Hands);
                SlotCheckbox("Legs", EquipSlot.Legs);
                SlotCheckbox("Feet", EquipSlot.Feet);
                ImGui.EndGroup();
                
                ImGui.SameLine(150);
                
                // Right Side (OffHand/Accessories)
                ImGui.BeginGroup();
                SlotCheckbox("Off Hand", EquipSlot.OffHand);
                SlotCheckbox("Ears", EquipSlot.Ears);
                SlotCheckbox("Neck", EquipSlot.Neck);
                SlotCheckbox("Wrists", EquipSlot.Wrists);
                SlotCheckbox("Rings", EquipSlot.Ring);
                ImGui.EndGroup();

                ImGui.TreePop();
            }
            
            // Item Categories
            if (ImGui.TreeNode("Item Sources"))
            {
                bool crafted = criteria.IncludeCrafted;
                if (ImGui.Checkbox("Crafted Items", ref crafted))
                {
                    criteria.IncludeCrafted = crafted;
                    changed = true;
                }

                bool gathered = criteria.IncludeGathered;
                if (ImGui.Checkbox("Gathered Items", ref gathered))
                {
                    criteria.IncludeGathered = gathered;
                    changed = true;
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Items obtained via Mining, Botany, or Fishing.");

                bool vendor = criteria.IncludeVendor;
                if (ImGui.Checkbox("Vendor Items", ref vendor))
                {
                    criteria.IncludeVendor = vendor;
                    changed = true;
                }

                ImGui.Spacing();

                bool tradeable = criteria.IncludeMarketTradeable;
                if (ImGui.Checkbox("Market Prohibited / Untradeable", ref tradeable))
                {
                     // Logic inversion in UI vs Model often tricky, but here we want to Include Tradeable usually.
                     // Wait, the checkbox says "Market Prohibited" but the model says "IncludeMarketTradeable".
                     // Let's stick to positive affirmation for checkboxes usually.
                }
                
                // Let's redo the UI for Tradeability to be clearer
                bool includeTradeable = criteria.IncludeMarketTradeable;
                if (ImGui.Checkbox("Market Tradeable", ref includeTradeable))
                {
                    criteria.IncludeMarketTradeable = includeTradeable;
                    changed = true;
                }
                
                bool excludeUntradable = criteria.ExcludeUntradable;
                if (ImGui.Checkbox("Exclude Untradeable / Ex", ref excludeUntradable))
                {
                    criteria.ExcludeUntradable = excludeUntradable;
                    changed = true;
                }
                
                ImGui.TreePop();
            }

            // Item Categories
            if (ImGui.TreeNode("Item Types"))
            {
                bool combat = criteria.IncludeCombatGear;
                if (ImGui.Checkbox("Combat Gear (Weapons & Armor)", ref combat))
                {
                    criteria.IncludeCombatGear = combat;
                    changed = true;
                }

                bool craftGather = criteria.IncludeCraftingGatheringGear;
                if (ImGui.Checkbox("Crafting & Gathering Gear", ref craftGather))
                {
                    criteria.IncludeCraftingGatheringGear = craftGather;
                    changed = true;
                }

                bool consumables = criteria.IncludeConsumables;
                if (ImGui.Checkbox("Consumables (Food, Potions)", ref consumables))
                {
                    criteria.IncludeConsumables = consumables;
                    changed = true;
                }

                bool materials = criteria.IncludeMaterials;
                if (ImGui.Checkbox("Crafting Materials", ref materials))
                {
                    criteria.IncludeMaterials = materials;
                    changed = true;
                }

                bool furniture = criteria.IncludeFurniture;
                if (ImGui.Checkbox("Furniture & Housing", ref furniture))
                {
                    criteria.IncludeFurniture = furniture;
                    changed = true;
                }
                
                ImGui.TreePop();
            }

            // Additional Properties
            if (ImGui.TreeNode("Item Properties"))
            {
                // Rarity Range
                int minRarity = criteria.MinRarity;
                int maxRarity = criteria.MaxRarity;
                
                ImGui.Text("Rarity (1-7):");
                ImGui.SetNextItemWidth(100);
                if (ImGui.DragInt("##MinRarity", ref minRarity, 0.1f, 1, 7))
                {
                    criteria.MinRarity = Math.Clamp(minRarity, 1, 7);
                    changed = true;
                }
                ImGui.SameLine();
                ImGui.Text("-");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100);
                if (ImGui.DragInt("##MaxRarity", ref maxRarity, 0.1f, 1, 7))
                {
                    criteria.MaxRarity = Math.Clamp(maxRarity, criteria.MinRarity, 7);
                    changed = true;
                }
                
                // Materia Slots
                int minMateria = criteria.MinMateriaSlots;
                if (ImGui.SliderInt("Min Materia Slots", ref minMateria, 0, 5))
                {
                    criteria.MinMateriaSlots = minMateria;
                    changed = true;
                }
                
                ImGui.Spacing();
                
                // Boolean Flags
                bool dyeable = criteria.IsDyeableOnly;
                if (ImGui.Checkbox("Dyeable Only", ref dyeable))
                {
                    criteria.IsDyeableOnly = dyeable;
                    changed = true;
                }
                
                bool collectable = criteria.IsCollectableOnly;
                if (ImGui.Checkbox("Collectable Only", ref collectable))
                {
                    criteria.IsCollectableOnly = collectable;
                    changed = true;
                }
                
                bool excludeUnique = criteria.ExcludeUnique;
                if (ImGui.Checkbox("Exclude Unique/Untradeable", ref excludeUnique))
                {
                    criteria.ExcludeUnique = excludeUnique;
                    changed = true;
                }
                
                bool excludeUntradable = criteria.ExcludeUntradable;
                if (ImGui.Checkbox("Exclude Market Prohibited", ref excludeUntradable))
                {
                    criteria.ExcludeUntradable = excludeUntradable;
                    changed = true;
                }
                
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
