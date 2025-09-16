using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using markapp.Helpers;

namespace markapp.Services
{
    public static class MatcherService
    {
        public static void MatchSetsAndInserts(string setsPath, string insertsPath, string outputDir, IProgress<int> progress = null)
        {
            LogHelper.WriteLog("Сопоставление", $"▶ Начато сопоставление.\nНаборы: {setsPath}\nВложения: {insertsPath}");

            var originalSets = File.ReadAllLines(setsPath).ToList();
            var originalInserts = File.ReadAllLines(insertsPath).ToList();

            var setGroups = GroupByGtin(originalSets, isSet: true);
            var insertGroups = GroupByGtin(originalInserts, isSet: false);

            var usedInserts = new HashSet<string>();
            int wave = 1;

            while (setGroups.Count > 0 && insertGroups.Count > 0)
            {
                var usedSetsInWave = new List<string>();
                var usedInsertsInWave = new List<string>();
                var matchedGroups = new List<string>();

                foreach (var setGroup in setGroups.OrderByDescending(g => g.Value.Count).ToList())
                {
                    string setGtin = setGroup.Key;
                    var setItems = setGroup.Value;

                    // Найти лучшую подходящую группу вложений
                    var bestInsertGroup = insertGroups
                        .OrderByDescending(g => g.Value.Count)
                        .FirstOrDefault();

                    if (bestInsertGroup.Value == null || bestInsertGroup.Value.Count == 0)
                        continue;

                    int pairsToTake = Math.Min(setItems.Count, bestInsertGroup.Value.Count);

                    var matchedPairs = setItems
                        .Take(pairsToTake)
                        .Zip(bestInsertGroup.Value.Take(pairsToTake), (s, i) => new { Set = s, Insert = i })
                        .ToList();

                    foreach (var pair in matchedPairs)
                    {
                        usedSetsInWave.Add(pair.Set);
                        usedInsertsInWave.Add(pair.Insert);
                        usedInserts.Add(pair.Insert);
                    }

                    // Удаляем использованные коды из групп
                    setGroup.Value.RemoveAll(usedSetsInWave.Contains);
                    bestInsertGroup.Value.RemoveAll(usedInsertsInWave.Contains);

                    if (setGroup.Value.Count == 0)
                        setGroups.Remove(setGtin);

                    if (bestInsertGroup.Value.Count == 0)
                        insertGroups.Remove(bestInsertGroup.Key);

                    matchedGroups.Add($"{setGtin} ← {bestInsertGroup.Key}");

                    progress?.Report((int)(100.0 * usedInserts.Count / originalInserts.Count));
                }

                if (usedSetsInWave.Count > 0 && usedInsertsInWave.Count > 0)
                {
                    SaveWaveFiles(outputDir, wave, usedSetsInWave, usedInsertsInWave, matchedGroups);

                    LogHelper.WriteLog("Сопоставление", $"Волна {wave} завершена.\nСопоставлено наборов: {usedSetsInWave.Count}\nСопоставлено вложений: {usedInsertsInWave.Count}");

                    wave++;
                }
                else
                {
                    break;
                }
            }

            // Сохраняем остаток вложений
            var remainingInserts = originalInserts.Where(i => !usedInserts.Contains(i)).ToList();
            File.WriteAllLines(insertsPath, remainingInserts);

            LogHelper.WriteLog("Сопоставление", $"✅ Сопоставление завершено.\nВсего использовано вложений: {usedInserts.Count} из {originalInserts.Count}\nОсталось: {originalInserts.Count - usedInserts.Count}");
        }

        private static Dictionary<string, List<string>> GroupByGtin(List<string> lines, bool isSet)
        {
            var dict = new Dictionary<string, List<string>>();

            foreach (var line in lines)
            {
                string gtin = isSet ? ExtractSetGtin(line) : ExtractInsertGtin(line);
                if (string.IsNullOrEmpty(gtin)) continue;

                if (!dict.ContainsKey(gtin))
                    dict[gtin] = new List<string>();

                dict[gtin].Add(line);
            }

            return dict;
        }

        public static string ExtractSetGtin(string line)
        {
            var match = Regex.Match(line, @"01(\d{14})21");
            return match.Success ? match.Groups[1].Value : null;
        }

        public static string ExtractInsertGtin(string line)
        {
            var match = Regex.Match(line, @"(04\d{12})");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static void SaveWaveFiles(string baseFolder, int wave, List<string> sets, List<string> inserts, List<string> groups)
        {
            var waveDir = Path.Combine(baseFolder, $"волна_{wave}");
            Directory.CreateDirectory(waveDir);

            File.WriteAllLines(Path.Combine(waveDir, $"наборы_волна_{wave}.txt"), sets);
            File.WriteAllLines(Path.Combine(waveDir, $"вложения_волна_{wave}.txt"), inserts);
            File.WriteAllLines(Path.Combine(waveDir, $"группы_волна_{wave}.txt"), groups);

            LogHelper.WriteLog("Сопоставление", $"📁 Волна {wave} сохранена в: {waveDir}");
        }

        public static bool HasEnoughInserts(string setsPath, string insertsPath)
        {
            var setGtinCount = File.ReadLines(setsPath)
                .Select(ExtractSetGtin)
                .Where(gtin => gtin != null)
                .GroupBy(g => g)
                .ToDictionary(g => g.Key, g => g.Count());

            int totalRequired = setGtinCount.Values.Sum();

            var insertsAvailable = File.ReadLines(insertsPath)
                .Select(ExtractInsertGtin)
                .Where(gtin => gtin != null)
                .ToList();

            return insertsAvailable.Count >= totalRequired;
        }
    }
}
