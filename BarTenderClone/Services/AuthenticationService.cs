using BarTenderClone.Models;
using Newtonsoft.Json;
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
                        return true;
                    }

                    var directResult = JsonConvert.DeserializeObject<LoginResponse>(responseString);
                    if (!string.IsNullOrEmpty(directResult?.AccessToken))
                    {
                        _sessionService.AccessToken = directResult.AccessToken;
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

        private class AbpResponseWrapper<T>
        {
            public T Result { get; set; }
            public bool Success { get; set; }
            public object Error { get; set; }
            public bool UnAuthorizedRequest { get; set; }
        }
    }
}
