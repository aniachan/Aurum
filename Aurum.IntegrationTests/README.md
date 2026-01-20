# Aurum Integration Tests

Standalone console application to test Universalis API integration and market analysis without needing FFXIV or Dalamud running.

## Running the Tests

```bash
dotnet run --project Aurum.IntegrationTests/Aurum.IntegrationTests.csproj
```

## What Gets Tested

### Test 1: Single Item Market Data
- Fetches market data for a popular item (Rarefied Sykon Bavarois)
- Validates all price fields, listings count, and sales history
- Displays recent sales with timestamps

### Test 2: Bulk Market Data
- Fetches multiple items in a single API call
- Tests batch query performance
- Validates that all requested items are returned

### Test 3: Invalid Item Handling
- Tests error handling for non-existent item IDs
- Validates proper HTTP status codes

### Test 4: Multi-World Queries
- Fetches same item from different worlds (Gilgamesh, Cactuar, Faerie)
- Demonstrates world-specific pricing

### Test 5: Data Center Query
- Tests data center-wide aggregation
- Shows combined listings across all worlds in a DC

## Expected Output

```
=== Aurum Universalis API Integration Tests ===

Test 1: Fetching single item market data
-------------------------------------------
GET https://universalis.app/api/v2/Gilgamesh/36112
✓ Item ID: 36112
✓ World: Gilgamesh
✓ Current Listings: 7
✓ Current Avg Price: 878 gil
✓ Current Min Price: 300 gil
✓ Recent Sales: 5
...
✓ PASS

[More tests...]

=== All Tests Complete ===
```

## Why Integration Tests?

Unlike unit tests with mocks, these integration tests:
- **Verify real API behavior** - Catch breaking changes in Universalis API
- **Test actual data parsing** - Ensure JSON deserialization works correctly
- **Run without game** - No need for FFXIV client or Dalamud framework
- **Fast feedback** - Run in seconds, iterate quickly
- **CI/CD ready** - Can be automated in build pipelines

## Adding New Tests

To add a new test, create a method in `Program.cs`:

```csharp
static async Task TestMyNewFeature()
{
    Console.WriteLine("Test N: My test description");
    Console.WriteLine("----------------------------");
    
    try
    {
        // Your test logic here
        Console.WriteLine("✓ PASS\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ FAIL: {ex.Message}\n");
    }
}
```

Then call it from `Main()`:

```csharp
await TestMyNewFeature();
```

## API Documentation

Universalis API docs: https://docs.universalis.app/

### Key Endpoints Used

- `GET /api/v2/{world}/{itemId}` - Single item query
- `GET /api/v2/{world}/{itemIds}` - Bulk query (comma-separated IDs)
- `GET /api/v2/{dataCenter}/{itemId}` - Data center-wide query

### Response Format

```json
{
  "itemID": 36112,
  "worldName": "Gilgamesh",
  "lastUploadTime": 1737408000000,
  "listingsCount": 7,
  "currentAveragePrice": 878,
  "minPrice": 300,
  "recentHistory": [
    {
      "hq": true,
      "pricePerUnit": 350,
      "quantity": 2,
      "timestamp": 1704322860
    }
  ]
}
```
