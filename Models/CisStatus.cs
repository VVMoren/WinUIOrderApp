using System.Text.Json.Serialization;

namespace WinUIOrderApp.Models
{
    public class CisStatus
    {
        [JsonPropertyName("id")]
        public int Id
        {
            get; set;
        }

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}