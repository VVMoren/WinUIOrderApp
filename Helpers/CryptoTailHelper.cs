using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WinUIOrderApp.Helpers
{
    public static class CryptoTailHelper
    {
        // ��������� ��� KM (������) �� txt ������ �����
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

        // ��������� �� (GTIN+Serial) �� ������� ���� ���������� �� �������� (������ 14 ���� GTIN � ���������������� �������� ����� ����� ���)
        // ��� ��������� �����������: ����� ������ 14 ���� ��� GTIN, ����� ��������� N �������� �� ������� ������� ����������� ��� ����� ������ ��� �������� �����
        public static (string gtin, string ki) ExtractKi(string fullCode)
        {
            if (string.IsNullOrEmpty(fullCode))
                return (string.Empty, string.Empty);

            // ������ ��������� �������
            var s = fullCode.Trim();

            // ���� ������ 14 ���� ������
            var digits = new string(s.Where(char.IsDigit).ToArray());
            if (digits.Length < 14)
                return (string.Empty, string.Empty);

            var gtin = digits.Substring(0, 14);

            // ����� GTIN � �������� ������ ���� ������� GTIN � ����� ��������� ������� ��� �������� �����
            var idx = s.IndexOf(gtin);
            if (idx == -1)
                return (gtin, string.Empty);

            var after = s.Substring(idx + gtin.Length);
            // �������� �����: ����� �� ������� ����������� (�������� '.', '-', ' ', '|')
            var separators = new[] { '.', '-', ' ', '|', ':' };
            var endIdx = after.IndexOfAny(separators);
            var serial = endIdx == -1 ? after : after.Substring(0, endIdx);
            serial = new string(serial.Where(c => !char.IsWhiteSpace(c)).ToArray());

            var ki = gtin + serial;
            return (gtin, ki);
        }
    }
}
