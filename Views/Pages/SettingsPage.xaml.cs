using System.IO;
using System.Windows;
using System.Windows.Controls;
using WinUIOrderApp.Helpers;
using WinUIOrderApp.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace WinUIOrderApp.Views.Pages
{
    public partial class SettingsPage : INavigableView<SettingsViewModel>
    {
        public SettingsViewModel ViewModel
        {
            get;
        }

        public SettingsPage(SettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;

            InitializeComponent();
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.OnCertificateSelectionChanged();
        }

        private void OpenSettingsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var inn = AppState.ExtractInn(AppState.Instance.CertificateOwner);
                if (!string.IsNullOrEmpty(inn))
                {
                    var settingsPath = CertificateSettingsManager.GetCertificateSettingsPath(inn);
                    var directory = Path.GetDirectoryName(settingsPath);

                    if (Directory.Exists(directory))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", directory);
                    }
                    else
                    {
                        MessageBox.Show("Папка с настройками не найдена.", "Информация",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия папки: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}