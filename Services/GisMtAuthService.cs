using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using CryptoPro.Security.Cryptography.Pkcs;
using CryptoPro.Security.Cryptography.X509Certificates;
using WinUIOrderApp.Helpers;

namespace WinUIOrderApp.Services
{
    public static class GisMtAuthService
    {
        private const string GisUri = "https://markirovka.crpt.ru";
        private static readonly string TokenCachePath =
            System.IO.Path.Combine(AppContext.BaseDirectory, "Token.txt");


        /// Асинхронно получает токен для True API, подписывая challenge встроенными средствами CryptoPro.
        public static async Task<string?> AuthorizeGisMtAsync(X509Certificate2? selectedCert)
        {
            try
            {
                X509Certificate2 cert = selectedCert ?? PromptSelectCert();
                if (cert == null)
                {
                    MessageBox.Show("Сертификат не выбран.", "ГИС МТ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                // === 1. Проверка кэша токена С ПРИВЯЗКОЙ К СЕРТИФИКАТУ ===
                string cacheKey = $"{cert.Thumbprint}";
                string TokenCachePath = GetTokenCachePath(cacheKey);

                if (System.IO.File.Exists(TokenCachePath))
                {
                    string cached = System.IO.File.ReadAllText(TokenCachePath).Trim();
                    if (!string.IsNullOrEmpty(cached))
                    {
                        if (await IsTokenValidAsync(cached))
                        {
                            // Проверяем, что токен соответствует текущему сертификату
                            AppState.Instance.Token = cached;
                            AppState.Instance.SelectedCertificate = cert;
                            AppState.Instance.CertificateOwner = cert.Subject;
                            AppState.Instance.CertificateOwnerPublicName =
                                cert.GetNameInfo(X509NameType.SimpleName, false)?.ToUpperInvariant();
                            AppState.Instance.NotifyTokenUpdated();
                            LogHelper.WriteLog("GisMtAuthService", $"Токен из кэша активен для сертификата: {cert.Subject}");
                            return cached;
                        }
                        LogHelper.WriteLog("GisMtAuthService", $"Токен из кэша просрочен для сертификата: {cert.Subject}");
                    }
                }

                // === 2. Получаем challenge (uuid, data) ===
                using var http = new HttpClient();
                var keyResp = await http.GetAsync($"{GisUri}/api/v3/true-api/auth/key");
                if (!keyResp.IsSuccessStatusCode)
                {
                    MessageBox.Show("Ошибка при получении ключа авторизации.", "ГИС МТ",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                var keyJson = await keyResp.Content.ReadAsStringAsync();
                var key = JsonSerializer.Deserialize<AuthKeyResponse>(keyJson);
                if (key == null || string.IsNullOrWhiteSpace(key.uuid) || string.IsNullOrWhiteSpace(key.data))
                {
                    MessageBox.Show("Некорректный ответ сервера при получении ключа.", "ГИС МТ",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                // === 3. Подписываем challenge встроенным CryptoPro ===
                string signature = SignDataCryptoPro(cert, key.data);
                if (string.IsNullOrEmpty(signature))
                {
                    MessageBox.Show("Ошибка при формировании подписи.", "ГИС МТ",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                // === 4. Отправляем запрос на simpleSignIn ===
                var body = new { uuid = key.uuid, data = signature };
                var json = JsonSerializer.Serialize(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var signResp = await http.PostAsync($"{GisUri}/api/v3/true-api/auth/simpleSignIn", content);
                var signJson = await signResp.Content.ReadAsStringAsync();

                if (!signResp.IsSuccessStatusCode)
                {
                    MessageBox.Show($"Ошибка авторизации:\n{signJson}", "ГИС МТ",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    LogHelper.WriteLog("GisMtAuthService", $"Auth error: {signJson}");
                    return null;
                }

                var tokenObj = JsonSerializer.Deserialize<TokenResponse>(signJson);
                if (tokenObj == null || string.IsNullOrEmpty(tokenObj.token))
                {
                    MessageBox.Show("Не удалось получить токен.", "ГИС МТ",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                // === 5. Сохраняем токен С ПРИВЯЗКОЙ К СЕРТИФИКАТУ и обновляем состояние приложения ===
                AppState.Instance.Token = tokenObj.token;
                AppState.Instance.SelectedCertificate = cert;
                AppState.Instance.CertificateOwner = cert.Subject;
                AppState.Instance.CertificateOwnerPublicName =
                    cert.GetNameInfo(X509NameType.SimpleName, false)?.ToUpperInvariant();
                AppState.Instance.NotifyTokenUpdated();

                // Сохраняем токен с привязкой к отпечатку сертификата
                System.IO.File.WriteAllText(TokenCachePath, tokenObj.token);

                // Очищаем старые кэшированные токены
                CleanOldTokenCache(cacheKey);

                LogHelper.WriteLog("GisMtAuthService", $"Авторизация прошла успешно для сертификата: {cert.Subject}");
//                MessageBox.Show("Авторизация ГИС МТ выполнена успешно.", "ГИС МТ",MessageBoxButton.OK, MessageBoxImage.Information);

                return tokenObj.token;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("GisMtAuthService.Exception", ex.ToString());
                MessageBox.Show($"Ошибка: {ex.Message}", "Авторизация", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        // === Новые методы для управления кэшем ===
        private static string GetTokenCachePath(string cacheKey)
        {
            string cacheDir = System.IO.Path.Combine(AppContext.BaseDirectory, "TokenCache");
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);

            // Используем отпечаток сертификата как имя файла
            string safeFileName = cacheKey.Replace(" ", "_").Replace(":", "").ToLowerInvariant();
            return System.IO.Path.Combine(cacheDir, $"{safeFileName}.token");
        }

        private static void CleanOldTokenCache(string currentCacheKey)
        {
            try
            {
                string cacheDir = System.IO.Path.Combine(AppContext.BaseDirectory, "TokenCache");
                if (!Directory.Exists(cacheDir))
                    return;

                var currentCachePath = GetTokenCachePath(currentCacheKey);
                var allTokenFiles = Directory.GetFiles(cacheDir, "*.token");

                foreach (var file in allTokenFiles)
                {
                    if (file != currentCachePath)
                    {
                        File.Delete(file);
                    }
                }

                LogHelper.WriteLog("GisMtAuthService.CleanCache", $"Очищено {allTokenFiles.Length - 1} старых токенов");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("GisMtAuthService.CleanCache.Error", ex.Message);
            }
        }

        // === Проверка активности токена ===
        private static async Task<bool> IsTokenValidAsync(string token)
        {
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var resp = await http.GetAsync($"{GisUri}/bff-elk/v1/profile/edo/get");
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // === Подписание данных встроенным CryptoPro ===
        private static string SignDataCryptoPro(X509Certificate2 cert, string content)
        {
            try
            {
                var cpCert = GetCpCertificate(cert.Thumbprint);
                var signed = new CpSignedCms(new ContentInfo(Encoding.UTF8.GetBytes(content)), detached: false);
                signed.ComputeSignature(new CpCmsSigner(cpCert)
                {
                    IncludeOption = X509IncludeOption.WholeChain,
                    SignedAttributes = { new Pkcs9SigningTime(DateTime.Now) }
                });
                return Convert.ToBase64String(signed.Encode());
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("GisMtAuthService.SignData", ex.ToString());
                return string.Empty;
            }
        }

        private static CpX509Certificate2 GetCpCertificate(string thumbprint)
        {
            using var store = new CpX509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            foreach (var cert in store.Certificates)
                if (cert.Thumbprint?.Equals(thumbprint, StringComparison.OrdinalIgnoreCase) == true)
                    return cert;
            throw new Exception("Сертификат не найден в хранилище CryptoPro.");
        }

        private static X509Certificate2? PromptSelectCert()
        {
            MessageBox.Show("Не выбран сертификат для подключения к ГИС МТ.",
                "Выбор сертификата", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        private record AuthKeyResponse(string uuid, string data);
        private record TokenResponse(string token);
    }
}
