using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OfficeOpenXml;
using WinUIOrderApp.Helpers;
using WinUIOrderApp.Models;

namespace WinUIOrderApp.ViewModels.Pages
{
    public partial class DataPageViewModel : ObservableObject
    {
        public ObservableCollection<MarkingCodeRow> Codes { get; } = new();
        public ObservableCollection<MarkingCodeRow> FilteredCodes { get; } = new();
        public ObservableCollection<SummaryRow> SummaryEntries { get; } = new();

        public ObservableCollection<string> AvailableNames { get; } = new();
        public ObservableCollection<string> AvailableGtins { get; } = new();
        public ObservableCollection<string> AvailableCryptoStatuses { get; } = new() { "Все", "Найден", "Не найден" };

        [ObservableProperty]
        private string? selectedName;

        [ObservableProperty]
        private string? selectedGtin;

        [ObservableProperty]
        private string selectedCryptoStatus = "Все";

        [ObservableProperty]
        private bool isFilterPanelVisible;

        [ObservableProperty]
        private bool isSummaryVisible;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        public DataPageViewModel()
        {
            AppState.Instance.MarkingCodesUpdated += OnExternalDataChanged;
            AppState.Instance.AdvancedSettingsChanged += OnExternalDataChanged;
        }

        private async void OnExternalDataChanged()
        {
            await LoadLatestDataAsync();
        }

