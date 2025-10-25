using System;
using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WinUIOrderApp.Helpers;
using WinUIOrderApp.ViewModels.Pages;
using WinUIOrderApp.ViewModels.Windows;
using WinUIOrderApp.Views.Pages;
using WinUIOrderApp.Views.Windows;

namespace WinUIOrderApp
{
    public partial class App : Application
    {
        private IHost? _host;

        public static IServiceProvider? Services => (Current as App)?._host?.Services;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 📌 Путь к исходному файлу iDB.txt
        //  string sourceTxt = @"X:\1code_all order\DB\iDB.txt"; // file-txt

            // 📝 Проверяем, есть ли база. Если нет — создаём.
        //  AppDbInitializer.EnsureDatabase(sourceTxt);

            _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // --- ViewModels (регистрируем те, что используются в Page constructors) ---
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<DataPageViewModel>();
                services.AddSingleton<MainWindowViewModel>();
            
                // страницы часто используют ViewModel'ы — регистрируем их тоже
                services.AddSingleton<ExportsViewModel>();
                services.AddSingleton<SettingsViewModel>();
            
                // --- Pages ---
                services.AddSingleton<DashboardPage>();
                services.AddSingleton<DataPage>();
                services.AddSingleton<DocumentsPage>();
                services.AddSingleton<SUZPage>();
                services.AddSingleton<NationalCatalogPage>();
                services.AddSingleton<SearchPage>();
                services.AddSingleton<ExportsPage>();
                services.AddSingleton<SettingsPage>();
            
                // --- Main window and window VM ---
                services.AddSingleton<MainWindow>();
                // MainWindowViewModel уже зарегистрирован выше
            })

            .Build();
            _host.Start();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = _host.Services.GetRequiredService<MainWindowViewModel>();
            mainWindow.Show();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_host is not null)
                await _host.StopAsync();

            base.OnExit(e);
        }
    }
}
