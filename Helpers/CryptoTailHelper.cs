using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WinUIOrderApp.Helpers
{
    public static class CryptoTailHelper
    {
        // Загружает все KM (строки) из txt файлов папки
        public static List<string> LoadCodesFromFolder(string folder)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return result;

            foreach (var file in Directory.GetFiles(folder, "*.txt", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var lines = File.ReadAllLines(file);
                    foreach (var line in lines)
                    {
                        var trimmed = (line ?? string.Empty).Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                            result.Add(trimmed);
                    }
                }
                catch { }
            }

            return result.Distinct().ToList();
        }

        // Извлекает КИ (GTIN+Serial) из полного кода маркировки по правилам (первые 14 цифр GTIN и последовательный серийный номер после них)
        // Для упрощения реализовано: берем первые 14 цифр как GTIN, затем следующие N символов до первого символа разделителя или конца строки как серийный номер
        public static (string gtin, string ki) ExtractKi(string fullCode)
        {
            if (string.IsNullOrEmpty(fullCode))
                return (string.Empty, string.Empty);

            // Удалим возможные пробелы
            var s = fullCode.Trim();

            // Ищем первые 14 цифр подряд
            var digits = new string(s.Where(char.IsDigit).ToArray());
            if (digits.Length < 14)
                return (string.Empty, string.Empty);

            var gtin = digits.Substring(0, 14);

            // После GTIN в исходной строке ищем позицию GTIN и берем следующий сегмент как серийный номер
            var idx = s.IndexOf(gtin);
            if (idx == -1)
                return (gtin, string.Empty);

            var after = s.Substring(idx + gtin.Length);
            // серийный номер: берем до первого разделителя (например '.', '-', ' ', '|')
            var separators = new[] { '.', '-', ' ', '|', ':' };
            var endIdx = after.IndexOfAny(separators);
            var serial = endIdx == -1 ? after : after.Substring(0, endIdx);
            serial = new string(serial.Where(c => !char.IsWhiteSpace(c)).ToArray());

            var ki = gtin + serial;
            return (gtin, ki);
        }
    }
}
