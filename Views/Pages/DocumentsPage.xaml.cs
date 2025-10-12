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
using System.Windows.Media;
using WinUIOrderApp.Helpers;
using WinUIOrderApp.Models;

namespace WinUIOrderApp.Views.Pages
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
                MessageBox.Show($"Ошибка при загрузке документов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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

                LogHelper.WriteLog("LoadDictionaries",
                    $"Загружено типов документов: {_documentTypes.Count}, статусов: {_documentStatuses.Count}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("LoadDictionaries", $"Ошибка загрузки словарей: {ex.Message}");
                MessageBox.Show($"Ошибка загрузки словарей: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
                LogHelper.WriteLog("FetchDocumentsAsync", $"Данные получены, длина JSON: {json.Length} символов");

                var data = JsonSerializer.Deserialize<DocumentResponse>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var documents = data?.Results ?? new List<DocumentDto>();
                LogHelper.WriteLog("FetchDocumentsAsync", $"Десериализовано документов: {documents.Count}");

                // Преобразование типов и статусов
                int convertedTypes = 0;
                int convertedStatuses = 0;
                int convertedDates = 0;

                foreach (var doc in documents)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(doc.Type) && _documentTypes.TryGetValue(doc.Type, out var typeName))
                        {
                            doc.Type = typeName;
                            convertedTypes++;
                        }

                        if (!string.IsNullOrEmpty(doc.Status) && _documentStatuses.TryGetValue(doc.Status, out var statusName))
                        {
                            doc.Status = statusName;
                            convertedStatuses++;
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
                    Style = (Style)FindResource("DarkExpanderStyle"),
                    IsExpanded = totalExpanders < 3, // Раскрываем первые 3 месяца
                    Content = CreateDocumentList(group.ToList())
                };

                MonthsPanel.Children.Add(expander);
                totalExpanders++;

                LogHelper.WriteLog("PopulateDocumentsGroupedByMonth",
                    $"Добавлен Expander для {group.Key} с {group.Count()} документами");
            }

            LogHelper.WriteLog("PopulateDocumentsGroupedByMonth",
                $"Добавлено всего Expander'ов: {totalExpanders}");

            // Если документов нет, показываем сообщение
            if (totalExpanders == 0)
            {
                var noDataText = new TextBlock
                {
                    Text = "Документы не найдены",
                    Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)),
                    FontSize = 16,
                    FontStyle = FontStyles.Italic,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                };
                MonthsPanel.Children.Add(noDataText);
            }
        }

        private UIElement CreateDocumentList(List<DocumentDto> docs)
        {
            LogHelper.WriteLog("CreateDocumentList", $"Создание DataGrid для {docs.Count} документов");

            var grid = new DataGrid
            {
                Style = (Style)FindResource("DarkDataGridStyle"),
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Height = Math.Min(400, docs.Count * 30 + 40), // Адаптивная высота
                MaxHeight = 600,
                Margin = new Thickness(0, 5, 0, 5),
                ItemsSource = docs
            };

            // Настраиваем стили для колонок и ячеек
            grid.ColumnHeaderStyle = (Style)FindResource("DarkDataGridColumnHeaderStyle");
            grid.CellStyle = (Style)FindResource("DarkDataGridCellStyle");
            grid.RowStyle = (Style)FindResource("DarkDataGridRowStyle");

            // Добавляем колонки
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Получен",
                Binding = new System.Windows.Data.Binding("ReceivedAt"),
                Width = new DataGridLength(1.2, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Документ",
                Binding = new System.Windows.Data.Binding("Type"),
                Width = new DataGridLength(1.5, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Номер",
                Binding = new System.Windows.Data.Binding("Number"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Отправитель",
                Binding = new System.Windows.Data.Binding("SenderName"),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Получатель",
                Binding = new System.Windows.Data.Binding("ReceiverName"),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Статус",
                Binding = new System.Windows.Data.Binding("Status"),
                Width = new DataGridLength(1.2, DataGridLengthUnitType.Star)
            });

            LogHelper.WriteLog("CreateDocumentList", "DataGrid создан с 6 колонками");

            return grid;
        }
    }

    public class DocumentResponse
    {
        public List<DocumentDto> Results { get; set; } = new();
        public bool NextPage
        {
            get; set;
        }
    }

    public class DocumentDto
    {
        public string Number
        {
            get; set;
        }
        public string DocDate
        {
            get; set;
        }
        public string ReceivedAt
        {
            get; set;
        }
        public string Type
        {
            get; set;
        }
        public string Status
        {
            get; set;
        }
        public string SenderInn
        {
            get; set;
        }
        public string SenderName
        {
            get; set;
        }
        public string ReceiverInn
        {
            get; set;
        }
        public string ReceiverName
        {
            get; set;
        }
        public string DownloadDesc
        {
            get; set;
        }
        public string ProductGroup
        {
            get; set;
        }
    }
}