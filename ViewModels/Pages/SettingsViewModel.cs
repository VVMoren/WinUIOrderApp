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
using WinUIOrderApp.Helpers;
using System.IO;
using WinUIOrderApp.Models;
using WinUIOrderApp.Services;

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

    private const string CryptcpPath = @"H:\Документы\GitHub\WinUIOrderApp\Resources\Tools\cryptcp.win32.exe";


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
