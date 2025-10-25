using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WinUIOrderApp.Helpers
{
    public static class ProductCacheService
    {
        public class ProductCacheItem
        {
            [JsonPropertyName("good_id")]
            public long GoodId { get; set; }

            [JsonPropertyName("gtin")]
            public string? Gtin { get; set; }

            [JsonPropertyName("good_name")]
            public string? GoodName { get; set; }

            [JsonPropertyName("brand_name")]
            public string? BrandName { get; set; }

            [JsonPropertyName("tnved")]
            public string? Tnved { get; set; }
        }

        public static async Task SaveAsync(string filePath, IEnumerable<ProductCacheItem> items)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, items, options);
        }

        public static IReadOnlyDictionary<string, ProductCacheItem> Load(string? filePath)
        {
            var result = new Dictionary<string, ProductCacheItem>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return result;

            try
            {
                var json = File.ReadAllText(filePath);
                var items = JsonSerializer.Deserialize<List<ProductCacheItem>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ProductCacheItem>();

                foreach (var item in items)
                {
                    if (!string.IsNullOrWhiteSpace(item.Gtin) && !result.ContainsKey(item.Gtin))
                        result[item.Gtin] = item;
                }
            }
            catch
            {
                return result;
            }

            return result;
        }
    }
}
