using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Aurum.Models;
using Aurum.Utils;

namespace Aurum.Windows;

public class ShoppingListWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private ShoppingList? currentList;
    
    public ShoppingListWindow(Plugin plugin) : base("Shopping List##AurumShoppingList", ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }
    
    public override void PreDraw()
    {
        ThemeManager.PushWindowStyles(plugin.Configuration.ColorTheme);
        ImGui.GetIO().FontGlobalScale = plugin.Configuration.UIScale;
    }
    
    public override void PostDraw()
    {
        ThemeManager.PopWindowStyles();
        ImGui.GetIO().FontGlobalScale = 1.0f;
    }

    public void SetList(ShoppingList list)
    {
        currentList = list;
        IsOpen = true;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (currentList == null || !currentList.Items.Any())
        {
            ImGui.Text("No shopping list generated.");
            return;
        }

        ImGui.Text($"Shopping List Created: {currentList.CreatedAt.ToLocalTime()}");
        
        if (ImGui.Button("Copy to Clipboard"))
        {
            ImGui.SetClipboardText(currentList.ToClipboardString());
        }

        ImGui.SameLine();

        if (ImGui.Button("Export to CSV"))
        {
             try 
             {
                 var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                 var fileName = $"Aurum_ShoppingList_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                 var filePath = System.IO.Path.Combine(documentsPath, fileName);
                 System.IO.File.WriteAllText(filePath, currentList.ToCsvString());
                 
                 // Show a tooltip or log? For now maybe just a momentary visual feedback could be nice but difficult with immediate mode
                 // Let's rely on standard logging if available via Plugin, but we don't have easy access to Log here directly unless we injected it.
                 // The 'plugin' field is available.
                 Plugin.Log.Information($"Shopping list exported to {filePath}");
             }
             catch (Exception ex)
             {
                 Plugin.Log.Error(ex, "Failed to export shopping list to CSV");
             }
        }
        
        ImGui.Separator();

        if (ImGui.BeginTabBar("ShoppingListTabs"))
        {
            if (ImGui.BeginTabItem("Materials"))
            {
                DrawMaterialsTable();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Crafting Plan"))
            {
                DrawCraftingPlan();
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
    }

    private void DrawMaterialsTable()
    {
        if (currentList == null) return;

        if (ImGui.BeginTable("ShoppingListTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
        {
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Est. Cost", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableHeadersRow();

            foreach (var item in currentList.Items.OrderBy(i => i.SourceType).ThenBy(i => i.ItemName))
            {
                ImGui.TableNextRow();
                
                ImGui.TableNextColumn();
                ImGui.Text(item.ItemName);
                
                ImGui.TableNextColumn();
                ImGui.Text($"{item.AmountNeeded}");
                
                ImGui.TableNextColumn();
                var sourceColor = item.SourceType == MaterialSourceType.Vendor 
                    ? new Vector4(0f, 1f, 0.5f, 1f) 
                    : new Vector4(1f, 1f, 1f, 1f);
                ImGui.TextColored(sourceColor, item.SourceType.ToString());
                
                ImGui.TableNextColumn();
                ImGui.Text($"{item.TotalCost:N0}");
            }
            
            // Total Row
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextDisabled("TOTAL");
            
            ImGui.TableNextColumn();
            
            ImGui.TableNextColumn();
            
            ImGui.TableNextColumn();
            ImGui.TextColored(new Vector4(1f, 0.84f, 0f, 1f), $"{currentList.TotalEstimatedCost:N0}");

            ImGui.EndTable();
        }
    }

    private void DrawCraftingPlan()
    {
        if (currentList?.CraftingSteps == null || !currentList.CraftingSteps.Any())
        {
            ImGui.Text("No crafting steps available.");
            return;
        }

        if (ImGui.BeginTable("CraftingPlanTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
        {
            ImGui.TableSetupColumn("Step", ImGuiTableColumnFlags.WidthFixed, 40);
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Quantity", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Ingredients", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            foreach (var step in currentList.CraftingSteps.OrderBy(s => s.StepIndex))
            {
                ImGui.TableNextRow();
                
                ImGui.TableNextColumn();
                ImGui.Text($"{step.StepIndex}");
                
                ImGui.TableNextColumn();
                ImGui.Text($"{step.ItemName}");
                if (step.BatchSize > 1)
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled($"(Yields {step.BatchSize})");
                }
                
                ImGui.TableNextColumn();
                ImGui.Text($"{step.Quantity} ({step.TotalCrafts} crafts)");
                
                ImGui.TableNextColumn();
                // Simple list of ingredients
                if (step.Ingredients.Any())
                {
                    var ingredients = string.Join(", ", step.Ingredients.Select(i => $"{i.AmountNeeded}x {i.ItemName}"));
                    ImGui.TextWrapped(ingredients);
                }
                else
                {
                     ImGui.TextDisabled("None");
                }
            }
            
            ImGui.EndTable();
        }
    }
}
