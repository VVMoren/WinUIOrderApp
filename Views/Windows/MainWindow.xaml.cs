using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WinUIOrderApp.ViewModels.Windows;
using WinUIOrderApp.Views.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace WinUIOrderApp.Views.Windows
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly IServiceProvider _services;

        public MainWindow(MainWindowViewModel viewModel, IServiceProvider services)
        {
            InitializeComponent();

            _viewModel = viewModel;
            _services = services;
            DataContext = _viewModel;

            Loaded += (_, __) =>
            {
                if (NavList.Items.Count > 0)
                    NavList.SelectedIndex = 0;

                // Получаем DashboardPage с DI, чтобы подставился DashboardViewModel
                var dashboard = _services.GetRequiredService<DashboardPage>();
                ContentFrame.Navigate(dashboard);
            };

            AppState.Instance.NavigateToExportsRequested += OnNavigateToExportsRequested;
        }

        private void OnNavigateToExportsRequested()
        {
            Dispatcher.Invoke(() =>
            {
                var exportsItem = NavList.Items.Cast<ListBoxItem?>()
                    .FirstOrDefault(item => item?.Tag as string == "ExportsPage");

                if (exportsItem != null)
                {
                    NavList.SelectedItem = exportsItem;
                }
                else
                {
                    var exportsPage = _services.GetRequiredService<ExportsPage>();
                    ContentFrame.Navigate(exportsPage);
                }
            });
        }

        private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NavList.SelectedItem is not ListBoxItem item || item.Tag is not string tag)
                return;

            Page? page = tag switch
            {
                "DashboardPage" => _services.GetRequiredService<DashboardPage>(),
                "DataPage" => _services.GetRequiredService<DataPage>(),
                "DocumentsPage" => _services.GetRequiredService<DocumentsPage>(),
                "SUZPage" => _services.GetRequiredService<SUZPage>(),
                "NationalCatalogPage" => _services.GetRequiredService<NationalCatalogPage>(),
                "SearchPage" => _services.GetRequiredService<SearchPage>(),
                "ExportsPage" => _services.GetRequiredService<ExportsPage>(),
                "SettingsPage" => _services.GetRequiredService<SettingsPage>(),
                _ => null
            };

            if (page != null)
                ContentFrame.Navigate(page);
        }
    }
}
