using markapp.ViewModels.Windows;
using System;
using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Abstractions;

namespace markapp.Views.Windows
{
    public partial class MainWindow : FluentWindow, INavigationWindow
    {
        public MainWindowViewModel ViewModel { get; }

        private readonly INavigationService _navigationService;

        public MainWindow(
            MainWindowViewModel viewModel,
            INavigationViewPageProvider navigationViewPageProvider,
            INavigationService navigationService)
        {
            ViewModel = viewModel;
            DataContext = this;

            _navigationService = navigationService;

            SystemThemeWatcher.Watch(this);

            InitializeComponent();

            SetPageService(navigationViewPageProvider);
            _navigationService.SetNavigationControl(RootNavigation);
        }

        #region INavigationWindow Implementation

        public INavigationView GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(INavigationViewPageProvider navigationViewPageProvider)
        {
            RootNavigation.SetPageProviderService(navigationViewPageProvider);
        }

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            // Optional DI logic if needed later
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Application.Current.Shutdown();
        }
    }
}
