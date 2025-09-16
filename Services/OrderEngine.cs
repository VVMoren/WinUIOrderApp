// Services/OrderEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WinUIOrderApp.Models;

namespace WinUIOrderApp.Services
{
    public class OrderEngine
    {
        private readonly string[] _allowedCreators;
        private readonly string[] _priorityInn;
        private readonly string[] _excludeInnStage2;
        private readonly string _outDir;

        public OrderEngine(string[] allowedCreators, string[] priorityInn, string[] excludeInnStage2, string outDir)
        {
            _allowedCreators = allowedCreators ?? Array.Empty<string>();
            _priorityInn = priorityInn ?? Array.Empty<string>();
            _excludeInnStage2 = excludeInnStage2 ?? Array.Empty<string>();
            _outDir = outDir ?? throw new ArgumentNullException(nameof(outDir));
        }

        public List<CisRow> BuildOrder(IEnumerable<CisRow> baseRows, Dictionary<(string name, string ip), int> map, HashSet<string> used)
        {
            var selected = new List<CisRow>();
            if (baseRows == null || map == null) return selected;

            foreach (var kv in map)
            {
                var need = kv.Value;
                var name = kv.Key.name ?? string.Empty;
                var ip = kv.Key.ip ?? string.Empty;

                // Отбираем пул кандидатов по имени, создателю, уникальности CIS и (опционально) ip
                var pool = baseRows.Where(r =>
                        string.Equals(r.Name ?? "", name, StringComparison.Ordinal)
                        && (_allowedCreators.Length == 0 || _allowedCreators.Contains(r.Created))
                        && !used.Contains(r.Cis)
                        && (string.IsNullOrEmpty(ip) || string.Equals(r.Ip ?? "", ip, StringComparison.Ordinal))
                    )
                    .ToList();

                // Сначала берем по приоритетным ИНН (stage1)
                var stage1 = pool.Where(r => _priorityInn.Contains(r.Inn)).Take(need).ToList();
                int remain = need - stage1.Count;

                // Затем добираем из остальных (кроме исключённых INN)
                var stage2 = remain > 0
                    ? pool.Where(r => !_excludeInnStage2.Contains(r.Inn)).Except(stage1).Take(remain).ToList()
                    : new List<CisRow>();

                foreach (var r in stage1.Concat(stage2))
                {
                    // отмечаем CIS как использованный и добавляем в выбор
                    used.Add(r.Cis);
                    selected.Add(r);
                }
            }

            return selected;
        }

        public string SaveOrder(IEnumerable<CisRow> rows)
        {
            Directory.CreateDirectory(_outDir);
            var count = rows?.Count() ?? 0;
            var path = Path.Combine(_outDir, $"cis_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{count}.txt");

            // Пишем по одному CIS в строку; защищаем от null
            File.WriteAllLines(path, rows?.Select(r => r.Cis ?? "") ?? Array.Empty<string>());

            return path;
        }
    }
}
