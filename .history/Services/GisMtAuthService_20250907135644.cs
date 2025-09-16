using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions; // добавлено для поиска ИНН в Subject
using System.Threading.Tasks;
using System.Windows;
using markapp.Helpers;
using markapp.Models;
using markapp.ViewModels;
using markapp.Views.Pages;
using markapp.Services;

namespace markapp.Services
{
    public static class GisMtAuthService
    {
        private static readonly string CryptcpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Tools\cryptcp.win32.exe");
        private static readonly string TokenPath = Path.Combine(Path.GetDirectoryName(CryptcpPath)!, "Token.txt");

        /// Получает токен для True API ГИС МТ. Сохраняет токен в файл и в состояние приложения.
        public static async Task<string?> AuthorizeGisMtAsync(X509Certificate2? selectedCert)
        {
            try
            {
                // 🚩 1. Проверяем наличие и активность сохранённого токена
                if (File.Exists(TokenPath))
                {
                    var existingToken = File.ReadAllText(TokenPath).Trim();
                    if (!string.IsNullOrEmpty(existingToken))
                    {
                        // проверка активности токена через /bff-elk/v1/profile/edo/get
                        using (var httpCheck = new HttpClient())
                        {
                            httpCheck.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", existingToken);
                            httpCheck.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                            var checkResp = await httpCheck.GetAsync("https://markirovka.crpt.ru/bff-elk/v1/profile/edo/get");
                            if (checkResp.IsSuccessStatusCode)
                            {
                                // токен активен; получаем данные профиля
                                var checkJson = await checkResp.Content.ReadAsStringAsync();
                                using var doc = JsonDocument.Parse(checkJson);
                                var innFromToken = doc.RootElement.TryGetProperty("inn", out var innEl) ? innEl.GetString() ?? "" : "";

                                // если пользователь не передал сертификат, просим его выбрать
                                X509Certificate2 certForCheck = selectedCert ?? PromptSelectCert();
                                if (certForCheck == null) return null;

                                // сравниваем ИНН из токена и ИНН сертификата
                                var certInn = ExtractInnFromSubject(certForCheck.Subject);
                                bool match = !string.IsNullOrEmpty(innFromToken) && !string.IsNullOrEmpty(certInn) && innFromToken == certInn;

                                if (match)
                                {
                                    // ИНН совпадают → используем существующий токен
                                    AppState.Instance.Token = existingToken;
                                    AppState.Instance.SelectedCertificate = certForCheck;
                                    AppState.Instance.CertificateOwner = certForCheck.Subject;
                                    AppState.Instance.CertificateOwnerPublicName = certForCheck.GetNameInfo(X509NameType.SimpleName, false)?.ToUpperInvariant();
                                    AppState.Instance.NotifyTokenUpdated();
                                    LogHelper.WriteLog("GisMtAuthService.TokenCache", "Токен загружен из файла и соответствует выбранному сертификату.");
                                    return existingToken;
                                }
                                else
                                {
                                    // ИНН не совпадают → спрашиваем, получить новый токен?
                                    var message = $"Сохранённый токен принадлежит ИНН {innFromToken},\nа выбранный сертификат имеет ИНН {certInn}.\nПолучить новый токен для выбранного сертификата?";
                                    var result = MessageBox.Show(message, "Несоответствие токена и сертификата", MessageBoxButton.YesNo, MessageBoxImage.Question);
                                    if (result == MessageBoxResult.No)
                                    {
                                        // пользователь решил оставить старый токен
                                        AppState.Instance.Token = existingToken;
                                        AppState.Instance.SelectedCertificate = certForCheck;
                                        AppState.Instance.CertificateOwner = certForCheck.Subject;
                                        AppState.Instance.CertificateOwnerPublicName = certForCheck.GetNameInfo(X509NameType.SimpleName, false)?.ToUpperInvariant();
                                        AppState.Instance.NotifyTokenUpdated();
                                        LogHelper.WriteLog("GisMtAuthService.TokenCache", "Токен загружен из файла несмотря на несоответствие ИНН.");
                                        return existingToken;
                                    }
                                    // иначе продолжаем и получаем новый токен
                                }
                            }
                            else
                            {
                                // статус не 200 → токен не активен, получаем новый
                                LogHelper.WriteLog("GisMtAuthService.TokenCheck", $"Сохранённый токен неактивен: {checkResp.StatusCode}");
                            }
                        }
                    }
                }

                // 🚩 2. Либо токен отсутствует, либо не активен / выбран новый ИНН → получаем новый токен
                // 🟡 Запрос uuid и случайных данных для подписи.
                using var http = new HttpClient();
                var keyUrl = "https://markirovka.crpt.ru/api/v3/true-api/auth/key";
                LogHelper.WriteLog("HTTP GET", keyUrl);

                var keyResponse = await http.GetAsync(keyUrl);
                var keyJson = await keyResponse.Content.ReadAsStringAsync();
                LogHelper.WriteLog("HTTP Response", $"Status: {keyResponse.StatusCode}\n{keyJson}");

                if (!keyResponse.IsSuccessStatusCode)
                {
                    MessageBox.Show("Ошибка при получении ключа авторизации", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                var keyData = JsonSerializer.Deserialize<AuthKeyResponse>(keyJson);
                if (keyData == null || string.IsNullOrWhiteSpace(keyData.uuid) || string.IsNullOrWhiteSpace(keyData.data))
                {
                    MessageBox.Show("Некорректный ответ от сервера (uuid или data отсутствуют)", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                // 🟢 Выбор сертификата (если не передан).
                X509Certificate2 cert = selectedCert ?? PromptSelectCert();
                if (cert == null) return null;

                AppState.Instance.SelectedCertificate = cert;
                AppState.Instance.CertificateOwner = cert.Subject;

                // Получаем ФИО для DN; используем заглавные буквы.
                var fio = cert.GetNameInfo(X509NameType.SimpleName, false);
                AppState.Instance.CertificateOwnerPublicName = fio?.ToUpperInvariant();
                var dn = $"CN={fio}";

                // 📄 Подготовка путей к файлам.
                var dataPath = Path.Combine(Path.GetDirectoryName(CryptcpPath)!, "data.txt");
                var signPath = Path.Combine(Path.GetDirectoryName(CryptcpPath)!, "data_sign.txt");

                // ✏️ Сохраняем строку данных без BOM.
                var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                File.WriteAllText(dataPath, keyData.data, utf8NoBom);

                // Удаляем старый файл подписи, если он есть.
                if (File.Exists(signPath)) File.Delete(signPath);

                if (!File.Exists(CryptcpPath))
                {
                    MessageBox.Show($"Не найден файл cryptcp:\n{CryptcpPath}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                // 🔐 Формируем присоединённую подпись (без -detached).
                string args = $"/c chcp 1251 > nul && \"{CryptcpPath}\" -sign -strict -der -dn \"{dn}\" \"{dataPath}\" \"{signPath}\"";
                LogHelper.WriteLog("CMD", $"cmd.exe {args}");

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string stdout = await process.StandardOutput.ReadToEndAsync();
                string stderr = await process.StandardError.ReadToEndAsync();
                process.WaitForExit();

                LogHelper.WriteLog("CMD Output", stdout);
                LogHelper.WriteLog("CMD Error", stderr);

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Ошибка подписи через cryptcp:\n{stderr}");
                }

                if (!File.Exists(signPath))
                    throw new FileNotFoundException("Файл подписи не создан");

                // 🧾 Читаем подпись как бинарный массив и кодируем в Base64.
                byte[] signatureBytes = await File.ReadAllBytesAsync(signPath);
                string signatureBase64 = Convert.ToBase64String(signatureBytes).Trim();

                // ⬇️ Отправляем данные на авторизацию.
                var signInData = new { uuid = keyData.uuid, data = signatureBase64 };
                var jsonContent = JsonSerializer.Serialize(signInData);
                LogHelper.WriteLog("HTTP POST", $"https://markirovka.crpt.ru/api/v3/true-api/auth/simpleSignIn\nBody:\n{jsonContent}");

                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var tokenResponse = await http.PostAsync("https://markirovka.crpt.ru/api/v3/true-api/auth/simpleSignIn", content);
                var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

                LogHelper.WriteLog("HTTP Response", $"Status: {tokenResponse.StatusCode}\n{tokenJson}");

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    MessageBox.Show($"Ошибка авторизации: {tokenJson}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                var tokenObj = JsonSerializer.Deserialize<TokenResponse>(tokenJson);
                if (tokenObj == null || string.IsNullOrEmpty(tokenObj.token))
                {
                    MessageBox.Show("Не удалось получить токен", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                // ✅ Сохраняем и возвращаем токен.
                AppState.Instance.Token = tokenObj.token;
                AppState.Instance.NotifyTokenUpdated();
                File.WriteAllText(TokenPath, tokenObj.token);

                // ⏬ Загружаем профиль, если есть соответствующая страница.
                if (Application.Current.MainWindow is not null &&
                    Application.Current.MainWindow.DataContext is SettingsViewModel vm)
                {
                    await vm.LoadUserProfileAndFilterProductGroups();
                }

                LogHelper.WriteLog("GisMtAuthService", "Авторизация прошла успешно.");
                MessageBox.Show("Токен успешно получен!", "ГИС МТ", MessageBoxButton.OK, MessageBoxImage.Information);
                return tokenObj.token;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Авторизация", MessageBoxButton.OK, MessageBoxImage.Error);
                LogHelper.WriteLog("GisMtAuthService.Exception", ex.ToString());
                return null;
            }
        }

        /// Извлекает ИНН (10–12 цифр) из строки Subject сертификата.
        private static string? ExtractInnFromSubject(string subject)
        {
            var match = Regex.Match(subject ?? string.Empty, @"\b\d{10,12}\b");
            return match.Success ? match.Value : null;
        }


        /// Показывает предупреждение о необходимости выбрать сертификат.
        private static X509Certificate2? PromptSelectCert()
        {
            var result = MessageBox.Show(
                "Не выбран сертификат для подключения к ГИС МТ",
                "Выбор сертификата",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.Yes);

            if (result == MessageBoxResult.Yes)
            {
                var viewModel = Application.Current.MainWindow?.DataContext as SettingsViewModel;
                if (viewModel != null)
                {
                    var settingsPage = new SettingsPage(viewModel);
                    Application.Current.MainWindow.Content = settingsPage;
                }
            }

            return null;
        }

        private record AuthKeyResponse(string uuid, string data);
        private record TokenResponse(string token);
    }
}
