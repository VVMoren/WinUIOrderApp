using System;
using System.Collections.Generic;
using System.Linq;
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

        public async Task<IReadOnlyList<NationalCatalogGood>> LoadAllGoodsAsync(string apiKey = null, CancellationToken cancellationToken = default)
        {
            var result = new List<NationalCatalogGood>();
            var offset = 0;
            var total = int.MaxValue;

            while (offset < total)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var url = $"product-list?limit={ProductListPageSize}&offset={offset}&from_date={FromDate}&to_date={ToDate}";

                var response = await ApiHelper.ExecuteNkRequestAsync<NationalCatalogProductListResponse>(
                    System.Net.Http.HttpMethod.Get, url, null, apiKey);

                if (!response.IsSuccess)
                    throw new System.Net.Http.HttpRequestException($"Не удалось получить список товаров: {response.ErrorMessage}");

                if (response.Data?.Result?.Goods == null)
                    break;

                if (total == int.MaxValue)
                    total = response.Data.Result.Total;

                result.AddRange(response.Data.Result.Goods);

                if (response.Data.Result.Goods.Count == 0)
                    break;

                offset += response.Data.Result.Goods.Count;
            }

            return result;
        }

        public async Task<IReadOnlyList<NationalCatalogProductInfo>> LoadProductInfoAsync(
            List<string> gtins, string apiKey = null, CancellationToken cancellationToken = default)
        {
            if (gtins == null || gtins.Count == 0)
                return Array.Empty<NationalCatalogProductInfo>();

            var result = new List<NationalCatalogProductInfo>();

            // Разбиваем на пачки по 50 GTIN
            var chunks = gtins
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Select(g => g.Trim())
                .Where(g => !string.IsNullOrEmpty(g))
                .Select((g, index) => new { g, index })
                .GroupBy(x => x.index / ProductInfoBatchSize)
                .Select(g => g.Select(x => x.g).ToList())
                .ToList();

            foreach (var chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var requestBody = new { gtins = chunk.ToArray() };

                var response = await ApiHelper.ExecuteNkRequestAsync<NationalCatalogProductInfoResponse>(
                    System.Net.Http.HttpMethod.Post, "product/info", requestBody, apiKey);

                if (!response.IsSuccess)
                    throw new System.Net.Http.HttpRequestException($"Не удалось получить данные товаров: {response.ErrorMessage}");

                if (response.Data?.Results != null)
                    result.AddRange(response.Data.Results);
            }

            return result;
        }
    }
}