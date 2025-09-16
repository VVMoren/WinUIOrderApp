using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using markapp.Helpers;
using System.IO;
using markapp.Models;
using markapp.Services;

public partial class SettingsViewModel : ObservableObject
{
    // Коллекция доступных сертификатов с приватным ключом
    public ObservableCollection<X509Certificate2> Certificates { get; } = new();

    // Выбранный сертификат
    private X509Certificate2? _selectedCertificate;
    public X509Certificate2? SelectedCertificate
    {
        get => _selectedCertificate;
        set
        {
            // При изменении сертификата уведомляем привязки и команду
            if (SetProperty(ref _selectedCertificate, value))
            {
                // обновляем состояние CanExecute для команды
                ConnectToGisMtCommand.NotifyCanExecuteChanged();
            }
        }
    }

    // Путь к текущему файлу журнала
    public string LogFilePath => LogHelper.LogFilePath;

    // Команда выбора пути для файла журнала
    public ICommand SelectLogFilePathCommand { get; }

    // Команда подключения к ГИС МТ (будет активна только при выбранном сертификате)
    public IAsyncRelayCommand ConnectToGisMtCommand { get; }

    // Текущий токен авторизации
    public string? AuthToken
    {
        get => AppState.Instance.Token;
        private set => AppState.Instance.Token = value;
    }

    private const string CryptcpPath = @"H:\Документы\GitHub\markapp\Resources\Tools\cryptcp.win32.exe";

    public SettingsViewModel()
    {
        // Команда выбора файла журнала
        SelectLogFilePathCommand = new RelayCommand(() =>
        {
            LogHelper.SelectLogFilePath();
            OnPropertyChanged(nameof(LogFilePath));
        });

        // Команда подключения: выполняет метод ConnectToGisMtAsync, активна только если выбран сертификат
        ConnectToGisMtCommand = new AsyncRelayCommand(
            ConnectToGisMtAsync,
            () => SelectedCertificate != null);

        // Загружаем список сертификатов и список товарных групп
        LoadCertificates();
        LoadProductGroups();
    }

    /// <summary>
    /// Загружает все доступные сертификаты с закрытым ключом из хранилища пользователя.
    /// </summary>
    private void LoadCertificates()
    {
        Certificates.Clear();
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        foreach (var cert in store.Certificates)
        {
            if (cert.HasPrivateKey)
                Certificates.Add(cert);
        }
    }

    /// <summary>
    /// Выполняет авторизацию в ГИС МТ, сохраняет токен и обновляет список доступных товарных групп.
    /// </summary>
    private async Task ConnectToGisMtAsync()
    {
        try
        {
            var token = await GisMtAuthService.AuthorizeGisMtAsync(SelectedCertificate);
            AuthToken = token;
            await LoadUserProfileAndFilterProductGroups();
            AppState.Instance.NotifyTokenUpdated();
            MessageBox.Show("Успешно получен токен!", "ГИС МТ",
                            MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            // Пользователь отказался от выбора сертификата или закрыл диалог
        }
        catch (Exception ex)
        {
            LogHelper.WriteLog("Ошибка авторизации ГИС МТ", ex.ToString());
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Загружает профиль участника из ГИС МТ и отмечает доступные товарные группы.
    /// </summary>
    public async Task LoadUserProfileAndFilterProductGroups()
    {
        if (string.IsNullOrWhiteSpace(AuthToken))
            return;

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthToken);

            var response = await http.GetAsync("https://markirovka.crpt.ru/api/v3/facade/profile/company2");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var profile = JsonSerializer.Deserialize<CompanyProfileDto>(json);

            // Множество доступных кодов для фильтрации групп
            var allowedCodes = profile?.productGroupsAndRoles.Select(p => p.code).ToHashSet()
                ?? new HashSet<string>();

            // Обновляем свойства IsEnabled у групп
            foreach (var pg in ProductGroups)
            {
                pg.IsEnabled = allowedCodes.Contains(pg.code);
            }

            // Сортируем: сначала доступные, затем заблокированные
            var sorted = ProductGroups
                .OrderByDescending(p => p.IsEnabled)
                .ThenBy(p => p.name)
                .ToList();
            ProductGroups.Clear();
            foreach (var item in sorted)
                ProductGroups.Add(item);
        }
        catch (Exception ex)
        {
            LogHelper.WriteLog("LoadUserProfile", ex.ToString());
            MessageBox.Show("Ошибка получения профиля участника.", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Коллекция доступных товарных групп
    public ObservableCollection<ProductGroupDto> ProductGroups { get; } = new();

    private ProductGroupDto? _selectedProductGroup;
    public ProductGroupDto? SelectedProductGroup
    {
        get => _selectedProductGroup;
        set
        {
            SetProperty(ref _selectedProductGroup, value);
            if (value != null)
            {
                AppState.Instance.SelectedProductGroupCode = value.code;
                AppState.Instance.SelectedProductGroupName = value.name;
            }
        }
    }

    /// <summary>
    /// Загружает список товарных групп из локального JSON.
    /// </summary>
    public void LoadProductGroups()
    {
        var json = File.ReadAllText("Resources/product_groups.json");
        var root = JsonSerializer.Deserialize<ProductGroupRoot>(json);
        ProductGroups.Clear();
        foreach (var pg in root?.result ?? Enumerable.Empty<ProductGroupDto>())
        {
            ProductGroups.Add(pg);
        }
    }

    // Вспомогательные типы для десериализации профиля и групп
    private class ProductGroupRoot
    {
        public List<ProductGroupDto> result { get; set; } = new();
    }

    public class CompanyProfileDto
    {
        public List<ProductGroupRole> productGroupsAndRoles { get; set; } = new();

        public class ProductGroupRole
        {
            public string code { get; set; } = string.Empty;
        }
    }

    private record AuthKeyResponse(string uuid, string data);
    private record TokenResponse(string token);
}
