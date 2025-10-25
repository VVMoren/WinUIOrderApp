using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WinUIOrderApp.Helpers;
using WinUIOrderApp.Models;
using WinUIOrderApp.ViewModels.Pages;

namespace WinUIOrderApp.Views.Pages
{
    public partial class ExportsPage : Page
    {
        private CancellationTokenSource _kmCts;
        private const string NCP_TOBACCO_URL = "https://tobacco.crpt.ru";
        private const string URL_CIS_SEARCH = $"{NCP_TOBACCO_URL}/bff-elk/v1/cis/search";
        private const int HTTP_TIMEOUT = 300;
        private const int MAX_CONCURRENT_REQUESTS = 5;
        private const int BATCH_SIZE = 1000;

        private CancellationTokenSource _updCts;
        private const string URL_UPD_SEARCH = "https://tobacco.crpt.ru/bff-elk/v1/documents/tobacco/search";

        private string KmBaseDir =>
            AppState.Instance.GetCurrentCertificateKmFolder() ??
            Path.Combine(AppContext.BaseDirectory, "km");

        private string UpdBaseDir
        {
            get
            {
                var root = AppState.Instance.GetCurrentCertificateFolder();
                return root != null
                    ? Path.Combine(root, "upd")
                    : Path.Combine(AppContext.BaseDirectory, "upd");
            }
        }

        private string UpdLogFile => Path.Combine(UpdBaseDir, "log_upd.txt");

        // Модели для КМ
        private class CisSearchResponse
        {
            public List<CisResult> result { get; set; } = new List<CisResult>();
            public bool isLastPage
            {
                get; set;
            }
        }

        private class CisResult
        {
            public string cis { get; set; } = string.Empty;
            public string cisPrintView { get; set; } = string.Empty;
            public string gtin { get; set; } = string.Empty;
            public string emissionDate { get; set; } = string.Empty;
            public string? name
            {
                get; set;
            }
        }

        // Модели для УПД
        public class UpdSearchResponse
        {
            public List<UpdDocument> content { get; set; } = new List<UpdDocument>();
            public int totalElements
            {
                get; set;
            }
            public bool last
            {
                get; set;
            }
        }

        public class UpdDocument
        {
            public string id { get; set; } = string.Empty;
            public string number { get; set; } = string.Empty;
            public long docDate
            {
                get; set;
            }
            public long receivedDate
            {
                get; set;
            }
            public string type { get; set; } = string.Empty;
            public string status { get; set; } = string.Empty;
            public string senderId { get; set; } = string.Empty;
            public string senderInn { get; set; } = string.Empty;
            public string senderName { get; set; } = string.Empty;
            public string receiverId { get; set; } = string.Empty;
            public string receiverInn { get; set; } = string.Empty;
            public string receiverName { get; set; } = string.Empty;
            public string invoiceNumber { get; set; } = string.Empty;
            public decimal total
            {
                get; set;
            }
        }

        public class UpdParticipant
        {
            public string inn { get; set; } = string.Empty;
            public string name { get; set; } = string.Empty;
        }

        public class UpdDetailResponse
        {
            public string id { get; set; } = string.Empty;
            public UpdBody? body
            {
                get; set;
            }
            public UpdHeader? header
            {
                get; set;
            }
        }

        public class UpdHeader
        {
            public string? invoiceId
            {
                get; set;
            }
            public string? invoiceDate
            {
                get; set;
            }
        }

        public class UpdBody
        {
            public List<UpdCisInfo> cisesInfo { get; set; } = new List<UpdCisInfo>();
            public UpdCounterparty? seller
            {
                get; set;
            }
            public UpdCounterparty? buyer
            {
                get; set;
            }
        }

        public class UpdCisInfo
        {
            public string cis { get; set; } = string.Empty;
            public string name { get; set; } = string.Empty;
            public string gtin { get; set; } = string.Empty;
        }

        public class UpdCounterparty
        {
            public string fullName { get; set; } = string.Empty;
            public string inn { get; set; } = string.Empty;
        }

        // Модель для отображения УПД в DataGrid
        public class UpdItem
        {
            public string Cis { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Gtin { get; set; } = string.Empty;
            public string Counterparty { get; set; } = string.Empty;
            public string DocumentNumber { get; set; } = string.Empty;
            public string DocumentDate { get; set; } = string.Empty;
            public string DocumentType { get; set; } = string.Empty;
            public string DocumentStatus { get; set; } = string.Empty;
        }

        // Потокобезопасный HashSet
        private class ConcurrentHashSet<T>
        {
            private readonly ConcurrentDictionary<T, byte> _dictionary = new ConcurrentDictionary<T, byte>();

            public bool Add(T item) => _dictionary.TryAdd(item, 0);
            public int Count => _dictionary.Count;
            public IEnumerable<T> Items => _dictionary.Keys;
        }

