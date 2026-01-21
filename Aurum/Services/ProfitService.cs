using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Aurum.Models;

namespace Aurum.Services;

/// <summary>
/// Service for calculating crafting profits with full ingredient trees
/// </summary>
public class ProfitService
{
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly RecipeService recipeService;
    private readonly UniversalisService universalisService;
    private readonly MarketAnalysisService marketAnalysisService;
    
    public ProfitService(
        IPluginLog log,
        Configuration config,
        RecipeService recipeService,
        UniversalisService universalisService,
        MarketAnalysisService marketAnalysisService)
    {
        this.log = log;
        this.config = config;
        this.recipeService = recipeService;
        this.universalisService = universalisService;
        this.marketAnalysisService = marketAnalysisService;
    }
    
    /// <summary>
    /// Calculate profit for a recipe including full ingredient tree analysis
    /// </summary>
    public async Task<ProfitCalculation?> CalculateProfitAsync(RecipeData recipe, string worldName)
    {
        try
        {
            // Get market data for the result item
            var marketData = await universalisService.GetMarketDataAsync(worldName, recipe.ResultItemId);
            
            if (marketData == null)
            {
                log.Warning($"No market data available for item {recipe.ResultItemId}");
                return CreateEmptyResult(recipe);
            }
            
            // Analyze market demand
            marketAnalysisService.AnalyzeMarket(marketData);
            
            // Build ingredient tree and calculate costs
            var ingredientTree = await BuildIngredientTreeAsync(recipe, worldName);
            
            // Calculate profit
            var profit = await CalculateProfitAsync(recipe, marketData, ingredientTree, worldName);
            
            // Save to database cache
            if (profit != null)
            {
                try
                {
                    Plugin.Instance?.DatabaseService?.UpsertRecipeCache(recipe, profit);
                    
                    // Set item priority based on recommendation score
                    var priority = Math.Max(1, profit.RecommendationScore);
                    Plugin.Instance?.DatabaseService?.UpsertItemPriority((int)recipe.ResultItemId, priority);
                }
                catch (Exception cacheEx)
                {
                    log.Warning(cacheEx, $"Failed to cache profit for {recipe.ItemName}");
                }
            }
            
            return profit;
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Error calculating profit for recipe {recipe.RecipeId}");
            return null;
        }
    }
    
