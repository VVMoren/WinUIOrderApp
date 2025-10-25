using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using WinUIOrderApp.Models;

namespace WinUIOrderApp.Helpers
{
    public static class CertificateStorageHelper
    {
        public const string ProductCacheFileName = "products_cache.json";

        public static string? GetProductCachePath(AppState state, bool ensureDirectory)
        {
            var dataFolder = state.GetCurrentCertificateDataFolder(ensureDirectory);
            if (string.IsNullOrEmpty(dataFolder))
                return null;
            return Path.Combine(dataFolder, ProductCacheFileName);
        }

        public static List<CachedGood> LoadCachedGoods(AppState state)
        {
            var path = GetProductCachePath(state, ensureDirectory: false);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return new List<CachedGood>();

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<CachedGood>>(json) ?? new List<CachedGood>();
            }
            catch
            {
                return new List<CachedGood>();
            }
        }

        public static void SaveCachedGoods(AppState state, IEnumerable<CachedGood> goods)
        {
            var path = GetProductCachePath(state, ensureDirectory: true);
            if (string.IsNullOrEmpty(path))
                return;

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(goods, options));
        }
    }
}
