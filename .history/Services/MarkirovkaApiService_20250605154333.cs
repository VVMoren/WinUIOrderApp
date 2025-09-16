using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using markapp.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace markapp.Services
{
    public class MarkirovkaApiService
    {
        private readonly HttpClient _client = new();

        // ✅ Поиск чеков ККТ
        public async Task<JArray> SearchReceiptsV4Async(string pg, int limit)
        {
            try
            {
                var token = AppState.Instance.Token;
                if (string.IsNullOrWhiteSpace(token))
                    throw new InvalidOperationException("Token не установлен.");

                var url = $"https://markirovka.crpt.ru/api/v4/true-api/receipt/list?" +
                          $"pg={Uri.EscapeDataString(pg)}&limit={limit}";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                LogHelper.WriteLog("Поиск чеков", $"URL: {url}");

                var response = await _client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                LogHelper.WriteLog("Ответ на поиск чеков", content);

                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"Ошибка поиска чеков: {response.StatusCode}");

                return JObject.Parse(content)["results"] as JArray ?? new JArray();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("Ошибка в SearchReceiptsV4Async", ex.ToString());
                throw;
            }
        }

        // ✅ Получение информации о чеке
        public async Task<JArray> GetReceiptInfoV4Async(string receiptId, string pg, bool withBody = true)
        {
            try
            {
                var token = AppState.Instance.Token;
                if (string.IsNullOrWhiteSpace(token))
                    throw new InvalidOperationException("Token не установлен.");

                var url = $"https://markirovka.crpt.ru/api/v4/true-api/receipt/{receiptId}/info?body={withBody}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                LogHelper.WriteLog("Получение чека", url);

                var response = await _client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                LogHelper.WriteLog("Ответ на чек", content);

                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"Ошибка получения чека: {response.StatusCode}");

                return JArray.Parse(content);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("Ошибка в GetReceiptInfoV4Async", ex.ToString());
                throw;
            }
        }

        // ✅ Получение CDN токена
        public async Task<string> GetCdnTokenAsync()
        {
            try
            {
                // 1. Подготовка данных
                string rawData = $"cdn-auth-{Guid.NewGuid()}";
                string dataPath = Path.GetTempFileName();
                string signPath = Path.GetTempFileName();
                File.WriteAllText(dataPath, rawData, Encoding.UTF8);

                // 2. Используем уже выбранный сертификат
                var cert = AppState.Instance.SelectedCertificate
                         ?? throw new Exception("Сертификат не выбран. Откройте настройки и выберите сертификат.");


                var dn = cert.SubjectName.Name
                         ?? throw new Exception("DN отсутствует в сертификате.");

                // 3. Подпись через cryptcp
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c G:\\py\\cryptcp.win32.exe -sign -dn \"{dn}\" \"{dataPath}\" \"{signPath}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                LogHelper.WriteLog("GetCdnTokenAsync", $"Запуск cryptcp: {process.StartInfo.Arguments}");
                process.Start();
                process.WaitForExit();

                if (process.ExitCode != 0)
                    throw new Exception($"Ошибка подписи cryptcp: код {process.ExitCode}");

                // 4. Запрос токена
                var signed = File.ReadAllText(signPath).Replace("\r", "").Replace("\n", "");
                var payload = new { data = signed };

                var request = new HttpRequestMessage(HttpMethod.Post,
                    "https://cdn.crpt.ru/api/v3/true-api/auth/permissive-access")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
                };

                var response = await _client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                LogHelper.WriteLog("CDN токен", content);

                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Ошибка получения CDN токена: {response.StatusCode}");

                return JObject.Parse(content)["access_token"]?.ToString() ?? "";
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("GetCdnTokenAsync", ex.ToString());
                throw;
            }
        }


        // ✅ Получение x-api-key для ЛМ ЧЗ
        public async Task<string> GetLmXApiKeyAsync() => await GetCdnTokenAsync();

        // ✅ Онлайн-проверка КМ
        public async Task<HttpResponseMessage> CheckCodesOnlineAsync(List<string> codes, string apiKey, int timeoutMs = 1500)
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
            var payload = JsonConvert.SerializeObject(new { codes });

            var request = new HttpRequestMessage(HttpMethod.Post, "https://cdn01.am.crptech.ru/api/v4/true-api/codes/check")
            {
                Headers = { { "X-API-KEY", apiKey } },
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            LogHelper.WriteLog("CheckCodesOnlineAsync", $"Запрос:\n{payload}");

            return await client.SendAsync(request);
        }

        // ✅ Получение информации по списку КМ
        public async Task<List<CisInfo>> GetCisesInfoAsync(List<string> cisList)
        {
            var token = AppState.Instance.Token;
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Token не установлен.");

            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://markirovka.crpt.ru/api/v3/true-api/cises/info")
            {
                Content = new StringContent(JsonConvert.SerializeObject(cisList), Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            LogHelper.WriteLog("GetCisesInfoAsync", json);

            var rawList = JsonConvert.DeserializeObject<List<CisInfoWrapper>>(json);

            return rawList?
                .Where(x => x.CisInfo != null && !string.IsNullOrWhiteSpace(x.CisInfo.ProductName))
                .Select(x => x.CisInfo)
                .ToList()
                ?? new();
        }

        // Модели
        public class CisInfoWrapper
        {
            [JsonProperty("cisInfo")]
            public CisInfo CisInfo { get; set; }
        }

        public class CisInfo
        {
            [JsonProperty("requestedCis")]
            public string RequestedCis { get; set; }

            [JsonProperty("cisWithoutBrackets")]
            public string CisWithoutBrackets { get; set; }

            [JsonProperty("productName")]
            public string ProductName { get; set; }

            [JsonProperty("ownerName")]
            public string OwnerName { get; set; }

            [JsonProperty("status")]
            public string Status { get; set; }
        }
    }
}
