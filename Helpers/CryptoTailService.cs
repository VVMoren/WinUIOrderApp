using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WinUIOrderApp.Helpers
{
    public static class CryptoTailService
    {
        public static Dictionary<string, string> BuildIndex(string? folderPath)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                return result;

            foreach (var file in Directory.EnumerateFiles(folderPath, "*.txt", SearchOption.AllDirectories))
            {
                foreach (var line in File.ReadLines(file))
                {
                    var fullCode = line.Trim();
                    if (string.IsNullOrEmpty(fullCode))
                        continue;

                    // ��������� ������ ���
                    result[fullCode] = fullCode;

                    // ����� ��������� ��������� �������� ��������
                    // ���� ��� ������� (>20 ��������), ��������� ������ 20-21 ������
                    if (fullCode.Length > 21)
                    {
                        var truncated = fullCode.Substring(0, 21);
                        if (!result.ContainsKey(truncated))
                        {
                            result[truncated] = fullCode;
                        }
                    }

                    if (fullCode.Length > 20)
                    {
                        var truncated = fullCode.Substring(0, 20);
                        if (!result.ContainsKey(truncated))
                        {
                            result[truncated] = fullCode;
                        }
                    }
                }
            }

            return result;
        }

        // ����� �����: ����� ������������ �� ���������� ����������
        public static string? FindCryptoTail(string partialCode, string? folderPath)
        {
            if (string.IsNullOrEmpty(partialCode) || string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return null;

            try
            {
                foreach (var file in Directory.EnumerateFiles(folderPath, "*.txt", SearchOption.AllDirectories))
                {
                    foreach (var line in File.ReadLines(file))
                    {
                        var fullCode = line.Trim();
                        if (string.IsNullOrEmpty(fullCode))
                            continue;

                        // ������ ����������
                        if (fullCode.Equals(partialCode, StringComparison.OrdinalIgnoreCase))
                            return fullCode;

                        // ��������� ����������: partialCode �������� ������� fullCode
                        if (fullCode.StartsWith(partialCode, StringComparison.OrdinalIgnoreCase))
                            return fullCode;

                        // �������� ����������: fullCode �������� ������� partialCode (������������, �� �� ������ ������)
                        if (partialCode.StartsWith(fullCode, StringComparison.OrdinalIgnoreCase))
                            return fullCode;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("CryptoTailService.FindCryptoTail", $"������ ������: {ex.Message}");
            }

            return null;
        }

        // ����� �����: �������� ��� ������������ ��� �������
        public static List<string> GetAllCryptoTails(string? folderPath)
        {
            var result = new List<string>();

            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return result;

            try
            {
                foreach (var file in Directory.EnumerateFiles(folderPath, "*.txt", SearchOption.AllDirectories))
                {
                    var lines = File.ReadAllLines(file);
                    result.AddRange(lines.Where(l => !string.IsNullOrWhiteSpace(l)));
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("CryptoTailService.GetAllCryptoTails", $"������: {ex.Message}");
            }

            return result.Distinct().ToList();
        }
    }
}