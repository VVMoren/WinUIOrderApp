using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Win32;

namespace markapp.Helpers
{
    public static class LogHelper
    {
        public static string LogFilePath
        {
            get => SettingsManager.Settings.LogFilePath;
            set
            {
                SettingsManager.Settings.LogFilePath = value;
                SettingsManager.Save();
            }
        }

        public static void WriteLog(string title, string content)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath));

                if (title == "CisHelper.GetShortCis")
                {
                    var matches = Regex.Matches(content, @"Вход: \[(.*?)\] → До GS: \[(.*?)\] → Экранировано: \[(.*?)\], длина: \d+");
                    int total = matches.Count;
                    int quoted = matches.Count(m => m.Groups[3].Value.Contains("\\\""));

                    content = $"✅ Получено: {total}  → До GS: {total} → Экранировано: {quoted}";
                }

                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}:\n{content}\n\n";
                File.AppendAllText(LogFilePath, logEntry);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка записи в лог-файл: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void SelectLogFilePath()
        {
            SaveFileDialog dlg = new SaveFileDialog
            {
                Title = "Выберите путь для лог-файла",
                Filter = "Текстовые файлы (*.txt)|*.txt",
                FileName = "TokenLog.txt"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dlg.FileName));
                    LogFilePath = dlg.FileName; // CHANGED: сохраняем в конфиг
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось создать папку: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
