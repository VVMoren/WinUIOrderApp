using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinUIOrderApp.Models;
using WinUIOrderApp.Helpers;

namespace WinUIOrderApp.ViewModels.Pages
{
    public partial class DataViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<RequestedCisItem> _requestedCisList = new();

        private const string ApiUrl = "https://markirovka.crpt.ru/api/v3/true-api/cises/info";
        private string LogFilePath => LogHelper.LogFilePath;

        [RelayCommand]
        public async Task LoadFromFileAsync(string filePath)
        {
            LogHelper.WriteLog("LoadFromFileAsync", $"Начало загрузки из файла: {filePath}");

            if (!File.Exists(filePath))
            {
                LogHelper.WriteLog("LoadFromFileAsync", $"Файл не найден: {filePath}");
                MessageBox.Show("Файл не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                LogHelper.WriteLog("LoadFromFileAsync", "Чтение файла");
                var lines = await File.ReadAllLinesAsync(filePath);
                var cisList = lines
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Distinct()
                    .ToList();

                LogHelper.WriteLog("LoadFromFileAsync", $"Прочитано строк: {lines.Length}, уникальных CIS: {cisList.Count}");

                RequestedCisList.Clear();
                foreach (var cis in cisList)
                {
                    RequestedCisList.Add(new RequestedCisItem
                    {
                        RequestedCis = cis,
                        ProductName = "N/D",
                        Status = "N/D"
                    });
                }

                if (RequestedCisList.Count == 0)
                {
                    LogHelper.WriteLog("LoadFromFileAsync", "Файл пуст или не содержит корректных кодов");
                    MessageBox.Show("Файл пуст или не содержит корректных кодов!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                LogHelper.WriteLog("LoadFromFileAsync", $"Загружено {RequestedCisList.Count} CIS кодов");
                await FetchCisInfoBatchedAsync();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("LoadFromFileAsync", $"Ошибка загрузки файла: {ex.Message}");
                MessageBox.Show($"Ошибка при чтении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task FetchCisInfoBatchedAsync()
        {
            LogHelper.WriteLog("FetchCisInfoBatchedAsync", "Начало получения информации о CIS");

            string token = AppState.Instance.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                LogHelper.WriteLog("FetchCisInfoBatchedAsync", "Токен не получен");
                MessageBox.Show("Токен не получен. Сначала подключитесь к ГИС МТ.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            LogHelper.WriteLog("FetchCisInfoBatchedAsync", "HTTP-клиент создан с авторизацией");

            int batchSize = 1000;
            int totalBatches = (int)Math.Ceiling((double)RequestedCisList.Count / batchSize);

            LogHelper.WriteLog("FetchCisInfoBatchedAsync", $"Всего CIS: {RequestedCisList.Count}, батчей: {totalBatches}, размер батча: {batchSize}");

            var responseList = new List<ApiResponse>();

            for (int i = 0; i < totalBatches; i++)
            {
                LogHelper.WriteLog("FetchCisInfoBatchedAsync", $"Обработка батча {i + 1}/{totalBatches}");

                var batch = RequestedCisList
                    .Skip(i * batchSize)
                    .Take(batchSize)
                    .Select(item => item.RequestedCis?.Trim() ?? "")
                    .Select(cis => cis.Length >= 25 ? cis.Substring(0, 25) : cis)
                    .Where(cis => !string.IsNullOrEmpty(cis) && cis.Length == 25)
                    .Select(cis => $"\"{cis.Replace("\"", "\\\"")}\"")
                    .ToList();

                LogHelper.WriteLog("FetchCisInfoBatchedAsync", $"Батч {i + 1}: {batch.Count} валидных CIS кодов");

                if (batch.Count == 0)
                {
                    LogHelper.WriteLog("FetchCisInfoBatchedAsync", $"Батч {i + 1} пуст - пропускаем");
                    continue;
                }

                string requestBody = "[\n    " + string.Join(",\n    ", batch) + "\n]";
                try
                {
                    LogHelper.WriteLog("FetchCisInfoBatchedAsync", $"Отправка запроса для батча {i + 1}");
                    var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(ApiUrl, content);

                    LogHelper.WriteLog("FetchCisInfoBatchedAsync", $"Ответ получен для батча {i + 1}, статус: {response.StatusCode}");

                    string responseData = await response.Content.ReadAsStringAsync();

                    LogToFile("=== API REQUEST ===");
                    LogToFile(requestBody);
                    LogToFile("=== API RESPONSE ===");
                    LogToFile(responseData);

                    if (response.IsSuccessStatusCode)
                    {
                        LogHelper.WriteLog("FetchCisInfoBatchedAsync", $"Успешный ответ для батча {i + 1}");
                        var parsed = JsonConvert.DeserializeObject<List<ApiResponse>>(responseData);
                        if (parsed != null)
                        {
                            responseList.AddRange(parsed);
                            LogHelper.WriteLog("FetchCisInfoBatchedAsync", $"Добавлено {parsed.Count} ответов из батча {i + 1}");
                        }
                    }
                    else
                    {
                        LogHelper.WriteLog("FetchCisInfoBatchedAsync", $"Ошибка HTTP для батча {i + 1}: {response.StatusCode}");
                        LogToFile($"HTTP Error: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLog("FetchCisInfoBatchedAsync", $"Исключение при обработке батча {i + 1}: {ex.Message}");
                    LogToFile("=== EXCEPTION ===");
                    LogToFile(ex.ToString());
                    MessageBox.Show($"Ошибка запроса: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                // Небольшая пауза между батчами чтобы не перегружать API
                await Task.Delay(100);
            }

            LogHelper.WriteLog("FetchCisInfoBatchedAsync", $"Все батчи обработаны. Получено ответов: {responseList.Count}");
            UpdateTable(responseList);
        }

        private void UpdateTable(List<ApiResponse> responseList)
        {
            LogHelper.WriteLog("UpdateTable", $"Начало обновления таблицы с {responseList.Count} ответами");

            int updatedCount = 0;
            int notFoundCount = 0;

            foreach (var response in responseList)
            {
                var item = RequestedCisList.FirstOrDefault(c =>
                    c.RequestedCis?.StartsWith(response.CisInfo.RequestedCis) == true);

                if (item != null)
                {
                    item.ProductName = response.CisInfo.ProductName;
                    item.Status = GetStatusDescription(response.CisInfo.Status);
                    item.OwnerName = response.CisInfo.OwnerName;
                    updatedCount++;

                    LogHelper.WriteLog("UpdateTable", $"Обновлен CIS: {response.CisInfo.RequestedCis}, статус: {item.Status}");
                }
                else
                {
                    notFoundCount++;
                    LogHelper.WriteLog("UpdateTable", $"CIS не найден в исходном списке: {response.CisInfo.RequestedCis}");
                }
            }

            OnPropertyChanged(nameof(RequestedCisList));
            LogHelper.WriteLog("UpdateTable", $"Обновление завершено. Обновлено: {updatedCount}, не найдено: {notFoundCount}");
        }

        private string GetStatusDescription(string status)
        {
            var result = status switch
            {
                "EMITTED" => "Эмитирован",
                "APPLIED" => "Нанесён",
                "INTRODUCED" => "В обороте",
                "WRITTEN_OFF" => "Списан",
                "WITHDRAWN" => "Выбыл",
                _ => "Неизвестно"
            };

            LogHelper.WriteLog("GetStatusDescription", $"Преобразование статуса: {status} -> {result}");
            return result;
        }

        private void LogToFile(string message)
        {
            try
            {
                File.AppendAllText(LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
                LogHelper.WriteLog("LogToFile", $"Запись в файл лога: {message.Substring(0, Math.Min(50, message.Length))}...");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("LogToFile", $"Ошибка записи в файл лога: {ex.Message}");
                // Игнорируем ошибки логирования
            }
        }

        public bool HasSelectedAppliedItems =>
            RequestedCisList.Any(item => item.IsSelected && item.Status == "Нанесён");

        public class ApiResponse
        {
            public CisInfo CisInfo { get; set; }
        }
    }
}