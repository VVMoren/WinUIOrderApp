using System.IO;

namespace WinUIOrderApp.Helpers
{
    public static class AppDbInitializer
    {
        /// <summary>
        /// Проверяет наличие локальной БД. Если нет — создаёт её из исходного файла iDB.txt.
        /// </summary>
        public static void EnsureDatabase(string iDbTxtPath)
        {
            var dbDir = Path.GetDirectoryName(AppDbConfig.DbPath)!;
            if (!Directory.Exists(dbDir))
                Directory.CreateDirectory(dbDir);

            if (!File.Exists(AppDbConfig.DbPath))
            {
                // Если базы нет — импортируем из iDB.txt
                if (!File.Exists(iDbTxtPath))
                    throw new FileNotFoundException("Файл iDB.txt не найден по указанному пути", iDbTxtPath);

                DatabaseImporter.ImportCsv(iDbTxtPath);
            }
        }
    }
}
