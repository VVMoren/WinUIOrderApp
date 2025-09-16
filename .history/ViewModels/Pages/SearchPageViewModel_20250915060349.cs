using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinUIOrderApp.Helpers;
using WinUIOrderApp.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

public record ColDef(string Key, string Header, bool WidthStar = true);

public class DynamicRow : ObservableObject
{
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    private bool _isSelected;

    /// <summary>Контейнер значений. Ключи = технические ключи столбцов (Key), значения = отформатированные строки.</summary>
    public Dictionary<string, string> Cells { get; } = new();
}

public class SearchMode
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public override string ToString() => Title;
}

public partial class SearchPageViewModel : ObservableObject
{
    // === Публичные привязки ===
    [ObservableProperty] private string? queryText;
    [ObservableProperty] private SearchMode? selectedMode;

    // Новые свойства для индикатора и блокировки элементов
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private double progress;

    // Вычисляемое свойство: элементы активны, если не занято
    public bool IsNotBusy => !IsBusy;

    public ObservableCollection<SearchMode> Modes { get; } = new()
    {
        new SearchMode{ Id="name", Title="Наименование"},
        new SearchMode{ Id="gtin", Title="ГТИН"},
        new SearchMode{ Id="inn",  Title="ИНН"},
    };

    public ObservableCollection<DynamicRow> Rows { get; } = new();

    public IRelayCommand SearchCommand { get; }

    // === Внутреннее ===
    private readonly Action<IReadOnlyList<ColDef>> _applyColumns;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private readonly Dictionary<string, string> _innNameCache = new(); // inn -> participant.name

    public SearchPageViewModel(Action<IReadOnlyList<ColDef>> applyColumns)
    {
        _applyColumns = applyColumns;
        SearchCommand = new AsyncRelayCommand(SearchAsync);
        SelectedMode = Modes.First(); // по умолчанию «Наименование»
        LogHelper.WriteLog("SearchPageViewModel.ctor", "ViewModel инициализирован");
    }

    // === HTTP ===
    private HttpClient CreateCrptClient()
    {
        var token = AppState.Instance.Token;
        if (string.IsNullOrWhiteSpace(token))
        {
            LogHelper.WriteLog("CreateCrptClient", "Ошибка: токен не получен");
            throw new InvalidOperationException("Токен не получен. Откройте Настройки и выполните вход в ГИС МТ.");
        }

        var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        LogHelper.WriteLog("CreateCrptClient", "HTTP-клиент создан с авторизацией");
        return http;
    }

    // === Общие утилиты ===
    private static string MsEpochToLocal(long ms)
    {
        try
        {
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().DateTime;
            return dt.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        }
        catch
        {
            LogHelper.WriteLog("MsEpochToLocal", $"Ошибка преобразования времени: {ms}");
            return "";
        }
    }

    private static string ParseDateTimeString(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            LogHelper.WriteLog("ParseDateTimeString", "Пустая строка даты");
            return "";
        }

        // ожидания: "2025-08-01 01:18:32" или ISO
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
            return dt.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        if (DateTimeOffset.TryParse(s, out var dto))
            return dto.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

