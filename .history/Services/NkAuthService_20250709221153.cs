using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using markapp.Helpers;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace markapp.Services
{
    public static class NkAuthService
    {
        private const string REQUIRED_THUMBPRINT = "C333E83AE307708B1CD5EB8EB94DB0D37CDF1C51";
        private const string REQUIRED_CN = "ГОЛУБЕЦ ВЛАДИСЛАВ ВИТАЛЬЕВИЧ";
        private const string INN = "771683739093";
        private const string NK_BASE_URL = "https://xn--j1ab.xn----7sbabas4ajkhfocclk9d3cvfsa.xn--p1ai";



        public static async Task<NkAuthResult> AuthenticateNkAsync()
        {
            try
            {
                LogHelper.WriteLog("NkAuthService", "Начало аутентификации в НК");

                // 1. Получаем данные для подписи
                var signedData = await GetSignedDataAsync();
                if (string.IsNullOrEmpty(signedData))
                {
                    throw new Exception("Не удалось получить данные для подписи");
                }

                // 2. Подписываем данные через cryptcp
                var signature = await SignDataWithCryptcpAsync(signedData);
                if (string.IsNullOrEmpty(signature))
                {
                    throw new Exception("Ошибка подписи данных");
                }

                // 3. Аутентификация в НК
                var authResult = await AuthenticateWithNkAsync(signature);
                if (authResult == null)
                {
                    throw new Exception("Ошибка аутентификации в НК");
                }

                LogHelper.WriteLog("NkAuthService", "Успешная аутентификация в НК");
                return authResult;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("NkAuthService.Error", ex.ToString());
                throw;
            }
        }

        private static async Task<string> GetSignedDataAsync()
        {
            using var http = new HttpClient();
            var response = await http.GetAsync($"{NK_BASE_URL}/rest/signed-data");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Ошибка получения данных для подписи: {response.StatusCode}\n{errorContent}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var authKey = JsonConvert.DeserializeObject<AuthKeyResponse>(json);

            LogHelper.WriteLog("NkAuthService.SignedData", $"Получены данные для подписи (длина: {authKey?.data?.Length ?? 0})");
            return authKey?.data;
        }

        private static async Task<string> SignDataWithCryptcpAsync(string data)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dataPath = Path.Combine(baseDir, "nk_data.txt");
            string signPath = Path.Combine(baseDir, "nk_data_sign.txt");

            try
            {
                // Записываем бинарные данные
                var dataBytes = Convert.FromBase64String(data);
                await File.WriteAllBytesAsync(dataPath, dataBytes);

                // Подписываем через cryptcp
                string exePath = EmbeddedExtractor.EnsureCryptcpExtracted();

                // Упрощаем команду - убираем chcp 1251, так как cryptcp работает с бинарными данными
              //string cmdArgs = $"/c \"{exePath}\" -sign -dn \"CN={REQUIRED_CN}\" -detached -der \"{dataPath}\" \"{signPath}\"";
                string cmdArgs = $"/c chcp 1251 > nul && \"{exePath}\" -sign -dn \"CN={REQUIRED_CN}\" \"{dataPath}\" \"{signPath}\"";


                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = cmdArgs,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    string error = await process.StandardError.ReadToEndAsync();
                    throw new Exception($"Ошибка cryptcp (код {process.ExitCode}): {error}");
                }

                byte[] signatureBytes = await File.ReadAllBytesAsync(signPath);
                return Convert.ToBase64String(signatureBytes);
            }
            finally
            {
                // Очистка временных файлов
                if (File.Exists(dataPath)) File.Delete(dataPath);
                if (File.Exists(signPath)) File.Delete(signPath);
            }
        }

        private static async Task<NkAuthResult> AuthenticateWithNkAsync(string signature)
        {
            using var http = new HttpClient();
            var payload = new
            {
                signature,
                fingerprint = REQUIRED_THUMBPRINT,
                inn = INN
            };

            var jsonPayload = JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{NK_BASE_URL}/rest/certlogin")
            {
                Content = content
            };

            // Устанавливаем необходимые заголовки
            request.Headers.Add("Accept", "application/json, text/plain, */*");
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 YaBrowser/25.6.0.0 Safari/537.36");
            request.Headers.Add("Referer", $"{NK_BASE_URL}/login");

            var response = await http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Ошибка аутентификации: {response.StatusCode}\n{errorContent}");
            }

            var json = await response.Content.ReadAsStringAsync();
            LogHelper.WriteLog("NkAuthService.AuthResponse", json);

            return JsonConvert.DeserializeObject<NkAuthResult>(json);
        }

        private class AuthKeyResponse
        {
            public string data { get; set; }
        }
    }

}