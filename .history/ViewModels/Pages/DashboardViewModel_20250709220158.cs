using System;
using System.IO;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows;
using markapp.Helpers;
using markapp.Services;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using markapp.Services;



namespace markapp.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _counter = 0;

        [ObservableProperty]
        private string _insertsFilePath = SettingsManager.Settings.InsertsFilePath;

        [ObservableProperty]
        private string _setsFilePath;

        [ObservableProperty]
        private int _matchProgress;

        [ObservableProperty]
        private string _matchStatus;

        private readonly string _productsDictPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "AGG", "products_dict.json");

        public bool IsStartEnabled => File.Exists(SetsFilePath) && File.Exists(InsertsFilePath);

        [ObservableProperty]
        private string _csrfToken;

        [ObservableProperty]
        private Dictionary<string, string> _sessionCookies;

        partial void OnInsertsFilePathChanged(string value)
        {
            SettingsManager.Settings.InsertsFilePath = value;
            SettingsManager.Save();
            OnPropertyChanged(nameof(IsStartEnabled));
        }

        partial void OnSetsFilePathChanged(string value)
        {
            OnPropertyChanged(nameof(IsStartEnabled));
        }

        [RelayCommand]
        private void SelectInsertsFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt",
                Title = "Выберите файл с вложениями"
            };

            if (dialog.ShowDialog() == true)
                InsertsFilePath = dialog.FileName;
        }

        [RelayCommand]
        private void SelectSetsFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt",
                Title = "Выберите файл с наборами"
            };

            if (dialog.ShowDialog() == true)
                SetsFilePath = dialog.FileName;
        }

        [RelayCommand]
        private void StartMatching()
        {
            if (!File.Exists(SetsFilePath))
            {
                MessageBox.Show("Файл наборов не выбран.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(InsertsFilePath))
            {
                MessageBox.Show("Файл вложений не выбран.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!MatcherService.HasEnoughInserts(SetsFilePath, InsertsFilePath))
            {
                MessageBox.Show("Недостаточно вложений для сопоставления.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string baseDir = Path.GetDirectoryName(SetsFilePath);
                string folderName = $"{DateTime.Now:yyyyMMdd_HHmm}_{File.ReadLines(SetsFilePath).Count()}";
                string outputDir = Path.Combine(baseDir, folderName);

                MatchProgress = 0;
                MatchStatus = "Сопоставление...";

                MatcherService.MatchSetsAndInserts(SetsFilePath, InsertsFilePath, outputDir,
                    new Progress<int>(p => MatchProgress = p));

                MatchStatus = $"Готово: {outputDir}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сопоставлении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task CreateProductsDictAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt",
                Title = "Выберите файл групп"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var lines = await File.ReadAllLinesAsync(dialog.FileName);
                var productMap = new Dictionary<string, (string GoodId, string SetGTIN)>();

                foreach (var line in lines)
                {
                    var parts = line.Split('|');
                    if (parts.Length != 2) continue;

                    var setRaw = parts[0].Trim();
                    var insertRaw = parts[1].Trim();

                    string setGtin = ExtractGtin(setRaw);
                    string insertGtin = ExtractInsertGtin(insertRaw);

                    if (setGtin is null || insertGtin is null || productMap.ContainsKey(setGtin))
                        continue;

                    string goodId = await FetchGoodIdAsync(setGtin);
                    if (string.IsNullOrEmpty(goodId))
                        continue;

                    productMap[setGtin] = (goodId, insertGtin);
                }

                var jsonMap = productMap.ToDictionary(
                    p => p.Key,
                    p => new { good_id = int.Parse(p.Value.GoodId), setGTIN = p.Value.SetGTIN });

                Directory.CreateDirectory(Path.GetDirectoryName(_productsDictPath));
                File.WriteAllText(_productsDictPath, System.Text.Json.JsonSerializer.Serialize(jsonMap, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                LogHelper.WriteLog("Привязка GTIN", $"✅ Словарь создан, товаров: {jsonMap.Count}");
                MessageBox.Show($"Создан словарь товаров: {jsonMap.Count} записей", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("Привязка GTIN", $"❌ Ошибка: {ex.Message}");
                MessageBox.Show($"Ошибка при создании словаря: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string ExtractGtin(string input)
        {
            var match = Regex.Match(input, @"01(\d{14})21");
            return match.Success ? match.Groups[1].Value.Substring(1) : null; // без ведущей 0
        }

        private static string ExtractInsertGtin(string input)
        {
            var match = Regex.Match(input, @"(04\d{12})");
            return match.Success ? match.Groups[1].Value : null;
        }

        private async Task<string> FetchGoodIdAsync(string gtin)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AppState.Instance.Token);
                var response = await client.GetAsync($"https://markirovka.crpt.ru/api/v3/true-api/nk/product?gtin={gtin}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("good_id", out var idProp))
                    return idProp.GetInt32().ToString();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("FetchGoodIdAsync", $"GTIN: {gtin}, Ошибка: {ex.Message}");
            }

            return null;
        }

        [RelayCommand]
        private async Task LinkSetGtin()
        {
            if (!File.Exists(_productsDictPath))
            {
                MessageBox.Show("Файл словаря товаров не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var json = File.ReadAllText(_productsDictPath);
                var dict = JsonConvert.DeserializeObject<Dictionary<string, ProductInfo>>(json);
                if (dict == null || dict.Count == 0)
                {
                    MessageBox.Show("Словарь пуст или повреждён.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var token = AppState.Instance.Token;
                if (string.IsNullOrEmpty(token))
                {
                    MessageBox.Show("Токен авторизации отсутствует.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var payload = dict.Select(entry => new
                {
                    good_id = entry.Value.GoodId,
                    moderation = 1,
                    set_gtins = new[] { new { gtin = entry.Value.SetGTIN, quantity = 1 } }
                }).ToList();

                var jsonPayload = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

                string url = "https://markirovka.crpt.ru/api/v3/true-api/nk/feed?apikey=uugdjuxx3uhw9l05";
                var response = await client.PostAsync(url, content);

                string responseBody = await response.Content.ReadAsStringAsync();
                LogHelper.WriteLog("Привязка setGTIN", $"POST {url}\nPayload:\n{jsonPayload}\nResponse:\n{responseBody}");

                if (response.IsSuccessStatusCode)
                    MessageBox.Show("Привязка setGTIN выполнена успешно.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show($"Ошибка при привязке: {response.StatusCode}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [ObservableProperty] private bool _isBrowserVisible;
        [ObservableProperty] private Uri _browserUrl;
        private NkAuthResult _nkSession;

        [RelayCommand]
        private async Task AuthenticateToNkAsync()
        {
            try
            {
                var result = await NkWebAuthService.GetNkTokenAndCookiesAsync(
                    certName: "ГОЛУБЕЦ ВЛАДИСЛАВ ВИТАЛЬЕВИЧ",
                    cryptcpExe: @"H:\Token_4z\cryptcp.win32.exe",
                    dataFile: @"H:\Token_4z\data.txt",
                    signedDataFile: @"H:\Token_4z\data_sign.txt",
                    nkWeb: "https://xn--j1ab.xn----7sbabas4ajkhfocclk9d3cvfsa.xn--p1ai",
                    fingerprint: "C333E83AE307708B1CD5EB8EB94DB0D37CDF1C51",
                    inn: "771683739093"
                );

                if (result == null)
                {
                    MessageBox.Show("Не удалось авторизоваться в НК.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                CsrfToken = result.CsrfToken;
                SessionCookies = result.SessionCookies;
                MessageBox.Show("Авторизация прошла успешно!", "НК", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка авторизации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task PublishProductsAsync()
        {
            try
            {
                MatchStatus = "Авторизация в ЛК НК...";
                _nkSession = await NkAuthService.AuthenticateNkAsync();

                if (_nkSession == null)
                {
                    MessageBox.Show("Ошибка авторизации.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                CsrfToken = _nkSession.CsrfToken;
                SessionCookies = new Dictionary<string, string> { { _nkSession.SessionName, _nkSession.SessionId } };
                IsBrowserVisible = true;
                BrowserUrl = new Uri("https://xn--j1ab.xn----7sbabas4ajkhfocclk9d3cvfsa.xn--p1ai/products");
                MatchStatus = "✅ Авторизация выполнена";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при авторизации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private class ProductLink
        {
            [JsonPropertyName("good_id")]
            public long GoodId { get; set; }

            [JsonPropertyName("setGTIN")]
            public string SetGtin { get; set; }
        }

        private class ProductInfo
        {
            [JsonProperty("good_id")]
            public long GoodId { get; set; }

            [JsonProperty("setGTIN")]
            public string SetGTIN { get; set; }
        }


    }
}