        // Модель для отображения в списке статусов
        public class StatusItem
        {
            public string Name { get; set; } = string.Empty;
            public int Id
            {
                get; set;
            }
            public bool IsSelected
            {
                get; set;
            }
        }

        // Коллекции
        public ObservableCollection<StatusItem> StatusItems { get; } = new ObservableCollection<StatusItem>();
        public ObservableCollection<CisItem> KmResults { get; } = new ObservableCollection<CisItem>();
        public ObservableCollection<UpdItem> UpdResults { get; } = new ObservableCollection<UpdItem>();

        public ExportsPage()
        {
            InitializeComponent();
            Loaded += ExportsPage_Loaded;
            Unloaded += ExportsPage_Unloaded;
            LoadCisStatuses();
        }

        public async Task StartKmDataFetchFromDashboardAsync()
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                if (!StatusItems.Any())
                    return;

                var statuses = StatusItems.Where(s => s.IsSelected).Select(s => s.Id).ToList();
                if (!statuses.Any())
                    statuses = StatusItems.Select(s => s.Id).ToList();

                await GetKmDataParallelAsync(statuses);
            });
        }

        private async void ExportsPage_Loaded(object sender, RoutedEventArgs e)
        {
            LogHelper.WriteLog("ExportsPage.ExportsPage_Loaded", "Страница ExportsPage загружена");
            UpdateTokenStatus();
            KmDataGrid.ItemsSource = KmResults;
            CisStatusListBox.ItemsSource = StatusItems;
            UpdDataGrid.ItemsSource = UpdResults;

            InitializeUpdTab();
        }

        private void ExportsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            LogHelper.WriteLog("ExportsPage.ExportsPage_Unloaded", "Страница ExportsPage выгружена");
            _kmCts?.Cancel();
            _kmCts?.Dispose();
            _kmCts = null;

            _updCts?.Cancel();
            _updCts?.Dispose();
            _updCts = null;
        }

        private void UpdateTokenStatus()
        {
            var appState = AppState.Instance;
            if (!string.IsNullOrEmpty(appState.Token))
            {
                TokenStatusText.Text = "Активен";
                TokenStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                AutoTokenStatus.Text = $"Сертификат: {appState.CertificateOwnerPublicName}";
                LogHelper.WriteLog("ExportsPage.UpdateTokenStatus", $"Токен активен, сертификат: {appState.CertificateOwnerPublicName}");
            }
            else
            {
                TokenStatusText.Text = "Не получен";
                TokenStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                AutoTokenStatus.Text = "Требуется авторизация";
                LogHelper.WriteLog("ExportsPage.UpdateTokenStatus", "Токен не получен");
            }
        }

        private void LoadCisStatuses()
        {
            try
            {
                var statusesPath = @"X:\VS\source\repos\WinUIOrderApp\Resources\cis_statuses.json";
                LogHelper.WriteLog("ExportsPage.LoadCisStatuses", $"Загрузка статусов из: {statusesPath}");

                if (File.Exists(statusesPath))
                {
                    var json = File.ReadAllText(statusesPath);
                    var cisStatuses = JsonSerializer.Deserialize<List<CisStatus>>(json);

                    StatusItems.Clear();
                    foreach (var status in cisStatuses)
                    {
                        StatusItems.Add(new StatusItem
                        {
                            Id = status.Id,
                            Name = status.Name,
                            IsSelected = status.Id == 2 || status.Id == 6 // INTRODUCED и DISAGGREGATION по умолчанию
                        });
                    }

                    LogHelper.WriteLog("ExportsPage.LoadCisStatuses.Success",
                        $"Загружено {cisStatuses.Count} статусов, выбрано по умолчанию: 2,6");
                }
                else
                {
                    LogHelper.WriteLog("ExportsPage.LoadCisStatuses.Error", $"Файл не найден: {statusesPath}");
                    MessageBox.Show($"Файл статусов не найден: {statusesPath}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("ExportsPage.LoadCisStatuses.Error",
                    $"Ошибка загрузки статусов: {ex.Message}\nStack: {ex.StackTrace}");
                MessageBox.Show($"Ошибка загрузки статусов КМ: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GetKmDataButton_Click(object sender, RoutedEventArgs e)
        {
            LogHelper.WriteLog("ExportsPage.GetKmDataButton_Click", "Нажата кнопка получения данных КМ");

            if (string.IsNullOrEmpty(AppState.Instance.Token))
            {
                var errorMsg = "Токен не получен";
                LogHelper.WriteLog("ExportsPage.GetKmDataButton_Click.Error", errorMsg);
                MessageBox.Show("Сначала выполните авторизацию в ГИС МТ", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedStatuses = StatusItems.Where(s => s.IsSelected).Select(s => s.Id).ToList();
            if (!selectedStatuses.Any())
            {
                var errorMsg = "Не выбраны статусы КМ";
                LogHelper.WriteLog("ExportsPage.GetKmDataButton_Click.Error", errorMsg);
                MessageBox.Show("Выберите хотя бы один статус КМ", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LogHelper.WriteLog("ExportsPage.GetKmDataButton_Click.SelectedStatuses",
                $"Выбраны статусы: {string.Join(", ", selectedStatuses)}");

            await GetKmDataParallelAsync(selectedStatuses);
        }

        private async Task GetKmDataParallelAsync(List<int> selectedStatuses)
        {
            _kmCts?.Cancel();
            _kmCts = new CancellationTokenSource();

            try
            {
                // Сброс предыдущих результатов
                KmResults.Clear();
                TotalCodesText.Text = "0";
                UniqueGtinText.Text = "0";
                KmProgressBar.Value = 0;
                KmStatusText.Text = "Начало получения данных...";

                LogHelper.WriteLog("ExportsPage.GetKmDataParallel", $"Начало параллельного получения КМ. Выбранные статусы: {string.Join(", ", selectedStatuses)}");

                // Получаем информацию о товарной группе
                var (productGroups, productGroupName) = await GetProductGroupInfoAsync();
                if (productGroups == null)
                    return;

                LogHelper.WriteLog("ExportsPage.GetKmDataParallel.Config",
                    $"Конфигурация запроса - Товарная группа ID: {productGroups[0]} ('{productGroupName}'), Статусы: {string.Join(", ", selectedStatuses)}");

                // Потокобезопасные коллекции для результатов
                var allCisData = new ConcurrentBag<CisItem>();
                var uniqueGtins = new ConcurrentHashSet<string>();
                var totalProcessed = 0;
                var lastUiUpdate = DateTime.MinValue;
                const int UI_UPDATE_INTERVAL_MS = 500;

                // Очередь для пагинации
                var paginationQueue = new ConcurrentQueue<(string emissionDate, string cisLast)>();

                // Семафор для ограничения параллельных запросов
                var semaphore = new SemaphoreSlim(MAX_CONCURRENT_REQUESTS);

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(HTTP_TIMEOUT);
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AppState.Instance.Token);
                httpClient.DefaultRequestHeaders.Add("User-Agent", "WinUIOrderApp/1.0");

                KmProgressText.Text = "Подготовка к параллельному получению данных...";

                // ШАГ 1: Получаем первую страницу
                var firstPage = await FetchPageAsync(httpClient, selectedStatuses, productGroups, null, null, _kmCts.Token);
                if (firstPage?.result == null || !firstPage.result.Any())
                {
                    LogHelper.WriteLog("ExportsPage.GetKmDataParallel.Empty", "Нет данных для выбранных критериев");
                    MessageBox.Show("Нет данных КМ для выбранных критериев", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Обрабатываем первую страницу
                ProcessPageResults(firstPage, allCisData, uniqueGtins);
                totalProcessed += firstPage.result.Count;
                await UpdateProgressAsync(allCisData.Count, uniqueGtins.Count, 1);

                // Если есть еще страницы - добавляем в очередь
                if (!firstPage.isLastPage && firstPage.result.Count == BATCH_SIZE)
                {
                    var lastItem = firstPage.result.Last();
                    paginationQueue.Enqueue((lastItem.emissionDate, lastItem.cis));
                    LogHelper.WriteLog("ExportsPage.GetKmDataParallel.Queue", "Добавлена первая пагинация в очередь");
                }

                // ШАГ 2: Запускаем параллельные задачи для остальных страниц
                var tasks = new List<Task>();
                int activeWorkers = 0;
                var totalPages = 1; // Уже обработали первую страницу

                while ((!paginationQueue.IsEmpty || activeWorkers > 0) && !_kmCts.Token.IsCancellationRequested)
                {
                    if (paginationQueue.TryDequeue(out var pagination))
                    {
                        await semaphore.WaitAsync(_kmCts.Token);
                        activeWorkers++;

                        var task = Task.Run(async () =>
                        {
                            try
                            {
                                var pageData = await FetchPageAsync(httpClient, selectedStatuses, productGroups,
                                    pagination.emissionDate, pagination.cisLast, _kmCts.Token);

                                if (pageData?.result != null && pageData.result.Any())
                                {
                                    // Обрабатываем результаты
                                    ProcessPageResults(pageData, allCisData, uniqueGtins);

                                    Interlocked.Add(ref totalProcessed, pageData.result.Count);
                                    totalPages++;

                                    // Обновляем прогресс (с ограничением частоты)
                                    if ((DateTime.Now - lastUiUpdate).TotalMilliseconds >= UI_UPDATE_INTERVAL_MS)
                                    {
                                        await UpdateProgressAsync(allCisData.Count, uniqueGtins.Count, totalPages);
                                        lastUiUpdate = DateTime.Now;
                                    }

                                    // Если есть еще страницы - добавляем в очередь
                                    if (!pageData.isLastPage && pageData.result.Count == BATCH_SIZE)
                                    {
                                        var newLastItem = pageData.result.Last();
                                        paginationQueue.Enqueue((newLastItem.emissionDate, newLastItem.cis));
                                    }

                                    LogHelper.WriteLog("ExportsPage.GetKmDataParallel.PageSuccess",
                                        $"Обработана страница: {pageData.result.Count} записей, Всего: {allCisData.Count}, Уникальных GTIN: {uniqueGtins.Count}");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLog("ExportsPage.GetKmDataParallel.PageError",
                                    $"Ошибка при обработке страницы: {ex.Message}");
                            }
                            finally
                            {
                                semaphore.Release();
                                Interlocked.Decrement(ref activeWorkers);
                            }
                        }, _kmCts.Token);

                        tasks.Add(task);
                    }
                    else
                    {
                        // Если очередь пуста, ждем немного перед следующей проверкой
                        await Task.Delay(50, _kmCts.Token);
                    }
                }

                // Ждем завершения всех задач
                if (!_kmCts.Token.IsCancellationRequested)
                {
                    await Task.WhenAll(tasks);
                }

                // Финальное обновление UI
                if (!_kmCts.Token.IsCancellationRequested)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        // Добавляем все данные в UI коллекцию
                        foreach (var item in allCisData)
                        {
                            KmResults.Add(item);
                        }

                        TotalCodesText.Text = allCisData.Count.ToString("N0");
                        UniqueGtinText.Text = uniqueGtins.Count.ToString("N0");
                        KmProgressBar.Value = KmProgressBar.Maximum;
                    });

                    // Сохранение результатов в файл
                    var allDataList = allCisData.ToList();
                    await SaveKmResultsToFile(allDataList);
                    AppState.Instance.UpdateKmResults(allDataList);

                    KmStatusText.Text = "Готово";
                    KmProgressText.Text = $"Получено {allCisData.Count:N0} КМ, {uniqueGtins.Count:N0} уникальных GTIN";

                    LogHelper.WriteLog("ExportsPage.GetKmDataParallel.Success",
                        $"✅ ПАРАЛЛЕЛЬНОЕ ПОЛУЧЕНИЕ ЗАВЕРШЕНО:\n" +
                        $"• Получено КМ: {allCisData.Count:N0}\n" +
                        $"• Уникальных GTIN: {uniqueGtins.Count:N0}\n" +
                        $"• Обработано страниц: {totalPages}\n" +
                        $"• Товарная группа: {productGroups[0]} ('{productGroupName}')\n" +
                        $"• Статусы: {string.Join(", ", selectedStatuses)}");

                    MessageBox.Show($"Получено {allCisData.Count:N0} КМ\nУникальных GTIN: {uniqueGtins.Count:N0}\nТоварная группа: {productGroupName}",
                        "Запрос КМ завершён", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    KmStatusText.Text = "Операция отменена";
                    LogHelper.WriteLog("ExportsPage.GetKmDataParallel.Cancelled", "Операция отменена пользователем");
                }
            }
            catch (OperationCanceledException)
            {
                KmStatusText.Text = "Операция отменена пользователем";
                LogHelper.WriteLog("ExportsPage.GetKmDataParallel.Cancelled", "Операция отменена на уровне метода");
            }
            catch (Exception ex)
            {
                KmStatusText.Text = "Ошибка получения данных";
                LogHelper.WriteLog("ExportsPage.GetKmDataParallel.Error",
                    $"❌ КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}\nStack: {ex.StackTrace}");
                MessageBox.Show($"Ошибка при получении данных КМ: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<(int[] productGroups, string productGroupName)> GetProductGroupInfoAsync()
        {
            var productGroupsPath = @"X:\VS\source\repos\WinUIOrderApp\Resources\product_groups.json";
            if (!File.Exists(productGroupsPath))
            {
                var errorMsg = $"Файл справочника товарных групп не найден: {productGroupsPath}";
                LogHelper.WriteLog("ExportsPage.GetProductGroupInfo.Error", errorMsg);
                MessageBox.Show(errorMsg, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return (null, null);
            }

            var productGroupsJson = await File.ReadAllTextAsync(productGroupsPath);
            var productGroupsResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(productGroupsJson);

            var productGroupsList = new List<ProductGroupDto>();
            if (productGroupsResponse != null && productGroupsResponse.ContainsKey("result"))
            {
                var resultJson = productGroupsResponse["result"].ToString();
                productGroupsList = JsonSerializer.Deserialize<List<ProductGroupDto>>(resultJson);
            }

            if (productGroupsList == null || !productGroupsList.Any())
            {
                var errorMsg = "Не удалось загрузить справочник товарных групп";
                LogHelper.WriteLog("ExportsPage.GetProductGroupInfo.Error", errorMsg);
                MessageBox.Show(errorMsg, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return (null, null);
            }

            var selectedProductGroupCode = AppState.Instance.SelectedProductGroupCode;
            if (string.IsNullOrEmpty(selectedProductGroupCode))
            {
                var errorMsg = "Не выбрана товарная группа в настройках";
                LogHelper.WriteLog("ExportsPage.GetProductGroupInfo.Error", errorMsg);
                MessageBox.Show(errorMsg, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return (null, null);
            }

            var selectedProductGroup = productGroupsList.FirstOrDefault(pg =>
                pg.code.Equals(selectedProductGroupCode, StringComparison.OrdinalIgnoreCase));

            if (selectedProductGroup == null)
            {
                var errorMsg = $"Товарная группа с кодом '{selectedProductGroupCode}' не найдена в справочнике";
                LogHelper.WriteLog("ExportsPage.GetProductGroupInfo.Error", errorMsg);
                MessageBox.Show(errorMsg, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return (null, null);
            }

            LogHelper.WriteLog("ExportsPage.GetProductGroupInfo.Found",
                $"Найдена товарная группа: ID={selectedProductGroup.id}, Название='{selectedProductGroup.name}'");

            return (new[] { selectedProductGroup.id }, selectedProductGroup.name);
        }

        private async Task<CisSearchResponse> FetchPageAsync(HttpClient httpClient, List<int> selectedStatuses,
            int[] productGroups, string emissionDate, string cisLast, CancellationToken cancellationToken)
        {
            var states = selectedStatuses.Select(status => new { status }).ToList();

            object payload = string.IsNullOrEmpty(emissionDate) ? new
            {
                filter = new
                {
                    generalPackageTypes = new[] { 0 },
                    states = states,
                    productGroups = productGroups
                },
                pagination = new
                {
                    pageDir = "NEXT",
                    limit = BATCH_SIZE
                }
            } : new
            {
                filter = new
                {
                    generalPackageTypes = new[] { 0 },
                    states = states,
                    productGroups = productGroups
                },
                pagination = new
                {
                    pageDir = "NEXT",
                    limit = BATCH_SIZE,
                    emissionDate = emissionDate,
                    cis = cisLast
                }
            };

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(URL_CIS_SEARCH, requestContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Ошибка API: {response.StatusCode} - {errorContent}");
            }

            var responseData = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<CisSearchResponse>(responseData, jsonOptions);
        }

        private void ProcessPageResults(CisSearchResponse pageData, ConcurrentBag<CisItem> allCisData, ConcurrentHashSet<string> uniqueGtins)
        {
            foreach (var item in pageData.result)
            {
                var cisItem = new CisItem
                {
                    Cis = CleanString(item.cis),
                    Name = CleanString(item.name ?? item.cisPrintView ?? item.cis),
                    ProductName = CleanString(item.name ?? item.cisPrintView ?? item.cis)
                };

                allCisData.Add(cisItem);

                if (!string.IsNullOrEmpty(item.gtin))
                    uniqueGtins.Add(CleanString(item.gtin));
            }
        }

        private async Task UpdateProgressAsync(int totalCodes, int uniqueGtins, int currentPage)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TotalCodesText.Text = totalCodes.ToString("N0");
                UniqueGtinText.Text = uniqueGtins.ToString("N0");
                KmProgressBar.Value = currentPage;
                KmProgressBar.Maximum = Math.Max(currentPage + 10, 50); // Динамический максимум
                KmStatusText.Text = $"Обработано: {totalCodes:N0} КМ";
                KmProgressText.Text = $"Страница {currentPage}, КМ: {totalCodes:N0}, GTIN: {uniqueGtins:N0}";
            });
        }

        private async Task SaveKmResultsToFile(List<CisItem> cisData)
        {
            try
            {
                EnsureDirectoryExists(KmBaseDir);

                var certName = AppState.Instance.CertificateOwnerPublicName ?? "Unknown";
                var cleanCertName = new string(certName.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray());
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"{cisData.Count}_{timestamp}_{cleanCertName}.txt";
                var filePath = Path.Combine(KmBaseDir, fileName);

                var sb = new StringBuilder();
                sb.AppendLine("CIS");
                foreach (var item in cisData)
                {
                    sb.AppendLine(item.Cis);
                }

                await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);

                LogHelper.WriteLog("ExportsPage.SaveKmResultsToFile.Success",
                    $"Сохранено {cisData.Count} КМ в файл: {filePath}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("ExportsPage.SaveKmResultsToFile.Error",
                    $"Ошибка сохранения файла: {ex.Message}\nStack: {ex.StackTrace}");
                MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private string CleanString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";
            return input.Replace("\n", "").Replace("\r", "").Trim();
        }

        private void EnsureDirectoryExists(string path)
        {
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        // УПД функции

        private void InitializeUpdTab()
        {
            try
            {
                // Заполняем комбобокс статусами УПД
                UpdStatusComboBox.Items.Clear();
                var updStatuses = new[]
                {
                    new { Name = "Все", Value = "" },
                    new { Name = "Обработан успешно", Value = "CHECKED_OK" },
                    new { Name = "Обработан с ошибками", Value = "CHECKED_NOT_OK" }
                };

                foreach (var status in updStatuses)
                {
                    UpdStatusComboBox.Items.Add(status);
                }

                UpdStatusComboBox.DisplayMemberPath = "Name";
                UpdStatusComboBox.SelectedValuePath = "Value";
                UpdStatusComboBox.SelectedIndex = 0;

                // Создаем директории для УПД
                EnsureDirectoryExists(UpdBaseDir);
                EnsureDirectoryExists(Path.GetDirectoryName(UpdLogFile));

                LogHelper.WriteLog("ExportsPage.InitializeUpdTab", "Вкладка УПД инициализирована");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("ExportsPage.InitializeUpdTab.Error", $"Ошибка инициализации УПД: {ex.Message}");
            }
        }

        private async void GetUpdDataButton_Click(object sender, RoutedEventArgs e)
        {
            LogHelper.WriteLog("ExportsPage.GetUpdDataButton_Click", "Нажата кнопка получения УПД");

            if (string.IsNullOrEmpty(AppState.Instance.Token))
            {
                MessageBox.Show("Сначала выполните авторизацию в ГИС МТ", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await GetUpdDataAsync();
        }

        private async Task GetUpdDataAsync()
        {
            _updCts?.Cancel();
            _updCts = new CancellationTokenSource();

            try
            {
                // Сброс предыдущих результатов
                UpdResults.Clear();
                TotalDocsText.Text = "0";
                NewCisText.Text = "0";
                TotalCisText.Text = "0";
                UpdProgressBar.Value = 0;
                UpdStatusText.Text = "Начало получения УПД...";

                LogHelper.WriteLog("ExportsPage.GetUpdData", "Начало получения УПД");

                // Загружаем историю обработанных документов
                var loggedIds = LoadLoggedIds();
                LogHelper.WriteLog("ExportsPage.GetUpdData.LoggedIds", $"Загружено {loggedIds.Count} обработанных документов");

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(HTTP_TIMEOUT);
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AppState.Instance.Token);
                httpClient.DefaultRequestHeaders.Add("User-Agent", "WinUIOrderApp/1.0");

                UpdProgressText.Text = "Поиск документов УПД...";

                // Формируем payload для поиска УПД
                var cert = AppState.Instance.SelectedCertificate;
                var inn = ExtractInnFromCertificate(cert);

                if (string.IsNullOrEmpty(inn))
                {
                    MessageBox.Show("Не удалось определить ИНН из сертификата", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var selectedStatus = (UpdStatusComboBox.SelectedItem as dynamic)?.Value ?? "";
                var isOutgoing = UpdOutgoingRadio.IsChecked == true;
                var productGroupId = await GetCurrentProductGroupIdAsync();

                // ФОРМИРУЕМ КОРРЕКТНЫЙ PAYLOAD СОГЛАСНО ПРИМЕРУ
                var payload = new Dictionary<string, object>();
                var index = 0;

                // Добавляем фильтр по статусу если выбран
                if (!string.IsNullOrEmpty(selectedStatus))
                {
                    payload[$"{index}"] = new { id = "documentStatus", value = selectedStatus };
                    index++;
                    payload["documentStatus"] = selectedStatus;
                }

                // Добавляем фильтр по отправителю/получателю
                if (isOutgoing)
                {
                    payload[$"{index}"] = new { id = "senderInn", value = inn };
                    payload["senderInn"] = inn;
                }
                else
                {
                    payload[$"{index}"] = new { id = "receiverInn", value = inn };
                    payload["receiverInn"] = inn;
                }

                // Добавляем пагинацию и исключаемые типы
                payload["pagination"] = new
                {
                    limit = 10000,
                    offset = 0,
                    order = "DESC"
                };

                payload["excludingTypes"] = new[]
                {
                    "LK_ADD_APP_USER",
                    "LK_ADD_APP_USER_XML",
                    "GRAY_ZONE_DOCUMENT",
                    "LP_FTS_INTRODUCE_REQUEST"
                };

                // Отправляем запрос на поиск документов
                var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                LogHelper.WriteLog("ExportsPage.GetUpdData.Request",
                    $"Поиск УПД: {(isOutgoing ? "Исходящие" : "Входящие")}, Статус: {selectedStatus}, ИНН: {inn}");

                var response = await httpClient.PostAsync(URL_UPD_SEARCH, requestContent, _updCts.Token);
                var responseData = await response.Content.ReadAsStringAsync();

                LogHelper.WriteLog("ExportsPage.GetUpdData.Response",
                    $"Ответ поиска УПД: {response.StatusCode}, Длина: {responseData.Length}");

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Ошибка API при поиске УПД: {response.StatusCode} - {responseData}");
                }

                // ДЕСЕРИАЛИЗУЕМ КАК МАССИВ ДОКУМЕНТОВ
                var documents = JsonSerializer.Deserialize<List<UpdDocument>>(responseData) ?? new List<UpdDocument>();

                LogHelper.WriteLog("ExportsPage.GetUpdData.Found", $"Найдено документов: {documents.Count}");

                if (documents.Count == 0)
                {
                    MessageBox.Show("Документы УПД не найдены", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Настройка прогресс-бара
                UpdProgressBar.Maximum = documents.Count;
                UpdProgressBar.Value = 0;

                var allRows = new List<UpdItem>();
                var newCisList = new List<string>();
                var processedCount = 0;
                var newDocumentsCount = 0;

                // Обрабатываем каждый документ
                foreach (var doc in documents)
                {
                    if (_updCts.Token.IsCancellationRequested)
                        break;

                    try
                    {
                        // Пропускаем уже обработанные документы
                        if (loggedIds.Contains(doc.id))
                        {
                            processedCount++;
                            continue;
                        }

                        // Получаем детальную информацию о документе
                        var detailUrl = string.Format("https://tobacco.crpt.ru/bff-elk/v1/documents/{0}?productGroup={1}",
                            doc.id, productGroupId);

                        LogHelper.WriteLog("ExportsPage.GetUpdData.DetailRequest",
                            $"Запрос деталей документа: {detailUrl}");

                        var detailResponse = await httpClient.GetAsync(detailUrl, _updCts.Token);
                        var detailData = await detailResponse.Content.ReadAsStringAsync();

                        if (!detailResponse.IsSuccessStatusCode)
                        {
                            LogHelper.WriteLog("ExportsPage.GetUpdData.DetailError",
                                $"Ошибка получения деталей документа {doc.id}: {detailResponse.StatusCode}");
                            continue;
                        }

                        var detail = JsonSerializer.Deserialize<UpdDetailResponse>(detailData);

                        if (detail?.body?.cisesInfo != null)
                        {
                            var counterparty = isOutgoing
                                ? doc.receiverName
                                : doc.senderName;

                            var docNumber = doc.invoiceNumber ?? "Без номера";
                            var docDate = ConvertTimestampToDate(doc.docDate);

                            foreach (var cisInfo in detail.body.cisesInfo)
                            {
                                var updItem = new UpdItem
                                {
                                    Cis = CleanString(cisInfo.cis),
                                    Name = CleanString(cisInfo.name),
                                    Gtin = CleanString(cisInfo.gtin),
                                    Counterparty = CleanString(counterparty),
                                    DocumentNumber = CleanString(docNumber),
                                    DocumentDate = CleanString(docDate),
                                    DocumentType = isOutgoing ? "Исходящий" : "Входящий",
                                    DocumentStatus = GetStatusDisplayName(doc.status)
                                };

                                allRows.Add(updItem);
                                newCisList.Add(cisInfo.cis);
                            }

                            // Сохраняем ID обработанного документа
                            SaveLoggedId(doc.id);
                            newDocumentsCount++;
                        }

                        processedCount++;

                        // Обновляем прогресс
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            UpdProgressBar.Value = processedCount;
                            UpdProgressText.Text = $"Обработано {processedCount}/{documents.Count} документов";
                            TotalDocsText.Text = newDocumentsCount.ToString();
                            NewCisText.Text = newCisList.Count.ToString();
                            TotalCisText.Text = allRows.Count.ToString();
                        });

                        // Небольшая задержка чтобы не перегружать API
                        await Task.Delay(100, _updCts.Token);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLog("ExportsPage.GetUpdData.DocumentError",
                            $"Ошибка обработки документа {doc.id}: {ex.Message}");
                    }
                }

                // Добавляем результаты в UI
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var row in allRows)
                    {
                        UpdResults.Add(row);
                    }
                });

                // Сохраняем результаты в файл
                await SaveUpdResultsToFile(allRows);

                UpdStatusText.Text = "Готово";
                UpdProgressText.Text = $"Обработано {newDocumentsCount} новых документов";

                LogHelper.WriteLog("ExportsPage.GetUpdData.Success",
                    $"✅ УПД ЗАВЕРШЕНО:\n" +
                    $"• Новых документов: {newDocumentsCount}\n" +
                    $"• Новых КМ: {newCisList.Count}\n" +
                    $"• Всего записей: {allRows.Count}");

                MessageBox.Show($"Обработано {newDocumentsCount} новых документов\nНовых КМ: {newCisList.Count}",
                    "Запрос УПД завершён", MessageBoxButton.OK, MessageBoxImage.Information);

            }
            catch (OperationCanceledException)
            {
                UpdStatusText.Text = "Операция отменена пользователем";
                LogHelper.WriteLog("ExportsPage.GetUpdData.Cancelled", "Операция отменена");
            }
            catch (Exception ex)
            {
                UpdStatusText.Text = "Ошибка получения УПД";
                LogHelper.WriteLog("ExportsPage.GetUpdData.Error",
                    $"❌ ОШИБКА УПД: {ex.Message}\nStack: {ex.StackTrace}");
                MessageBox.Show($"Ошибка при получении УПД: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private string ConvertTimestampToDate(long timestamp)
        {
            try
            {
                var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
                return dateTime.ToString("dd.MM.yyyy HH:mm:ss");
            }
            catch (Exception)
            {
                return "Неизвестная дата";
            }
        }

        private string GetStatusDisplayName(string status)
        {
            return status switch
            {
                "CHECKED_OK" => "Обработан успешно",
                "CHECKED_NOT_OK" => "Обработан с ошибками",
                _ => status
            };
        }

        private HashSet<string> LoadLoggedIds()
        {
            var ids = new HashSet<string>();
            try
            {
                if (File.Exists(UpdLogFile))
                {
                    var lines = File.ReadAllLines(UpdLogFile);
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            ids.Add(line.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("ExportsPage.LoadLoggedIds.Error", $"Ошибка загрузки лога УПД: {ex.Message}");
            }
            return ids;
        }

        private void SaveLoggedId(string docId)
        {
            try
            {
                File.AppendAllText(UpdLogFile, docId + Environment.NewLine);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("ExportsPage.SaveLoggedId.Error", $"Ошибка сохранения ID документа: {ex.Message}");
            }
        }

        private async Task<int> GetCurrentProductGroupIdAsync()
        {
            try
            {
                var productGroupsPath = @"X:\VS\source\repos\WinUIOrderApp\Resources\product_groups.json";
                if (!File.Exists(productGroupsPath))
                    return 3; // По умолчанию табачная продукция

                var productGroupsJson = await File.ReadAllTextAsync(productGroupsPath);
                var productGroupsResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(productGroupsJson);

                var productGroupsList = new List<ProductGroupDto>();
                if (productGroupsResponse != null && productGroupsResponse.ContainsKey("result"))
                {
                    var resultJson = productGroupsResponse["result"].ToString();
                    productGroupsList = JsonSerializer.Deserialize<List<ProductGroupDto>>(resultJson);
                }

                var selectedProductGroupCode = AppState.Instance.SelectedProductGroupCode;
                var selectedProductGroup = productGroupsList?.FirstOrDefault(pg =>
                    pg.code.Equals(selectedProductGroupCode, StringComparison.OrdinalIgnoreCase));

                return selectedProductGroup?.id ?? 3;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("ExportsPage.GetCurrentProductGroupId.Error",
                    $"Ошибка получения ID товарной группы: {ex.Message}");
                return 3;
            }
        }

        private string ExtractInnFromCertificate(System.Security.Cryptography.X509Certificates.X509Certificate2 cert)
        {
            try
            {
                var subject = cert?.Subject ?? "";
                var innStart = subject.IndexOf("ИНН=");
                if (innStart >= 0)
                {
                    innStart += 4;
                    var innEnd = subject.IndexOf(",", innStart);
                    if (innEnd == -1) innEnd = subject.Length;
                    return subject.Substring(innStart, innEnd - innStart).Trim();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("ExportsPage.ExtractInnFromCertificate.Error", $"Ошибка извлечения ИНН: {ex.Message}");
            }
            return string.Empty;
        }

        private async Task SaveUpdResultsToFile(List<UpdItem> updData)
        {
            try
            {
                EnsureDirectoryExists(UpdBaseDir);

                var certName = AppState.Instance.CertificateOwnerPublicName ?? "Unknown";
                var cleanCertName = new string(certName.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray());
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"УПД_{updData.Count}_{timestamp}_{cleanCertName}.txt";
                var filePath = Path.Combine(UpdBaseDir, fileName);

                var sb = new StringBuilder();
                sb.AppendLine("CIS|Name|GTIN|Контрагент|№ УПД|Дата документа|Тип|Статус");
                foreach (var item in updData)
                {
                    sb.AppendLine($"{item.Cis}|{item.Name}|{item.Gtin}|{item.Counterparty}|{item.DocumentNumber}|{item.DocumentDate}|{item.DocumentType}|{item.DocumentStatus}");
                }

                await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);

                LogHelper.WriteLog("ExportsPage.SaveUpdResultsToFile.Success",
                    $"Сохранено {updData.Count} записей УПД в файл: {filePath}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("ExportsPage.SaveUpdResultsToFile.Error",
                    $"Ошибка сохранения файла УПД: {ex.Message}");
            }
        }

    }
}