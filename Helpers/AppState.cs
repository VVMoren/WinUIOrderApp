using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Security.Cryptography.X509Certificates;
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

                    NotifyCertificateSettingsChanged();
                }
            }
        }

        // === 🔹 Настройки сертификатов ===

        private readonly Dictionary<string, CertificatePreferences> _certificatePreferences = new();
        public IReadOnlyDictionary<string, CertificatePreferences> CertificatePreferences => _certificatePreferences;

        public CertificatePreferences GetOrCreatePreferences(string thumbprint)
        {
            if (!_certificatePreferences.TryGetValue(thumbprint, out var prefs))
            {
                prefs = new CertificatePreferences();
                _certificatePreferences[thumbprint] = prefs;
            }

            return prefs;
        }

        public CertificatePreferences? GetCurrentCertificatePreferences()
        {
            var thumbprint = SelectedCertificate?.Thumbprint;
            if (string.IsNullOrEmpty(thumbprint))
                return null;
            return GetOrCreatePreferences(thumbprint);
        }

        public void UpdateCertificatePreferences(string thumbprint, CertificatePreferences preferences)
        {
            _certificatePreferences[thumbprint] = preferences;
            NotifyCertificateSettingsChanged();
            SaveSettings();
        }

        public event Action? CertificateSettingsChanged;
        public void NotifyCertificateSettingsChanged() => CertificateSettingsChanged?.Invoke();

        // === 🔹 КМ и кешированные товары ===

        public List<CisItem> LastKmResults { get; private set; } = new();
        public event Action? KmDataUpdated;
        public void UpdateKmResults(IEnumerable<CisItem> items)
        {
            LastKmResults = items?.ToList() ?? new List<CisItem>();
            KmDataUpdated?.Invoke();
        }

        public Dictionary<string, CachedGood> CachedGoods { get; private set; } = new();
        public event Action? ProductCacheUpdated;
        public void UpdateProductCache(IEnumerable<CachedGood> goods)
        {
            CachedGoods = goods?
                .Where(g => !string.IsNullOrEmpty(g.Gtin))
                .GroupBy(g => g.Gtin!)
                .ToDictionary(g => g.Key, g => g.First())
                ?? new Dictionary<string, CachedGood>();

            ProductCacheUpdated?.Invoke();
        }

        // === 🔹 Товарная группа ===

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
                    ProductGroupCode = SelectedProductGroupCode,
                    Certificates = _certificatePreferences
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

                _certificatePreferences.Clear();
                if (settings.Certificates != null)
                {
                    foreach (var pair in settings.Certificates)
                    {
                        _certificatePreferences[pair.Key] = pair.Value ?? new CertificatePreferences();
                    }
                }

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
            public Dictionary<string, CertificatePreferences?>? Certificates
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

        // === 🔹 Папки сертификатов ===

        private static string GetCertificatesRoot()
        {
            var root = Path.Combine(AppContext.BaseDirectory, "certificates");
            if (!Directory.Exists(root))
                Directory.CreateDirectory(root);
            return root;
        }

        private static string? GetCertificateThumbprintFolder(string? thumbprint, bool ensure)
        {
            if (string.IsNullOrWhiteSpace(thumbprint))
                return null;

            var folder = Path.Combine(GetCertificatesRoot(), thumbprint);
            if (ensure && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return folder;
        }

        public string? GetCurrentCertificateFolder(bool ensure = true) =>
            GetCertificateThumbprintFolder(SelectedCertificate?.Thumbprint, ensure);

        public string? GetCurrentCertificateDataFolder(bool ensure = true)
        {
            var root = GetCurrentCertificateFolder(ensure);
            if (root == null)
                return null;
            var dataFolder = Path.Combine(root, "данные");
            if (ensure && !Directory.Exists(dataFolder))
                Directory.CreateDirectory(dataFolder);
            return dataFolder;
        }

        public string? GetCurrentCertificateKmFolder(bool ensure = true)
        {
            var root = GetCurrentCertificateFolder(ensure);
            if (root == null)
                return null;
            var kmFolder = Path.Combine(root, "km");
            if (ensure && !Directory.Exists(kmFolder))
                Directory.CreateDirectory(kmFolder);
            return kmFolder;
        }
    }
}
