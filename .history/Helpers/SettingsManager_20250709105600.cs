using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace markapp.Helpers
{
    public class AppSettings
    {
        public string LogFilePath { get; set; } = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Resources", "Tools", "TokenLog.txt");

        public string InsertsFilePath { get; set; } = string.Empty;
    }

    public static class SettingsManager
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "markapp", "user.config");

        private static AppSettings _settings;

        public static AppSettings Settings => _settings ??= Load();

        public static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения user.config: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки user.config: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return new AppSettings(); // значение по умолчанию
        }
    }
}
