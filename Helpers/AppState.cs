using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using WinUIOrderApp.Models;



namespace WinUIOrderApp.Helpers
{
    /// <summary>
    /// Глобальное состояние приложения:
    /// - хранит выбранный сертификат, токен, настройки и сведения об организации
    /// - обеспечивает сохранение и загрузку настроек
    /// - реализует уведомления об изменениях (ObservableObject)
    /// </summary>
    public sealed class AppState : ObservableObject
    {
        private static AppState? _instance;
        public static AppState Instance => _instance ??= new AppState();

        // === 🔹 Авторизация ===

        private string? _token;
        public string? Token
        {
            get => _token;
            set => SetProperty(ref _token, value);
        }

        public static string ExtractInn(string subject)
        {
            if (string.IsNullOrEmpty(subject)) return string.Empty;

            try
            {
                var innMatch = System.Text.RegularExpressions.Regex.Match(subject, @"ИНН=(\d+)");
                return innMatch.Success ? innMatch.Groups[1].Value : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }


        // === 🔹 Сертификат ===

        private string? _certificateOwner;
        public string? CertificateOwner
        {
            get => _certificateOwner;
            set => SetProperty(ref _certificateOwner, value);
        }

        private string? _certificateOwnerPublicName;
        public string? CertificateOwnerPublicName
        {
            get => _certificateOwnerPublicName;
            set => SetProperty(ref _certificateOwnerPublicName, value);
        }

        private X509Certificate2? _selectedCertificate;
        public X509Certificate2? SelectedCertificate
        {
            get => _selectedCertificate;
            set
            {
                if (SetProperty(ref _selectedCertificate, value))
                {
                    // Автоматически обновляем отображаемое имя (CN=...)
                    if (value != null)
                        CertificateOwnerPublicName = ExtractCN(value.Subject);
                }
            }
        }

        public static string ExtractInn(X509Certificate2 certificate)
        {
            if (certificate == null || string.IsNullOrEmpty(certificate.Subject))
                return string.Empty;

            try
            {
                var innMatch = System.Text.RegularExpressions.Regex.Match(certificate.Subject, @"ИНН=(\d+)");
                return innMatch.Success ? innMatch.Groups[1].Value : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        // === 🔹 Товарная группа ===

        private ObservableCollection<ProductGroupDto> _availableProductGroups = new();
        public ObservableCollection<ProductGroupDto> AvailableProductGroups
        {
            get => _availableProductGroups;
            set => SetProperty(ref _availableProductGroups, value);
        }

        private string? _selectedProductGroupCode;
        public string? SelectedProductGroupCode
        {
            get => _selectedProductGroupCode;
            set => SetProperty(ref _selectedProductGroupCode, value);
        }

        private string? _selectedProductGroupName;
        public string? SelectedProductGroupName
        {
            get => _selectedProductGroupName;
            set => SetProperty(ref _selectedProductGroupName, value);
        }

        // === 🔹 События ===

        public event Action? TokenUpdated;
        public void NotifyTokenUpdated() => TokenUpdated?.Invoke();

        public event Action? OnProductGroupChanged;
        public void NotifyProductGroupChanged() => OnProductGroupChanged?.Invoke();

        // === 🔹 Путь настроек ===

        private static readonly string SettingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");

        // === 🔹 Сохранение и загрузка настроек ===

        public void SaveSettings()
        {
            try
            {
                var settings = new AppUserSettings
                {
                    CertificateThumbprint = SelectedCertificate?.Thumbprint,
                    ProductGroupCode = SelectedProductGroupCode
                };

                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AppState] Ошибка сохранения настроек: {ex.Message}");
            }
        }

        public void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return;

                var settings = JsonSerializer.Deserialize<AppUserSettings>(File.ReadAllText(SettingsPath));
                if (settings is null)
                    return;

                SelectedProductGroupCode = settings.ProductGroupCode;

                if (!string.IsNullOrEmpty(settings.CertificateThumbprint))
                {
                    var cert = FindCertificateByThumbprint(settings.CertificateThumbprint);
                    if (cert != null)
                        SelectedCertificate = cert;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AppState] Ошибка загрузки настроек: {ex.Message}");
            }
        }

        private X509Certificate2? FindCertificateByThumbprint(string thumbprint)
        {
            using var store = new X509Store(StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            return store.Certificates
                .Find(X509FindType.FindByThumbprint, thumbprint, false)
                .Cast<X509Certificate2?>()
                .FirstOrDefault();
        }

        // === 🔹 Кэш организации ===

        public string OrganisationName { get; set; } = string.Empty;
        public string OrganisationInn { get; set; } = string.Empty;
        public string OrganisationOgrn { get; set; } = string.Empty;
        public DateTime OrganisationFetchedAt
        {
            get; set;
        }

        public bool HasValidOrganisationCache()
        {
            return !string.IsNullOrEmpty(OrganisationName)
                   && OrganisationFetchedAt > DateTime.Now.AddHours(-12);
        }

        // === 🔹 Вспомогательные типы ===

        public class AppUserSettings
        {
            public string? CertificateThumbprint
            {
                get; set;
            }
            public string? ProductGroupCode
            {
                get; set;
            }
        }

        // === 🔹 Утилита: извлечение CN из Subject сертификата ===

        public static string ExtractCN(string subject)
        {
            if (string.IsNullOrWhiteSpace(subject))
                return string.Empty;

            int start = subject.IndexOf("CN=", StringComparison.OrdinalIgnoreCase);
            if (start == -1)
                return subject;

            start += 3;
            int end = subject.IndexOf(',', start);
            if (end == -1)
                end = subject.Length;

            return subject.Substring(start, end - start).Trim();
        }
    }
}
