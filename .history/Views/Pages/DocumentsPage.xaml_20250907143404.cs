using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using markapp.Helpers;
using markapp.Models;

namespace markapp.Views.Pages
{
    public partial class DocumentsPage : Page
    {
        private Dictionary<string, string> _documentTypes;
        private Dictionary<string, string> _documentStatuses;
        private List<DocumentDto> _allDocuments = new();

        public DocumentsPage()
        {
            InitializeComponent();
            Loaded += DocumentsPage_Loaded;
            LogHelper.WriteLog("DocumentsPage.ctor", "Страница документов инициализирована");
        }

        private async void DocumentsPage_Loaded(object sender, RoutedEventArgs e)
        {
            LogHelper.WriteLog("DocumentsPage_Loaded", "Начало загрузки документов");

            try
            {
                LoadDictionaries();

                var documents = await FetchDocumentsAsync();
                _allDocuments = documents;
                LogHelper.WriteLog("DocumentsPage_Loaded", $"Загружено документов: {_allDocuments.Count}");

                PopulateDocumentsGroupedByMonth(_allDocuments);
                LogHelper.WriteLog("DocumentsPage_Loaded", "Документы сгруппированы по месяцам");

            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("DocumentsPage_Loaded", $"Ошибка при загрузке документов: {ex.Message}");
                MessageBox.Show($"Ошибка при загрузке документов: {ex.Message}");
            }
        }

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = FilterBox.Text;
            LogHelper.WriteLog("FilterBox_TextChanged", $"Фильтр изменен: '{text}'");
            FilterDocuments(text);
        }

