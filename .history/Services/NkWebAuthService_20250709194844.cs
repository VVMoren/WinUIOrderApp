using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace markapp.Services
{
    public static class NkWebAuthService
    {
        public static async Task<NkAuthResult?> GetNkTokenAndCookiesAsync(
            string certName,
            string cryptcpExe,
            string dataFile,
            string signedDataFile,
            string nkWeb,
            string fingerprint,
            string inn)
        {
            var cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler { CookieContainer = cookieContainer };
            using var client = new HttpClient(handler);

            var challenge = await client.GetAsync($"{nkWeb}/rest/signed-data");
            challenge.EnsureSuccessStatusCode();
            var json = await challenge.Content.ReadAsStringAsync();
            var data = JsonDocument.Parse(json).RootElement.GetProperty("data").GetString();

            await File.WriteAllBytesAsync(dataFile, Convert.FromBase64String(data));

            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{cryptcpExe}\" -sign -dn \"CN={certName}\" \"{dataFile}\" \"{signedDataFile}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            await proc.WaitForExitAsync();
            if (proc.ExitCode != 0) throw new Exception("Ошибка подписи");

            var payload = new
            {
                signature = Convert.ToBase64String(await File.ReadAllBytesAsync(signedDataFile)),
                fingerprint,
                inn
            };

            var response = await client.PostAsync($"{nkWeb}/rest/certlogin", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            var authJson = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(authJson).RootElement;

            return new NkAuthResult
            {
                CsrfToken = doc.GetProperty("csrfToken").GetString(),
                SessionId = doc.GetProperty("sessionId").GetString(),
                SessionName = doc.GetProperty("sessionName").GetString()
            };
        }
    }
}
