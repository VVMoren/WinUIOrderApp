using System;
using System.Linq;
using System.Text;

namespace WinUIOrderApp.Helpers
{
    public static class CodeParser
    {
        public static string ExtractKi(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return string.Empty;

            code = code.Trim();

            if (code.StartsWith("01"))
            {
                try
                {
                    var gtin = ExtractGtinFromDataMatrix(code);
                    var serial = ExtractSerialFromDataMatrix(code);
                    if (!string.IsNullOrEmpty(gtin) && !string.IsNullOrEmpty(serial))
                        return gtin + serial;
                    if (!string.IsNullOrEmpty(gtin))
                        return gtin;
                }
                catch
                {
                    // ignore and fallback
                }
            }

            // Для кодов с разделителем '-'
            var digits = new string(code.TakeWhile(char.IsDigit).ToArray());
            if (digits.Length >= 14)
            {
                var serialPart = code.Length > digits.Length ? code.Substring(digits.Length).TrimStart('-', ' ') : string.Empty;
                return digits.Substring(0, 14) + serialPart;
            }

            return code;
        }

        public static string ExtractGtin(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return string.Empty;

            var ki = ExtractKi(code);
            var digits = new string(ki.TakeWhile(char.IsDigit).ToArray());
            if (digits.Length >= 14)
                return digits.Substring(0, 14);
            return digits;
        }

        private static string ExtractGtinFromDataMatrix(string code)
        {
            int index = code.IndexOf("01", StringComparison.Ordinal);
            if (index < 0 || code.Length < index + 16)
                return string.Empty;
            return code.Substring(index + 2, 14);
        }

        private static string ExtractSerialFromDataMatrix(string code)
        {
            int index = code.IndexOf("21", StringComparison.Ordinal);
            if (index < 0)
                return string.Empty;

            int start = index + 2;
            var builder = new StringBuilder();
            for (int i = start; i < code.Length; i++)
            {
                char ch = code[i];
                if (ch == (char)29)
                    break;
                if (char.IsControl(ch))
                    break;
                builder.Append(ch);
            }
            return builder.ToString();
        }
    }
}
