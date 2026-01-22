# Advanced Item Filter Window - UI Design Specification

## Overview
This document outlines the UI layout and user experience design for the **Advanced Item Filter Window**. The goal is to provide power users with fine-grained control over which items are selected for profit analysis, optimizing the relevance of results and reducing unnecessary API calls to Universalis.

## Window Structure
The window will use a standard `ImGui` window with a tabbed interface to organize the large number of filter options without cluttering the screen.

**Window Title:** `Advanced Filters`
**Dimensions:** Default ~600x500, Resizable.

### Layout Hierarchy
1.  **Global Header:** Search bar and active preset display.
2.  **Tab Bar:** Segmented into logical filter groups.
    *   General (Levels, Jobs, Slots)
    *   Categories (Item types)
    *   Status (Crafted, Marketable, etc.)
    *   Presets (Management)
3.  **Action Footer:** Apply, Reset, and Close buttons.

---

## Detailed UI Components

### 1. Global Header
Always visible at the top of the window.
*   **Search Input:** `[ Search Item Name... ]` (Full width)
    *   *Behavior:* Filters the candidate list immediately or on 'Apply'.

### 2. Tab: General
Focuses on numeric ranges and equipment properties.

*   **Level Ranges:** (Two columns)
    *   `Character Level:` `[ Min ]` - `[ Max ]`
    *   `Item Level (iLvl):` `[ Min ]` - `[ Max ]`
*   **Class/Job Restriction:**
    *   **Combo Box:** `[ Select Job ]` (All, CRP, BSM, etc.) - *Existing simple selector.*
    *   *Future Enhancement:* Multi-select checkboxes for selecting multiple specific jobs.
*   **Equipment Slot:**
    *   **Combo Box:** `[ All Slots ]`
    *   Options: Main Hand, Off Hand, Head, Body, Hands, Legs, Feet, Ears, Neck, Wrists, Rings.

### 3. Tab: Categories
Uses a Tree or Collapsible Header structure to handle the hierarchy of item kinds.

*   **Logic:** `OR` logic within a group (e.g., Sword OR Axe), `AND` logic across major toggles if restrictive.
*   **Select All / None** helpers for each section.

**Structure:**
*   `[>] Main Arms` (PLD, WAR, DRK...)
*   `[>] Armor` (Head, Body...)
*   `[>] Accessories`
*   `[>] Crafting & Gathering` (Tools, Gear)
*   `[>] Consumables` (Meals, Potions)
*   `[>] Furniture` (Tables, Rugs...)
*   `[>] Materials` (Ores, Lumber...)

### 4. Tab: Status & Properties
Boolean flags and state-based filters.

*   **Source:**
    *   `[x] Craftable`
    *   `[ ] Gatherable` (Greyed out if not supported yet, or strictly for gathering log)
    *   `[ ] Vendor Purchased` (Exclude/Include)
*   **Market Status:**
    *   `[x] Marketable` (Can be sold on MB)
    *   `[ ] Stackable`
*   **Item Properties:**
    *   `[ ] HQ Possible`
    *   `[ ] Dyeable`
    *   `[ ] Collectable`

### 5. Tab: Presets
Interface for managing saved filter configurations.

*   **List View:** Scrollable list of saved presets.
*   **Controls:**
    *   `[ Load ]` Selected preset.
    *   `[ Save As... ]` Current settings as new preset.
    *   `[ Delete ]` Selected preset.
    *   `[ Rename ]` Selected preset.

### 6. Action Footer
Fixed at the bottom of the window.

*   **Left Aligned:**
    *   `[ Reset All ]` - Clears all filters to default.
*   **Right Aligned:**
    *   `[ Cancel ]` - Close without applying.
    *   `[ Apply Filters ]` - Commit changes, close window, and trigger `RefreshDataAsync()` on the Dashboard.

---

## User Interaction Flow

1.  **Opening:** User clicks "Advanced Filters" (or "Settings" icon) on the Dashboard.
2.  **Configuring:**
    *   User adjusts level ranges.
    *   User unchecks "Furniture" to focus on gear.
    *   User selects "Profitable Only" (if moved here) or specific job.
3.  **Preview (Optional):** A text label near the footer could show "Matches: ~1,200 items" (estimated) to warn if the filter is too broad.
4.  **Applying:**
    *   User clicks "Apply".
    *   `FilterWindow` saves state to `Configuration`.
    *   `DashboardWindow` detects configuration change or receives event.
    *   `DashboardWindow` refreshes the item list based on new criteria.

## Integration Points

*   **Configuration.cs:** Needs new properties to store these specific filter states (e.g., `MinLvl`, `MaxLvl`, `SelectedCategories`, `IsCraftableOnly`).
*   **FilterCriteria.cs:** A model class to encapsulate this state for passing between the UI and the `RecipeService` / `UniversalisService`.
*   **UniversalisService / RecipeService:** The `GetRecipesByLevel` method needs to be replaced or augmented with a `GetRecipesByCriteria(FilterCriteria criteria)` method to perform the actual filtering before data fetching.

## ImGui Implementation Notes

*   Use `ImGui.BeginTabBar` and `ImGui.BeginTabItem` for the main tabs.
*   Use `ImGui.BeginChild` for the Category tree to allow independent scrolling if the list is long.
*   Use `ImGui.InputInt` with `ImGuiInputTextFlags.CharsDecimal` for level inputs.
*   Ensure the window respects the global `ThemeManager` styles.
