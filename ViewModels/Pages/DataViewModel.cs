// WinUIOrderApp/ViewModels/Pages/DataViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.Sqlite;
using WinUIOrderApp.Helpers;
using WinUIOrderApp.Models;

namespace WinUIOrderApp.ViewModels.Pages
{
    public class DataViewModel
    {
        // внутреннее кэш-хранилище (частичный sample, чтобы не тянуть всю БД в память)
        private readonly List<CisRow> _allRows = new();
        private readonly List<(string Field, string Value)> _include = new();
        private readonly List<(string Field, string Value)> _exclude = new();
        private readonly Dictionary<string, string> _qtyStore = new();

        // биндинги для UI
        public ObservableCollection<CisRow> FilteredRows { get; } = new();
        public ObservableCollection<SummaryItem> SummaryItems { get; } = new();

        // коллекция "чипов" активных фильтров для отображения и удаления
        public ObservableCollection<FilterTag> ActiveFilters { get; } = new();

        public DataViewModel()
        {
        }

        public void LoadAll()
        {
            LoadAllAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Загружает summary (агрегацию name+ip) и небольшой sample детальных строк.
        /// Работает асинхронно и обновляет коллекции в UI-потоке.
        /// </summary>
        public async Task LoadAllAsync(CancellationToken cancellationToken = default)
        {
            // если БД отсутствует — очистим коллекции и выйдем
            if (!System.IO.File.Exists(AppDbConfig.DbPath))
            {
                // обновляем UI-поток без ConfigureAwait — DispatcherOperation не поддерживает ConfigureAwait
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _allRows.Clear();
                    FilteredRows.Clear();
                    SummaryItems.Clear();
                });
                return;
            }

            try
            {
                // 1) Собираем summary из БД (Name + Ip + Creator + CountByIp + TotalByName)
                var summaryList = await Task.Run(() =>
                {
                    var list = new List<(string Name, string Ip, string Creator, int TotalByName, int CountByIp)>();
                    using var conn = new SqliteConnection($"Data Source={AppDbConfig.DbPath}");
                    conn.Open();

                    using var cmd = conn.CreateCommand();
                    // LIMIT защищает UI от огромной нагрузки; при необходимости можно увеличить/убрать
                    cmd.CommandText = @"
                        SELECT Name, IFNULL(Ip,'') AS Ip, MIN(Created) AS Creator, COUNT(*) AS CountByIp,
                               (SELECT COUNT(*) FROM Items t2 WHERE t2.Name = t1.Name) AS TotalByName
                        FROM Items t1
                        GROUP BY Name, Ip
                        ORDER BY Name, Ip
                        LIMIT 20000;";

                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        var name = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);
                        var ip = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
                        var creator = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2);
                        var countByIp = rdr.IsDBNull(3) ? 0 : rdr.GetInt32(3);
                        var totalByName = rdr.IsDBNull(4) ? countByIp : rdr.GetInt32(4);

                        list.Add((name, ip, creator, totalByName, countByIp));
                    }

