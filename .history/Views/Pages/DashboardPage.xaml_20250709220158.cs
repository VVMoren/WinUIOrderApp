using System;
using System.Windows;
using System.Windows.Controls;
using markapp.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;
using Microsoft.Web.WebView2.Core;

namespace markapp.Views.Pages
{
    public partial class DashboardPage : Page, INavigableView<DashboardViewModel>
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPage(DashboardViewModel viewModel)
        {
            InitializeComponent();

            ViewModel = viewModel;
            DataContext = ViewModel;

            this.Loaded += DashboardPage_Loaded;
        }

        private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Инициализация WebView2
            await Browser.EnsureCoreWebView2Async();

            // Подписка после инициализации
            Browser.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
        }

        private async void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (DataContext is not DashboardViewModel vm || vm.SessionCookies is null || vm.CsrfToken is null)
                return;

            var cookieManager = Browser.CoreWebView2.CookieManager;
            string domain = ".xn--80aqu.xn----7sbabas4ajkhfocclk9d3cvfsa.xn--p1ai";

            foreach (var pair in vm.SessionCookies)
            {
                var cookie = cookieManager.CreateCookie(pair.Key, pair.Value, domain, "/");
                cookie.Expires = DateTime.Now.AddHours(1);
                cookieManager.AddOrUpdateCookie(cookie);
            }

            var csrfCookie = cookieManager.CreateCookie("csrf-token", vm.CsrfToken, domain, "/");
            csrfCookie.Expires = DateTime.Now.AddHours(1);
            cookieManager.AddOrUpdateCookie(csrfCookie);

            // Переход к авторизованной странице
            Browser.CoreWebView2.Navigate("https://xn--j1ab.xn----7sbabas4ajkhfocclk9d3cvfsa.xn--p1ai/products");
        }
    }
}
