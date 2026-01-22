# Manual Testing Checklist

## 1. Installation & Startup
- [ ] Plugin installs correctly via Dalamud.
- [ ] "Aurum" appears in the `/xlplugins` list.
- [ ] `/aurum` command opens the main dashboard.
- [ ] `/aurum config` opens the configuration window.

## 2. Dashboard Interface
- [ ] **Search & Filter:**
    - [ ] Searching for an item by name returns correct results.
    - [ ] Filtering by class (e.g., Alchemist, Blacksmith) works.
    - [ ] Filtering by level range works.
- [ ] **Sorting:**
    - [ ] Sorting by "Profit" works (high to low).
    - [ ] Sorting by "Demand" works.
    - [ ] Sorting by "Score" works.
- [ ] **Data Display:**
    - [ ] Profit margins are calculated and displayed.
    - [ ] Demand velocity (sales/day) is visible.
    - [ ] Risk indicators (Red/Yellow/Green) are correct.

## 3. Detail View
- [ ] Clicking an item opens the Detail Window.
- [ ] Price history chart loads.
- [ ] Ingredient tree is accurate.
- [ ] "Market Analysis" section shows valid data.

## 4. Configuration
- [ ] **General:**
    - [ ] Changing "Cache Duration" persists after restart.
- [ ] **Cost Modes:**
    - [ ] Switching between "Market Board" and "Vendor" costs updates profit calculations.
- [ ] **UI:**
    - [ ] UI scaling option changes window size.

## 5. Error Handling
- [ ] Disconnect internet -> Plugin handles API failures gracefully (no crash).
- [ ] Invalid search term shows "No results found".

## 6. Performance
- [ ] Plugin does not cause noticeable FPS drop in-game.
- [ ] Memory usage remains stable over 10 minutes of use.

## 7. Uninstallation
- [ ] Disabling the plugin stops all background tasks.
- [ ] Uninstalling removes the plugin files.