                    return list;
                }, cancellationToken).ConfigureAwait(false);

                // 2) Берём небольшой sample детальных строк (чтобы показать в правой части/FilteredRows)
                var sampleRows = await Task.Run(() =>
                {
                    var tmp = new List<CisRow>();
                    using var conn = new SqliteConnection($"Data Source={AppDbConfig.DbPath}");
                    conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT Cis,Ki,Gtin,Name,Status,Created,SetCode,Ip,Inn FROM Items LIMIT 1000;";

                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        var row = new CisRow
                        {
                            Cis = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0),
                            Ki = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1),
                            Gtin = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2),
                            Name = rdr.IsDBNull(3) ? string.Empty : rdr.GetString(3),
                            Status = rdr.IsDBNull(4) ? string.Empty : rdr.GetString(4),
                            Created = rdr.IsDBNull(5) ? string.Empty : rdr.GetString(5),
                            SetCode = rdr.IsDBNull(6) ? string.Empty : rdr.GetString(6),
                            Ip = rdr.IsDBNull(7) ? string.Empty : rdr.GetString(7),
                            Inn = rdr.IsDBNull(8) ? string.Empty : rdr.GetString(8)
                        };
                        tmp.Add(row);
                    }

                    return tmp;
                }, cancellationToken).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested) return;

                // 3) Обновляем коллекции в UI-потоке
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // internal cache (sample)
                    _allRows.Clear();
                    _allRows.AddRange(sampleRows);

                    // обновляем детальные видимые строки (ограничиваем первые 1000 элементов)
                    FilteredRows.Clear();
                    foreach (var r in sampleRows)
                        FilteredRows.Add(r);

                    // обновляем summary items
                    SummaryItems.Clear();
                    foreach (var s in summaryList)
                    {
                        var si = new SummaryItem
                        {
                            Name = s.Name,
                            Ip = s.Ip,
                            Creator = s.Creator,
                            TotalByName = s.TotalByName,
                            CountByIp = s.CountByIp,
                            Quantity = _qtyStore.TryGetValue($"{s.Name}||{s.Ip}", out var q) ? q : string.Empty
                        };

                        // подписка на изменение Quantity — чтобы сохранять в _qtyStore
                        si.PropertyChanged += (_, __) =>
                        {
                            _qtyStore[si.Key] = si.Quantity ?? string.Empty;
                        };

                        SummaryItems.Add(si);
                    }
                });

            }
            catch (OperationCanceledException)
            {
                // отмена — тихо
            }
            catch (Exception ex)
            {
                // Логирование удобно добавить здесь; пока — безопасно показать сообщение (если нужно)
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        // ---------------- Filters ----------------
        private (string whereSql, List<SqliteParameter> parameters) BuildWhereClause()
        {
            var parts = new List<string>();
            var parameters = new List<SqliteParameter>();
            int idx = 0;

            string ColFor(string field) => field switch
            {
                "name" => "Name",
                "ownerName" => "Ip",
                "ownerInn" => "Inn",
                "producerName" => "Created",
                _ => field
            };

            // include: группа по полю, внутри групп -> OR
            var includeGroups = _include.GroupBy(x => x.Field);
            foreach (var g in includeGroups)
            {
                var field = g.Key;
                var values = g.Select(x => x.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                if (!values.Any()) continue;

                if (field == "name")
                {
                    // Name LIKE '%val%'
                    var ors = new List<string>();
                    foreach (var v in values)
                    {
                        var pName = $"@p{idx++}";
                        parameters.Add(new SqliteParameter(pName, $"%{v}%"));
                        ors.Add($"Name LIKE {pName}");
                    }
                    parts.Add("(" + string.Join(" OR ", ors) + ")");
                }
                else
                {
                    // Col IN (@p0,@p1...)
                    var names = new List<string>();
                    foreach (var v in values)
                    {
                        var pName = $"@p{idx++}";
                        parameters.Add(new SqliteParameter(pName, v));
                        names.Add(pName);
                    }
                    var col = ColFor(field);
                    parts.Add($"{col} IN ({string.Join(",", names)})");
                }
            }

            // exclude: группы — объединяем через AND (т.е. каждый exclude ограничивает)
            var excludeGroups = _exclude.GroupBy(x => x.Field);
            foreach (var g in excludeGroups)
            {
                var field = g.Key;
                var values = g.Select(x => x.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                if (!values.Any()) continue;

                if (field == "name")
                {
                    var ors = new List<string>();
                    foreach (var v in values)
                    {
                        var pName = $"@p{idx++}";
                        parameters.Add(new SqliteParameter(pName, $"%{v}%"));
                        ors.Add($"Name LIKE {pName}");
                    }
                    // exclude name => NOT (Name LIKE p1 OR Name LIKE p2)
                    parts.Add("NOT (" + string.Join(" OR ", ors) + ")");
                }
                else
                {
                    var names = new List<string>();
                    foreach (var v in values)
                    {
                        var pName = $"@p{idx++}";
                        parameters.Add(new SqliteParameter(pName, v));
                        names.Add(pName);
                    }
                    var col = ColFor(field);
                    parts.Add($"{col} NOT IN ({string.Join(",", names)})");
                }
            }

            var whereSql = parts.Any() ? "WHERE " + string.Join(" AND ", parts) : string.Empty;
            return (whereSql, parameters);
        }



        /// <summary>Добавить фильтр include (включить строки с полем=value)</summary>
        public void AddIncludeFilter(string field, string value)
        {
            if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(value)) return;
            _include.Add((field, value));
            ActiveFilters.Add(new FilterTag { Field = field, Value = value, IsInclude = true });
            ApplyFilters();
            _ = BuildAndPopulateSummaryAsync(); // обновление summary асинхронно
        }

        /// <summary>Добавить фильтр exclude (исключить строки с полем=value)</summary>
        public void AddExcludeFilter(string field, string value)
        {
            if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(value)) return;
            _exclude.Add((field, value));
            ActiveFilters.Add(new FilterTag { Field = field, Value = value, IsInclude = false });
            ApplyFilters();
            _ = BuildAndPopulateSummaryAsync();
        }

        /// <summary>Удалить фильтр (чип) — из видимых и из внутренних списков</summary>
        public void RemoveFilter(FilterTag tag)
        {
            if (tag == null) return;

            var existing = ActiveFilters.FirstOrDefault(x => x.Key == tag.Key);
            if (existing != null)
                ActiveFilters.Remove(existing);

            if (tag.IsInclude)
            {
                var found = _include.FirstOrDefault(x => x.Field == tag.Field && x.Value == tag.Value);
                if (found != default) _include.Remove(found);
            }
            else
            {
                var found = _exclude.FirstOrDefault(x => x.Field == tag.Field && x.Value == tag.Value);
                if (found != default) _exclude.Remove(found);
            }

            ApplyFilters();
            _ = BuildAndPopulateSummaryAsync();
        }

        /// <summary>Применяет фильтры к внутреннему sample _allRows и обновляет FilteredRows</summary>
        public void ApplyFilters()
        {
            IEnumerable<CisRow> filtered = _allRows;

            if (_include.Any())
            {
                var grouped = _include.GroupBy(f => f.Field);
                foreach (var group in grouped)
                {
                    if (group.Key == "name")
                    {
                        filtered = filtered.Where(r => group.Any(f => (r.Name ?? string.Empty).IndexOf(f.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0));
                    }
                    else
                    {
                        filtered = filtered.Where(r => group.Any(f => GetFieldValue(r, f.Field) == f.Value));
                    }
                }
            }

            if (_exclude.Any())
            {
                var grouped = _exclude.GroupBy(f => f.Field);
                foreach (var group in grouped)
                {
                    if (group.Key == "name")
                    {
                        filtered = filtered.Where(r => !group.Any(f => (r.Name ?? string.Empty).IndexOf(f.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0));
                    }
                    else
                    {
                        filtered = filtered.Where(r => !group.Any(f => GetFieldValue(r, f.Field) == f.Value));
                    }
                }
            }

            UpdateFilteredRows(filtered);
        }

        private static string GetFieldValue(CisRow r, string field) => field switch
        {
            "create" => r.Created ?? string.Empty,
            "ip" => r.Ip ?? string.Empty,
            "inn" => r.Inn ?? string.Empty,
            _ => string.Empty
        };

        /// <summary>Обновляет FilteredRows (ограничивает до первых 1000 элементов, чтобы UI не подвисал)</summary>
        private void UpdateFilteredRows(IEnumerable<CisRow> rows)
        {
            FilteredRows.Clear();
            foreach (var r in rows.Take(1000))
                FilteredRows.Add(r);
        }

        /// <summary>Собирает карту заказа (name, ip) -> qty по введённым значениям</summary>
        public Dictionary<(string name, string ip), int> GetOrderMapFromSummary()
        {
            var map = new Dictionary<(string name, string ip), int>();
            foreach (var si in SummaryItems)
            {
                if (int.TryParse(si.Quantity?.Trim(), out var v) && v > 0)
                {
                    map[(si.Name, si.Ip)] = v;
                }
            }
            return map;
        }

        /// <summary>
        /// Отдельный (внешний) вызов для пересборки summary при необходимости.
        /// Здесь — заглушка, т.к. LoadAllAsync уже заполняет summary.
        /// При желании можно перенести SQL-агрегацию сюда.
        /// </summary>
        public async Task BuildAndPopulateSummaryAsync(CancellationToken cancellationToken = default)
        {
            // проверка наличия БД
            if (!System.IO.File.Exists(AppDbConfig.DbPath))
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SummaryItems.Clear();
                    FilteredRows.Clear();
                });
                return;
            }

            try
            {
                // 1) конструируем WHERE и параметры на основе include/exclude
                var (whereSql, parameters) = BuildWhereClause();

                // 2) Получаем totals per name (учитывая фильтры)
                var totals = await Task.Run(() =>
                {
                    var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    using var conn = new SqliteConnection($"Data Source={AppDbConfig.DbPath}");
                    conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"SELECT Name, COUNT(*) AS Cnt FROM Items {whereSql} GROUP BY Name;";
                    foreach (var p in parameters) cmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));

                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        var name = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);
                        var cnt = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1);
                        if (!dict.ContainsKey(name)) dict[name] = cnt;
                    }

                    return dict;
                }, cancellationToken).ConfigureAwait(false);

                // 3) Получаем grouping by name+ip (summary rows)
                var summaryList = await Task.Run(() =>
                {
                    var list = new List<(string Name, string Ip, string Creator, int CountByIp)>();
                    using var conn = new SqliteConnection($"Data Source={AppDbConfig.DbPath}");
                    conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $@"
                SELECT Name, IFNULL(Ip,'') AS Ip, MIN(Created) AS Creator, COUNT(*) AS CountByIp
                FROM Items
                {whereSql}
                GROUP BY Name, Ip
                ORDER BY Name, Ip
                LIMIT 20000;";
                    foreach (var p in parameters) cmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));

                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        var name = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);
                        var ip = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
                        var creator = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2);
                        var cntIp = rdr.IsDBNull(3) ? 0 : rdr.GetInt32(3);
                        list.Add((name, ip, creator, cntIp));
                    }

                    return list;
                }, cancellationToken).ConfigureAwait(false);

                // 4) Получаем sample детальных строк с тем же WHERE (LIMIT для производительности)
                var sampleRows = await Task.Run(() =>
                {
                    var tmp = new List<CisRow>();
                    using var conn = new SqliteConnection($"Data Source={AppDbConfig.DbPath}");
                    conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"SELECT Cis,Ki,Gtin,Name,Status,Created,SetCode,Ip,Inn FROM Items {whereSql} LIMIT 1000;";
                    foreach (var p in parameters) cmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));

                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        tmp.Add(new CisRow
                        {
                            Cis = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0),
                            Ki = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1),
                            Gtin = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2),
                            Name = rdr.IsDBNull(3) ? string.Empty : rdr.GetString(3),
                            Status = rdr.IsDBNull(4) ? string.Empty : rdr.GetString(4),
                            Created = rdr.IsDBNull(5) ? string.Empty : rdr.GetString(5),
                            SetCode = rdr.IsDBNull(6) ? string.Empty : rdr.GetString(6),
                            Ip = rdr.IsDBNull(7) ? string.Empty : rdr.GetString(7),
                            Inn = rdr.IsDBNull(8) ? string.Empty : rdr.GetString(8)
                        });
                    }

                    return tmp;
                }, cancellationToken).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested) return;

                // 5) Обновляем UI-поток: SummaryItems и FilteredRows
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // обновляем sample cache и FilteredRows
                    _allRows.Clear();
                    _allRows.AddRange(sampleRows);

                    FilteredRows.Clear();
                    foreach (var r in sampleRows)
                        FilteredRows.Add(r);

                    // обновляем summary + total lookup
                    SummaryItems.Clear();
                    foreach (var s in summaryList)
                    {
                        var si = new SummaryItem
                        {
                            Name = s.Name,
                            Ip = s.Ip,
                            Creator = s.Creator,
                            CountByIp = s.CountByIp,
                            TotalByName = totals.TryGetValue(s.Name, out var t) ? t : s.CountByIp,
                            Quantity = _qtyStore.TryGetValue($"{s.Name}||{s.Ip}", out var q) ? q : string.Empty
                        };

                        si.PropertyChanged += (_, __) => { _qtyStore[si.Key] = si.Quantity ?? string.Empty; };

                        SummaryItems.Add(si);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // cancelled
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Ошибка при пересборке summary: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

    }
}
