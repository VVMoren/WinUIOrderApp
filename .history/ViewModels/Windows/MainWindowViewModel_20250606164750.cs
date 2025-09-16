using System.Collections.ObjectModel;
using Wpf.Ui.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using markapp.Helpers;

namespace markapp.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = "mormark";

        [ObservableProperty]
        private ObservableCollection<object> _menuItems = new()
        {
            new NavigationViewItem()
            {
                Content = "Главная",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Home24 },
                TargetPageType = typeof(Views.Pages.DashboardPage)
            },
            new NavigationViewItem()
            {
                Content = "Данные",
                Icon = new SymbolIcon { Symbol = SymbolRegular.DataHistogram24 },
                TargetPageType = typeof(Views.Pages.DataPage)
            },
            new NavigationViewItem()
            {
                Content = "СУЗ",
                Icon = new SymbolIcon { Symbol = SymbolRegular.BuildingRetail24 },
                TargetPageType = typeof(Views.Pages.SUZPage)
            },
            new NavigationViewItem()
            {
                Content = "Нац.Кат.",
                Icon = new SymbolIcon { Symbol = SymbolRegular.BookGlobe24 },
                TargetPageType = typeof(Views.Pages.NationalCatalogPage)
            },
            new NavigationViewItem()
            {
                Content = "Док.",
                Icon = new SymbolIcon { Symbol = SymbolRegular.DocumentText24 },
                TargetPageType = typeof(Views.Pages.DocumentsPage)
            },
            new NavigationViewItem()
            {
                Content = "Поиск",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Search24 },
                TargetPageType = typeof(Views.Pages.SearchPage)
            },
            new NavigationViewItem()
            {
                Content = "Выгрузки",
                Icon = new SymbolIcon { Symbol = SymbolRegular.ArrowDownload24 },
                TargetPageType = typeof(Views.Pages.ExportsPage)
            }
        };
        

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems = new()
        {
            new NavigationViewItem()
            {
                Content = "Настройки",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(Views.Pages.SettingsPage)
            }
        };

        [ObservableProperty]
        private ObservableCollection<MenuItem> _trayMenuItems = new()
        {
            new MenuItem { Header = "Home", Tag = "tray_home" }
        };

        public string? CertificateOwner => AppState.Instance.CertificateOwner;

        public MainWindowViewModel()
        {
            AppState.Instance.TokenUpdated += () =>
            {
                OnPropertyChanged(nameof(CertificateOwner));
            };
        }
    }
}
