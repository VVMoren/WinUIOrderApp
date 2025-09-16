using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Data.Sqlite;
using WinUIOrderApp.Helpers;
using WinUIOrderApp.Models;
using WinUIOrderApp.ViewModels.Pages;

namespace WinUIOrderApp.Views.Pages
{
    public partial class DataPage : Page
    {
        private CancellationTokenSource? _cts;
        private static readonly Regex _digitsRegex = new Regex("^[0-9]+$");
        private DataViewModel VM => (DataViewModel)DataContext!;

        public DataPage()
        {
            InitializeComponent();

            if (DataContext == null)
                DataContext = new DataViewModel();

            Loaded += DataPage_Loaded;
            Unloaded += DataPage_Unloaded;
        }

        private async void DataPage_Loaded(object? sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                IsEnabled = false;
                await VM.LoadAllAsync(_cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsEnabled = true;
                Mouse.OverrideCursor = null;
            }
        }

        private void DataPage_Unloaded(object? sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void Quantity_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !_digitsRegex.IsMatch(e.Text);
        }

        private void Quantity_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                var text = e.DataObject.GetData(DataFormats.Text) as string ?? "";
                if (!_digitsRegex.IsMatch(text))
                    e.CancelCommand();
            }
            else
            {
                e.CancelCommand();
            }
        }

        private async void CbField_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var field = (CbField.SelectedItem as ComboBoxItem)?.Content as string;
            if (string.IsNullOrWhiteSpace(field)) return;

            if (field == "name" || field == "ownerInn")
            {
                TbValue.Visibility = Visibility.Visible;
                CbValue.Visibility = Visibility.Collapsed;
                TbValue.Text = "";
            }
            else // ownerName / producerName
            {
                TbValue.Visibility = Visibility.Collapsed;
                CbValue.Visibility = Visibility.Visible;
                CbValue.ItemsSource = null;
                CbValue.Text = "";

                var values = await GetDistinctValuesFromDbAsync(field);
                CbValue.ItemsSource = values;
                if (values.Any()) CbValue.SelectedIndex = 0;
            }
        }

        private void BtnApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            var field = (CbField.SelectedItem as ComboBoxItem)?.Content as string;
            var condition = (CbCondition.SelectedItem as ComboBoxItem)?.Content as string;

            if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(condition))
            {
                MessageBox.Show("Выберите поле и условие.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string value = field == "name" || field == "ownerInn"
                ? TbValue.Text?.Trim() ?? ""
                : (CbValue.SelectedItem as string ?? CbValue.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                MessageBox.Show("Введите значение фильтра.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (condition == "Содержит")
                VM.AddIncludeFilter(field, value);
            else
                VM.AddExcludeFilter(field, value);

            CbField.SelectedIndex = -1;
            CbCondition.SelectedIndex = -1;
            TbValue.Text = "";
            CbValue.ItemsSource = null;
            TbValue.Visibility = Visibility.Collapsed;
            CbValue.Visibility = Visibility.Collapsed;
        }

        private async Task<List<string>> GetDistinctValuesFromDbAsync(string field)
        {
            var res = new List<string>();
            try
            {
                var col = field switch
                {
                    "ownerName" => "Ip",
                    "producerName" => "Created",
                    _ => field
                };

                await Task.Run(() =>
                {
                    using var conn = new SqliteConnection($"Data Source={AppDbConfig.DbPath}");
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"SELECT DISTINCT {col} FROM Items WHERE {col} IS NOT NULL AND {col}<>'' ORDER BY {col} LIMIT 10000;";
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        if (!rdr.IsDBNull(0))
                            res.Add(rdr.GetString(0));
                    }
                });
            }
            catch { }
            return res;
        }

        // удаление чипа
        private void RemoveFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FilterTag tag)
            {
                VM.RemoveFilter(tag);
            }
        }

        // Формирование заказа — оставляем как раньше (использует VM)
        private void ProcessOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var map = VM.GetOrderMapFromSummary();
                if (map == null || map.Count == 0)
                {
                    MessageBox.Show("Нет указанных количеств для формирования заказа.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var allowed = new[] { "ООО \"ФОРМУЛА\"", "ООО \"КОСМОС\"" };
                var priority = new[] { "500504388749", "380128094636" };
                var exclude = new[] { "500504388749", "380128094636", "771586249840", "502730263137" };
                var outDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WinUIOrderApp");

                var engine = new Services.OrderEngine(allowed, priority, exclude, outDir);
                var baseRows = VM.FilteredRows.ToList();
                var used = new HashSet<string>();

                var selected = engine.BuildOrder(baseRows, map, used);
                if (selected == null || selected.Count == 0)
                {
                    MessageBox.Show("Не удалось подобрать CIS.", "Результат", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var path = engine.SaveOrder(selected);
                MessageBox.Show($"Сформировано {selected.Count} CIS в {path}", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при формировании заказа: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
