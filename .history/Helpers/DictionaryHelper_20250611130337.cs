using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace markapp.Helpers
{
    public static class DictionaryHelper
    {
        public static Dictionary<string, string> Load(string relativePath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourcePath = $"markapp.{relativePath.Replace("/", ".")}";
            string json;

            using var stream = assembly.GetManifestResourceStream(resourcePath);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                json = reader.ReadToEnd();
            }
            else
            {
                var filePath = Path.Combine(Path.GetDirectoryName(assembly.Location) ?? string.Empty,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(filePath))
                    return new();

                json = File.ReadAllText(filePath);
            }
            var data = JsonSerializer.Deserialize<List<DictionaryEntry>>(json);
            var dict = new Dictionary<string, string>();

            foreach (var entry in data)
            {
                if (!string.IsNullOrEmpty(entry.code) && !string.IsNullOrEmpty(entry.name))
                    dict[entry.code] = entry.name;
                else if (!string.IsNullOrEmpty(entry.code) && !string.IsNullOrEmpty(entry.description))
                    dict[entry.code] = entry.description;
            }

            return dict;
        }

        private class DictionaryEntry
        {
            public string code { get; set; }
            public string name { get; set; }
            public string description { get; set; }
        }
    }
}
