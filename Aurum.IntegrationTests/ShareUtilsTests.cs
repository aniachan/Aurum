using System;
using Aurum.Models;
using Aurum.Utils;
using Xunit;
using System.Text.Json;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Aurum.IntegrationTests.Utils
{
    public class ShareUtilsTests
    {
        [Fact]
        public void GenerateShareLink_IncludesScores()
        {
            // Arrange
            var calc = new ProfitCalculation
            {
                Recipe = new RecipeData { ItemName = "Test Item", ResultItemId = 123 },
                MarketData = new MarketData { WorldName = "Balmung" },
                TotalCraftCost = 1000,
                ExpectedSalePrice = 2000,
                RawProfit = 1000,
                ProfitMargin = 50,
                RiskLevel = RiskLevel.Low,
                RecommendationScore = 85,
                ProfitScore = 90,
                DemandScore = 70
            };

            // Act
            var link = ShareUtils.GenerateShareLink(calc);

            // Assert
            Assert.Contains("#v2_", link);
            
            // Decode to verify contents
            // The format is "...#v2_{payload}"
            // We need to be careful because the payload itself might contain underscores
            var parts = link.Split(new[] { '_' }, 2);
            var payload = parts[1];
            
            // Revert Base64 URL encoding
            payload = payload.Replace("-", "+").Replace("_", "/");
            while (payload.Length % 4 != 0)
            {
                payload += "=";
            }
            
            var bytes = Convert.FromBase64String(payload);
            
            using var memoryStream = new MemoryStream(bytes);
            using var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
            using var reader = new StreamReader(gZipStream, Encoding.UTF8);
            var json = reader.ReadToEnd();
            
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            Assert.Equal(85, root.GetProperty("score").GetInt32());
            Assert.Equal(90, root.GetProperty("profitScore").GetInt32());
            Assert.Equal(70, root.GetProperty("demandScore").GetInt32());
            Assert.Equal(2, root.GetProperty("v").GetInt32());
        }

        [Fact]
        public void GenerateShareLink_HandlesNullScores()
        {
            // Arrange
            var calc = new ProfitCalculation
            {
                Recipe = new RecipeData { ItemName = "Test Item", ResultItemId = 123 },
                MarketData = new MarketData { WorldName = "Balmung" },
                // Scores default to 0
            };

            // Act
            var link = ShareUtils.GenerateShareLink(calc);
            
             // Decode to verify contents
            var parts = link.Split(new[] { '_' }, 2);
            var payload = parts[1];

            payload = payload.Replace("-", "+").Replace("_", "/");
            while (payload.Length % 4 != 0)
            {
                payload += "=";
            }
            
            var bytes = Convert.FromBase64String(payload);
            
            using var memoryStream = new MemoryStream(bytes);
            using var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
            using var reader = new StreamReader(gZipStream, Encoding.UTF8);
            var json = reader.ReadToEnd();
            
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            Assert.Equal(0, root.GetProperty("score").GetInt32());
        }
    }
}
