using BarTenderClone.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BarTenderClone.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly HttpClient _httpClient;
        private readonly ISessionService _sessionService;

        public AuthenticationService(HttpClient httpClient, ISessionService sessionService)
        {
            _httpClient = httpClient;
            _sessionService = sessionService;
        }

        public string AccessToken => _sessionService.AccessToken ?? string.Empty;

        public bool IsAuthenticated => _sessionService.IsAuthenticated;

        public async Task<bool> LoginAsync(string tenancyName, string username, string password)
        {
            // ... (keep request prep) ...
            var request = new LoginRequest
            {
                TenancyName = tenancyName,
                UserNameOrEmailAddress = username,
                Password = password,
                RememberClient = true
            };

            var jsonContent = JsonConvert.SerializeObject(request);
            try { System.IO.File.WriteAllText(@"C:\Temp\login_request.txt", jsonContent); } catch { }
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync("/api/TokenAuth/Authenticate", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    
                    var wrapper = JsonConvert.DeserializeObject<AbpResponseWrapper<LoginResponse>>(responseString);
                    if (wrapper?.Result != null)
                    {
                        _sessionService.AccessToken = wrapper.Result.AccessToken;
                        _sessionService.TenantId = ExtractTenantIdFromToken(wrapper.Result.AccessToken);
                        return true;
                    }

                    var directResult = JsonConvert.DeserializeObject<LoginResponse>(responseString);
                    if (!string.IsNullOrEmpty(directResult?.AccessToken))
                    {
                        _sessionService.AccessToken = directResult.AccessToken;
                        _sessionService.TenantId = ExtractTenantIdFromToken(directResult.AccessToken);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login failed: {ex.Message}");
            }

            return false;
        }

        private static int? ExtractTenantIdFromToken(string? token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            try
            {
                var parts = token.Split('.');
                if (parts.Length < 2) return null;
                var payload = parts[1];
                // Base64url → Base64
                payload = payload.Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                var obj = JObject.Parse(json);
                var tenantId = obj["http://www.aspnetboilerplate.com/identity/claims/tenantId"]
                               ?? obj["tenantId"]
                               ?? obj["tenant_id"];
                if (tenantId != null && int.TryParse(tenantId.ToString(), out var id))
                    return id;
            }
            catch { }
            return null;
        }

        private class AbpResponseWrapper<T>
        {
            public T Result { get; set; }
            public bool Success { get; set; }
            public object Error { get; set; }
            public bool UnAuthorizedRequest { get; set; }
        }
    }
}
