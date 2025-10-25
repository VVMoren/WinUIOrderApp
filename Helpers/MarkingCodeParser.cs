using System;
using System.Linq;
using System.Text;

namespace WinUIOrderApp.Helpers
{
    public static class MarkingCodeParser
    {
        public static string ExtractKi(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return string.Empty;

            var sanitized = code.Replace("\u001D", string.Empty)
                                 .Replace("\r", string.Empty)
                                 .Replace("\n", string.Empty)
                                 .Trim();

            if (sanitized.Length == 0)
                return string.Empty;

            if (sanitized.StartsWith("01") && sanitized.Length > 16)
            {
                var payload = sanitized.Substring(2);
                if (payload.Length >= 14)
                {
                    var gtin = payload.Substring(0, 14);
                    var remainder = payload.Substring(14);

                    if (remainder.StartsWith("21"))
                    {
                        remainder = remainder.Substring(2);
                        var serial = ExtractSerial(remainder);
                        if (!string.IsNullOrEmpty(serial))
                            return gtin + serial;
                    }
                }
            }

            if (sanitized.Length >= 14 && sanitized.Take(14).All(char.IsDigit))
            {
                var gtin = new string(sanitized.Take(14).ToArray());
                var remainder = sanitized.Substring(14).TrimStart('-', ' ');
                if (remainder.Length > 0)
                {
                    var serial = new string(remainder
                        .TakeWhile(ch => ch != '-' && ch != ' ' && ch != '\u001D' && ch != '\r' && ch != '\n')
                        .ToArray());

                    if (!string.IsNullOrEmpty(serial))
                        return gtin + serial;
                }
            }

            return sanitized;
        }

        public static string ExtractGtin(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return string.Empty;

            var sanitized = code.Replace("\u001D", string.Empty)
                                 .Replace("\r", string.Empty)
                                 .Replace("\n", string.Empty)
                                 .Trim();

            if (sanitized.StartsWith("01") && sanitized.Length > 16)
            {
                var payload = sanitized.Substring(2);
                if (payload.Length >= 14)
                    return payload.Substring(0, 14);
            }

            if (sanitized.Length >= 14 && sanitized.Take(14).All(char.IsDigit))
                return new string(sanitized.Take(14).ToArray());

            return string.Empty;
        }

        private static string ExtractSerial(string payload)
        {
            if (string.IsNullOrEmpty(payload))
                return string.Empty;

            var builder = new StringBuilder();
            int i = 0;
            while (i < payload.Length)
            {
                var ch = payload[i];
                if (ch == '\u001D')
                    break;

                if (i + 1 < payload.Length && char.IsDigit(payload[i]) && char.IsDigit(payload[i + 1]))
                {
                    var aiCandidate = payload.Substring(i, Math.Min(4, payload.Length - i));
                    if (IsApplicationIdentifier(aiCandidate))
                        break;
                }

                builder.Append(ch);
                i++;
            }

            return builder.ToString().Trim();
        }

        private static bool IsApplicationIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            var two = value.Length >= 2 ? value.Substring(0, 2) : value;
            return two is "90" or "91" or "92" or "93" or "94" or "95" or "96" or "97" or "98" or "99";
        }
    }
}
