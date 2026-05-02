using System.Net.Http;
using System.Text.Json;

namespace CarHub.Services
{
    public class IpInfoService
    {
        private readonly HttpClient _http;
        private const string TOKEN = "56307bc3be192e";

        public IpInfoService(HttpClient http)
        {
            _http = http;
        }

        public async Task<string> GetCountryAsync(string ip)
        {
            var url = $"https://ipinfo.io/{ip}/json?token={TOKEN}";

            var json = await _http.GetStringAsync(url);

            var data = JsonSerializer.Deserialize<IpInfoResponse>(json);

            return data?.country ?? "AU";
        }
    }

    public class IpInfoResponse
    {
        public string ip { get; set; }
        public string country { get; set; }
    }
}