using BarTenderClone.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BarTenderClone.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly HttpClient _httpClient;
        private readonly ISessionService _sessionService;
        private readonly string[] _configuredBaseUrls;
        private readonly string _oidcAuthority;
        private readonly string _oidcClientId;
        private readonly string _oidcScope;
        private readonly string _oidcRedirectUri;
        private readonly string _preferredApiBaseUrl;

        public AuthenticationService(HttpClient httpClient, ISessionService sessionService, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _sessionService = sessionService;
            _configuredBaseUrls = GetConfiguredBaseUrls(configuration);
            _oidcAuthority = NormalizeBaseUrl(configuration["OidcSettings:Authority"] ?? "https://auth.bizpro.mn");
            _oidcClientId = configuration["OidcSettings:ClientId"] ?? "nextjs_public";
            _oidcScope = configuration["OidcSettings:Scope"] ?? "openid profile email offline_access bizpro_api";
            _oidcRedirectUri = configuration["OidcSettings:RedirectUri"] ?? "http://localhost:3000/auth/callback";
            _preferredApiBaseUrl = NormalizeBaseUrl(configuration["OidcSettings:PreferredApiBaseUrl"] ?? "https://bizpro.mn");
        }

        public string AccessToken => _sessionService.AccessToken ?? string.Empty;

        public bool IsAuthenticated => _sessionService.IsAuthenticated;

        public async Task<bool> LoginAsync(string tenancyName, string username, string password)
        {
            _sessionService.AccessToken = null;
            _sessionService.TenantId = null;
            _sessionService.ApiBaseUrl = null;

            if (await TryLoginWithAuthServerAsync(tenancyName, username, password))
                return true;

            return await TryLegacyLoginAsync(tenancyName, username, password);
        }

        private async Task<bool> TryLoginWithAuthServerAsync(string tenancyName, string username, string password)
        {
            try
            {
                var codeVerifier = GenerateCodeVerifier();
                var codeChallenge = CreateCodeChallenge(codeVerifier);
                var state = Guid.NewGuid().ToString("N");
                var authorizePath = BuildAuthorizePath(codeChallenge, state);
                var loginPath = $"/account/login?returnUrl={Uri.EscapeDataString(authorizePath)}&tenancyName={Uri.EscapeDataString(tenancyName ?? string.Empty)}";

                using var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    CookieContainer = new CookieContainer(),
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                };
                using var authClient = new HttpClient(handler)
                {
                    BaseAddress = new Uri($"{_oidcAuthority}/")
                };

                var loginPageResponse = await authClient.GetAsync(loginPath);
                var loginPageHtml = await loginPageResponse.Content.ReadAsStringAsync();
                await WriteTraceAsync("oidc_login_page.html", loginPageHtml);

                if (!loginPageResponse.IsSuccessStatusCode)
                    return false;

                var requestVerificationToken = ExtractRequestVerificationToken(loginPageHtml);
                if (string.IsNullOrWhiteSpace(requestVerificationToken))
                    return false;

                using var loginContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("__RequestVerificationToken", requestVerificationToken),
                    new KeyValuePair<string, string>("ReturnUrl", authorizePath),
                    new KeyValuePair<string, string>("TenancyName", tenancyName ?? string.Empty),
                    new KeyValuePair<string, string>("Username", username ?? string.Empty),
                    new KeyValuePair<string, string>("Password", password ?? string.Empty),
                    new KeyValuePair<string, string>("RememberMe", "false")
                });

                using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/account/login");
                loginRequest.Content = loginContent;
                loginRequest.Headers.Referrer = BuildUri(_oidcAuthority, loginPath);

                var loginResponse = await authClient.SendAsync(loginRequest);
                var loginResponseBody = await loginResponse.Content.ReadAsStringAsync();
                await WriteTraceAsync(
                    "oidc_login_response.txt",
                    $"STATUS: {(int)loginResponse.StatusCode}{Environment.NewLine}LOCATION: {loginResponse.Headers.Location}{Environment.NewLine}{loginResponseBody}");

                if (loginResponse.StatusCode != HttpStatusCode.Redirect &&
                    loginResponse.StatusCode != HttpStatusCode.Found)
                {
                    return false;
                }

                if (loginResponse.Headers.Location == null)
                    return false;

                var authorizeResponse = await authClient.GetAsync(authorizePath);
                var authorizeResponseBody = await authorizeResponse.Content.ReadAsStringAsync();
                await WriteTraceAsync(
                    "oidc_authorize_response.txt",
                    $"STATUS: {(int)authorizeResponse.StatusCode}{Environment.NewLine}LOCATION: {authorizeResponse.Headers.Location}{Environment.NewLine}{authorizeResponseBody}");

                if ((authorizeResponse.StatusCode != HttpStatusCode.Redirect &&
                     authorizeResponse.StatusCode != HttpStatusCode.Found) ||
                    authorizeResponse.Headers.Location == null)
                {
                    return false;
                }

                var callbackUri = ResolveUri(_oidcAuthority, authorizeResponse.Headers.Location);
                var code = GetQueryParameter(callbackUri, "code");
                var returnedState = GetQueryParameter(callbackUri, "state");
                if (string.IsNullOrWhiteSpace(code) || !string.Equals(state, returnedState, StringComparison.Ordinal))
                    return false;

                using var tokenContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("client_id", _oidcClientId),
                    new KeyValuePair<string, string>("redirect_uri", _oidcRedirectUri),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("code_verifier", codeVerifier)
                });

                var tokenResponse = await authClient.PostAsync("/connect/token", tokenContent);
                var tokenResponseJson = await tokenResponse.Content.ReadAsStringAsync();
                await WriteTraceAsync(
                    "oidc_token_response.txt",
                    $"STATUS: {(int)tokenResponse.StatusCode}{Environment.NewLine}{tokenResponseJson}");

                if (!tokenResponse.IsSuccessStatusCode)
                    return false;

                var tokenObject = JObject.Parse(tokenResponseJson);
                var accessToken = tokenObject.Value<string>("access_token");
                if (string.IsNullOrWhiteSpace(accessToken))
                    return false;

                _sessionService.AccessToken = accessToken;
                _sessionService.TenantId = ExtractTenantIdFromToken(accessToken);
                _sessionService.ApiBaseUrl = _preferredApiBaseUrl;
                return true;
            }
            catch (Exception ex)
            {
                await WriteTraceAsync("oidc_login_error.txt", ex.ToString());
                return false;
            }
        }

        private async Task<bool> TryLegacyLoginAsync(string tenancyName, string username, string password)
        {
            var request = new LoginRequest
            {
                TenancyName = tenancyName,
                UserNameOrEmailAddress = username,
                Password = password,
                RememberClient = true
            };

            var jsonContent = JsonConvert.SerializeObject(request);
            await WriteTraceAsync("login_request.txt", jsonContent);

            foreach (var baseUrl in GetCandidateBaseUrls())
            {
                try
                {
                    using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync(BuildUri(baseUrl, "/api/TokenAuth/Authenticate"), content);

                    if (!response.IsSuccessStatusCode)
                        continue;

                    var responseString = await response.Content.ReadAsStringAsync();
                    await WriteTraceAsync("login_response.txt", $"BASE URL: {baseUrl}{Environment.NewLine}{responseString}");

                    var wrapper = JsonConvert.DeserializeObject<AbpResponseWrapper<LoginResponse>>(responseString);
                    if (wrapper?.Result != null)
                    {
                        _sessionService.AccessToken = wrapper.Result.AccessToken;
                        _sessionService.TenantId = ExtractTenantIdFromToken(wrapper.Result.AccessToken);
                        _sessionService.ApiBaseUrl = baseUrl;
                        return true;
                    }

                    var directResult = JsonConvert.DeserializeObject<LoginResponse>(responseString);
                    if (!string.IsNullOrEmpty(directResult?.AccessToken))
                    {
                        _sessionService.AccessToken = directResult.AccessToken;
                        _sessionService.TenantId = ExtractTenantIdFromToken(directResult.AccessToken);
                        _sessionService.ApiBaseUrl = baseUrl;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    await WriteTraceAsync("legacy_login_error.txt", $"BASE URL: {baseUrl}{Environment.NewLine}{ex}");
                }
            }

            return false;
        }

        private string BuildAuthorizePath(string codeChallenge, string state)
        {
            var parameters = new[]
            {
                new KeyValuePair<string, string>("client_id", _oidcClientId),
                new KeyValuePair<string, string>("redirect_uri", _oidcRedirectUri),
                new KeyValuePair<string, string>("response_type", "code"),
                new KeyValuePair<string, string>("scope", _oidcScope),
                new KeyValuePair<string, string>("code_challenge", codeChallenge),
                new KeyValuePair<string, string>("code_challenge_method", "S256"),
                new KeyValuePair<string, string>("state", state)
            };

            return "/connect/authorize?" + string.Join("&",
                parameters.Select(parameter =>
                    $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));
        }

        private IEnumerable<string> GetCandidateBaseUrls()
        {
            return _configuredBaseUrls
                .Prepend(_sessionService.ApiBaseUrl)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(NormalizeBaseUrl)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static string[] GetConfiguredBaseUrls(IConfiguration configuration)
        {
            var urls = new List<string>();
            var primary = configuration["ApiSettings:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(primary))
                urls.Add(primary);

            var fallbackUrls = configuration.GetSection("ApiSettings:FallbackBaseUrls").GetChildren();
            foreach (var child in fallbackUrls)
            {
                if (!string.IsNullOrWhiteSpace(child.Value))
                    urls.Add(child.Value);
            }

            return urls
                .Select(NormalizeBaseUrl)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string ExtractRequestVerificationToken(string html)
        {
            var match = Regex.Match(
                html,
                "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            return WebUtility.HtmlDecode(match.Success ? match.Groups[1].Value : string.Empty);
        }

        private static string GenerateCodeVerifier()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Base64UrlEncode(bytes);
        }

        private static string CreateCodeChallenge(string codeVerifier)
        {
            var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
            return Base64UrlEncode(hash);
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string GetQueryParameter(Uri uri, string key)
        {
            var query = uri.Query.TrimStart('?');
            if (string.IsNullOrWhiteSpace(query))
                return string.Empty;

            foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var split = part.Split('=', 2);
                var parameterKey = Uri.UnescapeDataString(split[0]);
                if (!string.Equals(parameterKey, key, StringComparison.Ordinal))
                    continue;

                return split.Length > 1 ? Uri.UnescapeDataString(split[1]) : string.Empty;
            }

            return string.Empty;
        }

        private static Uri ResolveUri(string baseUrl, Uri location)
        {
            return location.IsAbsoluteUri ? location : BuildUri(baseUrl, location.ToString());
        }

        private static string NormalizeBaseUrl(string? baseUrl)
        {
            return (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        }

        private static Uri BuildUri(string baseUrl, string relativePath)
        {
            return new Uri($"{NormalizeBaseUrl(baseUrl)}/{relativePath.TrimStart('/')}");
        }

        private static async Task WriteTraceAsync(string fileName, string content)
        {
            try
            {
                System.IO.Directory.CreateDirectory(@"C:\Temp");
                await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(@"C:\Temp", fileName), content);
            }
            catch
            {
            }
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