        LogHelper.WriteLog("ParseDateTimeString", $"Не удалось распарсить дату: {s}");
        return s;
    }

    // CRPT: имя участника по ИНН (кешируется)
    private async Task<string> GetInnNameAsync(HttpClient http, string inn)
    {
        if (string.IsNullOrWhiteSpace(inn))
        {
            LogHelper.WriteLog("GetInnNameAsync", "Пустой ИНН");
            return "";
        }

        if (_innNameCache.TryGetValue(inn, out var cached))
        {
            LogHelper.WriteLog("GetInnNameAsync", $"Имя из кэша для ИНН: {inn}");
            return cached;
        }

        var url = $"https://tobacco.crpt.ru/bff-elk/g/edo-api/api/v1/participants/suggestions?limit=1&query={Uri.EscapeDataString(inn)}";

        try
        {
            LogHelper.WriteLog("GetInnNameAsync", $"Запрос имени для ИНН: {inn}");
            var resp = await http.GetAsync(url);
            resp.EnsureSuccessStatusCode();

            LogHelper.WriteLog("GetInnNameAsync", $"Ответ получен для ИНН: {inn}, статус: {resp.StatusCode}");

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var name = doc.RootElement
                .GetProperty("items")
                .EnumerateArray()
                .FirstOrDefault()
                .GetProperty("participant")
                .GetProperty("name")
                .GetString() ?? "";

            _innNameCache[inn] = name;
            LogHelper.WriteLog("GetInnNameAsync", $"Найдено имя '{name}' для ИНН: {inn}");

            return name;
        }
        catch (Exception ex)
        {
            LogHelper.WriteLog("GetInnNameAsync", $"Ошибка для ИНН {inn}: {ex.Message}");
            return "";
        }
    }

    // === Основное действие ===
    private async Task SearchAsync()
    {
        LogHelper.WriteLog("SearchAsync", "Начало поиска");

        // Блокируем элементы и сбрасываем прогресс
        IsBusy = true;
        Progress = 0;

        try
        {
            Rows.Clear();
            _innNameCache.Clear();
            LogHelper.WriteLog("SearchAsync", "Очищены строки и кэш");

            if (string.IsNullOrWhiteSpace(QueryText))
            {
                LogHelper.WriteLog("SearchAsync", "Пустой запрос - показано сообщение");
                MessageBox.Show("Введите значение в поле поиска.", "Поиск", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (SelectedMode == null)
            {
                LogHelper.WriteLog("SearchAsync", "Режим поиска не выбран");
                return;
            }

            LogHelper.WriteLog("SearchAsync", $"Выбран режим: {SelectedMode.Id}, запрос: {QueryText}");

            switch (SelectedMode.Id)
            {
                case "name":
                    await SearchByNameAsync(QueryText!);
                    break;
                case "gtin":
                    await SearchByGtinAsync(QueryText!);
                    break;
                case "inn":
                    await SearchByInnAsync(QueryText!);
                    break;
            }

            LogHelper.WriteLog("SearchAsync", $"Поиск завершен. Найдено строк: {Rows.Count}");
        }
        catch (Exception ex)
        {
            LogHelper.WriteLog("SearchAsync", $"Критическая ошибка: {ex.Message}");
            MessageBox.Show(ex.Message, "Ошибка поиска", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            // Завершаем прогресс и разблокируем элементы
            Progress = 100;
            IsBusy = false;
            LogHelper.WriteLog("SearchAsync", "Поиск завершен, интерфейс разблокирован");
        }
    }

    // === РЕЖИМ 1: Наименование ===
    private async Task SearchByNameAsync(string text)
    {
        LogHelper.WriteLog("SearchByNameAsync", $"Начало поиска по наименованию: {text}");

        using var http = CreateCrptClient();
        Progress = 10; // начало поиска

        // 1) suggestions -> список GTIN
        var sugUrl = $"https://tobacco.crpt.ru/bff-elk/v1/products/suggestions?limit=1000&text={Uri.EscapeDataString(text)}";

        LogHelper.WriteLog("SearchByNameAsync", $"Запрос suggestions: {sugUrl}");
        var sugResp = await http.GetAsync(sugUrl);
        sugResp.EnsureSuccessStatusCode();

        LogHelper.WriteLog("SearchByNameAsync", $"Suggestions ответ получен, статус: {sugResp.StatusCode}");

        var sugJson = await sugResp.Content.ReadAsStringAsync();
        using var sugDoc = JsonDocument.Parse(sugJson);
        var gtins = sugDoc.RootElement.GetProperty("results")
            .EnumerateArray()
            .Select(x => x.GetProperty("gtin").GetString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .ToList();

        Progress = 30; // получили список GTIN
        LogHelper.WriteLog("SearchByNameAsync", $"Найдено уникальных GTIN: {gtins.Count}");

        if (gtins.Count == 0)
        {
            LogHelper.WriteLog("SearchByNameAsync", "Результатов не найдено");
            _applyColumns(new[]
            {
                new ColDef("info","Нет результатов")
            });
            return;
        }

        // 2) products/list по всем GTIN
        var postUrl = "https://tobacco.crpt.ru/bff-elk/v2/products/list";
        var body = JsonSerializer.Serialize(new { gtin = gtins }, _json);

        LogHelper.WriteLog("SearchByNameAsync", $"Запрос products/list для {gtins.Count} GTIN");
        var resp = await http.PostAsync(postUrl, new StringContent(body, Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();

        LogHelper.WriteLog("SearchByNameAsync", $"Products/list ответ получен, статус: {resp.StatusCode}");

        var listJson = await resp.Content.ReadAsStringAsync();
        using var listDoc = JsonDocument.Parse(listJson);
        var results = listDoc.RootElement.GetProperty("results").EnumerateArray().ToList();
        Progress = 50; // загрузили список товаров

        LogHelper.WriteLog("SearchByNameAsync", $"Получено товаров: {results.Count}");

        // Колонки для режима «Наименование/ГТИН»
        var colDefs = new List<ColDef>
        {
            new("firstSignDate","Дата первой подписи"),
            new("ncCreateDate","Дата НК"),
            new("gtin","ГТИН"),
            new("name","Наим.тов."),
            new("brand","Брэнд"),
            new("innName","<Наименование ИНН>"),
            new("inn","ИНН"),
            new("isSet","Набор")
        };
        _applyColumns(colDefs);
        LogHelper.WriteLog("SearchByNameAsync", "Колонки применены");

        // Заполняем строки; прогресс от 50 до 90 %
        int totalRows = results.Count;
        int processed = 0;

        LogHelper.WriteLog("SearchByNameAsync", $"Начинается обработка {totalRows} строк");

        foreach (var item in results)
        {
            var inn = item.TryGetProperty("inn", out var innEl) ? innEl.GetString() ?? "" : "";
            var innName = await GetInnNameAsync(http, inn);

            var row = new DynamicRow();
            row.Cells["firstSignDate"] = item.TryGetProperty("firstSignDate", out var fsd) ? MsEpochToLocal(fsd.GetInt64()) : "";
            row.Cells["ncCreateDate"] = item.TryGetProperty("ncCreateDate", out var ncd) ? MsEpochToLocal(ncd.GetInt64()) : "";
            row.Cells["gtin"] = item.GetProperty("gtin").GetString() ?? "";
            row.Cells["name"] = item.GetProperty("name").GetString() ?? "";
            row.Cells["brand"] = item.TryGetProperty("brand", out var b) ? b.GetString() ?? "" : "";
            row.Cells["innName"] = innName;
            row.Cells["inn"] = inn;
            row.Cells["isSet"] = item.TryGetProperty("isSet", out var isSet) && isSet.GetBoolean() ? "да" : "нет";

            Rows.Add(row);

            processed++;
            // каждые 20 % обновляем прогресс (50 → 90)
            if (totalRows > 0 && processed % Math.Max(1, totalRows / 4) == 0)
            {
                double percent = 50 + 40.0 * processed / totalRows;
                Progress = percent;
                LogHelper.WriteLog("SearchByNameAsync", $"Обработано {processed}/{totalRows} строк ({percent:F0}%)");
            }
        }

        Progress = 90; // почти завершено; окончание в finally SearchAsync
        LogHelper.WriteLog("SearchByNameAsync", $"Обработка завершена. Добавлено строк: {Rows.Count}");
    }

    // === РЕЖИМ 2: ГТИН ===
    private async Task SearchByGtinAsync(string gtin)
    {
        LogHelper.WriteLog("SearchByGtinAsync", $"Начало поиска по ГТИН: {gtin}");

        using var http = CreateCrptClient();
        Progress = 10;

        var url = "https://tobacco.crpt.ru/bff-elk/v2/products/list";
        var body = JsonSerializer.Serialize(new { gtin = new[] { gtin } }, _json);

        LogHelper.WriteLog("SearchByGtinAsync", $"Запрос products/list для ГТИН: {gtin}");
        var resp = await http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();

        LogHelper.WriteLog("SearchByGtinAsync", $"Ответ получен, статус: {resp.StatusCode}");

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var results = doc.RootElement.GetProperty("results").EnumerateArray().ToList();
        Progress = 50;

        LogHelper.WriteLog("SearchByGtinAsync", $"Получено товаров: {results.Count}");

        var colDefs = new List<ColDef>
        {
            new("firstSignDate","Дата первой подписи"),
            new("ncCreateDate","Дата НК"),
            new("gtin","ГТИН"),
            new("name","Наим.тов."),
            new("brand","Брэнд"),
            new("innName","<Наименование ИНН>"),
            new("inn","ИНН"),
            new("isSet","Набор")
        };
        _applyColumns(colDefs);
        LogHelper.WriteLog("SearchByGtinAsync", "Колонки применены");

        int totalRows = results.Count;
        int processed = 0;

        LogHelper.WriteLog("SearchByGtinAsync", $"Начинается обработка {totalRows} строк");

        foreach (var item in results)
        {
            var inn = item.TryGetProperty("inn", out var innEl) ? innEl.GetString() ?? "" : "";
            var innName = await GetInnNameAsync(http, inn);

            var row = new DynamicRow();
            row.Cells["firstSignDate"] = item.TryGetProperty("firstSignDate", out var fsd) ? MsEpochToLocal(fsd.GetInt64()) : "";
            row.Cells["ncCreateDate"] = item.TryGetProperty("ncCreateDate", out var ncd) ? MsEpochToLocal(ncd.GetInt64()) : "";
            row.Cells["gtin"] = item.GetProperty("gtin").GetString() ?? "";
            row.Cells["name"] = item.GetProperty("name").GetString() ?? "";
            row.Cells["brand"] = item.TryGetProperty("brand", out var b) ? b.GetString() ?? "" : "";
            row.Cells["innName"] = innName;
            row.Cells["inn"] = inn;
            row.Cells["isSet"] = item.TryGetProperty("isSet", out var isSet) && isSet.GetBoolean() ? "да" : "нет";

            Rows.Add(row);

            processed++;
            if (totalRows > 0 && processed % Math.Max(1, totalRows / 4) == 0)
            {
                double percent = 50 + 40.0 * processed / totalRows;
                Progress = percent;
                LogHelper.WriteLog("SearchByGtinAsync", $"Обработано {processed}/{totalRows} строк ({percent:F0}%)");
            }
        }

        Progress = 90;
        LogHelper.WriteLog("SearchByGtinAsync", $"Обработка завершена. Добавлено строк: {Rows.Count}");
    }

    // === РЕЖИМ 3: ИНН (Нац.каталог) ===
    private async Task SearchByInnAsync(string inn)
    {
        LogHelper.WriteLog("SearchByInnAsync", $"Начало поиска по ИНН: {inn}");

        using var http = CreateCrptClient();
        Progress = 10;

        // 1) /v3/etagslist → считаем total и идём «с хвоста»
        string baseUrl = "https://апи.национальный-каталог.рф/v3/etagslist";

        // получим first page для total
        LogHelper.WriteLog("SearchByInnAsync", $"Запрос etagslist для ИНН: {inn}");
        var first = await http.GetAsync($"{baseUrl}?owner_inn={Uri.EscapeDataString(inn)}&offset=0");
        first.EnsureSuccessStatusCode();

        LogHelper.WriteLog("SearchByInnAsync", $"Etagslist ответ получен, статус: {first.StatusCode}");

        var firstJson = await first.Content.ReadAsStringAsync();
        using var firstDoc = JsonDocument.Parse(firstJson);
        int total = firstDoc.RootElement.GetProperty("result").GetProperty("total").GetInt32();

        LogHelper.WriteLog("SearchByInnAsync", $"Всего записей найдено: {total}");

        int limit = Math.Min(1000, total);
        int offset = Math.Max(total - 100, 0);
        int minOffset = Math.Max(total - limit, 0);

        // Собираем good_id
        var goodsIds = new HashSet<string>();
        LogHelper.WriteLog("SearchByInnAsync", $"Начинается сбор good_id с offset {offset} до {minOffset}");

        while (offset >= minOffset)
        {
            LogHelper.WriteLog("SearchByInnAsync", $"Запрос etagslist с offset: {offset}");
            var resp = await http.GetAsync($"{baseUrl}?owner_inn={Uri.EscapeDataString(inn)}&offset={offset}");
            resp.EnsureSuccessStatusCode();

            using var d = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            int count = 0;
            foreach (var g in d.RootElement.GetProperty("result").GetProperty("goods").EnumerateArray())
            {
                // Сохраняем good_id сразу в коллекцию
                if (g.TryGetProperty("good_id", out var gid))
                {
                    goodsIds.Add(gid.GetInt64().ToString());
                    count++;
                }
            }

            LogHelper.WriteLog("SearchByInnAsync", $"Добавлено {count} good_id с offset {offset}");
            offset -= 100;
            await Task.Delay(300);
        }

        Progress = 30; // получили список good_id
        LogHelper.WriteLog("SearchByInnAsync", $"Собрано уникальных good_id: {goodsIds.Count}");

        // берём только нужное количество ID
        var goodIds = goodsIds.Take(limit).ToList();
        LogHelper.WriteLog("SearchByInnAsync", $"Будет обработано good_id: {goodIds.Count}");

        // 2) /v3/product по 20 id
        var products = new List<JsonElement>();
        int totalChunks = (int)Math.Ceiling(goodIds.Count / 20.0);
        int processedChunks = 0;

        LogHelper.WriteLog("SearchByInnAsync", $"Начинается загрузка продуктов ({totalChunks} чанков)");

        for (int i = 0; i < goodIds.Count; i += 20)
        {
            var chunk = goodIds.Skip(i).Take(20);
            var url = $"https://апи.национальный-каталог.рф/v3/product?good_ids={Uri.EscapeDataString(string.Join(";", chunk))}";

            LogHelper.WriteLog("SearchByInnAsync", $"Запрос продуктов для чанка {processedChunks + 1}/{totalChunks}");

            for (int attempt = 1; attempt <= 2; attempt++)
            {
                var r = await http.GetAsync(url);
                if ((int)r.StatusCode == 429 || (int)r.StatusCode >= 500)
                {
                    LogHelper.WriteLog("SearchByInnAsync", $"Попытка {attempt}: Ошибка {r.StatusCode}, пауза 1 сек");
                    if (attempt == 1) { await Task.Delay(1000); continue; }
                }
                r.EnsureSuccessStatusCode();

                using var dj = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
                int added = 0;
                foreach (var elem in dj.RootElement.GetProperty("result").EnumerateArray())
                {
                    products.Add(elem.Clone()); // Clone() создаёт независимую копию JsonElement
                    added++;
                }

                LogHelper.WriteLog("SearchByInnAsync", $"Добавлено {added} продуктов из чанка");
                break;
            }

            processedChunks++;
            Progress = 30 + 30.0 * processedChunks / totalChunks; // 30 → 60
            await Task.Delay(400);
        }

        Progress = 60; // завершили загрузку продуктов
        LogHelper.WriteLog("SearchByInnAsync", $"Загружено всего продуктов: {products.Count}");

        // 3) Колонки для режима ИНН
        var colDefs = new List<ColDef>
        {
            new("create_date","Дата создания"),
            new("update_date", "Дата обновления"),
            new("first_sign_date", "Дата первой подписи"),
            new("value", "ГТИН"),
            new("good_name", "Наим.тов."),
            new("brand_name", "Бренд")
        };
        _applyColumns(colDefs);
        LogHelper.WriteLog("SearchByInnAsync", "Колонки применены");

        // 4) Заполнение
        int totalRows = products.Count;
        int processedRows = 0;

        LogHelper.WriteLog("SearchByInnAsync", $"Начинается обработка {totalRows} продуктов");

        foreach (var p in products)
        {
            var row = new DynamicRow();

            // value (GTIN) берём из identified_by[type=gtin]
            string gtin = "";
            if (p.TryGetProperty("identified_by", out var arr))
            {
                foreach (var id in arr.EnumerateArray())
                {
                    if (id.GetProperty("type").GetString() == "gtin")
                    {
                        gtin = id.GetProperty("value").GetString() ?? "";
                        break;
                    }
                }
            }

            row.Cells["create_date"] = ParseDateTimeString(p.TryGetProperty("create_date", out var cd) ? cd.GetString() : null);
            row.Cells["update_date"] = ParseDateTimeString(p.TryGetProperty("update_date", out var ud) ? ud.GetString() : null);
            row.Cells["first_sign_date"] = ParseDateTimeString(p.TryGetProperty("first_sign_date", out var fsd) ? fsd.GetString() : null);
            row.Cells["value"] = gtin;
            row.Cells["good_name"] = p.TryGetProperty("good_name", out var gn) ? gn.GetString() ?? "" : "";

            // brand_name сидит либо на корне, либо в attrs (варианты API); используем корень, как в примере
            row.Cells["brand_name"] = p.TryGetProperty("brand_name", out var bn) ? bn.GetString() ?? "" : "";

            Rows.Add(row);

            processedRows++;
            if (totalRows > 0 && processedRows % Math.Max(1, totalRows / 4) == 0)
            {
                double percent = 60 + 30.0 * processedRows / totalRows; // 60 → 90
                Progress = percent;
                LogHelper.WriteLog("SearchByInnAsync", $"Обработано {processedRows}/{totalRows} строк ({percent:F0}%)");
            }
        }

        Progress = 90; // почти завершили; SearchAsync установит 100
        LogHelper.WriteLog("SearchByInnAsync", $"Обработка завершена. Добавлено строк: {Rows.Count}");
    }
}