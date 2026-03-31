using BarTenderClone.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace BarTenderClone.Services
{
    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IAuthenticationService _authService;
        private const string ApiUrl = "/api/services/app/Resource/Resources";

        public ApiService(HttpClient httpClient, IAuthenticationService authService)
        {
            _httpClient = httpClient;
            _authService = authService;
        }

        public async Task<ResourceResult?> GetResourcesAsync(int skip = 0, int take = 25, string filter = "")
        {
            if (!_authService.IsAuthenticated || string.IsNullOrEmpty(_authService.AccessToken))
            {
                throw new System.Exception("Not authenticated. Please log in again.");
            }

            var requestModel = new ResourceRequest
            {
                RequireTotalCount = true,
                Skip = skip,
                Take = take,
                Key = "tms_product_rfid",
                Joins = new List<ResourceJoin>
                {
                    new ResourceJoin { Sid = "tms_product_rfid", Tid = "tms_product", Type = "left join", Sf = "product_id", Tf = "id" }
                },
                Sort = new List<ResourceSort>
                {
                    new ResourceSort { Selector = "tms_product_rfid.CreationTime", Desc = true }
                }
            };

            var jsonContent = JsonConvert.SerializeObject(requestModel);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
            request.Content = content;
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authService.AccessToken);

            var response = await _httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            // Log response for debugging
            try {
                System.IO.Directory.CreateDirectory(@"C:\Temp");
                await System.IO.File.WriteAllTextAsync(@"C:\Temp\api_request.txt", jsonContent);
                await System.IO.File.WriteAllTextAsync(@"C:\Temp\api_response.txt", responseString);
            } catch { }

            if (!response.IsSuccessStatusCode)
            {
                throw new System.Exception($"API Error ({response.StatusCode}): {responseString}");
            }

            // Try to deserialize as ABP wrapper first
            try
            {
                var wrapper = JsonConvert.DeserializeObject<ResourceResponseWrapper>(responseString);
                if (wrapper?.Result != null)
                {
                    foreach (var item in wrapper.Result.Items)
                    {
                        if (!string.IsNullOrEmpty(item.DocumentJson))
                        {
                            try
                            {
                                item.ParsedDocument = JsonConvert.DeserializeObject<ResourceDocument>(item.DocumentJson);
                            }
                            catch { /* Ignore parsing error for individual items */ }
                        }
                    }
                    return wrapper.Result;
                }
            }
            catch { }

            try 
            {
                var directResult = JsonConvert.DeserializeObject<ResourceResult>(responseString);
                if (directResult != null)
                {
                    foreach (var item in directResult.Items)
                    {
                        if (!string.IsNullOrEmpty(item.DocumentJson))
                        {
                            try
                            {
                                item.ParsedDocument = JsonConvert.DeserializeObject<ResourceDocument>(item.DocumentJson);
                            }
                            catch { /* Ignore parsing error for individual items */ }
                        }
                    }
                    return directResult;
                }
            }
            catch { }

            throw new System.Exception($"Failed to deserialize response. Length: {responseString.Length}. Check 'api_latest_response.txt' for details.");
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

                var updateUrl = "/api/services/app/ResourceManager/CreateOrUpdateResource";

                var payload = new
                {
                    Id = rfidId,
                    Key = "tms_product_rfid",
                    Document = rfidPart
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, updateUrl);
                request.Content = content;
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authService.AccessToken);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"UpdatePrintStatus failed: {response.StatusCode}\n{errorBody}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdatePrintStatus exception: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PrintAndPushRfidAsync(long productRfidId)
        {
            if (!_authService.IsAuthenticated || string.IsNullOrEmpty(_authService.AccessToken))
                return false;

            var url = $"/api/services/app/AssetSync/PrintAndPushRfid?productRfidId={productRfidId}";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authService.AccessToken);

            try
            {
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PrintAndPushRfid exception: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> EnqueueBranchSyncAsync()
        {
            if (!_authService.IsAuthenticated || string.IsNullOrEmpty(_authService.AccessToken))
                return false;

            var url = "/api/services/app/Asset/EnqueueBranchSync";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authService.AccessToken);

            try
            {
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnqueueBranchSync exception: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> EnqueueEquipmentSyncAsync()
        {
            if (!_authService.IsAuthenticated || string.IsNullOrEmpty(_authService.AccessToken))
                return false;

            var url = "/api/services/app/Asset/EnqueueEquipmentSync";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authService.AccessToken);

            try
            {
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnqueueEquipmentSync exception: {ex.Message}");
                return false;
            }
        }
    }
}
