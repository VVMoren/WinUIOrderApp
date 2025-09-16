using System.IO;
using System.Reflection;

namespace markapp.Helpers
{
    public static class EmbeddedExtractor
    {
        public static string EnsureCryptcpExtracted()
        {
            string exeName = "cryptcp.win32.exe";
            string outPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exeName);

            if (File.Exists(outPath))
                return outPath;

            var resourceName = Assembly.GetExecutingAssembly()
                .GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(exeName, StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
                throw new FileNotFoundException("Embedded resource cryptcp.win32.exe not found.");

            using var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            using var fileStream = File.Create(outPath);
            resourceStream!.CopyTo(fileStream);

            return outPath;
        }
    }
}
