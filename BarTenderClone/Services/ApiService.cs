using BarTenderClone.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace BarTenderClone.Services
{
    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IAuthenticationService _authService;
        private readonly ISessionService _sessionService;
        private readonly string[] _configuredBaseUrls;
        private const string ApiUrl = "/api/services/app/Resource/Resources";
        private const string ResourceDefinitionsUrl = "/api/services/app/DynamicEntity/GetResourceDefinitions";
        private const string DynamicEntityResourcesByKeyUrl = "/api/services/app/DynamicEntity/GetResourcesByKeyFilter";
        private static readonly ResourceQueryProfile[] ResourceQueryProfiles =
        {
            new("tms_product_rfid", "tms_product", "tms_product_rfid.CreationTime"),
            new("product_rfid", "product", "product_rfid.CreationTime")
        };

        public ApiService(HttpClient httpClient, IAuthenticationService authService, ISessionService sessionService, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _authService = authService;
            _sessionService = sessionService;
            _configuredBaseUrls = GetConfiguredBaseUrls(configuration);
        }

        public async Task<ResourceResult?> GetResourcesAsync(int skip = 0, int take = 25, string filter = "")
        {
            if (!_authService.IsAuthenticated || string.IsNullOrEmpty(_authService.AccessToken))
            {
                throw new System.Exception("Not authenticated. Please log in again.");
            }

            var failures = new List<string>();
            
            foreach (var baseUrl in GetCandidateBaseUrls())
            {
                var attemptedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var profile in ResourceQueryProfiles)
                {
                    attemptedKeys.Add(profile.ResourceKey);
                    var requestModel = CreateResourceRequest(profile, skip, take);
                    var jsonContent = JsonConvert.SerializeObject(requestModel);
                    using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(baseUrl, ApiUrl));
                    request.Content = content;
                    AddAuthorizationHeaders(request);

                    HttpResponseMessage response;
                    string responseString;

                    try
                    {
                        response = await _httpClient.SendAsync(request);
                        responseString = await response.Content.ReadAsStringAsync();
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{baseUrl} -> {profile.ResourceKey}: {ex.Message}");
                        continue;
                    }

                    await WriteApiTraceAsync(baseUrl, jsonContent, responseString);

                    if (!response.IsSuccessStatusCode)
                    {
                        if (IsMissingResourceError(responseString))
                        {
                            failures.Add($"{baseUrl} -> {profile.ResourceKey}: missing resource");
                            continue;
                        }

                        failures.Add($"{baseUrl} -> {profile.ResourceKey}: API {(int)response.StatusCode}");
                        continue;
                    }

                    var parsedResult = TryDeserializeResourceResult(responseString, profile.ResourceKey);
                    if (parsedResult != null)
                    {
                        _sessionService.ApiBaseUrl = NormalizeBaseUrl(baseUrl);
                        return parsedResult;
                    }

                    failures.Add($"{baseUrl} -> {profile.ResourceKey}: unexpected response");
                }

                var discoveredKeys = await DiscoverCandidateResourceKeysAsync(baseUrl);
                foreach (var key in discoveredKeys.Where(key => !attemptedKeys.Contains(key)))
                {
                    attemptedKeys.Add(key);

                    var discoveredResult = await TryGetResourcesByKeyFilterAsync(baseUrl, key, skip, take);
                    if (discoveredResult != null && discoveredResult.Items.Count > 0)
                    {
                        _sessionService.ApiBaseUrl = NormalizeBaseUrl(baseUrl);
                        return discoveredResult;
                    }

                    failures.Add(discoveredResult != null
                        ? $"{baseUrl} -> {key}: no rows returned"
                        : $"{baseUrl} -> {key}: lookup failed");
                }
            }

            var failureSummary = failures.Count > 0 ? $" Tried: {string.Join("; ", failures)}." : string.Empty;
            throw new System.Exception($"Could not load RFID data from any configured API host.{failureSummary}");
        }

        public async Task<bool> UpdatePrintStatusAsync(
            ResourceItem item,
            bool isPrinted,
            DateTime? lastPrintedTime = null,
            string? errorMessage = null)
        {
            if (!_authService.IsAuthenticated || string.IsNullOrEmpty(_authService.AccessToken))
                return false;

            try
            {
                // Parse the existing document
                var document = JObject.Parse(item.DocumentJson);
                
                // Locate the RFID portion
                // The key could be "product_rfid" or "tms_product_rfid" depending on joins
                JToken rfidPart = document["tms_product_rfid"] ?? document["product_rfid"];
                
                if (rfidPart == null || !rfidPart.HasValues)
                {
                    System.Diagnostics.Debug.WriteLine("UpdatePrintStatus: RFID document part not found.");
                    return false;
                }

                // Get ID
                long rfidId = rfidPart.Value<long>("id");
                if (rfidId <= 0)
                {
                    System.Diagnostics.Debug.WriteLine("UpdatePrintStatus: Invalid RFID ID.");
                    return false;
                }

                // Update isPrint (2 = Printed, 1 = Not printed)
                rfidPart["isPrint"] = isPrinted ? 2 : 1;
                rfidPart["LastPrintedTime"] = (lastPrintedTime ?? DateTime.Now).ToString("O");

                var payload = new
                {
                    Id = rfidId,
                    Key = ResolveResourceKey(item, document),
                    Document = rfidPart
                };

                var json = JsonConvert.SerializeObject(payload);
                foreach (var baseUrl in GetCandidateBaseUrls())
                {
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var request = new HttpRequestMessage(
                        HttpMethod.Post,
                        BuildUri(baseUrl, "/api/services/app/ResourceManager/CreateOrUpdateResource"));
                    request.Content = content;
                    AddAuthorizationHeaders(request);

                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        _sessionService.ApiBaseUrl = NormalizeBaseUrl(baseUrl);
                        return true;
                    }

                    var errorBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"UpdatePrintStatus failed for {baseUrl}: {response.StatusCode}\n{errorBody}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdatePrintStatus exception: {ex.Message}");
            }

            return false;
        }

        public async Task<bool> PrintAndPushRfidAsync(long productRfidId)
        {
            if (!_authService.IsAuthenticated || string.IsNullOrEmpty(_authService.AccessToken))
                return false;

            var url = $"/api/services/app/AssetSync/PrintAndPushRfid?productRfidId={productRfidId}";

            try
            {
                foreach (var baseUrl in GetCandidateBaseUrls())
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(baseUrl, url));
                    AddAuthorizationHeaders(request);

                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        _sessionService.ApiBaseUrl = NormalizeBaseUrl(baseUrl);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PrintAndPushRfid exception: {ex.Message}");
            }

            return false;
        }

        public async Task<bool> EnqueueBranchSyncAsync()
        {
            if (!_authService.IsAuthenticated || string.IsNullOrEmpty(_authService.AccessToken))
                return false;

            var url = "/api/services/app/Asset/EnqueueBranchSync";

            try
            {
                foreach (var baseUrl in GetCandidateBaseUrls())
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(baseUrl, url));
                    AddAuthorizationHeaders(request);

                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        _sessionService.ApiBaseUrl = NormalizeBaseUrl(baseUrl);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnqueueBranchSync exception: {ex.Message}");
            }

            return false;
        }

        public async Task<bool> EnqueueEquipmentSyncAsync()
        {
            if (!_authService.IsAuthenticated || string.IsNullOrEmpty(_authService.AccessToken))
                return false;

            var url = "/api/services/app/Asset/EnqueueEquipmentSync";

            try
            {
                foreach (var baseUrl in GetCandidateBaseUrls())
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(baseUrl, url));
                    AddAuthorizationHeaders(request);

                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        _sessionService.ApiBaseUrl = NormalizeBaseUrl(baseUrl);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnqueueEquipmentSync exception: {ex.Message}");
            }

            return false;
        }

        private static ResourceRequest CreateResourceRequest(ResourceQueryProfile profile, int skip, int take)
        {
            return new ResourceRequest
            {
                RequireTotalCount = true,
                Skip = skip,
                Take = take,
                Key = profile.ResourceKey,
                Joins = new List<ResourceJoin>
                {
                    new ResourceJoin
                    {
                        Sid = profile.ResourceKey,
                        Tid = profile.JoinedResourceKey,
                        Type = "left join",
                        Sf = "product_id",
                        Tf = "id"
                    }
                },
                Sort = new List<ResourceSort>
                {
                    new ResourceSort
                    {
                        Selector = profile.SortSelector,
                        Desc = true
                    }
                }
            };
        }

        private async Task<List<string>> DiscoverCandidateResourceKeysAsync(string baseUrl)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(baseUrl, ResourceDefinitionsUrl));
                AddAuthorizationHeaders(request);

                var response = await _httpClient.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return new List<string>();

                var definitions = TryDeserializeResourceDefinitions(responseString);
                if (definitions.Count == 0)
                    return new List<string>();

                return definitions
                    .Where(definition => !string.IsNullOrWhiteSpace(definition.Key))
                    .Select(definition => new
                    {
                        Key = definition.Key!,
                        Score = ScoreCandidateResource(definition)
                    })
                    .Where(candidate => candidate.Score > 0)
                    .OrderByDescending(candidate => candidate.Score)
                    .ThenBy(candidate => candidate.Key)
                    .Select(candidate => candidate.Key)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(10)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DiscoverCandidateResourceKeys failed for {baseUrl}: {ex.Message}");
                return new List<string>();
            }
        }

        private async Task<ResourceResult?> TryGetResourcesByKeyFilterAsync(string baseUrl, string key, int skip, int take)
        {
            var url = $"{DynamicEntityResourcesByKeyUrl}?key={Uri.EscapeDataString(key)}&Skip={skip}&Take={take}";
            var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(baseUrl, url));
            AddAuthorizationHeaders(request);

            var response = await _httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            await WriteApiTraceAsync(baseUrl, url, responseString);

            if (!response.IsSuccessStatusCode)
                return null;

            var dynamicItems = TryDeserializeDynamicEntityItems(responseString);
            if (dynamicItems == null)
                return null;

            var mappedItems = dynamicItems
                .Select(item => new ResourceItem
                {
                    Id = item.Id,
                    DocumentJson = item.Document ?? string.Empty,
                    ResourceKey = key
                })
                .ToList();

            HydrateItems(mappedItems, key);

            return new ResourceResult
            {
                Items = mappedItems,
                TotalCount = skip + mappedItems.Count + (mappedItems.Count == take ? 1 : 0)
            };
        }

        private static ResourceResult? TryDeserializeResourceResult(string responseString, string resourceKey)
        {
            try
            {
                var wrapper = JsonConvert.DeserializeObject<ResourceResponseWrapper>(responseString);
                if (wrapper?.Result != null)
                {
                    HydrateItems(wrapper.Result.Items, resourceKey);
                    return wrapper.Result;
                }
            }
            catch
            {
            }

            try
            {
                var directResult = JsonConvert.DeserializeObject<ResourceResult>(responseString);
                if (directResult != null)
                {
                    HydrateItems(directResult.Items, resourceKey);
                    return directResult;
                }
            }
            catch
            {
            }

            return null;
        }

        private static List<DynamicEntityDefinitionDto> TryDeserializeResourceDefinitions(string responseString)
        {
            try
            {
                var wrapper = JsonConvert.DeserializeObject<AbpResponseWrapper<List<DynamicEntityDefinitionDto>>>(responseString);
                if (wrapper?.Result != null)
                    return wrapper.Result;
            }
            catch
            {
            }

            try
            {
                var listWrapper = JsonConvert.DeserializeObject<ListResultDto<DynamicEntityDefinitionDto>>(responseString);
                if (listWrapper?.Items != null)
                    return listWrapper.Items;
            }
            catch
            {
            }

            try
            {
                var direct = JsonConvert.DeserializeObject<List<DynamicEntityDefinitionDto>>(responseString);
                if (direct != null)
                    return direct;
            }
            catch
            {
            }

            return new List<DynamicEntityDefinitionDto>();
        }

        private static List<DynamicEntityListDto>? TryDeserializeDynamicEntityItems(string responseString)
        {
            try
            {
                var wrapper = JsonConvert.DeserializeObject<AbpResponseWrapper<ListResultDto<DynamicEntityListDto>>>(responseString);
                if (wrapper?.Result?.Items != null)
                    return wrapper.Result.Items;
            }
            catch
            {
            }

            try
            {
                var direct = JsonConvert.DeserializeObject<ListResultDto<DynamicEntityListDto>>(responseString);
                if (direct?.Items != null)
                    return direct.Items;
            }
            catch
            {
            }

            return null;
        }

        private static void HydrateItems(IEnumerable<ResourceItem> items, string resourceKey)
        {
            foreach (var item in items)
            {
                item.ResourceKey = resourceKey;

                if (string.IsNullOrEmpty(item.DocumentJson))
                    continue;

                try
                {
                    item.ParsedDocument = JsonConvert.DeserializeObject<ResourceDocument>(item.DocumentJson);
                }
                catch
                {
                }
            }
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

            foreach (var child in configuration.GetSection("ApiSettings:FallbackBaseUrls").GetChildren())
            {
                if (!string.IsNullOrWhiteSpace(child.Value))
                    urls.Add(child.Value);
            }

            return urls
                .Select(NormalizeBaseUrl)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string NormalizeBaseUrl(string? baseUrl)
        {
            return (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        }

        private static Uri BuildUri(string baseUrl, string relativePath)
        {
            return new Uri($"{NormalizeBaseUrl(baseUrl)}/{relativePath.TrimStart('/')}");
        }

        private static async Task WriteApiTraceAsync(string baseUrl, string requestPayload, string responsePayload)
        {
            try
            {
                System.IO.Directory.CreateDirectory(@"C:\Temp");
                await System.IO.File.WriteAllTextAsync(
                    @"C:\Temp\api_request.txt",
                    $"BASE URL: {NormalizeBaseUrl(baseUrl)}{Environment.NewLine}{requestPayload}");
                await System.IO.File.WriteAllTextAsync(
                    @"C:\Temp\api_response.txt",
                    $"BASE URL: {NormalizeBaseUrl(baseUrl)}{Environment.NewLine}{responsePayload}");
            }
            catch
            {
            }
        }

        private static bool IsMissingResourceError(string responseString)
        {
            return responseString.IndexOf("resource not found", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AddAuthorizationHeaders(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authService.AccessToken);
            if (_sessionService.TenantId.HasValue)
                request.Headers.Add("Abp.TenantId", _sessionService.TenantId.Value.ToString());
        }

        private static int ScoreCandidateResource(DynamicEntityDefinitionDto definition)
        {
            var haystack = $"{definition.Key} {definition.Name}".ToLowerInvariant();
            var score = 0;

            if (haystack.Contains("rfid")) score += 10;
            if (haystack.Contains("product")) score += 6;
            if (haystack.Contains("asset")) score += 5;
            if (haystack.Contains("equipment")) score += 5;
            if (haystack.Contains("barcode")) score += 4;
            if (haystack.Contains("branch")) score -= 2;
            if (haystack.Contains("history")) score -= 4;
            if (haystack.Contains("log")) score -= 4;

            return score;
        }

        private static string ResolveResourceKey(ResourceItem item, JObject document)
        {
            if (!string.IsNullOrWhiteSpace(item.ResourceKey))
                return item.ResourceKey;

            if (document["tms_product_rfid"] != null)
                return "tms_product_rfid";

            if (document["product_rfid"] != null)
                return "product_rfid";

            return "tms_product_rfid";
        }

        private sealed record ResourceQueryProfile(
            string ResourceKey,
            string JoinedResourceKey,
            string SortSelector);

        private sealed class AbpResponseWrapper<T>
        {
            [JsonProperty("result")]
            public T? Result { get; set; }
        }

        private sealed class ListResultDto<T>
        {
            [JsonProperty("items")]
            public List<T> Items { get; set; } = new();
        }

        private sealed class DynamicEntityDefinitionDto
        {
            [JsonProperty("key")]
            public string? Key { get; set; }

            [JsonProperty("name")]
            public string? Name { get; set; }
        }

        private sealed class DynamicEntityListDto
        {
            [JsonProperty("id")]
            public long Id { get; set; }

            [JsonProperty("document")]
            public string? Document { get; set; }
        }
    }
}
