using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinUIOrderApp.Helpers;

namespace WinUIOrderApp.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject
    {
        [ObservableProperty]
        private string organisationName = "Загрузка...";

        [ObservableProperty]
        private string organisationInn = "";

        [ObservableProperty]
        private string organisationOgrn = "";

        [ObservableProperty]
        private string productGroupName = "";

        public DashboardViewModel()
        {
            // Подписываемся на события изменения состояния
            AppState.Instance.OnProductGroupChanged += OnAppStateChanged;
            AppState.Instance.TokenUpdated += OnAppStateChanged;

            // Загружаем данные при создании
            LoadOrganisationAsync();
        }

        private void OnAppStateChanged()
        {
            // Обновляем данные при изменении сертификата или токена
            LoadOrganisationAsync();
        }

        [RelayCommand]
        private async Task LoadOrganisationAsync()
        {
            try
            {
                var code = AppState.Instance.SelectedProductGroupCode;
                var token = AppState.Instance.Token;
                var cert = AppState.Instance.SelectedCertificate;

                // Если в кеше актуальные данные — используем
                if (AppState.Instance.HasValidOrganisationCache())
                {
                    OrganisationName = AppState.Instance.OrganisationName;
                    OrganisationInn = AppState.Instance.OrganisationInn;
                    OrganisationOgrn = AppState.Instance.OrganisationOgrn;
                    ProductGroupName = ResolveProductGroupName(code);
                    return;
                }

                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(token) || cert == null)
                {
                    OrganisationName = "Не подключено к ГИС МТ";
                    OrganisationInn = "";
                    OrganisationOgrn = "";
                    ProductGroupName = "";
                    return;
                }

                var inn = ExtractInn(cert.Subject);
                if (string.IsNullOrEmpty(inn))
                    inn = "000000000000";

                string url = $"https://{code}.crpt.ru/bff-elk/v1/organisation/list?inns={inn}";

                using var http = new HttpClient();
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await http.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<List<OrganisationResponse>>(json);

                if (data == null || data.Count == 0)
                {
                    OrganisationName = "Организация не найдена";
                    return;
                }

                var org = data[0];
                OrganisationName = org.name;
                OrganisationInn = org.inn;
                OrganisationOgrn = org.ogrn;
                ProductGroupName = ResolveProductGroupName(code);

                // сохраняем в AppState на 12 часов
                AppState.Instance.OrganisationName = org.name;
                AppState.Instance.OrganisationInn = org.inn;
                AppState.Instance.OrganisationOgrn = org.ogrn;
                AppState.Instance.OrganisationFetchedAt = DateTime.Now;
            }
            catch (Exception ex)
            {
                OrganisationName = $"Ошибка: {ex.Message}";
                OrganisationInn = "";
                OrganisationOgrn = "";
                ProductGroupName = "";
            }
        }

        [RelayCommand]
        private void RefreshData()
        {
            // Принудительное обновление данных
            AppState.Instance.OrganisationFetchedAt = DateTime.MinValue;
            LoadOrganisationAsync();
        }

        private string ResolveProductGroupName(string code)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "product_groups.json");
                if (!File.Exists(path)) return code ?? "Не выбрана";

                var json = File.ReadAllText(path);
                var root = JsonSerializer.Deserialize<ProductGroupRoot>(json);
                var group = root?.result?.Find(pg => pg.code == code);
                return group?.name ?? code ?? "Не выбрана";
            }
            catch
            {
                return code ?? "Не выбрана";
            }
        }

        private static string ExtractInn(string subject)
        {
            if (string.IsNullOrEmpty(subject)) return null;
            var parts = subject.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var p = part.Trim();
                if (p.StartsWith("ИНН=", StringComparison.OrdinalIgnoreCase))
                    return p.Substring(4);
            }
            return null;
        }

        private class OrganisationResponse
        {
            public string name
            {
                get; set;
            }
            public string inn
            {
                get; set;
            }
            public string ogrn
            {
                get; set;
            }
        }

        private class ProductGroupRoot
        {
            public List<ProductGroupItem> result
            {
                get; set;
            }
        }

        private class ProductGroupItem
        {
            public string code
            {
                get; set;
            }
            public string name
            {
                get; set;
            }
        }
    }
}