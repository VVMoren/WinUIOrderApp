using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
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
        public ObservableCollection<MarkedCodeRow> Rows { get; } = new();
        public ObservableCollection<string> NameFilterOptions { get; } = new();
        public ObservableCollection<string> GtinFilterOptions { get; } = new();

        private List<MarkedCodeRow> _allRows = new();
        private bool _isUpdatingFilters;

        [ObservableProperty]
        private string? selectedNameFilter;

        [ObservableProperty]
        private string? selectedGtinFilter;

        [ObservableProperty]
        private string summaryText = string.Empty;

        [ObservableProperty]
        private bool isFilterPanelVisible;

        public DataPageViewModel()
        {
            NameFilterOptions.Add("Все");
            GtinFilterOptions.Add("Все");
            SelectedNameFilter = "Все";
            SelectedGtinFilter = "Все";

            AppState.Instance.KmDataUpdated += OnDataChanged;
            AppState.Instance.ProductCacheUpdated += OnDataChanged;
            AppState.Instance.CertificateSettingsChanged += OnDataChanged;

            RebuildData();
        }

        private void OnDataChanged()
        {
            RebuildData();
        }

        private void RebuildData()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var kmData = AppState.Instance.LastKmResults ?? new List<CisItem>();
                var goods = AppState.Instance.CachedGoods;
                var prefs = AppState.Instance.GetCurrentCertificatePreferences();
                var folder = prefs?.CryptoTailFolder;
                Dictionary<string, CryptoTailRecord> tails = new();

                if (prefs?.EnableCryptoTailSearch == true && !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                {
                    tails = LoadCryptoTailRecords(folder);
                }

                _allRows = kmData.Select(cis => BuildRow(cis, goods, tails)).ToList();
                UpdateFilters();
                ApplyFilters();
            });
        }

        private MarkedCodeRow BuildRow(CisItem cis, IReadOnlyDictionary<string, CachedGood> goods, Dictionary<string, CryptoTailRecord> tails)
        {
            var ki = CodeParser.ExtractKi(cis.Cis);
            var gtin = CodeParser.ExtractGtin(cis.Cis);
            goods.TryGetValue(gtin, out var cached);
            tails.TryGetValue(ki, out var tail);

            return new MarkedCodeRow
            {
                Cis = cis.Cis,
                Ki = ki,
                Gtin = gtin,
                Name = cis.Name ?? cis.ProductName ?? string.Empty,
                CachedName = cached?.Name ?? string.Empty,
                FullCryptoCode = tail?.FullCode ?? string.Empty,
                SourceFile = tail?.SourceFile ?? string.Empty
            };
        }

        private Dictionary<string, CryptoTailRecord> LoadCryptoTailRecords(string folder)
        {
            var dict = new Dictionary<string, CryptoTailRecord>();

            foreach (var file in Directory.EnumerateFiles(folder, "*.txt", SearchOption.AllDirectories))
            {
                foreach (var rawLine in File.ReadLines(file))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    var ki = CodeParser.ExtractKi(line);
                    if (string.IsNullOrEmpty(ki) || dict.ContainsKey(ki))
                        continue;

                    dict[ki] = new CryptoTailRecord
                    {
                        FullCode = line,
                        SourceFile = Path.GetFileName(file)
                    };
                }
            }

            return dict;
        }

        private void UpdateFilters()
        {
            _isUpdatingFilters = true;

            var previousName = SelectedNameFilter;
            var previousGtin = SelectedGtinFilter;

            NameFilterOptions.Clear();
            NameFilterOptions.Add("Все");
            foreach (var name in _allRows
                         .Select(r => string.IsNullOrEmpty(r.CachedName) ? r.Name : r.CachedName)
                         .Where(n => !string.IsNullOrEmpty(n))
                         .Distinct()
                         .OrderBy(n => n))
            {
                NameFilterOptions.Add(name);
            }

            GtinFilterOptions.Clear();
            GtinFilterOptions.Add("Все");
            foreach (var gtin in _allRows
                         .Select(r => r.Gtin)
                         .Where(g => !string.IsNullOrEmpty(g))
                         .Distinct()
                         .OrderBy(g => g))
            {
                GtinFilterOptions.Add(gtin);
            }

            SelectedNameFilter = NameFilterOptions.Contains(previousName ?? string.Empty) ? previousName : NameFilterOptions.FirstOrDefault();
            SelectedGtinFilter = GtinFilterOptions.Contains(previousGtin ?? string.Empty) ? previousGtin : GtinFilterOptions.FirstOrDefault();

            _isUpdatingFilters = false;
        }

        partial void OnSelectedNameFilterChanged(string? value)
        {
            ApplyFilters();
        }

        partial void OnSelectedGtinFilterChanged(string? value)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (_isUpdatingFilters)
                return;

            var nameFilter = SelectedNameFilter;
            var gtinFilter = SelectedGtinFilter;

            var filtered = _allRows.Where(row =>
                (string.IsNullOrEmpty(nameFilter) || nameFilter == "Все" || row.Name == nameFilter || row.CachedName == nameFilter) &&
                (string.IsNullOrEmpty(gtinFilter) || gtinFilter == "Все" || row.Gtin == gtinFilter)).ToList();

            Rows.Clear();
            foreach (var row in filtered)
                Rows.Add(row);
        }

        [RelayCommand]
        private void ToggleFilters()
        {
            IsFilterPanelVisible = !IsFilterPanelVisible;
        }

        [RelayCommand]
        private void CalculateSummary()
        {
            var groups = Rows
                .GroupBy(r => string.IsNullOrEmpty(r.CachedName) ? r.Name : r.CachedName)
                .Select(g => new
                {
                    Name = string.IsNullOrEmpty(g.Key) ? "Без наименования" : g.Key,
                    Count = g.Count()
                })
                .ToList();

            SummaryText = groups.Any()
                ? string.Join("; ", groups.Select(g => $"{g.Name}: {g.Count} шт."))
                : "Нет данных для свода.";
        }

        [RelayCommand]
        private async Task SaveAsAsync()
        {
            if (!Rows.Any())
            {
                MessageBox.Show("Нет данных для сохранения.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Сохранить данные",
                Filter = "Excel (*.xlsx)|*.xlsx|Текст (*.txt)|*.txt",
                FileName = "km_data"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                if (dialog.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                    using var package = new ExcelPackage();
                    var worksheet = package.Workbook.Worksheets.Add("Данные");

                    string[] headers =
                    {
                        "Код КМ (API)", "КИ", "GTIN", "Наименование (API)", "Наименование (кеш)", "Полный КМ", "Источник"
                    };

                    for (int i = 0; i < headers.Length; i++)
                        worksheet.Cells[1, i + 1].Value = headers[i];

                    int rowIndex = 2;
                    foreach (var row in Rows)
                    {
                        worksheet.Cells[rowIndex, 1].Value = row.Cis;
                        worksheet.Cells[rowIndex, 2].Value = row.Ki;
                        worksheet.Cells[rowIndex, 3].Value = row.Gtin;
                        worksheet.Cells[rowIndex, 4].Value = row.Name;
                        worksheet.Cells[rowIndex, 5].Value = row.CachedName;
                        worksheet.Cells[rowIndex, 6].Value = row.FullCryptoCode;
                        worksheet.Cells[rowIndex, 7].Value = row.SourceFile;
                        rowIndex++;
                    }

                    worksheet.Cells.AutoFitColumns();
                    await package.SaveAsAsync(new FileInfo(dialog.FileName));
                }
                else
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Код КМ|КИ|GTIN|Наименование (API)|Наименование (кеш)|Полный КМ|Источник");
                    foreach (var row in Rows)
                    {
                        sb.AppendLine($"{row.Cis}|{row.Ki}|{row.Gtin}|{row.Name}|{row.CachedName}|{row.FullCryptoCode}|{row.SourceFile}");
                    }

                    await File.WriteAllTextAsync(dialog.FileName, sb.ToString(), Encoding.UTF8);
                }

                MessageBox.Show("Данные сохранены.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private class CryptoTailRecord
        {
            public string FullCode { get; set; } = string.Empty;
            public string SourceFile { get; set; } = string.Empty;
        }
    }
}
