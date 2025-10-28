using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using WinUIOrderApp.ViewModels.Pages;

namespace WinUIOrderApp.Views.Pages
{
    public partial class DashboardPage : Page
    {
        private DashboardViewModel? _viewModel;

        public DashboardPage()
        {
            InitializeComponent();

            // СОЗДАЕМ ViewModel ВРУЧНУЮ В CODE-BEHIND
            _viewModel = new DashboardViewModel();
            DataContext = _viewModel;

            Debug.WriteLine("=== DASHBOARD PAGE CREATED ===");
        }

        private async void ForceFetchProductCache_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("=== FORCE BUTTON CLICKED ===");

                if (_viewModel == null)
                {
                    MessageBox.Show("ViewModel is NULL!", "ERROR");
                    return;
                }

                // ВЫЗЫВАЕМ МЕТОД НАПРЯМУЮ
                await _viewModel.FetchProductCacheAsync();

                Debug.WriteLine("Method completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FORCE ERROR: {ex}");
                MessageBox.Show($"FORCE ERROR: {ex.Message}", "ERROR");
            }
        }
    }
}