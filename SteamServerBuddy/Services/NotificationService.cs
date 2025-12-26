using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SteamServerBuddy.Services
{
    public class NotificationService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task SendDiscordAlertAsync(string webhookUrl, string message, string color = null)
        {
            if (string.IsNullOrWhiteSpace(webhookUrl)) return;

            try
            {
                var payload = new
                {
                    content = message
                };

                // Advanced embed payload if needed, but keeping it simple for now
                if (color != null)
                {
                    // If we wanted to use embeds we would structure it differently
                    // For now, simple text message
                }

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await _httpClient.PostAsync(webhookUrl, content);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to send Discord alert: {ex.Message}");
            }
        }
    }
}
