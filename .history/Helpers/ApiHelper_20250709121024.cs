using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using markapp.Models;
using markapp.Helpers;

namespace markapp.Helpers
{
    public static class ApiHelper
    {
        public static async Task SendSetGtinLinkAsync(long goodId, string setGtin)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AppState.Instance.Token);
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            var payload = new[]
            {
                new
                {
                    good_id = goodId,
                    moderation = 1,
                    set_gtins = new[] { new { gtin = setGtin, quantity = 1 } }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://markirovka.crpt.ru/api/v3/true-api/nk/feed?apikey=uugdjuxx3uhw9l05", content);
            var result = await response.Content.ReadAsStringAsync();

            LogHelper.WriteLog("Привязка GTIN", $"▶ POST для good_id={goodId}, setGTIN={setGtin}\nОтвет: {result}");

            response.EnsureSuccessStatusCode();
        }
    }
}
