using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using WinUIOrderApp.Models;
using WinUIOrderApp.Helpers;

namespace WinUIOrderApp.Helpers
{
    public static class ApiHelper
    {
        private static HttpClient _httpClient = new HttpClient();
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Основной метод для выполнения запросов с автоматическим логированием
        public static async Task<ApiResponse<T>> ExecuteRequestAsync<T>(
            HttpMethod method,
            string url,
            object data = null)
        {
            var inn = ExtractInnFromCertificate(AppState.Instance.SelectedCertificate);
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var timestamp = DateTime.Now;

            try
            {
                var request = new HttpRequestMessage(method, url);

                // Всегда используем Bearer Token для обычных запросов
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AppState.Instance.Token);

                LogHelper.WriteCertificateLog(inn, $"ApiHelper.REQUEST[{requestId}]",
                    $"URL: {url}\nMethod: {method}\nAuth: Bearer Token");

                if (data != null && (method == HttpMethod.Post || method == HttpMethod.Put))
                {
                    var jsonContent = JsonSerializer.Serialize(data, _jsonOptions);
                    request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    LogHelper.WriteCertificateLog(inn, $"ApiHelper.REQUEST_DATA[{requestId}]", jsonContent);
                }

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                LogHelper.WriteCertificateLog(inn, $"ApiHelper.RESPONSE[{requestId}]",
                    $"Status: {response.StatusCode}\nContent: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    if (string.IsNullOrEmpty(responseContent))
                        return ApiResponse<T>.CreateSuccess(default(T));

                    var result = JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
                    return ApiResponse<T>.CreateSuccess(result);
                }
                else
                {
                    return ApiResponse<T>.CreateError($"HTTP {response.StatusCode}: {responseContent}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteCertificateLog(inn, $"ApiHelper.ERROR[{requestId}]", ex.ToString());
                return ApiResponse<T>.CreateError($"Exception: {ex.Message}");
            }
        }

        // Методы для Национального каталога с поддержкой API Key
        public static async Task<ApiResponse<T>> ExecuteNkRequestAsync<T>(
            HttpMethod method,
            string endpoint,
            object data = null,
            string apiKey = null)
        {
            var inn = ExtractInnFromCertificate(AppState.Instance.SelectedCertificate);
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);

            try
            {
                // Формируем URL с API Key если он передан
                var url = string.IsNullOrEmpty(apiKey)
                    ? $"https://markirovka.crpt.ru/api/v3/true-api/nk/{endpoint}"
                    : $"https://markirovka.crpt.ru/api/v3/true-api/nk/{endpoint}?apikey={apiKey}";

                var request = new HttpRequestMessage(method, url);

                // Если API Key не указан - используем Bearer Token
                if (string.IsNullOrEmpty(apiKey))
                {
                    request.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AppState.Instance.Token);
                }

                LogHelper.WriteCertificateLog(inn, $"ApiHelper.NK_REQUEST[{requestId}]",
                    $"URL: {url}\nMethod: {method}\nAuth: {(string.IsNullOrEmpty(apiKey) ? "Bearer Token" : "API Key")}");

                if (data != null && (method == HttpMethod.Post || method == HttpMethod.Put))
                {
                    var jsonContent = JsonSerializer.Serialize(data, _jsonOptions);
                    request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    LogHelper.WriteCertificateLog(inn, $"ApiHelper.NK_REQUEST_DATA[{requestId}]", jsonContent);
                }

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                LogHelper.WriteCertificateLog(inn, $"ApiHelper.NK_RESPONSE[{requestId}]",
                    $"Status: {response.StatusCode}\nContent: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    if (string.IsNullOrEmpty(responseContent))
                        return ApiResponse<T>.CreateSuccess(default(T));

                    var result = JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
                    return ApiResponse<T>.CreateSuccess(result);
                }
                else
                {
                    return ApiResponse<T>.CreateError($"HTTP {response.StatusCode}: {responseContent}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteCertificateLog(inn, $"ApiHelper.NK_ERROR[{requestId}]", ex.ToString());
                return ApiResponse<T>.CreateError($"Exception: {ex.Message}");
            }
        }

        // 1. Получение информации об участнике
        public static async Task<ApiResponse<List<ParticipantInfo>>> GetParticipantInfoAsync(string inn)
        {
            var url = $"https://markirovka.crpt.ru/api/v3/true-api/participants?inns={inn}";
            return await ExecuteRequestAsync<List<ParticipantInfo>>(HttpMethod.Get, url);
        }

        // 2. Привязка GTIN в НК (с поддержкой API Key)
        public static async Task<ApiResponse<string>> SendSetGtinLinkAsync(long goodId, string setGtin, string apiKey = null)
        {
            var payload = new[]
            {
                new
                {
                    good_id = goodId,
                    moderation = 1,
                    set_gtins = new[] { new { gtin = setGtin, quantity = 1 } }
                }
            };

            return await ExecuteNkRequestAsync<string>(HttpMethod.Post, "feed", payload, apiKey);
        }

        // 3. Другие методы НК можно добавить по аналогии
        public static async Task<ApiResponse<T>> GetNkProductsAsync<T>(string apiKey = null)
        {
            return await ExecuteNkRequestAsync<T>(HttpMethod.Get, "products", null, apiKey);
        }

        // Метод извлечения ИНН из сертификата
        private static string ExtractInnFromCertificate(X509Certificate2 certificate)
        {
            if (certificate == null || string.IsNullOrEmpty(certificate.Subject))
                return string.Empty;

            try
            {
                var innMatch = System.Text.RegularExpressions.Regex.Match(certificate.Subject, @"ИНН=(\d+)");
                return innMatch.Success ? innMatch.Groups[1].Value : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        // Метод извлечения ИНН из строки subject (для обратной совместимости)
        private static string ExtractInnFromString(string subject)
        {
            if (string.IsNullOrEmpty(subject))
                return string.Empty;

            try
            {
                var innMatch = System.Text.RegularExpressions.Regex.Match(subject, @"ИНН=(\d+)");
                return innMatch.Success ? innMatch.Groups[1].Value : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public class ApiResponse<T>
    {
        public bool IsSuccess
        {
            get; set;
        }
        public T Data
        {
            get; set;
        }
        public string ErrorMessage
        {
            get; set;
        }

        public static ApiResponse<T> CreateSuccess(T data)
        {
            return new ApiResponse<T> { IsSuccess = true, Data = data };
        }

        public static ApiResponse<T> CreateError(string error)
        {
            return new ApiResponse<T> { IsSuccess = false, ErrorMessage = error };
        }
    }

    public class ParticipantInfo
    {
        public string Inn { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<string> ProductGroups { get; set; } = new List<string>();
        public List<ProductGroupInfo> ProductGroupInfo { get; set; } = new List<ProductGroupInfo>();
        public bool Is_registered
        {
            get; set;
        }
        public bool Is_kfh
        {
            get; set;
        }
    }

    public class ProductGroupInfo
    {
        public string ProductGroup { get; set; } = string.Empty;
        public bool Farmer
        {
            get; set;
        }
    }
}