        private void FilterDocuments(string filter)
        {
            LogHelper.WriteLog("FilterDocuments", $"Применение фильтра: '{filter}'");

            if (string.IsNullOrWhiteSpace(filter))
            {
                LogHelper.WriteLog("FilterDocuments", "Пустой фильтр - отображение всех документов");
                PopulateDocumentsGroupedByMonth(_allDocuments);
                return;
            }

            var terms = filter.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            LogHelper.WriteLog("FilterDocuments", $"Термины фильтра: {string.Join(", ", terms)}");

            var filtered = _allDocuments.Where(doc =>
                terms.Any(term =>
                    (doc.Type?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (doc.Status?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (doc.SenderInn?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (doc.SenderName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (doc.ReceiverInn?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (doc.ReceiverName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                )).ToList();

            LogHelper.WriteLog("FilterDocuments", $"Найдено документов после фильтрации: {filtered.Count}");
            PopulateDocumentsGroupedByMonth(filtered);
        }
        private void LoadDictionaries()
        {
            try
            {
                LogHelper.WriteLog("LoadDictionaries", "Загрузка словарей документов");
                _documentTypes = DictionaryHelper.Load("Resources/document_types.json");
                _documentStatuses = DictionaryHelper.Load("Resources/document_statuses.json");

                // Логируем примеры ключей из словарей для отладки
                var typeKeys = _documentTypes.Keys.Take(5).ToList();
                var statusKeys = _documentStatuses.Keys.Take(5).ToList();

                LogHelper.WriteLog("LoadDictionaries",
                    $"Загружено типов документов: {_documentTypes.Count}, статусов: {_documentStatuses.Count}");
                LogHelper.WriteLog("LoadDictionaries",
                    $"Примеры типов: {string.Join(", ", typeKeys)}");
                LogHelper.WriteLog("LoadDictionaries",
                    $"Примеры статусов: {string.Join(", ", statusKeys)}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("LoadDictionaries", $"Ошибка загрузки словарей: {ex.Message}");
                MessageBox.Show($"Ошибка загрузки словарей: {ex.Message}");
            }
        }

        private async Task<List<DocumentDto>> FetchDocumentsAsync()
        {
            LogHelper.WriteLog("FetchDocumentsAsync", "Начало получения документов из API");

            var token = AppState.Instance.Token;
            var groupCode = AppState.Instance.SelectedProductGroupCode;

            if (string.IsNullOrWhiteSpace(token))
            {
                LogHelper.WriteLog("FetchDocumentsAsync", "Ошибка: токен не получен");
                throw new InvalidOperationException("Токен не получен. Выполните вход в систему.");
            }

            LogHelper.WriteLog("FetchDocumentsAsync", $"Код продуктовой группы: {groupCode}");

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            LogHelper.WriteLog("FetchDocumentsAsync", "HTTP-клиент создан с авторизацией");

            var url = $"https://markirovka.crpt.ru/api/v4/true-api/doc/list?limit=10000&pg={groupCode}";
            LogHelper.WriteLog("FetchDocumentsAsync", $"URL запроса: {url}");

            try
            {
                LogHelper.WriteLog("FetchDocumentsAsync", "Отправка GET запроса");
                var response = await httpClient.GetAsync(url);
                LogHelper.WriteLog("FetchDocumentsAsync", $"Ответ получен, статус: {response.StatusCode}");

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();

                // Логируем первые 200 символов JSON для отладки
                var jsonPreview = json.Length > 200 ? json.Substring(0, 200) + "..." : json;
                LogHelper.WriteLog("FetchDocumentsAsync",
                    $"Данные получены, длина JSON: {json.Length} символов, превью: {jsonPreview}");

                var data = JsonSerializer.Deserialize<DocumentResponse>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var documents = data?.Results ?? new List<DocumentDto>();
                LogHelper.WriteLog("FetchDocumentsAsync", $"Десериализовано документов: {documents.Count}");

                // Логируем первые несколько документов для отладки
                if (documents.Count > 0)
                {
                    var sampleDocs = documents.Take(3).Select(d =>
                        $"№{d.Number}, тип: {d.Type}, статус: {d.Status}, от: {d.SenderName}").ToList();

                    LogHelper.WriteLog("FetchDocumentsAsync",
                        $"Примеры документов: {string.Join("; ", sampleDocs)}");
                }

                // Преобразование типов и статусов
                int convertedTypes = 0;
                int convertedStatuses = 0;
                int convertedDates = 0;
                var conversionErrors = new List<string>();

                foreach (var doc in documents)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(doc.Type) && _documentTypes.TryGetValue(doc.Type, out var typeName))
                        {
                            doc.Type = typeName;
                            convertedTypes++;
                        }
                        else if (!string.IsNullOrEmpty(doc.Type))
                        {
                            conversionErrors.Add($"Тип не найден: {doc.Type}");
                        }

                        if (!string.IsNullOrEmpty(doc.Status) && _documentStatuses.TryGetValue(doc.Status, out var statusName))
                        {
                            doc.Status = statusName;
                            convertedStatuses++;
                        }
                        else if (!string.IsNullOrEmpty(doc.Status))
                        {
                            conversionErrors.Add($"Статус не найден: {doc.Status}");
                        }

                        if (DateTime.TryParse(doc.ReceivedAt, out var parsed))
                        {
                            doc.ReceivedAt = parsed.ToString("dd.MM.yyyy HH:mm");
                            convertedDates++;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLog("FetchDocumentsAsync",
                            $"Ошибка преобразования документа {doc.Number}: {ex.Message}");
                    }
                }

                LogHelper.WriteLog("FetchDocumentsAsync",
                    $"Преобразовано: типов - {convertedTypes}, статусов - {convertedStatuses}, дат - {convertedDates}");

                if (conversionErrors.Count > 0)
                {
                    LogHelper.WriteLog("FetchDocumentsAsync",
                        $"Ошибки преобразования: {string.Join("; ", conversionErrors.Take(5))}");
                }

                return documents;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("FetchDocumentsAsync",
                    $"Ошибка получения документов: {ex.Message}\nStackTrace: {ex.StackTrace}");
                throw;
            }
        }

        private void PopulateDocumentsGroupedByMonth(List<DocumentDto> documents)
        {
            LogHelper.WriteLog("PopulateDocumentsGroupedByMonth",
                $"Начало группировки {documents.Count} документов по месяцам");

            MonthsPanel.Children.Clear();
            LogHelper.WriteLog("PopulateDocumentsGroupedByMonth", "Очищена панель месяцев");

            var validDocuments = documents
                .Where(d => DateTime.TryParse(d.ReceivedAt, out _))
                .ToList();

            LogHelper.WriteLog("PopulateDocumentsGroupedByMonth",
                $"Документов с валидными датами: {validDocuments.Count}");

            var grouped = validDocuments
                .GroupBy(d =>
                {
                    var dt = DateTime.ParseExact(d.ReceivedAt, "dd.MM.yyyy HH:mm", new CultureInfo("ru-RU"));
                    return dt.ToString("MMMM yyyy", new CultureInfo("ru-RU"));
                })
                .OrderByDescending(g =>
                    DateTime.ParseExact(g.Key, "MMMM yyyy", new CultureInfo("ru-RU"))
                );

            LogHelper.WriteLog("PopulateDocumentsGroupedByMonth",
                $"Создано групп: {grouped.Count()}");

            int totalExpanders = 0;
            foreach (var group in grouped)
            {
                var expander = new Expander
                {
                    Header = group.Key,
                    Margin = new Thickness(0, 5, 0, 5),
                    IsExpanded = true,
                    Content = CreateDocumentList(group.ToList())
                };

                MonthsPanel.Children.Add(expander);
                totalExpanders++;

                LogHelper.WriteLog("PopulateDocumentsGroupedByMonth",
                    $"Добавлен Expander для {group.Key} с {group.Count()} документами");
            }

            LogHelper.WriteLog("PopulateDocumentsGroupedByMonth",
                $"Добавлено всего Expander'ов: {totalExpanders}");
        }

        private UIElement CreateDocumentList(List<DocumentDto> docs)
        {
            LogHelper.WriteLog("CreateDocumentList", $"Создание DataGrid для {docs.Count} документов");

            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Height = 200,
                Margin = new Thickness(0, 5, 0, 5),
                ItemsSource = docs
            };

            grid.Columns.Add(new DataGridTextColumn { Header = "Получен", Binding = new System.Windows.Data.Binding("ReceivedAt") });
            grid.Columns.Add(new DataGridTextColumn { Header = "Документ", Binding = new System.Windows.Data.Binding("Type") });
            grid.Columns.Add(new DataGridTextColumn { Header = "Номер", Binding = new System.Windows.Data.Binding("Number") });
            grid.Columns.Add(new DataGridTextColumn { Header = "Отправитель", Binding = new System.Windows.Data.Binding("SenderName") });
            grid.Columns.Add(new DataGridTextColumn { Header = "Получатель", Binding = new System.Windows.Data.Binding("ReceiverName") });
            grid.Columns.Add(new DataGridTextColumn { Header = "Статус", Binding = new System.Windows.Data.Binding("Status") });

            LogHelper.WriteLog("CreateDocumentList", "DataGrid создан с 6 колонками");

            return grid;
        }
    }

    public class DocumentResponse
    {
        public List<DocumentDto> Results { get; set; } = new();
        public bool NextPage { get; set; }
    }

    public class DocumentDto
    {
        public string Number { get; set; }
        public string DocDate { get; set; }
        public string ReceivedAt { get; set; }
        public string Type { get; set; }
        public string Status { get; set; }
        public string SenderInn { get; set; }
        public string SenderName { get; set; }
        public string ReceiverInn { get; set; }
        public string ReceiverName { get; set; }
        public string DownloadDesc { get; set; }
        public string ProductGroup { get; set; }
    }
}