using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WinUIOrderApp.Helpers;
using WinUIOrderApp.Models;

namespace WinUIOrderApp.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject
    {
        [ObservableProperty]
        private string organisationName = "Загрузка...";

        [ObservableProperty]
        private string organisationInn = "";

        [ObservableProperty]
        private string organisationOgrn = "";

        [ObservableProperty]
        private string productGroupName = "";

        [ObservableProperty]
        private bool isCryptoTailEnabled;

        [ObservableProperty]
        private string cryptoTailFolderPath = string.Empty;

        [ObservableProperty]
        private string productCacheStatus = string.Empty;

        [ObservableProperty]
        private string cryptoTailStatus = string.Empty;

        private const string ProductListBaseUrl = "https://xn--80aqu.xn----7sbabas4ajkhfocclk9d3cvfsa.xn--p1ai/v4/product-list";
        private const string FeedProductBaseUrl = "https://xn--80aqu.xn----7sbabas4ajkhfocclk9d3cvfsa.xn--p1ai/v3/feed-product";

        public DashboardViewModel()
        {
            AppState.Instance.OnProductGroupChanged += OnAppStateChanged;
            AppState.Instance.TokenUpdated += OnAppStateChanged;
            AppState.Instance.AdvancedSettingsChanged += RefreshAdvancedState;

            LoadOrganisationAsync();
            RefreshAdvancedState();
        }

        private void OnAppStateChanged()
        {
            LoadOrganisationAsync();
        }

        [RelayCommand]
        private async Task LoadOrganisationAsync()
        {
            try
            {
                var code = AppState.Instance.SelectedProductGroupCode;
                var token = AppState.Instance.Token;
                var cert = AppState.Instance.SelectedCertificate;

                if (AppState.Instance.HasValidOrganisationCache())
                {
                    OrganisationName = AppState.Instance.OrganisationName;
                    OrganisationInn = AppState.Instance.OrganisationInn;
                    OrganisationOgrn = AppState.Instance.OrganisationOgrn;
                    ProductGroupName = ResolveProductGroupName(code);
                    return;
                }

                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(token) || cert == null)
                {
                    OrganisationName = "Не подключено к ГИС МТ";
                    OrganisationInn = "";
                    OrganisationOgrn = "";
                    ProductGroupName = "";
                    return;
                }

                var inn = ExtractInn(cert.Subject);
                if (string.IsNullOrEmpty(inn))
                    inn = "000000000000";

                string url = $"https://{code}.crpt.ru/bff-elk/v1/organisation/list?inns={inn}";

                using var http = new HttpClient();
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await http.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<List<OrganisationResponse>>(json);

                if (data == null || data.Count == 0)
                {
                    OrganisationName = "Организация не найдена";
                    return;
                }

                var org = data[0];
                OrganisationName = org.name;
                OrganisationInn = org.inn;
                OrganisationOgrn = org.ogrn;
                ProductGroupName = ResolveProductGroupName(code);

                AppState.Instance.OrganisationName = org.name;
                AppState.Instance.OrganisationInn = org.inn;
                AppState.Instance.OrganisationOgrn = org.ogrn;
                AppState.Instance.OrganisationFetchedAt = DateTime.Now;
            }
            catch (Exception ex)
            {
                OrganisationName = $"Ошибка: {ex.Message}";
                OrganisationInn = "";
                OrganisationOgrn = "";
                ProductGroupName = "";
            }
        }

        [RelayCommand]
        private void RefreshData()
        {
            AppState.Instance.OrganisationFetchedAt = DateTime.MinValue;
            LoadOrganisationAsync();
        }

        private void RefreshAdvancedState()
        {
            IsCryptoTailEnabled = AppState.Instance.UseCryptoTailSearch;
            CryptoTailFolderPath = AppState.Instance.CryptoTailFolderPath ?? string.Empty;
            ProductCacheStatus = BuildProductCacheStatus();
            CryptoTailStatus = BuildCryptoTailStatus();
        }

        private string BuildProductCacheStatus()
        {
            var cachePath = AppState.Instance.ProductCacheFilePath;
            if (string.IsNullOrEmpty(cachePath))
                return "Кеш товаров отсутствует";

            if (!File.Exists(cachePath))
                return "Кеш товаров недоступен";

            try
            {
                var info = new FileInfo(cachePath);
                var sizeKb = Math.Max(1, info.Length / 1024);

                var inn = AppState.ExtractInn(AppState.Instance.SelectedCertificate?.Subject ?? "");
                if (!string.IsNullOrEmpty(inn))
                {
                    var settings = CertificateSettingsManager.LoadSettings(inn);
                    var count = settings.Advanced.ProductCacheCount;
                    if (count > 0)
                    {
                        return $"Кеш: {count} товаров, {info.LastWriteTime:dd.MM.yyyy HH:mm} (~{sizeKb} КБ)";
                    }
                }

                return $"Кеш товаров от {info.LastWriteTime:dd.MM.yyyy HH:mm} (~{sizeKb} КБ)";
            }
            catch
            {
                return "Кеш товаров (ошибка чтения)";
            }
        }

        private string BuildCryptoTailStatus()
        {
            var folderPath = AppState.Instance.CryptoTailFolderPath;
            if (string.IsNullOrEmpty(folderPath))
                return "Папка не выбрана";

            if (!Directory.Exists(folderPath))
                return "Папка не существует";

            try
            {
                var files = Directory.GetFiles(folderPath, "*.txt");
                var totalLines = 0;
                foreach (var file in files)
                {
                    var lines = File.ReadAllLines(file);
                    totalLines += lines.Length;
                }

                return $"Криптохвосты: {files.Length} файлов, {totalLines} кодов";
            }
            catch
            {
                return "Ошибка чтения папки";
            }
        }

        private string ResolveProductGroupName(string code)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "product_groups.json");
                if (!File.Exists(path)) return code ?? "Не выбрана";

                var json = File.ReadAllText(path);
                var root = JsonSerializer.Deserialize<ProductGroupRoot>(json);
                var group = root?.result?.Find(pg => pg.code == code);
                return group?.name ?? code ?? "Не выбрана";
            }
            catch
            {
                return code ?? "Не выбрана";
            }
        }

        private static string ExtractInn(string subject)
        {
            if (string.IsNullOrEmpty(subject)) return null;
            var parts = subject.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var p = part.Trim();
                if (p.StartsWith("ИНН=", StringComparison.OrdinalIgnoreCase))
                    return p.Substring(4);
            }
            return null;
        }

        [RelayCommand]
        private void SelectCryptoTailFolder()
        {
            if (!AppState.Instance.UseCryptoTailSearch)
            {
                MessageBox.Show("Включите опцию в настройках сертификата.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var certificate = AppState.Instance.SelectedCertificate;
            if (certificate == null)
            {
                MessageBox.Show("Активный сертификат не выбран.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var inn = AppState.ExtractInn(certificate.Subject);
            if (string.IsNullOrEmpty(inn))
            {
                MessageBox.Show("Не удалось определить ИНН сертификата.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dialog = new OpenFolderDialog
            {
                Title = "Выберите папку с криптохвостами",
                Multiselect = false
            };

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                try
                {
                    var settings = CertificateSettingsManager.LoadSettings(inn);
                    settings.Advanced.CryptoTailFolderPath = dialog.FolderName;
                    settings.Advanced.EnableCryptoTailSearch = true; // Включаем опцию
                    settings.Advanced.UseCryptoTailSearch = true; // Для обратной совместимости
                    CertificateSettingsManager.SaveSettings(inn, settings);

                    AppState.Instance.CryptoTailFolderPath = dialog.FolderName;
                    CryptoTailFolderPath = dialog.FolderName;
                    ProductCacheStatus = BuildProductCacheStatus();

                    MessageBox.Show("Папка сохранена.", "Готово",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        public async Task FetchProductCacheAsync()
        {
            try
            {
                Debug.WriteLine("=== FETCH PRODUCT CACHE STARTED ===");

                // УБИРАЕМ ВСЕ ПРОВЕРКИ НАХЕР
                Mouse.OverrideCursor = Cursors.Wait;

                MessageBox.Show("🚀 ЗАПУСК ПОЛУЧЕНИЯ КЕША ТОВАРОВ!", "СТАРТ");

                using var http = new HttpClient();
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AppState.Instance.Token);

                Debug.WriteLine("Starting product list fetch...");

                var allProducts = await FetchProductListAsync(http);

                Debug.WriteLine($"Found {allProducts.Count} products");

                if (allProducts.Count == 0)
                {
                    MessageBox.Show("Товары не найдены", "INFO");
                    return;
                }

                Debug.WriteLine("Starting detailed info fetch...");

                var detailedProducts = await FetchDetailedProductInfoAsync(http, allProducts);

                Debug.WriteLine("Saving cache...");

                var inn = AppState.ExtractInn(AppState.Instance.SelectedCertificate?.Subject ?? "");
                await SaveProductCacheAsync(inn, detailedProducts);

                MessageBox.Show($"✅ УСПЕХ! Получено {allProducts.Count} товаров", "ГОТОВО");

                Debug.WriteLine("=== FETCH PRODUCT CACHE COMPLETED ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FETCH ERROR: {ex}");
                MessageBox.Show($"ОШИБКА: {ex.Message}", "ERROR");
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }
        private async Task<List<ProductListItem>> FetchProductListAsync(HttpClient http)
        {
            var allProducts = new List<ProductListItem>();
            var offset = 0;
            const int limit = 1000;
            var total = int.MaxValue;

            var fromDate = Uri.EscapeDataString("2000-01-01 00:00:00");
            var toDate = Uri.EscapeDataString(DateTime.Now.ToString("yyyy-MM-dd 23:59:59"));

            LogHelper.WriteLog("Dashboard.FetchProductList", "Начало получения списка товаров");

            while (offset < total)
            {
                try
                {
                    ProductCacheStatus = $"Получение товаров... {offset}/{total}";

                    var url = $"{ProductListBaseUrl}?limit={limit}&offset={offset}&from_date={fromDate}&to_date={toDate}";

                    LogHelper.WriteLog("Dashboard.FetchProductList.Request", $"URL: {url}");

                    var response = await http.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<ProductListResponseV4>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (data?.Result?.Goods == null || data.Result.Goods.Count == 0)
                        break;

                    allProducts.AddRange(data.Result.Goods);
                    total = data.Result.Total;
                    offset += data.Result.Goods.Count;

                    LogHelper.WriteLog("Dashboard.FetchProductList.Page",
                        $"Получено страница: {data.Result.Goods.Count} товаров, всего: {allProducts.Count}/{total}");

                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLog("Dashboard.FetchProductList.PageError",
                        $"Ошибка страницы {offset}: {ex.Message}");
                    break;
                }
            }

            LogHelper.WriteLog("Dashboard.FetchProductList.Completed",
                $"Завершено: получено {allProducts.Count} товаров");

            return allProducts;
        }

        private async Task<List<CachedProduct>> FetchDetailedProductInfoAsync(HttpClient http, List<ProductListItem> products)
        {
            var detailedProducts = new List<CachedProduct>();
            const int batchSize = 25;

            LogHelper.WriteLog("Dashboard.FetchDetailedInfo",
                $"Начало детализации {products.Count} товаров, размер батча: {batchSize}");

            for (int i = 0; i < products.Count; i += batchSize)
            {
                try
                {
                    ProductCacheStatus = $"Детализация товаров... {Math.Min(i + batchSize, products.Count)}/{products.Count}";

                    var batch = products.Skip(i).Take(batchSize).ToList();
                    var gtins = string.Join(";", batch.Select(p => p.Gtin));

                    var url = $"{FeedProductBaseUrl}?gtins={gtins}";

                    LogHelper.WriteLog("Dashboard.FetchDetailedInfo.Request",
                        $"Батч {i / batchSize + 1}: {batch.Count} GTIN");

                    var response = await http.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<FeedProductResponseV3>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (data?.Result != null)
                    {
                        foreach (var detail in data.Result)
                        {
                            var cachedProduct = MapToCachedProduct(detail, products);
                            detailedProducts.Add(cachedProduct);
                        }
                    }

                    LogHelper.WriteLog("Dashboard.FetchDetailedInfo.Batch",
                        $"Батч {i / batchSize + 1}: обработано {data?.Result?.Count ?? 0} товаров");

                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLog("Dashboard.FetchDetailedInfo.BatchError",
                        $"Ошибка батча {i / batchSize + 1}: {ex.Message}");
                }
            }

            LogHelper.WriteLog("Dashboard.FetchDetailedInfo.Completed",
                $"Завершено: детализировано {detailedProducts.Count} товаров");

            return detailedProducts;
        }

        private CachedProduct MapToCachedProduct(FeedProductItem detail, List<ProductListItem> products)
        {
            var basicInfo = products.FirstOrDefault(p => p.Gtin == detail.IdentifiedBy.FirstOrDefault()?.Value);

            var cachedProduct = new CachedProduct
            {
                GoodId = detail.GoodId,
                Gtin = detail.IdentifiedBy.FirstOrDefault()?.Value ?? string.Empty,
                GoodName = detail.GoodName,
                Tnved = basicInfo?.Tnved ?? string.Empty,
                BrandName = detail.BrandName,
                GoodStatus = detail.GoodStatus,
                ProducerInn = detail.ProducerInn,
                ProducerName = detail.ProducerName,
                Categories = detail.Categories.Select(c => c.CatName).ToList(),
                GoodMarkFlag = detail.GoodMarkFlag,
                GoodTurnFlag = detail.GoodTurnFlag,
                FirstSignDate = detail.FirstSignDate,
                UpdatedDate = basicInfo?.UpdatedDate ?? string.Empty
            };

            foreach (var attr in detail.GoodAttrs)
            {
                if (!string.IsNullOrEmpty(attr.AttrName) && !string.IsNullOrEmpty(attr.AttrValue))
                {
                    cachedProduct.Attributes[attr.AttrName] = attr.AttrValue;
                }
            }

            return cachedProduct;
        }

        private async Task SaveProductCacheAsync(string inn, List<CachedProduct> products)
        {
            try
            {
                var dataDir = CertificateSettingsManager.GetCertificateDataDirectory(inn);
                Directory.CreateDirectory(dataDir);

                var cacheFile = Path.Combine(dataDir, "products_detailed_cache.json");

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(products, options);
                await File.WriteAllTextAsync(cacheFile, json, Encoding.UTF8);

                var settings = CertificateSettingsManager.LoadSettings(inn);
                settings.Advanced.ProductCacheFileName = Path.GetFileName(cacheFile);
                settings.Advanced.ProductCacheUpdatedAt = DateTime.Now;
                settings.Advanced.ProductCacheCount = products.Count;
                settings.Advanced.ProductCacheFormat = "detailed";
                settings.Advanced.ProductCacheLastSync = DateTime.Now;
                settings.Advanced.ProductCacheVersion = 1;
                CertificateSettingsManager.SaveSettings(inn, settings);

                AppState.Instance.ProductCacheFilePath = cacheFile;

                LogHelper.WriteLog("Dashboard.SaveProductCache",
                    $"Сохранен кеш: {products.Count} товаров, файл: {cacheFile}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("Dashboard.SaveProductCache.Error",
                    $"Ошибка сохранения кеша: {ex.Message}");
                throw;
            }
        }

        [RelayCommand]
        private void TestCryptoTails()
        {
            var folderPath = AppState.Instance.CryptoTailFolderPath;
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                MessageBox.Show("Папка с криптохвостами не выбрана или не существует", "Ошибка");
                return;
            }

            try
            {
                var files = Directory.GetFiles(folderPath, "*.txt");
                var allCodes = new List<string>();

                foreach (var file in files)
                {
                    var lines = File.ReadAllLines(file);
                    allCodes.AddRange(lines.Where(l => !string.IsNullOrWhiteSpace(l)));
                }

                var uniqueCodes = allCodes.Distinct().ToList();

                MessageBox.Show($"Найдено:\nФайлов: {files.Length}\nВсего кодов: {allCodes.Count}\nУникальных: {uniqueCodes.Count}\n\nПримеры:\n{string.Join("\n", uniqueCodes.Take(5))}",
                    "Тест криптохвостов");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка");
            }
        }

        [RelayCommand]
        private void FetchStock()
        {
            if (!AppState.Instance.UseCryptoTailSearch)
            {
                MessageBox.Show("Включите опцию работы с криптохвостами в настройках сертификата.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AppState.Instance.RequestNavigateToExports();
            AppState.Instance.RequestKmDownload();
        }

        // Модели данных
        private class OrganisationResponse
        {
            public string name
            {
                get; set;
            }
            public string inn
            {
                get; set;
            }
            public string ogrn
            {
                get; set;
            }
        }

        private class ProductGroupRoot
        {
            public List<ProductGroupItem> result
            {
                get; set;
            }
        }

        private class ProductGroupItem
        {
            public string code
            {
                get; set;
            }
            public string name
            {
                get; set;
            }
        }

        public class ProductListResponseV4
        {
            [JsonPropertyName("result")]
            public ProductListResult Result
            {
                get; set;
            }
        }

        public class ProductListResult
        {
            [JsonPropertyName("goods")]
            public List<ProductListItem> Goods { get; set; } = new();

            [JsonPropertyName("total")]
            public int Total
            {
                get; set;
            }

            [JsonPropertyName("offset")]
            public int Offset
            {
                get; set;
            }

            [JsonPropertyName("limit")]
            public int Limit
            {
                get; set;
            }
        }

        public class ProductListItem
        {
            [JsonPropertyName("good_id")]
            public long GoodId
            {
                get; set;
            }

            [JsonPropertyName("gtin")]
            public string Gtin { get; set; } = string.Empty;

            [JsonPropertyName("good_name")]
            public string GoodName { get; set; } = string.Empty;

            [JsonPropertyName("tnved")]
            public string Tnved { get; set; } = string.Empty;

            [JsonPropertyName("brand_name")]
            public string BrandName { get; set; } = string.Empty;

            [JsonPropertyName("good_status")]
            public string GoodStatus { get; set; } = string.Empty;

            [JsonPropertyName("updated_date")]
            public string UpdatedDate { get; set; } = string.Empty;
        }

        public class FeedProductResponseV3
        {
            [JsonPropertyName("result")]
            public List<FeedProductItem> Result { get; set; } = new();
        }

        public class FeedProductItem
        {
            [JsonPropertyName("good_id")]
            public long GoodId
            {
                get; set;
            }

            [JsonPropertyName("identified_by")]
            public List<IdentifiedBy> IdentifiedBy { get; set; } = new();

            [JsonPropertyName("good_name")]
            public string GoodName { get; set; } = string.Empty;

            [JsonPropertyName("is_kit")]
            public bool IsKit
            {
                get; set;
            }

            [JsonPropertyName("is_set")]
            public bool IsSet
            {
                get; set;
            }

            [JsonPropertyName("good_url")]
            public string GoodUrl { get; set; } = string.Empty;

            [JsonPropertyName("good_status")]
            public string GoodStatus { get; set; } = string.Empty;

            [JsonPropertyName("good_signed")]
            public bool GoodSigned
            {
                get; set;
            }

            [JsonPropertyName("good_mark_flag")]
            public bool GoodMarkFlag
            {
                get; set;
            }

            [JsonPropertyName("good_turn_flag")]
            public bool GoodTurnFlag
            {
                get; set;
            }

            [JsonPropertyName("producer_inn")]
            public string ProducerInn { get; set; } = string.Empty;

            [JsonPropertyName("producer_name")]
            public string ProducerName { get; set; } = string.Empty;

            [JsonPropertyName("categories")]
            public List<Category> Categories { get; set; } = new();

            [JsonPropertyName("brand_id")]
            public long BrandId
            {
                get; set;
            }

            [JsonPropertyName("brand_name")]
            public string BrandName { get; set; } = string.Empty;

            [JsonPropertyName("good_attrs")]
            public List<GoodAttribute> GoodAttrs { get; set; } = new();

            [JsonPropertyName("first_sign_date")]
            public string FirstSignDate { get; set; } = string.Empty;
        }

        public class IdentifiedBy
        {
            [JsonPropertyName("value")]
            public string Value { get; set; } = string.Empty;

            [JsonPropertyName("type")]
            public string Type { get; set; } = string.Empty;

            [JsonPropertyName("multiplier")]
            public int Multiplier
            {
                get; set;
            }

            [JsonPropertyName("level")]
            public string Level { get; set; } = string.Empty;
        }

        public class Category
        {
            [JsonPropertyName("cat_id")]
            public long CatId
            {
                get; set;
            }

            [JsonPropertyName("cat_name")]
            public string CatName { get; set; } = string.Empty;
        }

        public class GoodAttribute
        {
            [JsonPropertyName("attr_id")]
            public long AttrId
            {
                get; set;
            }

            [JsonPropertyName("attr_name")]
            public string AttrName { get; set; } = string.Empty;

            [JsonPropertyName("attr_value")]
            public string AttrValue { get; set; } = string.Empty;

            [JsonPropertyName("attr_group_id")]
            public long AttrGroupId
            {
                get; set;
            }

            [JsonPropertyName("attr_group_name")]
            public string AttrGroupName { get; set; } = string.Empty;
        }

        public class CachedProduct
        {
            public long GoodId
            {
                get; set;
            }
            public string Gtin { get; set; } = string.Empty;
            public string GoodName { get; set; } = string.Empty;
            public string Tnved { get; set; } = string.Empty;
            public string BrandName { get; set; } = string.Empty;
            public string GoodStatus { get; set; } = string.Empty;
            public string ProducerInn { get; set; } = string.Empty;
            public string ProducerName { get; set; } = string.Empty;
            public List<string> Categories { get; set; } = new();
            public Dictionary<string, string> Attributes { get; set; } = new();
            public bool GoodMarkFlag
            {
                get; set;
            }
            public bool GoodTurnFlag
            {
                get; set;
            }
            public string FirstSignDate { get; set; } = string.Empty;
            public string UpdatedDate { get; set; } = string.Empty;
        }
    }
}