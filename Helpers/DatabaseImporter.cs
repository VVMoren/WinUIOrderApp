// WinUIOrderApp/Helpers/DatabaseImporter.cs
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.VisualBasic.FileIO;

namespace WinUIOrderApp.Helpers
{
    public static class DatabaseImporter
    {
        /// <summary>
        /// Импортирует текстовый iDB.txt в SQLite file (перезаписывает, если существует).
        /// Ожидаем формат строк: cis|ki|gtin|name|status|create|set|ip|inn
        /// </summary>
        public static void ImportCsv(string csvPath)
        {
            var dbDir = Path.GetDirectoryName(AppDbConfig.DbPath)!;
            if (!Directory.Exists(dbDir))
                Directory.CreateDirectory(dbDir);

            // перезаписываем базу, если есть
            if (File.Exists(AppDbConfig.DbPath))
                File.Delete(AppDbConfig.DbPath);

            using var connection = new SqliteConnection($"Data Source={AppDbConfig.DbPath}");
            connection.Open();

            var createCmd = connection.CreateCommand();
            createCmd.CommandText =
            @"CREATE TABLE Items (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Cis TEXT, Ki TEXT, Gtin TEXT, Name TEXT, Status TEXT,
                Created TEXT, SetCode TEXT, Ip TEXT, Inn TEXT
            );";
            createCmd.ExecuteNonQuery();

            using var transaction = connection.BeginTransaction();
            using var reader = new StreamReader(csvPath);

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('|');
                if (parts.Length < 9) continue;

                var cmd = connection.CreateCommand();
                cmd.CommandText =
                    @"INSERT INTO Items (Cis, Ki, Gtin, Name, Status, Created, SetCode, Ip, Inn)
                      VALUES ($cis,$ki,$gtin,$name,$status,$created,$set,$ip,$inn);";
                cmd.Parameters.AddWithValue("$cis", parts[0] ?? "");
                cmd.Parameters.AddWithValue("$ki", parts[1] ?? "");
                cmd.Parameters.AddWithValue("$gtin", parts[2] ?? "");
                cmd.Parameters.AddWithValue("$name", parts[3] ?? "");
                cmd.Parameters.AddWithValue("$status", parts[4] ?? "");
                cmd.Parameters.AddWithValue("$created", parts[5] ?? "");
                cmd.Parameters.AddWithValue("$set", parts[6] ?? "");
                cmd.Parameters.AddWithValue("$ip", parts[7] ?? "");
                cmd.Parameters.AddWithValue("$inn", parts[8] ?? "");
                cmd.ExecuteNonQuery();
            }




