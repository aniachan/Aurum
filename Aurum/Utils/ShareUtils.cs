using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aurum.Models;

namespace Aurum.Utils;

public static class ShareUtils
{
    private const string SHARE_URL_BASE = "https://aurum-app.com/share/";
    private const int VERSION = 1;

    public class SharedProfitData
    {
        [JsonPropertyName("v")]
        public int Version { get; set; } = VERSION;
        
        [JsonPropertyName("ts")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("itemId")]
        public uint ItemId { get; set; }
        
        [JsonPropertyName("itemName")]
        public string ItemName { get; set; } = string.Empty;

        [JsonPropertyName("world")]
        public string WorldName { get; set; } = string.Empty;

        [JsonPropertyName("craftCost")]
        public uint CraftCost { get; set; }

        [JsonPropertyName("salePrice")]
        public uint SalePrice { get; set; }

        [JsonPropertyName("profit")]
        public int Profit { get; set; }

        [JsonPropertyName("margin")]
        public float Margin { get; set; }

        [JsonPropertyName("risk")]
        public string RiskLevel { get; set; } = string.Empty;
    }

    public static string GenerateShareLink(ProfitCalculation calc)
    {
        if (calc == null) return string.Empty;

        var data = new SharedProfitData
        {
            Timestamp = DateTime.UtcNow,
            ItemId = calc.ItemId,
            ItemName = calc.Recipe.ItemName,
            WorldName = calc.MarketData?.WorldName ?? "Unknown",
            CraftCost = calc.TotalCraftCost,
            SalePrice = calc.ExpectedSalePrice,
            Profit = calc.RawProfit,
            Margin = calc.ProfitMargin,
            RiskLevel = calc.RiskLevel.ToString()
        };

        var json = JsonSerializer.Serialize(data);
        var compressed = Compress(json);
        var safeString = Base64UrlEncode(compressed);

        return $"{SHARE_URL_BASE}#v{VERSION}_{safeString}";
    }

    private static byte[] Compress(string str)
    {
        var bytes = Encoding.UTF8.GetBytes(str);

        using var memoryStream = new MemoryStream();
        using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress))
        {
            gZipStream.Write(bytes, 0, bytes.Length);
        }

        return memoryStream.ToArray();
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}
