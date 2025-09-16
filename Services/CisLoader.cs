// Services/CisLoader.cs
using System.Collections.Generic;
using System.IO;
using WinUIOrderApp.Models;

namespace WinUIOrderApp.Services
{
    public static class CisLoader
    {
        public static List<CisRow> Load(string path)
        {
            var result = new List<CisRow>();
            if (!File.Exists(path)) return result;

            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('|');
                if (parts.Length < 1) continue;
                result.Add(new CisRow
                {

                });
            }
            return result;
        }
    }
}
