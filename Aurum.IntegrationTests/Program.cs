using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aurum.IntegrationTests;

/// <summary>
/// Standalone test harness for Universalis API integration
/// Tests the API without needing Dalamud or FFXIV running
/// </summary>
class Program
{
    private static readonly HttpClient httpClient = new();
    
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Aurum Universalis API Integration Tests ===\n");
        
        await TestSingleItemMarketData();
        await TestBulkMarketData();
        await TestInvalidItem();
        await TestDifferentWorlds();
        await TestDataCenterQuery();
        
        Console.WriteLine("\n=== All Tests Complete ===");
    }
    
    static async Task TestSingleItemMarketData()
    {
        Console.WriteLine("Test 1: Fetching single item market data");
        Console.WriteLine("-------------------------------------------");
        
        const uint itemId = 36112; // Rarefied Sykon Bavarois (popular CUL item)
        const string world = "Gilgamesh";
        
        var url = $"https://universalis.app/api/v2/{world}/{itemId}";
        Console.WriteLine($"GET {url}");
        
        try
        {
            var response = await httpClient.GetStringAsync(url);
            var json = JsonDocument.Parse(response);
            var root = json.RootElement;
            
            var itemIdReturned = root.GetProperty("itemID").GetUInt32();
            var worldName = root.GetProperty("worldName").GetString();
            var currentAveragePrice = root.GetProperty("currentAveragePrice").GetDouble();
            var currentMinPrice = root.GetProperty("minPrice").GetUInt32();
            var listingsCount = root.GetProperty("listingsCount").GetInt32();
            var recentHistoryCount = root.GetProperty("recentHistory").GetArrayLength();
            
            Console.WriteLine($"✓ Item ID: {itemIdReturned}");
            Console.WriteLine($"✓ World: {worldName}");
            Console.WriteLine($"✓ Current Listings: {listingsCount}");
            Console.WriteLine($"✓ Current Avg Price: {currentAveragePrice:N0} gil");
            Console.WriteLine($"✓ Current Min Price: {currentMinPrice:N0} gil");
            Console.WriteLine($"✓ Recent Sales: {recentHistoryCount}");
            
            if (recentHistoryCount > 0)
            {
                Console.WriteLine($"\nRecent sales:");
                var history = root.GetProperty("recentHistory");
                int count = 0;
                foreach (var sale in history.EnumerateArray())
                {
                    if (count++ >= 5) break;
                    var price = sale.GetProperty("pricePerUnit").GetUInt32();
                    var quantity = sale.GetProperty("quantity").GetInt32();
                    var hq = sale.GetProperty("hq").GetBoolean();
                    var timestamp = DateTimeOffset.FromUnixTimeSeconds(sale.GetProperty("timestamp").GetInt64());
                    
                    Console.WriteLine($"  - Sold {quantity}x at {price:N0} gil{(hq ? " (HQ)" : "")} ({timestamp:g})");
                }
            }
            
            Console.WriteLine("✓ PASS\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ FAIL: {ex.Message}\n");
        }
    }
    
    static async Task TestBulkMarketData()
    {
        Console.WriteLine("Test 2: Fetching bulk market data");
        Console.WriteLine("-----------------------------------");
        
        var itemIds = new List<uint> { 5114, 5116, 5335, 36112 }; // Popular crafting materials
        const string world = "Gilgamesh";
        
        var itemsParam = string.Join(",", itemIds);
        var url = $"https://universalis.app/api/v2/{world}/{itemsParam}";
        Console.WriteLine($"GET {url}");
        
        try
        {
            var response = await httpClient.GetStringAsync(url);
            var json = JsonDocument.Parse(response);
            var root = json.RootElement;
            
            var items = root.GetProperty("items");
            var results = new Dictionary<uint, string>();
            
            foreach (var item in items.EnumerateObject())
            {
                var itemId = uint.Parse(item.Name);
                var itemData = item.Value;
                var listings = itemData.GetProperty("listingsCount").GetInt32();
                var avgPrice = itemData.GetProperty("currentAveragePrice").GetDouble();
                var sales = itemData.GetProperty("recentHistory").GetArrayLength();
                
                Console.WriteLine($"✓ Item {itemId}: {listings} listings, avg {avgPrice:N0} gil, {sales} recent sales");
                results[itemId] = "OK";
            }
            
            if (results.Count == itemIds.Count)
            {
                Console.WriteLine($"✓ PASS - Fetched all {itemIds.Count} items\n");
            }
            else
            {
                Console.WriteLine($"✗ FAIL - Only fetched {results.Count}/{itemIds.Count} items\n");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ FAIL: {ex.Message}\n");
        }
    }
    
    static async Task TestInvalidItem()
    {
        Console.WriteLine("Test 3: Handling invalid item ID");
        Console.WriteLine("----------------------------------");
        
        const uint itemId = 999999999; // Invalid
        const string world = "Gilgamesh";
        
        var url = $"https://universalis.app/api/v2/{world}/{itemId}";
        Console.WriteLine($"GET {url}");
        
        try
        {
            var response = await httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✓ Got expected error status: {response.StatusCode}");
                Console.WriteLine("✓ PASS\n");
            }
            else
            {
                Console.WriteLine("✗ FAIL - Expected error but got success\n");
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"✓ Got expected exception: {ex.Message}");
            Console.WriteLine("✓ PASS\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ FAIL: Unexpected exception: {ex.Message}\n");
        }
    }
    
    static async Task TestDifferentWorlds()
    {
        Console.WriteLine("Test 4: Fetching from different worlds");
        Console.WriteLine("----------------------------------------");
        
        const uint itemId = 5114; // Darksteel Ore
        var worlds = new[] { "Gilgamesh", "Cactuar", "Faerie" };
        
        try
        {
            foreach (var world in worlds)
            {
                var url = $"https://universalis.app/api/v2/{world}/{itemId}";
                var response = await httpClient.GetStringAsync(url);
                var json = JsonDocument.Parse(response);
                var root = json.RootElement;
                
                var worldName = root.GetProperty("worldName").GetString();
                var listings = root.GetProperty("listingsCount").GetInt32();
                var avgPrice = root.GetProperty("currentAveragePrice").GetDouble();
                
                Console.WriteLine($"✓ {worldName}: {listings} listings at avg {avgPrice:N0} gil");
            }
            
            Console.WriteLine("✓ PASS\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ FAIL: {ex.Message}\n");
        }
    }
    
    static async Task TestDataCenterQuery()
    {
        Console.WriteLine("Test 5: Data center-wide query");
        Console.WriteLine("--------------------------------");
        
        const uint itemId = 36112;
        const string dataCenter = "Aether"; // Gilgamesh's data center
        
        var url = $"https://universalis.app/api/v2/{dataCenter}/{itemId}";
        Console.WriteLine($"GET {url}");
        
        try
        {
            var response = await httpClient.GetStringAsync(url);
            var json = JsonDocument.Parse(response);
            var root = json.RootElement;
            
            var dcName = root.GetProperty("dcName").GetString();
            var listings = root.GetProperty("listingsCount").GetInt32();
            var avgPrice = root.GetProperty("currentAveragePrice").GetDouble();
            var sales = root.GetProperty("recentHistory").GetArrayLength();
            
            Console.WriteLine($"✓ Data Center: {dcName}");
            Console.WriteLine($"✓ Total Listings: {listings}");
            Console.WriteLine($"✓ Average Price: {avgPrice:N0} gil");
            Console.WriteLine($"✓ Recent Sales: {sales}");
            Console.WriteLine("✓ PASS\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ FAIL: {ex.Message}\n");
        }
    }
}
