using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WinUIOrderApp.Models
{
    public class CertificateSettings
    {
        public LkSettings Lk { get; set; } = new LkSettings();
        public SuzSettings Suz { get; set; } = new SuzSettings();
        public NkSettings Nk { get; set; } = new NkSettings();
        public AdvancedSettings Advanced { get; set; } = new AdvancedSettings();
    }

    public class LkSettings
    {
        public List<string> ActiveProductGroups { get; set; } = new List<string>();
        public DateTime LastSync { get; set; } = DateTime.Now;
        public string OrganizationName { get; set; } = string.Empty;
        public string Inn { get; set; } = string.Empty;
        public string Ogrn { get; set; } = string.Empty;
    }

    public class SuzSettings
    {
        public bool AutoOrderProcessing { get; set; } = true;
        public int DefaultWarehouseId { get; set; } = 0;
        public string NotificationEmail { get; set; } = string.Empty;

        // Новые параметры
        public string OmsId { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
    }

    public class NkSettings
    {
        public bool AutoPrint { get; set; } = false;
        public string DefaultTransportType { get; set; } = "AUTO";
        public bool RequireSignatures { get; set; } = true;
        public string NkApiKey { get; set; } = string.Empty;
    }

    public class AdvancedSettings
    {
        public bool DebugMode { get; set; } = false;
        public int RequestTimeout { get; set; } = 30;
        public bool EnableTelemetry { get; set; } = true;
        public string CustomApiUrl { get; set; } = string.Empty;
        public bool EnableCryptoTailSearch { get; set; } = false;
        public string CryptoTailFolderPath { get; set; } = string.Empty;
        public string ProductCacheFileName { get; set; } = string.Empty;
        public DateTime? ProductCacheUpdatedAt { get; set; }

    }
}