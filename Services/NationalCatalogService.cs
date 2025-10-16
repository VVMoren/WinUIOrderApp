using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinUIOrderApp.Helpers;
using WinUIOrderApp.Models;

namespace WinUIOrderApp.Services
{
    public class NationalCatalogService
    {
        private const int ProductListPageSize = 1000;
        private const int ProductInfoBatchSize = 50;
        private static readonly string FromDate = Uri.EscapeDataString("2000-01-01 00:00:00");
        private static readonly string ToDate = Uri.EscapeDataString("2026-10-16 23:59:59");
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<IReadOnlyList<NationalCatalogGood>> LoadAllGoodsAsync(string token, CancellationToken cancellationToken)
        {
            using var client = CreateClient(token);
            var result = new List<NationalCatalogGood>();
            var offset = 0;
            var total = int.MaxValue;

            while (offset < total)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var url = $"https://xn--80aqu.xn----7sbabas4ajkhfocclk9d3cvfsa.xn--p1ai/v4/product-list?limit={ProductListPageSize}&offset={offset}&from_date={FromDate}&to_date={ToDate}";
                using var response = await client.GetAsync(url, cancellationToken);
                var payload = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    LogHelper.WriteLog("NationalCatalogService.ProductList.Error", $"{response.StatusCode}: {payload}");
                    throw new HttpRequestException($"Не удалось получить список товаров. Код: {response.StatusCode}");
                }

                var parsed = JsonSerializer.Deserialize<NationalCatalogProductListResponse>(payload, JsonOptions);
                if (parsed?.Result?.Goods == null)
                {
                    LogHelper.WriteLog("NationalCatalogService.ProductList.Empty", payload);
                    break;
                }

                if (total == int.MaxValue)
                    total = parsed.Result.Total;

                result.AddRange(parsed.Result.Goods);

                if (parsed.Result.Goods.Count == 0)
                    break;

                offset += parsed.Result.Goods.Count;
            }

            return result;
        }

        public async Task<IReadOnlyList<NationalCatalogProductInfo>> LoadProductInfoAsync(string token, IReadOnlyCollection<string> gtins, CancellationToken cancellationToken)
        {
            if (gtins.Count == 0)
                return Array.Empty<NationalCatalogProductInfo>();

            using var client = CreateClient(token);
            var result = new List<NationalCatalogProductInfo>();

            foreach (var chunk in gtins
                         .Where(g => !string.IsNullOrWhiteSpace(g))
                         .Select(g => g.Trim())
                         .Chunk(ProductInfoBatchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var requestBody = new
                {
                    gtins = chunk.ToArray()
                };

                using var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                using var response = await client.PostAsync("https://markirovka.crpt.ru/api/v4/true-api/product/info", content, cancellationToken);
                var payload = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    LogHelper.WriteLog("NationalCatalogService.ProductInfo.Error", $"{response.StatusCode}: {payload}");
                    throw new HttpRequestException($"Не удалось получить данные товаров. Код: {response.StatusCode}");
                }

                var parsed = JsonSerializer.Deserialize<NationalCatalogProductInfoResponse>(payload, JsonOptions);
                if (parsed?.Results != null)
                {
                    result.AddRange(parsed.Results);
                }
            }

            return result;
        }

        private static HttpClient CreateClient(string token)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }
    }
}
