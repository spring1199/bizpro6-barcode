using System;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Test
{
    class Program
    {
        static void Main()
        {
            try
            {
                var responseString = File.ReadAllText(@"C:\Users\mooji\OneDrive\Desktop\mobicom-barcode-v2\api_latest_response.txt");
                var directResult = JsonConvert.DeserializeObject<ResourceResultWrapper>(responseString);
                var items = directResult?.Result?.Items ?? new List<ResourceItem>();
                int c = 0;
                foreach (var item in items)
                {
                    if (!string.IsNullOrEmpty(item.DocumentJson))
                    {
                        try
                        {
                            var parsed = JsonConvert.DeserializeObject<ResourceDocument>(item.DocumentJson);
                            c++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error parsing item {item.Id}: {ex.Message}");
                            return;
                        }
                    }
                }
                Console.WriteLine($"Success! Parsed {c} documents.");
            }
            catch(Exception ex)
            {
                Console.WriteLine("Main error: " + ex.Message);
            }
        }
    }

    public class ResourceResultWrapper
    {
        [JsonProperty("result")]
        public ResourceResult? Result { get; set; }
    }

    public class ResourceResult
    {
        [JsonProperty("totalCount")]
        public int TotalCount { get; set; }
        [JsonProperty("items")]
        public List<ResourceItem> Items { get; set; } = new List<ResourceItem>();
    }

    public class ResourceItem
    {
        [JsonProperty("id")]
        public long Id { get; set; }
        [JsonProperty("document")]
        public string DocumentJson { get; set; } = string.Empty;
    }

    public class ResourceDocument
    {
        [JsonProperty("product")]
        public ProductDto Product { get; set; } = new();

        [JsonProperty("product_rfid")]
        public ProductRfidDto ProductRfid { get; set; } = new();
    }

    public class ProductDto
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("itemCode")]
        public string? ItemCode { get; set; }

        [JsonProperty("measureUnit")]
        public string? MeasureUnit { get; set; }

        [JsonProperty("cost")]
        public decimal Cost { get; set; }
        
        [JsonProperty("CreationTime")]
        public DateTime CreationTime { get; set; }
    }

    public class ProductRfidDto
    {
        [JsonProperty("rfid")]
        public string? Rfid { get; set; }

        [JsonProperty("branch")]
        public string? Branch { get; set; }

        [JsonProperty("boxNumber")]
        public string? BoxNumber { get; set; }

        [JsonProperty("status")]
        public int Status { get; set; }

        [JsonProperty("isPrint")]
        public object? IsPrintRaw { get; set; }

        [JsonProperty("lastPrintedTime")]
        public DateTime? LastPrintedTime { get; set; }

        [JsonProperty("printErrorMessage")]
        public string? PrintErrorMessage { get; set; }
    }
}
