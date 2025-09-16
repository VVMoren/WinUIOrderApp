using Microsoft.Win32;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using markapp.Models;
using markapp.ViewModels.Pages;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Abstractions;
using markapp.Helpers;
using markapp.Services;
using markapp.Views.Windows;
using markapp;

namespace markapp.Views.Pages
{
    public partial class DataPage : INavigableView<DataViewModel>
    {
        public DataViewModel ViewModel { get; }

        public DataPage(DataViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;
            InitializeComponent();
        }


        // ✅ Обработчик кнопки для загрузки из файла
        private async void btnLoadTxt_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt",
                Title = "Выберите файл"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                await ViewModel.LoadFromFileAsync(openFileDialog.FileName);

            }
        }

        // Обработчик кнопки для очистки таблицы
        private void btnClearTable_Click(object sender, RoutedEventArgs e)
        {
            // Очистка списка, отображаемого в таблице
            ViewModel.RequestedCisList.Clear();
        }

        // Обработчик кнопки для удаления диапазона строк
        private void btnDeleteRange_Click(object sender, RoutedEventArgs e)
        {
            // Создаем и открываем окно
            var dialog = new DellDiapason();
            var result = dialog.ShowDialog();

            if (result == true)
            {
                // Парсим введённые значения
                if (int.TryParse(dialog.StartIndex, out int startIndex) &&
                    int.TryParse(dialog.EndIndex, out int endIndex))
                {
                    if (startIndex < 0 || endIndex < 0 || startIndex > endIndex || endIndex >= ViewModel.RequestedCisList.Count)
                    {
                        MessageBox.Show("Указан некорректный диапазон.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Удаляем строки в диапазоне
                    var itemsToRemove = ViewModel.RequestedCisList.Skip(startIndex).Take(endIndex - startIndex + 1).ToList();
                    foreach (var item in itemsToRemove)
                    {
                        ViewModel.RequestedCisList.Remove(item);
                    }
                }
                else
                {
                    MessageBox.Show("Введите корректные числовые значения.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        // Обработчик кнопки для обновления данных
        private async void btnUpdateData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Автоматически получаем актуальный статус без запроса
                if (ViewModel != null)
                {
                    // Вызываем метод FetchCisInfoBatchedAsync из ViewModel
                    await ViewModel.FetchCisInfoBatchedAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void dataGridRequestedCis_CurrentCellChanged(object sender, EventArgs e)
        {
            //btnSell.Visibility = ViewModel.HasSelectedAppliedItems ? Visibility.Visible : Visibility.Collapsed;
        }



    }
}
