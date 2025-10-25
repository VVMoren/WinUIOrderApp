using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinUIOrderApp.Helpers;
using Forms = System.Windows.Forms;

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

        private const string ProductListBaseUrl = "https://xn--80aqu.xn----7sbabas4ajkhfocclk9d3cvfsa.xn--p1ai/v4/product-list";
        private const int ProductListPageSize = 200;
        private static readonly string ProductListFromDate = Uri.EscapeDataString("2000-01-01 00:00:00");
        private static readonly string ProductListToDate = Uri.EscapeDataString("2026-10-16 23:59:59");

        public DashboardViewModel()
        {
            // Подписываемся на события изменения состояния
            AppState.Instance.OnProductGroupChanged += OnAppStateChanged;
            AppState.Instance.TokenUpdated += OnAppStateChanged;
            AppState.Instance.AdvancedSettingsChanged += RefreshAdvancedState;

            // Загружаем данные при создании
            LoadOrganisationAsync();
            RefreshAdvancedState();
        }

        private void OnAppStateChanged()
        {
            // Обновляем данные при изменении сертификата или токена
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

                // Если в кеше актуальные данные — используем
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

                // сохраняем в AppState на 12 часов
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
            // Принудительное обновление данных
            AppState.Instance.OrganisationFetchedAt = DateTime.MinValue;
            LoadOrganisationAsync();
        }

        private void RefreshAdvancedState()
        {
            IsCryptoTailEnabled = AppState.Instance.UseCryptoTailSearch;
            CryptoTailFolderPath = AppState.Instance.CryptoTailFolderPath ?? string.Empty;
            ProductCacheStatus = BuildProductCacheStatus();
        }

        private string BuildProductCacheStatus()
        {
            var cachePath = AppState.Instance.ProductCacheFilePath;
            if (string.IsNullOrEmpty(cachePath))
                return "Кеш товаров отсутствует";

            if (!File.Exists(cachePath))
                return "Кеш товаров недоступен";

            var info = new FileInfo(cachePath);
            var sizeKb = Math.Max(1, info.Length / 1024);
            return $"Кеш товаров от {info.LastWriteTime:dd.MM.yyyy HH:mm} (~{sizeKb} КБ)";
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

            using var dialog = new Forms.FolderBrowserDialog
            {
                Description = "Выберите папку с криптохвостами"
            };

            if (dialog.ShowDialog() == Forms.DialogResult.OK)
            {
                try
                {
                    var settings = CertificateSettingsManager.LoadSettings(inn);
                    settings.Advanced.CryptoTailFolderPath = dialog.SelectedPath;
                    CertificateSettingsManager.SaveSettings(inn, settings);

                    AppState.Instance.CryptoTailFolderPath = dialog.SelectedPath;
                    CryptoTailFolderPath = dialog.SelectedPath;
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
        private async Task FetchProductCacheAsync()
        {
            if (!AppState.Instance.UseCryptoTailSearch)
            {
                MessageBox.Show("Включите опцию работы с криптохвостами в настройках сертификата.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrEmpty(AppState.Instance.Token))
            {
                MessageBox.Show("Необходимо авторизоваться в ГИС МТ.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var certificate = AppState.Instance.SelectedCertificate;
            if (certificate == null)
            {
                MessageBox.Show("Активный сертификат отсутствует.", "Информация",
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

            try
            {
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                var goods = new List<ProductCacheService.ProductCacheItem>();
                var offset = 0;
                var total = int.MaxValue;

                using var http = new HttpClient();
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AppState.Instance.Token);

                while (offset < total)
                {
                    var url = $"{ProductListBaseUrl}?limit={ProductListPageSize}&offset={offset}&from_date={ProductListFromDate}&to_date={ProductListToDate}";
                    var response = await http.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<ProductListResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    var pageGoods = data?.Result?.Goods ?? new List<ProductCacheService.ProductCacheItem>();
                    goods.AddRange(pageGoods);

                    total = data?.Result?.Total ?? data?.Total ?? goods.Count;
                    if (pageGoods.Count == 0)
                        break;

                    offset += pageGoods.Count;
                }

                var dataDir = CertificateSettingsManager.GetCertificateDataDirectory(inn);
                var cacheFile = Path.Combine(dataDir, "products_cache.json");
                await ProductCacheService.SaveAsync(cacheFile, goods);

                var settings = CertificateSettingsManager.LoadSettings(inn);
                settings.Advanced.ProductCacheFileName = Path.GetFileName(cacheFile);
                settings.Advanced.ProductCacheUpdatedAt = DateTime.Now;
                CertificateSettingsManager.SaveSettings(inn, settings);

                AppState.Instance.ProductCacheFilePath = cacheFile;
                ProductCacheStatus = BuildProductCacheStatus();

                MessageBox.Show($"Получено {goods.Count} товаров. Данные сохранены.", "Готово",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка получения данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
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

        private class ProductListResponse
        {
            public int? Total { get; set; }
            public ProductListResult? Result { get; set; }
        }

        private class ProductListResult
        {
            public List<ProductCacheService.ProductCacheItem> Goods { get; set; } = new();
            public int? Total { get; set; }
        }
    }
}