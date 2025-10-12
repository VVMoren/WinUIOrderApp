using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WinUIOrderApp.Helpers;
using WinUIOrderApp.Models;
using WinUIOrderApp.Services;
using WinUIOrderApp.ViewModels.Pages;
using WinUIOrderApp.Views.Windows;



namespace WinUIOrderApp.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableObject
    {
        // Коллекция доступных сертификатов с приватным ключом
        public ObservableCollection<X509Certificate2> Certificates { get; } = new();

        private X509Certificate2? _selectedCertificate;
        public X509Certificate2? SelectedCertificate
        {
            get => _selectedCertificate;
            set
            {
                if (SetProperty(ref _selectedCertificate, value))
                {
                    ConnectToGisMtCommand.NotifyCanExecuteChanged();
                }
            }
        }

        // Команды
        public ICommand SelectLogFilePathCommand
        {
            get;
        }
        public IAsyncRelayCommand ConnectToGisMtCommand
        {
            get;
        }

        // Путь к текущему лог-файлу (только чтение)
        public string LogFilePath => LogHelper.LogFilePath;

        public ICommand OpenProductGroupSelectionCommand
        {
            get;
        }
        public ICommand SelectProductGroupCommand
        {
            get;
        }


        // ctor
        public SettingsViewModel()
        {
            // команды
            SelectLogFilePathCommand = new RelayCommand(SelectLogFilePath);
            ConnectToGisMtCommand = new AsyncRelayCommand(ConnectToGisMtAsync, CanConnectToGisMt);
            OpenProductGroupSelectionCommand = new RelayCommand(OpenProductGroupSelection);
            SelectProductGroupCommand = new RelayCommand<ProductGroupDto>(SelectProductGroup);

            LoadCertificates();
            LoadProductGroups();
            if (!string.IsNullOrEmpty(AppState.Instance.Token))
            {
                _ = LoadUserProfileAndFilterProductGroups();
            }
        }

        // --- загрузка сертификатов (сделал public чтобы можно было вызывать и с View)
        public void LoadCertificates()
        {
            Certificates.Clear();

            // читаем CurrentUser + LocalMachine для надёжности
            foreach (var loc in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
            {
                try
                {
                    using var store = new X509Store(StoreName.My, loc);
                    store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                    foreach (var cert in store.Certificates)
                    {
                        if (HasPrivate(cert) && !Certificates.Any(c => c.Thumbprint == cert.Thumbprint))
                            Certificates.Add(cert);
                    }
                }
                catch (Exception ex)
                {
                    // логируем, но не падаем
                    System.Diagnostics.Debug.WriteLine($"LoadCertificates [{loc}] error: {ex.Message}");
                }
            }
        }

        // надёжная проверка наличия приватного ключа
        private static bool HasPrivate(X509Certificate2 cert)
        {
            if (cert == null) return false;
            try
            {
                if (cert.HasPrivateKey) return true;
                return cert.GetRSAPrivateKey() != null
                    || cert.GetDSAPrivateKey() != null
                    || cert.GetECDsaPrivateKey() != null;
            }
            catch
            {
                return false;
            }
        }

        // --- SelectLogFilePath: открывает SaveFileDialog и сохраняет путь в лог-хелпер (если нужно)
        private void SelectLogFilePath()
        {
            var dlg = new SaveFileDialog
            {
                Title = "Выберите файл для логов",
                Filter = "Log files (*.log)|*.log|All files|*.*",
                FileName = System.IO.Path.GetFileName(LogHelper.LogFilePath)
            };

            var ok = dlg.ShowDialog();
            if (ok == true)
            {
                try
                {
                    OnPropertyChanged(nameof(LogFilePath));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось установить путь для лога: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // --- ConnectToGisMt: выбираем файл, подписываем и показываем результат
        private bool CanConnectToGisMt() => SelectedCertificate != null;

        private async Task ConnectToGisMtAsync()
        {
            if (SelectedCertificate == null)
            {
                MessageBox.Show("Выберите сертификат.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                // Очищаем кэш токена при смене сертификата
                if (AppState.Instance.SelectedCertificate?.Thumbprint != SelectedCertificate.Thumbprint)
                {
                    ClearTokenCache();
                    LogHelper.WriteLog("SettingsViewModel.ConnectToGisMtAsync",
                        $"Смена сертификата: {AppState.Instance.SelectedCertificate?.Thumbprint} -> {SelectedCertificate.Thumbprint}. Кэш очищен.");
                }

                var token = await GisMtAuthService.AuthorizeGisMtAsync(SelectedCertificate);
                if (!string.IsNullOrEmpty(token))
                {
                    MessageBox.Show("Авторизация прошла успешно.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                    AppState.Instance.Token = token;
                    AppState.Instance.SelectedCertificate = SelectedCertificate;
                    AppState.Instance.CertificateOwner = SelectedCertificate.Subject;
                    AppState.Instance.CertificateOwnerPublicName = AppState.ExtractCN(SelectedCertificate.Subject);

                    // Уведомляем об обновлении токена
                    AppState.Instance.NotifyTokenUpdated();

                    await LoadUserProfileAndFilterProductGroups();
                    var enabledGroups = ProductGroups.Where(pg => pg.IsEnabled).ToList();
                    if (enabledGroups.Any())
                    {
                        await Task.Delay(300);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            OpenProductGroupSelection();
                        });
                    }
                    else
                    {
                        MessageBox.Show("Авторизация прошла успешно, но у вас нет доступных товарных групп.",
                            "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    try
                    {
                        var dashboardVm = new ViewModels.Pages.DashboardViewModel();
                        var method = dashboardVm.GetType().GetMethod(
                            "LoadOrganisationAsync",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        if (method != null)
                        {
                            var task = method.Invoke(dashboardVm, null) as Task;
                            if (task != null)
                                await task;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Ошибка при автообновлении Dashboard: " + ex.Message);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("AuthorizeGisMtAsync вернул null/пустой токен.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при авторизации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // Добавьте этот метод в класс SettingsViewModel для очистки кэша
        private void ClearTokenCache()
        {
            try
            {
                // Очищаем старый единый файл кэша
                string oldCachePath = System.IO.Path.Combine(AppContext.BaseDirectory, "Token.txt");
                if (System.IO.File.Exists(oldCachePath))
                {
                    System.IO.File.Delete(oldCachePath);
                    LogHelper.WriteLog("SettingsViewModel.ClearTokenCache", "Удален старый файл Token.txt");
                }

                // Очищаем папку с кэшем токенов
                string cacheDir = System.IO.Path.Combine(AppContext.BaseDirectory, "TokenCache");
                if (Directory.Exists(cacheDir))
                {
                    Directory.Delete(cacheDir, true);
                    LogHelper.WriteLog("SettingsViewModel.ClearTokenCache", "Очищена папка TokenCache");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("SettingsViewModel.ClearTokenCache.Error", $"Ошибка очистки кэша: {ex.Message}");
            }
        }
        // -тг

        private void OpenProductGroupSelection()
        {
            var selectionWindow = new ProductGroupSelectionWindow();
            selectionWindow.Owner = Application.Current.MainWindow;
            selectionWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            selectionWindow.ShowDialog();
        }

        private void SelectProductGroup(ProductGroupDto productGroup)
        {
            if (productGroup != null && productGroup.IsEnabled)
            {
                SelectedProductGroup = productGroup;

                // Закрываем окно выбора после выбора
                var window = Application.Current.Windows.OfType<ProductGroupSelectionWindow>().FirstOrDefault();
                window?.Close();

                MessageBox.Show($"Выбрана товарная группа: {productGroup.name}", "Выбор завершен",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Данная товарная группа недоступна", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // --- Product groups: коллекция и выбор
        public ObservableCollection<ProductGroupDto> ProductGroups { get; } = new();

        private ProductGroupDto? _selectedProductGroup;
        public ProductGroupDto? SelectedProductGroup
        {
            get => _selectedProductGroup;
            set
            {
                if (SetProperty(ref _selectedProductGroup, value) && value != null)
                {
                    AppState.Instance.SelectedProductGroupCode = value.code;
                    AppState.Instance.SelectedProductGroupName = value.name;

                    // 🔔 уведомляем все подписанные части UI, что группа изменилась
                    AppState.Instance.NotifyProductGroupChanged();
                }
            }
        }


        /// <summary>
        /// Загружает список товарных групп из локального JSON.
        /// </summary>
        public void LoadProductGroups()
        {
            try
            {
                var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "product_groups.json");
                if (!File.Exists(jsonPath))
                {
                    System.Diagnostics.Debug.WriteLine($"product_groups.json not found: {jsonPath}");
                    return;
                }

                var json = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var root = JsonSerializer.Deserialize<ProductGroupRoot>(json, options);
                ProductGroups.Clear();

                if (root?.result == null) return;

                foreach (var pg in root.result)
                {
                    // Нормализуем название (убираем лишние \n и пробелы)
                    pg.name = (pg.name ?? "").Replace("\r", "").Replace("\n", " ").Trim();

                    // По умолчанию IsEnabled=false — будет обновлено после LoadUserProfile...
                    ProductGroups.Add(pg);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("LoadProductGroups", ex.ToString());
            }
        }


        /// <summary>
        /// Загружает профиль участника через /bff-elk/v1/profile/organisation
        /// и отмечает доступные товарные группы.
        /// </summary>
        public async Task LoadUserProfileAndFilterProductGroups()
        {
            if (string.IsNullOrWhiteSpace(AppState.Instance.Token))
                return;

            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", AppState.Instance.Token);

                // ✅ Новый API эндпоинт
                var url = "https://markirovka.crpt.ru/bff-elk/v1/profile/organisation";
                var response = await http.GetAsync(url);

                var content = await response.Content.ReadAsStringAsync();
                LogHelper.WriteLog("LoadUserProfile", $"{response.StatusCode}\n{content}");

                if (!response.IsSuccessStatusCode)
                {
                    // При 401/410 → токен устарел → пробуем обновить
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                        (int)response.StatusCode == 410)
                    {
                        LogHelper.WriteLog("LoadUserProfile", "Токен устарел — повторная авторизация...");
                        var newToken = await GisMtAuthService.AuthorizeGisMtAsync(AppState.Instance.SelectedCertificate);
                        if (!string.IsNullOrEmpty(newToken))
                        {
                            AppState.Instance.Token = newToken;
                            await LoadUserProfileAndFilterProductGroups();
                            return;
                        }
                    }

                    throw new HttpRequestException($"Ошибка {response.StatusCode}: {content}");
                }

                // === Парсим JSON ===
                var organisation = JsonSerializer.Deserialize<OrganisationProfile>(
                    content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (organisation == null)
                {
                    MessageBox.Show("Не удалось разобрать ответ сервера.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // === Обновляем глобальный кэш организации ===
                AppState.Instance.OrganisationName = organisation.name ?? "";
                AppState.Instance.OrganisationInn = organisation.inn ?? "";
                AppState.Instance.OrganisationOgrn = organisation.ogrn ?? "";
                AppState.Instance.OrganisationFetchedAt = DateTime.Now;

                // === Отмечаем разрешённые группы ===
                var allowedCodes = organisation.productGroupsAndRoles?
                    .Select(pg => pg.code)
                    .ToHashSet() ?? new HashSet<string>();

                foreach (var pg in ProductGroups)
                    pg.IsEnabled = allowedCodes.Contains(pg.code);

                // === Сортируем список ===
                var sorted = ProductGroups
                    .OrderByDescending(p => p.IsEnabled)
                    .ThenBy(p => p.name)
                    .ToList();

                ProductGroups.Clear();
                foreach (var item in sorted)
                    ProductGroups.Add(item);

                LogHelper.WriteLog("LoadUserProfile", $"OK. Активных групп: {allowedCodes.Count}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("LoadUserProfile Error", ex.ToString());
                MessageBox.Show("Ошибка получения профиля участника.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // DTO для product_groups.json
        private class ProductGroupRoot
        {
            public List<ProductGroupDto> result { get; set; } = new();
        }

        private class OrganisationProfile
        {
            public long id
            {
                get; set;
            }                      // ✅ число, не строка
            public string? inn
            {
                get; set;
            }
            public string? name
            {
                get; set;
            }
            public string? shortName
            {
                get; set;
            }
            public string? fullName
            {
                get; set;
            }
            public string? ogrn
            {
                get; set;
            }
            public string? status
            {
                get; set;
            }
            public string? organizationForm
            {
                get; set;
            }

            public List<ProductGroupRole> productGroupsAndRoles { get; set; } = new();

            public class ProductGroupRole
            {
                public string code { get; set; } = string.Empty;
                public List<string>? types
                {
                    get; set;
                }
                public bool farmer
                {
                    get; set;
                }
            }
        }




    }
}
