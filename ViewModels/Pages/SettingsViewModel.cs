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
                    LoadSuzSettings();
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
        public ICommand OpenProductGroupSelectionCommand
        {
            get;
        }
        public ICommand SelectProductGroupCommand
        {
            get;
        }
        public ICommand SaveSuzSettingsCommand { get; }
        public string LogFilePath => LogHelper.LogFilePath;

        // ctor
        public SettingsViewModel()
        {
            // команды
            SelectLogFilePathCommand = new RelayCommand(SelectLogFilePath);
            ConnectToGisMtCommand = new AsyncRelayCommand(ConnectToGisMtAsync, CanConnectToGisMt);
            OpenProductGroupSelectionCommand = new RelayCommand(OpenProductGroupSelection);
            SelectProductGroupCommand = new RelayCommand<ProductGroupDto>(SelectProductGroup);
            SaveSuzSettingsCommand = new RelayCommand(SaveSuzSettings);
            
            LoadCertificates();
            LoadProductGroups();
        }

        // --- загрузка сертификатов
        public void LoadCertificates()
        {
            Certificates.Clear();

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
                    System.Diagnostics.Debug.WriteLine($"LoadCertificates [{loc}] error: {ex.Message}");
                }
            }
        }

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

        private void SelectLogFilePath()
        {
            var dlg = new SaveFileDialog
            {
                Title = "Выберите файл для логов",
                Filter = "Log files (*.log)|*.log|All files|*.*",
                FileName = System.IO.Path.GetFileName(LogHelper.LogFilePath)
            };

            if (dlg.ShowDialog() == true)
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

        private void LoadSuzSettings()
        {
            if (SelectedCertificate == null) return;

            try
            {
                var inn = AppState.ExtractInn(SelectedCertificate.Subject);
                if (string.IsNullOrEmpty(inn)) return;

                var settings = CertificateSettingsManager.LoadSettings(inn);
                SuzOmsId = settings.Suz.OmsId ?? string.Empty;
                SuzConnectionId = settings.Suz.ConnectionId ?? string.Empty;
            }
            catch (Exception ex)
            {
                LogHelper.WriteCertificateLog(
                    AppState.ExtractInn(SelectedCertificate.Subject), 
                    "LoadSuzSettings.Error", 
                    ex.ToString()
                );
            }
        }

        private void SaveSuzSettings()
        {
            if (SelectedCertificate == null)
            {
                MessageBox.Show("Выберите сертификат для сохранения настроек.", "Внимание", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var inn = AppState.ExtractInn(SelectedCertificate.Subject);
                if (string.IsNullOrEmpty(inn))
                {
                    MessageBox.Show("Не удалось определить ИНН из сертификата.", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var settings = CertificateSettingsManager.LoadSettings(inn);
                settings.Suz.OmsId = SuzOmsId;
                settings.Suz.ConnectionId = SuzConnectionId;

                CertificateSettingsManager.SaveSettings(inn, settings);

                MessageBox.Show("Настройки С.У.З. успешно сохранены.", "Успех", 
                    MessageBoxButton.OK, MessageBoxImage.Information);

                LogHelper.WriteCertificateLog(inn, "SuzSettingsSaved", 
                    $"OMS ID: {SuzOmsId}, Connection ID: {SuzConnectionId}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении настроек С.У.З.: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                
                LogHelper.WriteCertificateLog(
                    AppState.ExtractInn(SelectedCertificate?.Subject), 
                    "SaveSuzSettings.Error", 
                    ex.ToString()
                );
            }
        }

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

                if (AppState.Instance.SelectedCertificate?.Thumbprint != SelectedCertificate.Thumbprint)
                {
                    ClearTokenCache();
                    LogHelper.WriteLog("SettingsViewModel.ConnectToGisMtAsync",
                        $"Смена сертификата: {AppState.Instance.SelectedCertificate?.Thumbprint} -> {SelectedCertificate.Thumbprint}. Кэш очищен.");
                }

                var token = await GisMtAuthService.AuthorizeGisMtAsync(SelectedCertificate);
                if (!string.IsNullOrEmpty(token))
                {
                    AppState.Instance.Token = token;
                    AppState.Instance.SelectedCertificate = SelectedCertificate;
                    AppState.Instance.CertificateOwner = SelectedCertificate.Subject;
                    AppState.Instance.CertificateOwnerPublicName = AppState.ExtractCN(SelectedCertificate.Subject);
                    AppState.Instance.NotifyTokenUpdated();

                    var inn = AppState.ExtractInn(SelectedCertificate.Subject);
                    if (!string.IsNullOrEmpty(inn))
                    {
                        CertificateSettingsManager.EnsureBaseDirectory();
                        var certSettings = CertificateSettingsManager.LoadSettings(inn);
                        var participantResponse = await ApiHelper.GetParticipantInfoAsync(inn);
                        if (participantResponse.IsSuccess && participantResponse.Data.Count > 0)
                        {
                            var participant = participantResponse.Data[0];
                            certSettings.Lk.Inn = participant.Inn;
                            certSettings.Lk.ActiveProductGroups = participant.ProductGroups;
                            certSettings.Lk.LastSync = DateTime.Now;
                            certSettings.Lk.OrganizationName = AppState.Instance.CertificateOwnerPublicName;
                            CertificateSettingsManager.SaveSettings(inn, certSettings);

                            LogHelper.WriteCertificateLog(inn, "SettingsInitialized",
                                $"Настройки созданы/обновлены. Группы: {string.Join(", ", participant.ProductGroups)}");
                        }
                        else
                        {
                            LogHelper.WriteCertificateLog(inn, "SettingsInitialized.Error",
                                $"Ошибка получения информации об участнике: {participantResponse.ErrorMessage}");
                        }

                        // Загружаем настройки С.У.З. после успешной авторизации
                        LoadSuzSettings();
                    }

                    // Остальной существующий код...
                    var enabledGroups = GetEnabledGroupsFromSettings(inn);
                    LogHelper.WriteCertificateLog(inn, "DEBUG_FinalCheck",
                        $"Финальная проверка - доступно групп: {enabledGroups.Count}\n" +
                        $"Группы: {string.Join(", ", enabledGroups.Select(pg => pg.code))}");

                    if (enabledGroups.Any())
                    {
                        await Task.Delay(300);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AppState.Instance.AvailableProductGroups = new ObservableCollection<ProductGroupDto>(enabledGroups);
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

                if (AppState.Instance.SelectedCertificate?.Thumbprint != SelectedCertificate.Thumbprint)
                {
                    ClearTokenCache();
                    LogHelper.WriteLog("SettingsViewModel.ConnectToGisMtAsync",
                        $"Смена сертификата: {AppState.Instance.SelectedCertificate?.Thumbprint} -> {SelectedCertificate.Thumbprint}. Кэш очищен.");
                }

                var token = await GisMtAuthService.AuthorizeGisMtAsync(SelectedCertificate);
                if (!string.IsNullOrEmpty(token))
                {
                    AppState.Instance.Token = token;
                    AppState.Instance.SelectedCertificate = SelectedCertificate;
                    AppState.Instance.CertificateOwner = SelectedCertificate.Subject;
                    AppState.Instance.CertificateOwnerPublicName = AppState.ExtractCN(SelectedCertificate.Subject);
                    AppState.Instance.NotifyTokenUpdated();

                    var inn = AppState.ExtractInn(SelectedCertificate.Subject);
                    if (!string.IsNullOrEmpty(inn))
                    {
                        CertificateSettingsManager.EnsureBaseDirectory();
                        var certSettings = CertificateSettingsManager.LoadSettings(inn);
                        var participantResponse = await ApiHelper.GetParticipantInfoAsync(inn);
                        if (participantResponse.IsSuccess && participantResponse.Data.Count > 0)
                        {
                            var participant = participantResponse.Data[0];
                            certSettings.Lk.Inn = participant.Inn;
                            certSettings.Lk.ActiveProductGroups = participant.ProductGroups;
                            certSettings.Lk.LastSync = DateTime.Now;
                            certSettings.Lk.OrganizationName = AppState.Instance.CertificateOwnerPublicName;
                            CertificateSettingsManager.SaveSettings(inn, certSettings);

                            LogHelper.WriteCertificateLog(inn, "SettingsInitialized",
                                $"Настройки созданы/обновлены. Группы: {string.Join(", ", participant.ProductGroups)}");
                        }
                        else
                        {
                            LogHelper.WriteCertificateLog(inn, "SettingsInitialized.Error",
                                $"Ошибка получения информации об участнике: {participantResponse.ErrorMessage}");
                        }
                    }

                    var enabledGroups = GetEnabledGroupsFromSettings(inn);
                    LogHelper.WriteCertificateLog(inn, "DEBUG_FinalCheck",
                        $"Финальная проверка - доступно групп: {enabledGroups.Count}\n" +
                        $"Группы: {string.Join(", ", enabledGroups.Select(pg => pg.code))}");

                    if (enabledGroups.Any())
                    {
                        await Task.Delay(300);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AppState.Instance.AvailableProductGroups = new ObservableCollection<ProductGroupDto>(enabledGroups);
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

        /// Получает доступные товарные группы путем сравнения кодов из файла с кодами из настроек сертификата
        private List<ProductGroupDto> GetEnabledGroupsFromSettings(string inn)
        {
            var enabledGroups = new List<ProductGroupDto>();

            if (string.IsNullOrEmpty(inn))
            {
                LogHelper.WriteCertificateLog(inn, "GetEnabledGroupsFromSettings", "ИНН пустой - возвращаем пустой список");
                return enabledGroups;
            }

            try
            {
                if (ProductGroups.Count == 0)
                {
                    LogHelper.WriteCertificateLog(inn, "GetEnabledGroupsFromSettings", "ProductGroups пустая - загружаем группы");
                    LoadProductGroups();
                }

                var settings = CertificateSettingsManager.LoadSettings(inn);
                var activeGroupCodes = settings.Lk.ActiveProductGroups ?? new List<string>();

                LogHelper.WriteCertificateLog(inn, "GetEnabledGroupsFromSettings",
                    $"Активные коды из настроек: {string.Join(", ", activeGroupCodes)}\n" +
                    $"Всего групп в ProductGroups: {ProductGroups.Count}");

                enabledGroups = ProductGroups
                    .Where(pg => activeGroupCodes.Contains(pg.code))
                    .OrderBy(pg => pg.name)
                    .ToList();

                LogHelper.WriteCertificateLog(inn, "GetEnabledGroupsFromSettings",
                    $"Найдено доступных групп: {enabledGroups.Count}\n" +
                    $"Доступные группы: {string.Join(", ", enabledGroups.Select(pg => pg.code))}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteCertificateLog(inn, "GetEnabledGroupsFromSettings.Error", ex.ToString());
            }

            return enabledGroups;
        }

        private void OpenProductGroupSelection()
        {
            var selectionWindow = new ProductGroupSelectionWindow();
            selectionWindow.DataContext = this;
            selectionWindow.Owner = Application.Current.MainWindow;
            selectionWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            selectionWindow.ShowDialog();
        }

        private void SelectProductGroup(ProductGroupDto productGroup)
        {
            if (productGroup != null)
            {
                SelectedProductGroup = productGroup;

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
                    AppState.Instance.NotifyProductGroupChanged();
                }
            }
        }

        public void LoadProductGroups()
        {
            try
            {
                var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "product_groups.json");
                if (!File.Exists(jsonPath))
                {
                    LogHelper.WriteLog("LoadProductGroups", $"Файл не найден: {jsonPath}");
                    return;
                }

                var json = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var root = JsonSerializer.Deserialize<ProductGroupRoot>(json, options);
                LogHelper.WriteLog("LoadProductGroups",
                    $"Десериализовано групп: {root?.result?.Count ?? 0}");

                ProductGroups.Clear();

                if (root?.result == null)
                {
                    LogHelper.WriteLog("LoadProductGroups", "root.result is null");
                    return;
                }

                foreach (var pg in root.result)
                {
                    pg.name = (pg.name ?? "").Replace("\r", "").Replace("\n", " ").Trim();
                    pg.startDate ??= null;
                    pg.description ??= null;
                    pg.productGroupStatus ??= "COMMERCIAL";
                    pg.tnvedDtoSet ??= Array.Empty<object>();
                    pg.farmer = false;
                    ProductGroups.Add(pg);
                }

                LogHelper.WriteLog("LoadProductGroups",
                    $"Загружено групп в коллекцию: {ProductGroups.Count}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("LoadProductGroups.Error", $"Ошибка: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void ClearTokenCache()
        {
            try
            {
                string oldCachePath = System.IO.Path.Combine(AppContext.BaseDirectory, "Token.txt");
                if (System.IO.File.Exists(oldCachePath))
                {
                    System.IO.File.Delete(oldCachePath);
                    LogHelper.WriteLog("SettingsViewModel.ClearTokenCache", "Удален старый файл Token.txt");
                }

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

        // DTO для product_groups.json
        private class ProductGroupRoot
        {
            public List<ProductGroupDto> result { get; set; } = new();
        }
    }
}