        public async Task LoadLatestDataAsync()
        {
            if (IsLoading)
                return;

            try
            {
                IsLoading = true;
                var cisItems = AppState.Instance.LatestCisItems?.ToList() ?? new List<CisItem>();

                Dictionary<string, string> cryptoIndex = new();
                if (AppState.Instance.UseCryptoTailSearch && !string.IsNullOrWhiteSpace(AppState.Instance.CryptoTailFolderPath))
                {
                    cryptoIndex = await Task.Run(() => CryptoTailService.BuildIndex(AppState.Instance.CryptoTailFolderPath));
                }

                var productCache = ProductCacheService.Load(AppState.Instance.ProductCacheFilePath);

                var rows = new List<MarkingCodeRow>();
                foreach (var item in cisItems)
                {
                    var row = new MarkingCodeRow
                    {
                        Cis = item.Cis,
                        Ki = string.IsNullOrEmpty(item.Ki) ? MarkingCodeParser.ExtractKi(item.Cis) : item.Ki,
                        Gtin = !string.IsNullOrEmpty(item.Gtin) ? item.Gtin : MarkingCodeParser.ExtractGtin(item.Cis),
                        SourceName = item.Name,
                    };

                    if (!string.IsNullOrWhiteSpace(row.Ki) && cryptoIndex.TryGetValue(row.Ki, out var fullCode))
                    {
                        row.FullCode = fullCode;
                        row.CryptoStatus = "Найден";
                    }
                    else
                    {
                        row.CryptoStatus = "Не найден";
                    }

                    if (!string.IsNullOrEmpty(row.Gtin) && productCache.TryGetValue(row.Gtin, out var product))
                    {
                        row.ProductName = product.GoodName ?? item.ProductName ?? item.Name ?? string.Empty;
                        row.Brand = product.BrandName ?? string.Empty;
                        row.Tnved = product.Tnved ?? string.Empty;
                    }
                    else
                    {
                        row.ProductName = item.ProductName ?? item.Name ?? string.Empty;
                    }

                    rows.Add(row);
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Codes.Clear();
                    foreach (var row in rows.OrderBy(r => r.ProductName))
                        Codes.Add(row);

                    UpdateFilters();
                    ApplyFilters();
                    SummaryEntries.Clear();
                    IsSummaryVisible = false;
                    StatusMessage = Codes.Count == 0 ? "Данные КМ не загружены" : $"Загружено записей: {Codes.Count}";
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка загрузки данных: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateFilters()
        {
            AvailableNames.Clear();
            AvailableNames.Add("Все");
            foreach (var name in Codes.Select(c => c.ProductName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n))
                AvailableNames.Add(name);

            AvailableGtins.Clear();
            AvailableGtins.Add("Все");
            foreach (var gtin in Codes.Select(c => c.Gtin).Where(g => !string.IsNullOrWhiteSpace(g)).Distinct().OrderBy(g => g))
                AvailableGtins.Add(gtin);

            if (!AvailableNames.Contains(SelectedName))
                SelectedName = "Все";

            if (!AvailableGtins.Contains(SelectedGtin))
                SelectedGtin = "Все";

            if (string.IsNullOrEmpty(SelectedCryptoStatus))
                SelectedCryptoStatus = "Все";
        }

        private void ApplyFilters()
        {
            IEnumerable<MarkingCodeRow> query = Codes;

            if (!string.IsNullOrWhiteSpace(SelectedName) && SelectedName != "Все")
                query = query.Where(row => string.Equals(row.ProductName, SelectedName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SelectedGtin) && SelectedGtin != "Все")
                query = query.Where(row => string.Equals(row.Gtin, SelectedGtin, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SelectedCryptoStatus) && SelectedCryptoStatus != "Все")
                query = query.Where(row => string.Equals(row.CryptoStatus, SelectedCryptoStatus, StringComparison.OrdinalIgnoreCase));

            var result = query.ToList();

            FilteredCodes.Clear();
            foreach (var row in result)
                FilteredCodes.Add(row);

            StatusMessage = result.Count == 0 ? "Нет данных для выбранных фильтров" : $"Отфильтровано записей: {result.Count}";
        }

        partial void OnSelectedNameChanged(string? value) => ApplyFilters();
        partial void OnSelectedGtinChanged(string? value) => ApplyFilters();
        partial void OnSelectedCryptoStatusChanged(string value) => ApplyFilters();

        [RelayCommand]
        private void ToggleFilters()
        {
            IsFilterPanelVisible = !IsFilterPanelVisible;
        }

        [RelayCommand]
        private void ShowSummary()
        {
            var source = FilteredCodes.Count > 0 ? FilteredCodes : Codes;

            SummaryEntries.Clear();
            foreach (var group in source.GroupBy(row => string.IsNullOrWhiteSpace(row.ProductName) ? "Без наименования" : row.ProductName))
            {
                SummaryEntries.Add(new SummaryRow
                {
                    Name = group.Key,
                    Count = group.Count()
                });
            }

            IsSummaryVisible = SummaryEntries.Count > 0;
        }

        [RelayCommand]
        private async Task SaveAsAsync()
        {
            var source = FilteredCodes.Count > 0 ? FilteredCodes.ToList() : Codes.ToList();
            if (source.Count == 0)
            {
                MessageBox.Show("Нет данных для сохранения.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var choice = MessageBox.Show("Сохранить в формате Excel? (Нет — сохранить в TXT)", "Сохранить как",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Yes);

            if (choice == MessageBoxResult.Cancel)
                return;

            var certificate = AppState.Instance.SelectedCertificate;
            var inn = certificate != null ? AppState.ExtractInn(certificate.Subject) : string.Empty;
            var baseDir = !string.IsNullOrEmpty(inn)
                ? CertificateSettingsManager.GetCertificateDirectory(inn)
                : AppContext.BaseDirectory;

            if (choice == MessageBoxResult.Yes)
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    InitialDirectory = baseDir,
                    FileName = $"km-data-{DateTime.Now:yyyyMMddHHmm}.xlsx"
                };

                if (dialog.ShowDialog() == true)
                {
                    await SaveToExcelAsync(dialog.FileName, source);
                    MessageBox.Show($"Данные сохранены в {dialog.FileName}", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else if (choice == MessageBoxResult.No)
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Текстовый файл (*.txt)|*.txt",
                    InitialDirectory = baseDir,
                    FileName = $"km-data-{DateTime.Now:yyyyMMddHHmm}.txt"
                };

                if (dialog.ShowDialog() == true)
                {
                    await SaveToTextAsync(dialog.FileName, source);
                    MessageBox.Show($"Данные сохранены в {dialog.FileName}", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private static async Task SaveToExcelAsync(string filePath, List<MarkingCodeRow> data)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Данные КМ");

            var headers = new[] { "КИ", "Полный КМ", "GTIN", "Наименование", "Статус криптохвоста", "Бренд", "Источник" };
            for (int i = 0; i < headers.Length; i++)
                worksheet.Cells[1, i + 1].Value = headers[i];

            for (int row = 0; row < data.Count; row++)
            {
                var entry = data[row];
                worksheet.Cells[row + 2, 1].Value = entry.Ki;
                worksheet.Cells[row + 2, 2].Value = entry.FullCode;
                worksheet.Cells[row + 2, 3].Value = entry.Gtin;
                worksheet.Cells[row + 2, 4].Value = entry.ProductName;
                worksheet.Cells[row + 2, 5].Value = entry.CryptoStatus;
                worksheet.Cells[row + 2, 6].Value = entry.Brand;
                worksheet.Cells[row + 2, 7].Value = entry.SourceName;
            }

            worksheet.Cells[1, 1, 1, headers.Length].Style.Font.Bold = true;
            worksheet.Cells.AutoFitColumns();

            await package.SaveAsAsync(new FileInfo(filePath));
        }

        private static async Task SaveToTextAsync(string filePath, List<MarkingCodeRow> data)
        {
            using var writer = new StreamWriter(filePath);
            await writer.WriteLineAsync("КИ|Полный КМ|GTIN|Наименование|Статус криптохвоста|Бренд|Источник");
            foreach (var entry in data)
            {
                var line = string.Join("|", new[]
                {
                    entry.Ki,
                    entry.FullCode,
                    entry.Gtin,
                    entry.ProductName,
                    entry.CryptoStatus,
                    entry.Brand,
                    entry.SourceName
                });
                await writer.WriteLineAsync(line);
            }
        }

        public class MarkingCodeRow : ObservableObject
        {
            public string Cis { get; set; } = string.Empty;
            public string Ki { get; set; } = string.Empty;
            public string FullCode { get; set; } = string.Empty;
            public string Gtin { get; set; } = string.Empty;
            public string ProductName { get; set; } = string.Empty;
            public string Brand { get; set; } = string.Empty;
            public string CryptoStatus { get; set; } = string.Empty;
            public string SourceName { get; set; } = string.Empty;
            public string Tnved { get; set; } = string.Empty;
        }

        public class SummaryRow
        {
            public string Name { get; set; } = string.Empty;
            public int Count { get; set; }
        }
    }
}
