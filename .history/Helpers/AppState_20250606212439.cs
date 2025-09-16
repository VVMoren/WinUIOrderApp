using System;
using System.IO;
using System.Text.Json;
using System.Security.Cryptography.X509Certificates;
using CommunityToolkit.Mvvm.ComponentModel;

namespace markapp.Helpers
{
    public sealed class AppState : ObservableObject
    {
        private static AppState? _instance;
        public static AppState Instance => _instance ??= new AppState();

        // 🔹 Токен авторизации
        private string? _token;
        public string? Token
        {
            get => _token;
            set
            {
                _token = value;
                OnPropertyChanged(nameof(Token));
            }
        }

        // 🔹 Сертификат: CN=...
        private string? _certificateOwner;
        public string? CertificateOwner
        {
            get => _certificateOwner;
            set
            {
                _certificateOwner = value;
                OnPropertyChanged(nameof(CertificateOwner));
            }
        }

        // 🔹 Сертификат: ФИО в верхней панели
        private string? _certificateOwnerPublicName;
        public string? CertificateOwnerPublicName
        {
            get => _certificateOwnerPublicName;
            set
            {
                _certificateOwnerPublicName = value;
                OnPropertyChanged(nameof(CertificateOwnerPublicName));
            }
        }

        // 🔹 Код выбранной товарной группы (например: milk, shoes)
        private string? _selectedProductGroupCode;
        public string? SelectedProductGroupCode
        {
            get => _selectedProductGroupCode;
            set
            {
                _selectedProductGroupCode = value;
                OnPropertyChanged(nameof(SelectedProductGroupCode));
            }
        }

        // 🔹 Название выбранной товарной группы (например: Молочная продукция)
        private string? _selectedProductGroupName;
        public string? SelectedProductGroupName
        {
            get => _selectedProductGroupName;
            set
            {
                _selectedProductGroupName = value;
                OnPropertyChanged(nameof(SelectedProductGroupName));
            }
        }

        private X509Certificate2? _selectedCertificate;
        public X509Certificate2? SelectedCertificate
        {
            get => _selectedCertificate;
            set
            {
                _selectedCertificate = value;
                OnPropertyChanged(nameof(SelectedCertificate));
            }
        }


        // 🔹 Событие для подписки
        public event Action? TokenUpdated;
        public void NotifyTokenUpdated() => TokenUpdated?.Invoke();

        private static readonly string SettingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");

        public void SaveSettings()
        {
            var settings = new AppUserSettings
            {
                CertificateThumbprint = SelectedCertificate?.Thumbprint,
                ProductGroupCode = SelectedProductGroupCode
            };

            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings));
        }

        public void LoadSettings()
        {
            if (!File.Exists(SettingsPath)) return;

            var settings = JsonSerializer.Deserialize<AppUserSettings>(File.ReadAllText(SettingsPath));
            if (settings is null) return;

            SelectedProductGroupCode = settings.ProductGroupCode;

            if (!string.IsNullOrEmpty(settings.CertificateThumbprint))
            {
                var cert = FindCertificateByThumbprint(settings.CertificateThumbprint);
                if (cert != null)
                    SelectedCertificate = cert;
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

        public class AppUserSettings
        {
            public string? CertificateThumbprint { get; set; }
            public string? ProductGroupCode { get; set; }
        }


    }
}