    /// <summary>
    /// Calculate profits for multiple recipes in batch
    /// </summary>
    public async Task<List<ProfitCalculation>> CalculateProfitsBatchAsync(
        IEnumerable<RecipeData> recipes, 
        string worldName, 
        int maxConcurrent = 5)
    {
        var results = new List<ProfitCalculation>();
        var recipeList = recipes.ToList();
        
        // Get all item IDs we need market data for
        var itemIds = recipeList.Select(r => r.ResultItemId).Distinct().ToList();
        
        // Fetch market data in batches
        var marketDataDict = await universalisService.GetMarketDataBatchAsync(worldName, itemIds);
        
        // Calculate profits
        foreach (var recipe in recipeList)
        {
            try
            {
                if (!marketDataDict.TryGetValue(recipe.ResultItemId, out var marketData))
                {
                    log.Debug($"No market data for {recipe.ItemName}");
                    continue;
                }
                
                marketAnalysisService.AnalyzeMarket(marketData);
                var ingredientTree = await BuildIngredientTreeAsync(recipe, worldName);
                var profit = await CalculateProfitAsync(recipe, marketData, ingredientTree, worldName);
                
                if (profit != null)
                {
                    results.Add(profit);
                    
                    // Save to database cache
                    try
                    {
                        Plugin.Instance?.DatabaseService?.UpsertRecipeCache(recipe, profit);
                        
                        // Set item priority based on recommendation score
                        var priority = Math.Max(1, profit.RecommendationScore);
                        Plugin.Instance?.DatabaseService?.UpsertItemPriority((int)recipe.ResultItemId, priority);
                    }
                    catch (Exception cacheEx)
                    {
                        log.Warning(cacheEx, $"Failed to cache profit for {recipe.ItemName}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, $"Error calculating profit for {recipe.ItemName}");
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// Build complete ingredient tree with costs resolved recursively
    /// </summary>
    private async Task<IngredientTree> BuildIngredientTreeAsync(RecipeData recipe, string worldName, CostMode? overrideMode = null)
    {
        var tree = new IngredientTree
        {
            RootRecipeId = recipe.RecipeId,
            ResultItemId = recipe.ResultItemId,
            CalculationMode = overrideMode ?? config.DefaultCostMode
        };
        
        var processedItems = new HashSet<uint>(); // Prevent cycles
        
        // Process regular ingredients
        foreach (var ingredient in recipe.Ingredients)
        {
            var cost = await CalculateIngredientCostAsync(ingredient, worldName, processedItems, 0, overrideMode);
            tree.RootIngredients.Add(cost);
            AddToFlatList(tree.FlatIngredientList, cost);
        }
        
        // Process crystals (usually cheap, often vendor-bought)
        foreach (var crystal in recipe.Crystals)
        {
            var cost = await CalculateIngredientCostAsync(crystal, worldName, processedItems, 0, overrideMode);
            tree.RootIngredients.Add(cost);
            AddToFlatList(tree.FlatIngredientList, cost);
        }
        
        // Calculate total cost
        tree.TotalCost = (uint)tree.FlatIngredientList.Sum(i => i.TotalCost);
        
        return tree;
    }
    
    /// <summary>
    /// Recursively calculate cost for an ingredient (may involve sub-recipes)
    /// </summary>
    private async Task<IngredientCost> CalculateIngredientCostAsync(
        RecipeIngredient ingredient,
        string worldName,
        HashSet<uint> processedItems,
        int depth = 0,
        CostMode? overrideMode = null)
    {
        const int MaxDepth = 10; // Prevent infinite recursion
        
        var cost = new IngredientCost
        {
            ItemId = ingredient.ItemId,
            ItemName = ingredient.ItemName,
            Quantity = ingredient.AmountNeeded,
            IsHQ = ingredient.IsHQ
        };
        
        // Check for cycles
        if (processedItems.Contains(ingredient.ItemId))
        {
            log.Warning($"Cycle detected for item {ingredient.ItemId}, using market price");
            cost.UnitCost = await GetMarketPriceAsync(ingredient.ItemId, worldName);
            cost.TotalCost = cost.UnitCost * (uint)cost.Quantity;
            cost.Source = CostSource.MarketBoard;
            return cost;
        }
        
        if (depth >= MaxDepth)
        {
            log.Warning($"Max depth reached for item {ingredient.ItemId}");
            cost.UnitCost = await GetMarketPriceAsync(ingredient.ItemId, worldName);
            cost.TotalCost = cost.UnitCost * (uint)cost.Quantity;
            cost.Source = CostSource.MarketBoard;
            return cost;
        }
        
        processedItems.Add(ingredient.ItemId);
        
        var modeToUse = overrideMode ?? config.DefaultCostMode;
        
        // Determine cost based on mode
        switch (modeToUse)
        {
            case CostMode.MarketBoard:
                cost.UnitCost = await GetMarketPriceAsync(ingredient.ItemId, worldName);
                cost.Source = CostSource.MarketBoard;
                break;
                
            case CostMode.Vendor:
                cost.UnitCost = GetVendorPrice(ingredient.ItemId);
                cost.Source = CostSource.Vendor;
                break;
                
            case CostMode.Cheapest:
                var marketPrice = await GetMarketPriceAsync(ingredient.ItemId, worldName);
                var vendorPrice = GetVendorPrice(ingredient.ItemId);
                
                if (vendorPrice > 0 && vendorPrice < marketPrice)
                {
                    cost.UnitCost = vendorPrice;
                    cost.Source = CostSource.Vendor;
                }
                else
                {
                    cost.UnitCost = marketPrice;
                    cost.Source = CostSource.MarketBoard;
                }
                break;
                
            case CostMode.SelfGathered:
                cost.UnitCost = 0;
                cost.Source = CostSource.SelfGathered;
                break;
        }
        
        // If this ingredient can be crafted, consider crafting cost
        if (ingredient.SubRecipeId.HasValue && modeToUse != CostMode.SelfGathered)
        {
            var subRecipe = recipeService.GetRecipe(ingredient.SubRecipeId.Value);
            if (subRecipe != null)
            {
                // Calculate crafting cost for this sub-recipe
                var subTree = await BuildIngredientTreeAsync(subRecipe, worldName, overrideMode);
                var craftCost = subTree.TotalCost / (uint)subRecipe.ResultAmount;
                
                // Use whichever is cheaper (craft or buy)
                if (craftCost < cost.UnitCost || cost.UnitCost == 0)
                {
                    cost.UnitCost = craftCost;
                    cost.Source = CostSource.SubRecipe;
                    cost.SubIngredients = subTree.FlatIngredientList;
                }
            }
        }
        
        cost.TotalCost = cost.UnitCost * (uint)cost.Quantity;
        
        processedItems.Remove(ingredient.ItemId);
        
        return cost;
    }
    
    /// <summary>
    /// Get market board price for an item (average or min based on config)
    /// </summary>
    private async Task<uint> GetMarketPriceAsync(uint itemId, string worldName)
    {
        var marketData = await universalisService.GetMarketDataAsync(worldName, itemId);
        
        if (marketData == null || marketData.CurrentListings == 0)
        {
            return 0; // No listings available
        }
        
        // Use HQ prices if configured and available
        if (config.UseHQPricesWhenAvailable && marketData.CurrentAveragePriceHQ > 0)
        {
            return marketData.CurrentAveragePriceHQ;
        }
        
        // Otherwise use NQ or overall average
        return marketData.CurrentAveragePriceNQ > 0 
            ? marketData.CurrentAveragePriceNQ 
            : marketData.MinPrice;
    }
    
    /// <summary>
    /// Get vendor price for an item (if available)
    /// </summary>
    private uint GetVendorPrice(uint itemId)
    {
        try
        {
            // First check if item has a direct vendor price from Item sheet
            var itemSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
            if (itemSheet != null && itemSheet.TryGetRow(itemId, out var item))
            {
                // PriceMid is the base vendor sell price
                // PriceLow is the vendor buy price (what vendors pay you)
                if (item.PriceMid > 0)
                {
                    return item.PriceMid;
                }
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            log.Warning($"Error getting vendor price for item {itemId}: {ex.Message}");
            return 0;
        }
    }
    
    /// <summary>
    /// Flatten ingredient tree into a list
    /// </summary>
    private void AddToFlatList(List<IngredientCost> flatList, IngredientCost cost)
    {
        flatList.Add(cost);
        
        if (cost.SubIngredients != null)
        {
            foreach (var subIngredient in cost.SubIngredients)
            {
                flatList.Add(subIngredient);
            }
        }
    }
    
    /// <summary>
    /// Calculate final profit metrics
    /// </summary>
    private async Task<ProfitCalculation> CalculateProfitAsync(
        RecipeData recipe,
        MarketData marketData,
        IngredientTree ingredientTree,
        string worldName)
    {
        var calculation = new ProfitCalculation
        {
            Recipe = recipe,
            MarketData = marketData,
            IngredientTree = ingredientTree,
            TotalCraftCost = ingredientTree.TotalCost,
            CalculatedAt = DateTime.UtcNow,
            CostMode = config.DefaultCostMode,
            IsDataComplete = true
        };
        
        // Suggested Price - Undercut current lowest price by 1 gil, or use vendor price if higher
        var lowestPrice = marketData.MinPrice;
        if (lowestPrice > 0)
        {
            calculation.ExpectedSalePrice = lowestPrice > 1 ? lowestPrice - 1 : lowestPrice;
        }
        else if (marketData.CurrentAveragePriceNQ > 0)
        {
            // Fallback to average NQ price if no listings
            calculation.ExpectedSalePrice = marketData.CurrentAveragePriceNQ;
        }
        else
        {
            // Fallback to vendor price * 3 (arbitrary markup if no market data)
            calculation.ExpectedSalePrice = calculation.VendorPrice * 3;
        }

        // Ensure we don't go below vendor price (plus a small margin)
        if (calculation.ExpectedSalePrice < calculation.VendorPrice)
        {
            calculation.ExpectedSalePrice = calculation.VendorPrice;
        }
        
        // Apply HQ pricing if available and configured
        if (config.UseHQPricesWhenAvailable && recipe.CanBeHQ && marketData.CurrentAveragePriceHQ > 0)
        {
            calculation.ExpectedSalePrice = marketData.CurrentAveragePriceHQ;
            
            // Also check current listings for HQ specifically to undercut properly
            var lowestHQ = marketData.Listings.Where(l => l.IsHQ).MinBy(l => l.PricePerUnit)?.PricePerUnit ?? 0;
            if (lowestHQ > 0)
            {
                 calculation.ExpectedSalePrice = lowestHQ > 1 ? lowestHQ - 1 : lowestHQ;
            }
        }
        
        // Calculate market board tax (5%)
        if (config.IncludeMarketTax)
        {
            calculation.MarketBoardTax = (uint)(calculation.ExpectedSalePrice * 0.05);
            calculation.NetSalePrice = calculation.ExpectedSalePrice - calculation.MarketBoardTax;
        }
        else
        {
            calculation.NetSalePrice = calculation.ExpectedSalePrice;
        }
        
        // Calculate profit
        calculation.RawProfit = (int)(calculation.NetSalePrice - calculation.TotalCraftCost);
        
        // Calculate opportunity cost (value if ingredients sold directly)
        // This is always based on MarketBoard prices, regardless of CostMode
        // We recalculate the ingredient tree cost using MarketBoard prices to get this value
        if (config.CalculateOpportunityCost)
        {
            var opportunityCostTree = await BuildIngredientTreeAsync(recipe, worldName, CostMode.MarketBoard);
            calculation.OpportunityCost = (int)opportunityCostTree.TotalCost;
            calculation.ProfitVsMaterials = (int)calculation.NetSalePrice - calculation.OpportunityCost;
        }

        // Calculate profit margin %
        if (calculation.ExpectedSalePrice > 0)
        {
            calculation.ProfitMargin = (float)calculation.RawProfit / calculation.ExpectedSalePrice * 100f;
        }
        
        // Calculate ROI %
        if (calculation.TotalCraftCost > 0)
        {
            calculation.ROI = (float)calculation.RawProfit / calculation.TotalCraftCost * 100f;
        }
        
        // Calculate gil per hour
        var craftTimeSeconds = recipe.EstimatedCraftTimeSeconds > 0 
            ? recipe.EstimatedCraftTimeSeconds 
            : config.DefaultCraftingTimeSeconds;
        
        // Add gathering time if using self-gathered items
        int gatheringTimeSeconds = 0;
        if (config.DefaultCostMode == CostMode.SelfGathered || config.DefaultCostMode == CostMode.Cheapest)
        {
            gatheringTimeSeconds = CalculateGatheringTime(ingredientTree);
        }
        calculation.GatheringTimeSeconds = gatheringTimeSeconds;
        
        var totalTimeSeconds = craftTimeSeconds + gatheringTimeSeconds;
        var totalTimeHours = totalTimeSeconds / 3600f;
        
        calculation.GilPerHour = totalTimeHours > 0 
            ? (int)(calculation.RawProfit / totalTimeHours)
            : 0;
        
        // Calculate profit score (0-100 based on profit alone)
        calculation.ProfitScore = CalculateProfitScore(calculation);

        // Calculate Efficiency Score
        calculation.EfficiencyScore = calculation.TotalCraftCost > 0
            ? (float)calculation.RawProfit / calculation.TotalCraftCost
            : 0;

        // Get demand score from market analysis
        calculation.DemandScore = marketData.RecommendationScore;
        
        // Calculate final weighted recommendation score
        calculation.RecommendationScore = CalculateRecommendationScore(calculation);
        
        // Copy risk assessment from market data
        calculation.RiskLevel = marketData.RiskLevel;
        calculation.RiskScore = marketData.RiskScore;
        calculation.Warnings = marketData.Warnings;
        calculation.RecommendedQuantity = marketData.RecommendedQuantity;
        calculation.MaxSafeQuantity = marketData.MaxSafeQuantity;
        calculation.Recommendation = marketData.Recommendation;
        calculation.EstimatedSellTimeDays = marketData.EstimatedSellTimeDays;
        
        return calculation;
    }
    
    /// <summary>
    /// Calculate estimated gathering time for self-gathered materials
    /// </summary>
    private int CalculateGatheringTime(IngredientTree tree)
    {
        int totalGatheringTime = 0;
        
        // Helper to check if item is gathered (CostSource.SelfGathered)
        // We iterate the flat list to find all SelfGathered items
        foreach (var item in tree.FlatIngredientList)
        {
            if (item.Source == CostSource.SelfGathered)
            {
                // Simple estimation: constant time per item gathered
                // In reality, this would depend on node locations, GP, etc.
                totalGatheringTime += item.Quantity * config.EstimatedGatheringTimeSeconds;
            }
        }
        
        return totalGatheringTime;
    }

    /// <summary>
    /// Calculate profit score (0-100) based on profit metrics
    /// </summary>
    private int CalculateProfitScore(ProfitCalculation calc)
    {
        float score = 0;
        
        // Profit margin score (0-40 points)
        float marginScore = calc.ProfitMargin switch
        {
            >= 100 => 40f,
            >= 50 => 30f,
            >= 25 => 20f,
            >= 10 => 10f,
            > 0 => 5f,
            _ => 0f
        };
        score += marginScore;
        
        // Gil per hour score (0-30 points)
        float gilHourScore = calc.GilPerHour switch
        {
            >= 1000000 => 30f, // 1M+/hour
            >= 500000 => 25f,
            >= 250000 => 20f,
            >= 100000 => 15f,
            >= 50000 => 10f,
            > 0 => 5f,
            _ => 0f
        };
        score += gilHourScore;
        
        // Raw profit score (0-30 points)
        float rawScore = calc.RawProfit switch
        {
            >= 100000 => 30f,
            >= 50000 => 25f,
            >= 25000 => 20f,
            >= 10000 => 15f,
            >= 5000 => 10f,
            > 0 => 5f,
            _ => 0f
        };
        score += rawScore;
        
        return (int)Math.Clamp(score, 0, 100);
    }
    
    /// <summary>
    /// Calculate final recommendation score (weighted combination of profit and demand)
    /// </summary>
    private int CalculateRecommendationScore(ProfitCalculation calc)
    {
        // Weighted scoring:
        // 30% Profit Score
        // 70% Demand Score (from market analysis)
        
        float score = (calc.ProfitScore * 0.3f) + (calc.DemandScore * 0.7f);
        
        // Apply penalty for very high risk
        if (calc.RiskLevel == RiskLevel.VeryHigh)
        {
            score *= 0.5f; // 50% penalty
        }
        else if (calc.RiskLevel == RiskLevel.High)
        {
            score *= 0.75f; // 25% penalty
        }
        
        return (int)Math.Clamp(score, 0, 100);
    }
    
    /// <summary>
    /// Create empty result when market data is unavailable
    /// </summary>
    private ProfitCalculation CreateEmptyResult(RecipeData recipe)
    {
        return new ProfitCalculation
        {
            Recipe = recipe,
            IsDataComplete = false,
            CalculatedAt = DateTime.UtcNow,
            RiskLevel = RiskLevel.VeryHigh,
            RiskScore = 100,
            Warnings = new List<MarketWarningInfo>
            {
                new MarketWarningInfo
                {
                    Type = MarketWarning.None,
                    Message = "No market data available",
                    Details = "Unable to fetch market data from Universalis API",
                    Level = WarningLevel.Danger
                }
            }
        };
    }
    
    /// <summary>
    /// Load cached profit calculations from database
    /// </summary>
    public List<ProfitCalculation> LoadCachedProfits(int maxAgeHours = 24)
    {
        try
        {
            log.Information($"Loading cached profits (max age: {maxAgeHours}h)...");
            var cachedData = Plugin.Instance?.DatabaseService?.GetAllCachedProfits(maxAgeHours);
            
            if (cachedData == null || !cachedData.Any())
            {
                log.Information("No cached profits found");
                return new List<ProfitCalculation>();
            }
            
            var results = new List<ProfitCalculation>();
            var worldName = GetCurrentWorldName();
            
            foreach (var (recipeId, profit, lastAnalyzed) in cachedData)
            {
                // Get recipe details
                var recipe = recipeService.GetRecipe(recipeId);
                if (recipe == null)
                {
                    log.Warning($"Recipe {recipeId} not found, skipping cached profit");
                    continue;
                }
                
                // Reconstruct ProfitCalculation with full recipe data
                profit.Recipe = recipe;
                profit.CalculatedAt = DateTimeOffset.FromUnixTimeSeconds(lastAnalyzed).UtcDateTime;
                profit.IsDataComplete = true;
                
                // Load cached MarketData for this item (last 7 days of data)
                if (!string.IsNullOrEmpty(worldName))
                {
                    try
                    {
                        var cachedMarketData = LoadCachedMarketData(recipe.ResultItemId, worldName);
                        if (cachedMarketData != null)
                        {
                            profit.MarketData = cachedMarketData;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Warning(ex, $"Failed to load cached market data for item {recipe.ResultItemId}");
                    }
                }
                
                // Set risk level based on score
                profit.RiskLevel = profit.RiskScore switch
                {
                    <= 25 => RiskLevel.Low,
                    <= 50 => RiskLevel.Medium,
                    <= 75 => RiskLevel.High,
                    _ => RiskLevel.VeryHigh
                };
                
                results.Add(profit);
            }
            
            log.Information($"Loaded {results.Count} cached profit calculations");
            return results;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to load cached profits");
            return new List<ProfitCalculation>();
        }
    }
    
    private string? GetCurrentWorldName()
    {
        try
        {
            var worldName = config.PreferredWorld;
            if (string.IsNullOrEmpty(worldName) || worldName.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                var currentWorld = Plugin.PlayerState?.CurrentWorld;
                if (currentWorld?.Value.RowId != 0)
                {
                    worldName = currentWorld?.Value.Name.ToString();
                }
            }
            return worldName;
        }
        catch
        {
            return null;
        }
    }
    
    private MarketData? LoadCachedMarketData(uint itemId, string worldName)
    {
        var worldId = universalisService.GetWorldIdByName(worldName);
        if (worldId == 0) return null;
        
        // Get cached market data (7 days old max)
        var marketData = Plugin.Instance?.DatabaseService?.GetMarketData((int)itemId, worldId, TimeSpan.FromDays(7));
        if (marketData == null) return null;
        
        // Load historical snapshots (last 90 days)
        var snapshots = Plugin.Instance?.DatabaseService?.GetMarketSnapshots((int)itemId, worldId, DateTime.UtcNow.AddDays(-90));
        if (snapshots != null && snapshots.Any())
        {
            marketData.HistorySnapshots = snapshots;
        }
        
        // Set world name
        marketData.WorldName = worldName;
        
        // Recalculate SaleVelocity from cached RecentHistory if it's missing/zero
        // This handles cases where the cached data was stored before analysis
        if (marketData.SaleVelocity == 0 && marketData.RecentHistory.Any())
        {
            var oldestSale = marketData.RecentHistory.Min(s => s.Timestamp);
            var newestSale = marketData.RecentHistory.Max(s => s.Timestamp);
            var timeSpan = (newestSale - oldestSale).TotalDays;
            
            if (timeSpan > 0)
            {
                var totalQuantity = marketData.RecentHistory.Sum(s => (int)s.Quantity);
                marketData.SaleVelocity = (float)(totalQuantity / timeSpan);
                log.Debug($"Recalculated SaleVelocity for item {itemId}: {marketData.SaleVelocity:F2}/day from {totalQuantity} sales over {timeSpan:F1} days");
            }
        }
        
        return marketData;
    }
}

