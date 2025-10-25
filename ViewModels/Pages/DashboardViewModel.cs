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
using Microsoft.Extensions.DependencyInjection;
using WinUIOrderApp.Helpers;
using WinUIOrderApp.Models;
using WinUIOrderApp.Views.Pages;
using WinForms = System.Windows.Forms;

namespace WinUIOrderApp.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject
    {
        [ObservableProperty]
        private string organisationName = "Загрузка...";

        [ObservableProperty]
        private string organisationInn = string.Empty;

        [ObservableProperty]
        private string organisationOgrn = string.Empty;

        [ObservableProperty]
        private string productGroupName = string.Empty;

        [ObservableProperty]
        private bool isCryptoTailFeatureEnabled;

        [ObservableProperty]
        private string cryptoTailFolder = "Папка не выбрана";

        [ObservableProperty]
        private string productCacheStatus = "Кеш товаров не создан";

        public DashboardViewModel()
        {
            AppState.Instance.OnProductGroupChanged += OnAppStateChanged;
            AppState.Instance.TokenUpdated += OnAppStateChanged;
            AppState.Instance.CertificateSettingsChanged += OnCertificateSettingsChanged;
            AppState.Instance.ProductCacheUpdated += OnProductCacheUpdated;
            AppState.Instance.PropertyChanged += OnAppStatePropertyChanged;

            LoadOrganisationAsync();
            UpdateCertificateState();
        }

        private void OnAppStateChanged()
        {
            LoadOrganisationAsync();
            BuildProductCacheCommand.NotifyCanExecuteChanged();
            RequestStocksCommand.NotifyCanExecuteChanged();
        }

        private void OnCertificateSettingsChanged()
        {
            UpdateCertificateState();
        }

        private void OnAppStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppState.SelectedCertificate))
            {
                UpdateCertificateState();
            }
        }

        private void OnProductCacheUpdated()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var count = AppState.Instance.CachedGoods.Count;
                ProductCacheStatus = count > 0
                    ? $"Кеш товаров: {count:N0} позиций"
                    : "Кеш товаров не создан";
            });
        }

        private void UpdateCertificateState()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var prefs = AppState.Instance.GetCurrentCertificatePreferences();
                IsCryptoTailFeatureEnabled = prefs?.EnableCryptoTailSearch == true;
                CryptoTailFolder = !IsCryptoTailFeatureEnabled || string.IsNullOrEmpty(prefs?.CryptoTailFolder)
                    ? "Папка не выбрана"
                    : prefs!.CryptoTailFolder!;

                ReloadProductCache();

                SelectCryptoTailFolderCommand.NotifyCanExecuteChanged();
                BuildProductCacheCommand.NotifyCanExecuteChanged();
                RequestStocksCommand.NotifyCanExecuteChanged();
            });
        }

        private void ReloadProductCache()
        {
            var goods = CertificateStorageHelper.LoadCachedGoods(AppState.Instance);
            AppState.Instance.UpdateProductCache(goods);
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
                    OrganisationInn = string.Empty;
                    OrganisationOgrn = string.Empty;
                    ProductGroupName = string.Empty;
                    return;
                }

                var inn = ExtractInn(cert.Subject) ?? "000000000000";
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
                OrganisationInn = string.Empty;
                OrganisationOgrn = string.Empty;
                ProductGroupName = string.Empty;
            }
        }

        [RelayCommand]
        private void RefreshData()
        {
            AppState.Instance.OrganisationFetchedAt = DateTime.MinValue;
            LoadOrganisationAsync();
        }

        private bool CanConfigureCryptoTailFolder() =>
            AppState.Instance.GetCurrentCertificatePreferences()?.EnableCryptoTailSearch == true;

        [RelayCommand(CanExecute = nameof(CanConfigureCryptoTailFolder))]
        private void SelectCryptoTailFolder()
        {
            var thumbprint = AppState.Instance.SelectedCertificate?.Thumbprint;
            if (string.IsNullOrEmpty(thumbprint))
            {
                MessageBox.Show("Выберите сертификат в настройках.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Выберите папку с TXT файлами криптохвостов",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                var current = AppState.Instance.GetOrCreatePreferences(thumbprint);
                var updated = new CertificatePreferences
                {
                    EnableCryptoTailSearch = current.EnableCryptoTailSearch,
                    CryptoTailFolder = dialog.SelectedPath
                };

                AppState.Instance.UpdateCertificatePreferences(thumbprint, updated);
                CryptoTailFolder = dialog.SelectedPath;
                MessageBox.Show("Папка с криптохвостами сохранена.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private bool CanRunCertificateActions() =>
            AppState.Instance.SelectedCertificate != null
            && !string.IsNullOrEmpty(AppState.Instance.Token);

        [RelayCommand(CanExecute = nameof(CanRunCertificateActions))]
        private async Task BuildProductCacheAsync()
        {
            try
            {
                var token = AppState.Instance.Token;
                if (string.IsNullOrEmpty(token))
                {
                    MessageBox.Show("Нет токена авторизации. Подключитесь к ГИС МТ.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var allGoods = new List<CachedGood>();
                const int limit = 500;
                int offset = 0;
                bool hasMore = true;
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                while (hasMore)
                {
                    var url = $"https://xn--80aqu.xn----7sbabas4ajkhfocclk9d3cvfsa.xn--p1ai/v4/product-list?limit={limit}&offset={offset}&from_date=2000-01-01%2000%3A00%3A00&to_date=2026-10-16%2023%3A59%3A59";
                    var response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<ProductListResponse>(json, options);

                    if (data?.result?.goods == null || data.result.goods.Count == 0)
                        break;

                    allGoods.AddRange(data.result.goods.Select(g => new CachedGood
                    {
                        GoodId = g.good_id,
                        Gtin = g.gtin,
                        Name = g.good_name,
                        Tnved = g.tnved,
                        BrandName = g.brand_name,
                        Status = g.good_status
                    }));

                    offset += data.result.limit;
                    hasMore = allGoods.Count < data.result.total;
                }

                CertificateStorageHelper.SaveCachedGoods(AppState.Instance, allGoods);
                AppState.Instance.UpdateProductCache(allGoods);

                MessageBox.Show($"Получено {allGoods.Count:N0} товаров. Данные сохранены.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении данных товаров: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanRequestStocks() =>
            AppState.Instance.SelectedCertificate != null
            && AppState.Instance.GetCurrentCertificatePreferences()?.EnableCryptoTailSearch == true;

        [RelayCommand(CanExecute = nameof(CanRequestStocks))]
        private async Task RequestStocksAsync()
        {
            NavigationHelper.NavigateTo("ExportsPage");

            var services = App.Services;
            if (services == null)
                return;

            var exportsPage = services.GetRequiredService<ExportsPage>();
            await exportsPage.StartKmDataFetchFromDashboardAsync();
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

        private static string? ExtractInn(string subject)
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
            public string name { get; set; } = string.Empty;
            public string inn { get; set; } = string.Empty;
            public string ogrn { get; set; } = string.Empty;
        }

        private class ProductGroupRoot
        {
            public List<ProductGroupItem> result { get; set; } = new();
        }

        private class ProductGroupItem
        {
            public string code { get; set; } = string.Empty;
            public string name { get; set; } = string.Empty;
        }

        private class ProductListResponse
        {
            public int apiversion { get; set; }
            public ProductListResult? result { get; set; }
        }

        private class ProductListResult
        {
            public List<ProductListGood> goods { get; set; } = new();
            public int offset { get; set; }
            public int limit { get; set; }
            public int total { get; set; }
        }

        private class ProductListGood
        {
            public long good_id { get; set; }
            public string? gtin { get; set; }
            public string? good_name { get; set; }
            public string? tnved { get; set; }
            public string? brand_name { get; set; }
            public string? good_status { get; set; }
        }
    }
}
