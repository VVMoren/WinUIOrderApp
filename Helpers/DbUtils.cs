// Helpers/DbUtils.cs (новый файл)
using Microsoft.Data.Sqlite;
using System.IO;

public static class DbUtils
{
    public static void EnsureIndexes(string dbPath)
    {
        if (!File.Exists(dbPath)) return;
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText =
        @"
        PRAGMA journal_mode = WAL;
        CREATE INDEX IF NOT EXISTS idx_name ON Items(Name);
        CREATE INDEX IF NOT EXISTS idx_name_ip ON Items(Name, Ip);
        CREATE INDEX IF NOT EXISTS idx_ip ON Items(Ip);
        CREATE INDEX IF NOT EXISTS idx_inn ON Items(Inn);
        ";
        cmd.ExecuteNonQuery();
    }
}