            transaction.Commit();
        }

        public static int ImportTxtToSqlite(string txtPath, string dbPath, int batchSize = 5000, Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;

            if (!File.Exists(txtPath))
                throw new FileNotFoundException("Source txt not found", txtPath);

            Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? ".");

            var inserted = 0;
            var connectionString = $"Data Source={dbPath}";

            using (var conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                // Performance pragmas
                using (var pragma = conn.CreateCommand())
                {
                    pragma.CommandText = @"
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA temp_store = MEMORY;
";
                    pragma.ExecuteNonQuery();
                }

                // Create table if not exists (match expected schema)
                using (var create = conn.CreateCommand())
                {
                    create.CommandText =
        @"CREATE TABLE IF NOT EXISTS Items (
    Cis TEXT,
    Ki TEXT,
    Gtin TEXT,
    Name TEXT,
    Status TEXT,
    Created TEXT,
    SetCode TEXT,
    Ip TEXT,
    Inn TEXT
);";
                    create.ExecuteNonQuery();
                }

                // prepare insert command and parameters
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
        @"INSERT INTO Items (Cis,Ki,Gtin,Name,Status,Created,SetCode,Ip,Inn)
  VALUES (@Cis,@Ki,@Gtin,@Name,@Status,@Created,@SetCode,@Ip,@Inn);";

                var pCis = cmd.CreateParameter(); pCis.ParameterName = "@Cis"; cmd.Parameters.Add(pCis);
                var pKi = cmd.CreateParameter(); pKi.ParameterName = "@Ki"; cmd.Parameters.Add(pKi);
                var pGtin = cmd.CreateParameter(); pGtin.ParameterName = "@Gtin"; cmd.Parameters.Add(pGtin);
                var pName = cmd.CreateParameter(); pName.ParameterName = "@Name"; cmd.Parameters.Add(pName);
                var pStatus = cmd.CreateParameter(); pStatus.ParameterName = "@Status"; cmd.Parameters.Add(pStatus);
                var pCreated = cmd.CreateParameter(); pCreated.ParameterName = "@Created"; cmd.Parameters.Add(pCreated);
                var pSetCode = cmd.CreateParameter(); pSetCode.ParameterName = "@SetCode"; cmd.Parameters.Add(pSetCode);
                var pIp = cmd.CreateParameter(); pIp.ParameterName = "@Ip"; cmd.Parameters.Add(pIp);
                var pInn = cmd.CreateParameter(); pInn.ParameterName = "@Inn"; cmd.Parameters.Add(pInn);

                // Read file using TextFieldParser (supports quoted fields)
                using var parser = new TextFieldParser(txtPath, encoding);
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters("|");
                parser.HasFieldsEnclosedInQuotes = true;

                int batchCounter = 0;
                var trans = conn.BeginTransaction();
                cmd.Transaction = trans;

                try
                {
                    while (!parser.EndOfData)
                    {
                        string[] fields;
                        try
                        {
                            fields = parser.ReadFields() ?? Array.Empty<string>();
                        }
                        catch (MalformedLineException)
                        {
                            // skip malformed line (or handle differently)
                            continue;
                        }

                        // ensure at least 9 fields: pad with empty strings if fewer
                        if (fields.Length < 9)
                        {
                            Array.Resize(ref fields, 9);
                            for (int i = 0; i < fields.Length; i++)
                                fields[i] = fields[i] ?? string.Empty;
                        }

                        // Map fields by expected order in original txt:
                        // Cis|Ki|Gtin|Name|Status|Create|Set|Ip|Inn
                        pCis.Value = fields.Length > 0 ? fields[0].Trim() : "";
                        pKi.Value = fields.Length > 1 ? fields[1].Trim() : "";
                        pGtin.Value = fields.Length > 2 ? fields[2].Trim() : "";
                        pName.Value = fields.Length > 3 ? fields[3].Trim() : "";
                        pStatus.Value = fields.Length > 4 ? fields[4].Trim() : "";
                        pCreated.Value = fields.Length > 5 ? fields[5].Trim() : "";
                        pSetCode.Value = fields.Length > 6 ? fields[6].Trim() : "";
                        pIp.Value = fields.Length > 7 ? fields[7].Trim() : "";
                        pInn.Value = fields.Length > 8 ? fields[8].Trim() : "";

                        cmd.ExecuteNonQuery();

                        inserted++;
                        batchCounter++;

                        if (batchCounter >= batchSize)
                        {
                            trans.Commit();
                            trans.Dispose();
                            // begin new transaction
                            trans = conn.BeginTransaction();
                            cmd.Transaction = trans;
                            batchCounter = 0;
                        }
                    }

                    // commit remaining
                    trans.Commit();
                    trans.Dispose();
                }
                catch
                {
                    try { trans.Rollback(); } catch { }
                    throw;
                }

                // create indexes on Items
                using (var idx = conn.CreateCommand())
                {
                    idx.CommandText =
        @"CREATE INDEX IF NOT EXISTS idx_name ON Items(Name);
CREATE INDEX IF NOT EXISTS idx_name_ip ON Items(Name, Ip);
CREATE INDEX IF NOT EXISTS idx_ip ON Items(Ip);
CREATE INDEX IF NOT EXISTS idx_inn ON Items(Inn);";
                    idx.ExecuteNonQuery();
                }

                conn.Close();
            }

            return inserted;
        }


    }
}
