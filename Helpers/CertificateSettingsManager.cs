using System;
using System.IO;
using System.Text.Json;
using WinUIOrderApp.Models;

namespace WinUIOrderApp.Helpers
{
    public static class CertificateSettingsManager
    {
        // Делаем BaseSettingsDir публичным
        public static string BaseSettingsDir
        {
            get;
        } = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "SettingApp");

        public static string GetCertificateSettingsPath(string inn)
        {
            var certDir = Path.Combine(BaseSettingsDir, inn);
            Directory.CreateDirectory(certDir);
            return Path.Combine(certDir, "Settings.json");
        }

        public static string GetCertificateLogPath(string inn)
        {
            var certDir = Path.Combine(BaseSettingsDir, inn);
            Directory.CreateDirectory(certDir);
            return Path.Combine(certDir, "certificate.log");
        }

        public static CertificateSettings LoadSettings(string inn)
        {
            try
            {
                var settingsPath = GetCertificateSettingsPath(inn);
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    return JsonSerializer.Deserialize<CertificateSettings>(json) ?? new CertificateSettings();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog($"CertificateSettingsManager.LoadSettings({inn})", $"Ошибка: {ex.Message}");
            }
            return new CertificateSettings();
        }

        public static void SaveSettings(string inn, CertificateSettings settings)
        {
            try
            {
                var settingsPath = GetCertificateSettingsPath(inn);
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(settingsPath, json);

                LogHelper.WriteLog($"CertificateSettingsManager.SaveSettings({inn})", "Настройки сохранены");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog($"CertificateSettingsManager.SaveSettings({inn})", $"Ошибка: {ex.Message}");
            }
        }

        public static bool SettingsExist(string inn)
        {
            var settingsPath = GetCertificateSettingsPath(inn);
            return File.Exists(settingsPath);
        }

        public static void EnsureBaseDirectory()
        {
            try
            {
                if (!Directory.Exists(BaseSettingsDir))
                {
                    Directory.CreateDirectory(BaseSettingsDir);
                    LogHelper.WriteLog("CertificateSettingsManager", $"Создана базовая папка: {BaseSettingsDir}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("CertificateSettingsManager.EnsureBaseDirectory", $"Ошибка: {ex.Message}");
            }
        }
    }
}