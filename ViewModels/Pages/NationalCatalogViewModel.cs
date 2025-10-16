using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WinUIOrderApp.Helpers;
using WinUIOrderApp.Models;
using WinUIOrderApp.Services;

namespace WinUIOrderApp.ViewModels.Pages
{
    public class NationalCatalogRow
    {
        public long GoodId
        {
            get; set;
        }

        public string Gtin
        {
            get; set;
        } = string.Empty;

        public string Name
        {
            get; set;
        } = string.Empty;

        public string Brand
        {
            get; set;
        } = string.Empty;

        public int? ProductGroupId
        {
            get; set;
        }

        public string ProductKind
        {
            get; set;
        } = string.Empty;

        public string ProductGroupCode
        {
            get; set;
        } = string.Empty;

        public string Status
        {
            get; set;
        } = string.Empty;

        public string DetailedStatus
        {
            get; set;
        } = string.Empty;

        public string TnVed
        {
            get; set;
        } = string.Empty;

        public string PackageType
        {
            get; set;
        } = string.Empty;

        public string Nicotine
        {
            get; set;
        } = string.Empty;

        public string Volume
        {
            get; set;
        } = string.Empty;

        public string Updated
        {
            get; set;
        } = string.Empty;
    }

    public partial class NationalCatalogViewModel : ObservableObject
    {
        private readonly NationalCatalogService _service = new();
        private readonly object _stateLock = new();
        private CancellationTokenSource? _loadingCts;
        private bool _isActive;
        private Dictionary<string, ProductGroupDto>? _productGroupCache;

        public ObservableCollection<NationalCatalogRow> Items
        {
            get;
        } = new();

        [ObservableProperty]
        private bool isLoading;

        public bool IsNotLoading => !IsLoading;

        [ObservableProperty]
        private string? statusMessage;

        [ObservableProperty]
        private string selectedProductGroupTitle = string.Empty;

        public IAsyncRelayCommand RefreshCommand
        {
            get;
        }

        public NationalCatalogViewModel()
        {
            RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsLoading);
            SelectedProductGroupTitle = ResolveSelectedProductGroupTitle();

            AppState.Instance.OnProductGroupChanged += HandleAppStateChanged;
            AppState.Instance.TokenUpdated += HandleAppStateChanged;
        }

        public void Activate()
        {
            if (_isActive)
                return;

            _isActive = true;
            SelectedProductGroupTitle = ResolveSelectedProductGroupTitle();
            _ = RefreshAsync();
        }

        public void Deactivate()
        {
            _isActive = false;
            CancelLoading();
        }

        private void CancelLoading()
        {
            lock (_stateLock)
            {
                _loadingCts?.Cancel();
                _loadingCts?.Dispose();
                _loadingCts = null;
            }
        }

