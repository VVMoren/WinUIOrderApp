using System.IO;
using System.Reflection;
using System.Windows.Threading;
using WinUIOrderApp.Helpers;
using WinUIOrderApp.Services;
using WinUIOrderApp.ViewModels.Pages;
using WinUIOrderApp.ViewModels.Windows;
using WinUIOrderApp.Views.Pages;
using WinUIOrderApp.Views.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;
using GostCryptography.Config;

namespace WinUIOrderApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        // The.NET Generic Host обеспечивает внедрение зависимостей, настройку, ведение журнала и другие сервисы.
        // https://docs.microsoft.com/dotnet/core/extensions/generic-host
        // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
        // https://docs.microsoft.com/dotnet/core/extensions/configuration
        // https://docs.microsoft.com/dotnet/core/extensions/logging
        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
            .ConfigureAppConfiguration(c => { c.SetBasePath(Path.GetDirectoryName(AppContext.BaseDirectory)); })
            .ConfigureServices((context, services) =>
            {
                GostCryptoConfig.Initialize();

                services.AddNavigationViewPageProvider();
            
                services.AddHostedService<ApplicationHostService>();
            
                // Темы и системные сервисы
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<ITaskBarService, TaskBarService>();
                services.AddSingleton<INavigationService, NavigationService>();
            
                // Главное окно
                services.AddSingleton<INavigationWindow, MainWindow>();
                services.AddSingleton<MainWindowViewModel>();
            
                // Базовые страницы
                services.AddSingleton<DashboardPage>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<DataPage>();
                services.AddSingleton<DataViewModel>();
                services.AddSingleton<SettingsPage>();
                services.AddSingleton<SettingsViewModel>();
            
                // 🔻 НОВЫЕ СТРАНИЦЫ (добавлены вручную)
                services.AddSingleton<SUZPage>();
                services.AddSingleton<NationalCatalogPage>();
                services.AddSingleton<DocumentsPage>();
                services.AddSingleton<SearchPage>();
                services.AddSingleton<ExportsPage>();
            }).Build();

        /// <summary>
        /// Получает услуги.
        /// </summary>
        public static IServiceProvider Services
        {
            get { return _host.Services; }
        }

        /// <summary>
        /// Происходит во время загрузки приложения.
        /// </summary>
        private async void OnStartup(object sender, StartupEventArgs e)
        {
            AppState.Instance.LoadSettings();
            await _host.StartAsync();
        }

        /// <summary>
        /// Происходит при закрытии приложения.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            AppState.Instance.SaveSettings();
            await _host.StopAsync();
            _host.Dispose();
        }

        /// <summary>
        /// Возникает, когда приложение генерирует исключение, но не обрабатывает его.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
        }
    }
}
