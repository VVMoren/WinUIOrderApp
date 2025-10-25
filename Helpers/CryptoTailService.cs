using System;
using System.Collections.Generic;
using System.IO;

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
                    var code = line.Trim();
                    if (string.IsNullOrEmpty(code))
                        continue;

                    var ki = MarkingCodeParser.ExtractKi(code);
                    if (string.IsNullOrEmpty(ki))
                        continue;

                    result[ki] = code;
                }
            }

            return result;
        }
    }
}