        private async Task RefreshAsync()
        {
            if (!_isActive)
                return;

            CancelLoading();

            CancellationToken token;
            lock (_stateLock)
            {
                _loadingCts = new CancellationTokenSource();
                token = _loadingCts.Token;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Загрузка данных Национального каталога...";
                Items.Clear();

                var authToken = AppState.Instance.Token;
                if (string.IsNullOrWhiteSpace(authToken))
                {
                    StatusMessage = "Необходимо авторизоваться в ГИС МТ в разделе Настройки.";
                    return;
                }

                var selectedGroupId = ResolveSelectedProductGroupId();
                if (selectedGroupId == null)
                {
                    StatusMessage = "Выберите доступную товарную группу в разделе Настройки.";
                    return;
                }

                var goods = await _service.LoadAllGoodsAsync(authToken, token);
                if (goods.Count == 0)
                {
                    StatusMessage = "Доступные товары в Национальном каталоге не найдены.";
                    return;
                }

                token.ThrowIfCancellationRequested();

                var gtins = goods
                    .Select(g => NormalizeGtin(g.Gtin))
                    .Where(g => !string.IsNullOrEmpty(g))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var info = await _service.LoadProductInfoAsync(authToken, gtins, token);
                var infoLookup = info
                    .Where(i => !string.IsNullOrWhiteSpace(i.Gtin))
                    .GroupBy(i => NormalizeGtin(i.Gtin))
                    .Where(g => !string.IsNullOrEmpty(g.Key))
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var filteredGoods = goods
                    .Where(g =>
                    {
                        var key = NormalizeGtin(g.Gtin);
                        if (string.IsNullOrEmpty(key))
                            return false;
                        if (!infoLookup.TryGetValue(key, out var detail))
                            return false;
                        return detail.ProductGroupId == selectedGroupId;
                    })
                    .ToList();

                foreach (var good in filteredGoods)
                {
                    token.ThrowIfCancellationRequested();

                    var key = NormalizeGtin(good.Gtin);
                    if (!infoLookup.TryGetValue(key, out var detail) || detail == null)
                        continue;

                    Items.Add(new NationalCatalogRow
                    {
                        GoodId = good.GoodId,
                        Gtin = good.Gtin ?? string.Empty,
                        Name = string.IsNullOrWhiteSpace(good.GoodName) ? detail.Name ?? string.Empty : good.GoodName,
                        Brand = string.IsNullOrWhiteSpace(good.BrandName) ? detail.Brand ?? string.Empty : good.BrandName,
                        ProductGroupId = detail.ProductGroupId,
                        ProductGroupCode = detail.ProductGroupCode ?? string.Empty,
                        ProductKind = detail.ProductKind ?? string.Empty,
                        Status = (detail.GoodStatus ?? good.GoodStatus) ?? string.Empty,
                        DetailedStatus = good.GoodDetailedStatus != null ? string.Join(", ", good.GoodDetailedStatus) : string.Empty,
                        TnVed = detail.TnVedCode10 ?? detail.TnVedCode ?? (good.Tnved ?? string.Empty),
                        PackageType = detail.PackageType ?? string.Empty,
                        Nicotine = detail.NicotineConcentration ?? string.Empty,
                        Volume = detail.VolumeLiquid ?? string.Empty,
                        Updated = FormatDate(good.UpdatedDate)
                    });
                }

                StatusMessage = Items.Count > 0
                    ? $"Найдено товаров: {Items.Count}"
                    : "По выбранной товарной группе товаров не найдено.";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Загрузка отменена.";
            }
            catch (HttpRequestException ex)
            {
                StatusMessage = ex.Message;
                LogHelper.WriteLog("NationalCatalogViewModel.HttpError", ex.ToString());
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка загрузки данных Национального каталога.";
                LogHelper.WriteLog("NationalCatalogViewModel.Unexpected", ex.ToString());
                MessageBox.Show("Не удалось получить данные Национального каталога. Проверьте настройки и повторите попытку.",
                    "Национальный каталог", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        partial void OnIsLoadingChanged(bool value)
        {
            OnPropertyChanged(nameof(IsNotLoading));
            RefreshCommand?.NotifyCanExecuteChanged();
        }

        private void HandleAppStateChanged()
        {
            Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                SelectedProductGroupTitle = ResolveSelectedProductGroupTitle();
                if (_isActive)
                {
                    _ = RefreshAsync();
                }
            });
        }

        private string ResolveSelectedProductGroupTitle()
        {
            var cached = AppState.Instance.SelectedProductGroupName;
            if (!string.IsNullOrWhiteSpace(cached))
                return cached;

            var code = AppState.Instance.SelectedProductGroupCode;
            if (string.IsNullOrWhiteSpace(code))
                return "Группа не выбрана";

            var groups = EnsureProductGroupCache();
            return groups.TryGetValue(code, out var dto) ? dto.name : code;
        }

        private int? ResolveSelectedProductGroupId()
        {
            var code = AppState.Instance.SelectedProductGroupCode;
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var groups = EnsureProductGroupCache();
            return groups.TryGetValue(code, out var dto) ? dto.id : null;
        }

        private Dictionary<string, ProductGroupDto> EnsureProductGroupCache()
        {
            if (_productGroupCache != null)
                return _productGroupCache;

            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "product_groups.json");
                if (!File.Exists(path))
                {
                    _productGroupCache = new Dictionary<string, ProductGroupDto>(StringComparer.OrdinalIgnoreCase);
                    return _productGroupCache;
                }

                var json = File.ReadAllText(path);
                var root = JsonSerializer.Deserialize<ProductGroupRoot>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _productGroupCache = root?.result?
                    .Where(pg => !string.IsNullOrWhiteSpace(pg.code))
                    .ToDictionary(pg => pg.code, pg => pg, StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, ProductGroupDto>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("NationalCatalogViewModel.ProductGroups", ex.ToString());
                _productGroupCache = new Dictionary<string, ProductGroupDto>(StringComparer.OrdinalIgnoreCase);
            }

            return _productGroupCache;
        }

        private static string NormalizeGtin(string? gtin)
        {
            if (string.IsNullOrWhiteSpace(gtin))
                return string.Empty;

            var trimmed = gtin.Trim();
            var withoutLeadingZeros = trimmed.TrimStart('0');
            return string.IsNullOrEmpty(withoutLeadingZeros) ? "0" : withoutLeadingZeros;
        }

        private static string FormatDate(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
                return parsed.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

            if (DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
                return dto.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

            return input;
        }

        private class ProductGroupRoot
        {
            public List<ProductGroupDto> result
            {
                get; set;
            } = new();
        }
    }
}
