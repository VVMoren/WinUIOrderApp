// WinUIOrderApp/Services/RobustImporter.cs
using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Data.Sqlite;

namespace WinUIOrderApp.Services
{
    public static class RobustImporter
    {
        /// <summary>
        /// Импортирует pipe-delimited txt в таблицу Items (создаёт её, если нет).
        /// Разбор: split по '|' → затем маппинг с обеих сторон:
        ///   0: Cis, 1: Ki, 2: Gtin, middle: Name (может содержать '|'), -5: Status, -4: Create, -3: SetCode, -2: Ip, -1: Inn
        /// Возвращает число вставленных строк.
        /// </summary>
        public static int ReimportTxtToItems(string txtPath, string dbPath, Encoding? encoding = null, int batchSize = 10000)
        {
            if (!File.Exists(txtPath)) throw new FileNotFoundException("Source txt not found", txtPath);
            encoding ??= Encoding.UTF8;

            // Резервная рекомендация: создай копию dbPath перед импортом.
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? ".");

            int inserted = 0;
            var cs = $"Data Source={dbPath}";

            using (var conn = new SqliteConnection(cs))
            {
                conn.Open();

                // pragmas для производительности
                using (var p = conn.CreateCommand())
                {
                    p.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA temp_store = MEMORY;";
                    p.ExecuteNonQuery();
                }

                // Создадим таблицу Items (если ещё нет) со схемой, которую ожидает приложение
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

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
@"INSERT INTO Items (Cis,Ki,Gtin,Name,Status,Created,SetCode,Ip,Inn)
  VALUES (@Cis,@Ki,@Gtin,@Name,@Status,@Created,@SetCode,@Ip,@Inn);";

                    var pCis = cmd.CreateParameter(); pCis.ParameterName = "@Cis"; cmd.Parameters.Add(pCis);
                    var pKi = cmd.CreateParameter(); pKi.ParameterName = "@Ki"; cmd.Parameters.Add(pKi);
                    var pGtin = cmd.CreateParameter(); pGtin.ParameterName = "@Gtin"; cmd.Parameters.Add(pGtin);
                    var pName = cmd.CreateParameter(); pName.ParameterName = "@Name"; cmd.Parameters.Add(pName);
                    var pStatus = cmd.CreateParameter(); pStatus.ParameterName = "@Status"; cmd.Parameters.Add(pStatus);
                    var pCreated = cmd.CreateParameter(); pCreated.ParameterName = "@Created"; cmd.Parameters.Add(pCreated);
                    var pSet = cmd.CreateParameter(); pSet.ParameterName = "@SetCode"; cmd.Parameters.Add(pSet);
                    var pIp = cmd.CreateParameter(); pIp.ParameterName = "@Ip"; cmd.Parameters.Add(pIp);
                    var pInn = cmd.CreateParameter(); pInn.ParameterName = "@Inn"; cmd.Parameters.Add(pInn);

                    // batched transaction
                    var transaction = conn.BeginTransaction();
                    cmd.Transaction = transaction;

                    try
                    {
                        using var sr = new StreamReader(txtPath, encoding);
                        string? line;
                        int counterInTx = 0;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            // разбор: делим на токены по '|' (включая любые " внутри поля)
                            var parts = line.Split('|');

                            if (parts.Length < 3)
                                continue; // странная строка — пропускаем

                            // Если у нас >=9 частей — распаковываем с обеих сторон,
                            // middle (3..n-6) — это Name, который может содержать '|' внутри.
                            string cis = parts.Length > 0 ? parts[0].Trim() : "";
                            string ki = parts.Length > 1 ? parts[1].Trim() : "";
                            string gtin = parts.Length > 2 ? parts[2].Trim() : "";

                            string name = "";
                            string status = "";
                            string created = "";
                            string setcode = "";
                            string ip = "";
                            string inn = "";

                            if (parts.Length >= 9)
                            {
                                int n = parts.Length;
                                // last five positions:
                                status = parts[n - 5]?.Trim() ?? "";
                                created = parts[n - 4]?.Trim() ?? "";
                                setcode = parts[n - 3]?.Trim() ?? "";
                                ip = parts[n - 2]?.Trim() ?? "";
                                inn = parts[n - 1]?.Trim() ?? "";

                                int nameCount = n - 8; // количество токенов, которые должны быть в Name
                                if (nameCount <= 0)
                                {
                                    name = "";
                                }
                                else
                                {
                                    name = string.Join("|", parts.Skip(3).Take(nameCount)).Trim();
                                }
                            }
                            else
                            {
                                // если меньше 9 токенов — попробуем назначить по позициям (без middle)
                                // заполним из конца по возможности
                                int n = parts.Length;
                                // если есть хотя бы 6 токенов, предположим стандарт:
                                if (n == 8)
                                {
                                    // 0..7 => map straightforward
                                    cis = parts[0].Trim();
                                    ki = parts[1].Trim();
                                    gtin = parts[2].Trim();
                                    name = parts[3].Trim();
                                    status = parts[4].Trim();
                                    created = parts[5].Trim();
                                    setcode = parts[6].Trim();
                                    ip = parts[7].Trim();
                                    inn = "";
                                }
                                else
                                {
                                    // просто распределим: cis, ki, gtin, name = join remaining
                                    cis = parts.ElementAtOrDefault(0)?.Trim() ?? "";
                                    ki = parts.ElementAtOrDefault(1)?.Trim() ?? "";
                                    gtin = parts.ElementAtOrDefault(2)?.Trim() ?? "";
                                    name = string.Join("|", parts.Skip(3)).Trim();
                                }
                            }

                            // Заполним параметры и выполним вставку
                            pCis.Value = cis;
                            pKi.Value = ki;
                            pGtin.Value = gtin;
                            pName.Value = name;
                            pStatus.Value = status;
                            pCreated.Value = created;
                            pSet.Value = setcode;
                            pIp.Value = ip;
                            pInn.Value = inn;

                            cmd.ExecuteNonQuery();
                            inserted++;
                            counterInTx++;

                            if (counterInTx >= batchSize)
                            {
                                transaction.Commit();
                                transaction.Dispose();
                                transaction = conn.BeginTransaction();
                                cmd.Transaction = transaction;
                                counterInTx = 0;
                            }
                        } // while
                        // финальный коммит
                        transaction.Commit();
                        transaction.Dispose();
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                } // using cmd

                // создаём индексы (ускорит GROUP BY/WHERE)
                using (var idx = conn.CreateCommand())
                {
                    idx.CommandText =
@"CREATE INDEX IF NOT EXISTS idx_items_name ON Items(Name);
CREATE INDEX IF NOT EXISTS idx_items_name_ip ON Items(Name, Ip);
CREATE INDEX IF NOT EXISTS idx_items_ip ON Items(Ip);
CREATE INDEX IF NOT EXISTS idx_items_inn ON Items(Inn);";
                    idx.ExecuteNonQuery();
                }

                conn.Close();
            } // using conn

            return inserted;
        }
    }
}
