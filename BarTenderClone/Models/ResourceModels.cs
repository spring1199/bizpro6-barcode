using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace BarTenderClone.Models
{
    public class ResourceRequest
    {
        [JsonProperty("requireTotalCount")]
        public bool RequireTotalCount { get; set; } = true;

        [JsonProperty("searchOperation")]
        public string SearchOperation { get; set; } = "contains";

        [JsonProperty("skip")]
        public int Skip { get; set; } = 0;

        [JsonProperty("take")]
        public int Take { get; set; } = 25;

        [JsonProperty("key")]
        public string Key { get; set; } = "tms_product_rfid";

        [JsonProperty("joins")]
        public List<ResourceJoin> Joins { get; set; } = new List<ResourceJoin>();

        [JsonProperty("sort")]
        public List<ResourceSort> Sort { get; set; } = new List<ResourceSort>();

        [JsonProperty("userData")]
        public object UserData { get; set; } = new object();
    }

    public class ResourceJoin
    {
        [JsonProperty("sid")]
        public string Sid { get; set; } = string.Empty;

        [JsonProperty("tid")]
        public string Tid { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = "left join";

        [JsonProperty("sf")]
        public string Sf { get; set; } = string.Empty;

        [JsonProperty("tf")]
        public string Tf { get; set; } = string.Empty;
    }

    public class ResourceSort
    {
        [JsonProperty("selector")]
        public string Selector { get; set; } = string.Empty;

        [JsonProperty("desc")]
        public bool Desc { get; set; }
    }

    public class ResourceResponseWrapper
    {
        [JsonProperty("result")]
        public ResourceResult? Result { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("error")]
        public object? Error { get; set; }
    }

    public class ResourceResult
    {
        [JsonProperty("totalCount")]
        public int TotalCount { get; set; }

        [JsonProperty("items")]
        public List<ResourceItem> Items { get; set; } = new List<ResourceItem>();
    }

    public enum PrintStatus
    {
        NotPrinted,
        Printed,
        Error
    }

    public class ResourceItem : ObservableObject
    {
        private JObject? _documentObject;
        private IReadOnlyDictionary<string, string>? _documentFieldValues;

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("document")]
        public string DocumentJson { get; set; } = string.Empty;

        [JsonIgnore]
        public string ResourceKey { get; set; } = string.Empty;

        // We will parse 'DocumentJson' into this property manually or via a helper
        [JsonIgnore]
        public ResourceDocument? ParsedDocument { get; set; }

        [JsonProperty("tms_product")]
        public ProductDto? TmsProduct
        {
            set => ApplyTopLevelData(product: value);
        }

        [JsonProperty("product")]
        public ProductDto? ProductAlias
        {
            set => ApplyTopLevelData(product: value);
        }

        [JsonProperty("tms_product_rfid")]
        public ProductRfidDto? TmsProductRfid
        {
            set => ApplyTopLevelData(productRfid: value);
        }

        [JsonProperty("product_rfid")]
        public ProductRfidDto? ProductRfidAlias
        {
            set => ApplyTopLevelData(productRfid: value);
        }

        // Helper properties for UI Binding
        [JsonIgnore]
        public string ProductName => ParsedDocument?.Product?.Name ?? "Unknown";

        [JsonIgnore]
        public decimal Price => ParsedDocument?.Product?.Cost ?? 0;

        [JsonIgnore]
        public string Code => ParsedDocument?.Product?.ItemCode ?? string.Empty;

        [JsonIgnore]
        public string Rfid => ParsedDocument?.ProductRfid?.Rfid ?? string.Empty;

        [JsonIgnore]
        public string Branch => ParsedDocument?.ProductRfid?.Branch ?? string.Empty;

        [JsonIgnore]
        public string BoxNumber => ParsedDocument?.ProductRfid?.BoxNumber ?? string.Empty;

        [JsonIgnore]
        public string Status => ParsedDocument?.ProductRfid?.Status.ToString() ?? "0";

        [JsonIgnore]
        public string Unit => ParsedDocument?.Product?.MeasureUnit ?? string.Empty;

        [JsonIgnore]
        public string Color => "N/A"; // Not seen in JSON sample, placeholder

        [JsonIgnore]
        public DateTime CreationTime => ParsedDocument?.Product?.CreationTime ?? DateTime.MinValue;

        [JsonIgnore]
        public DateTime? RfidCreationTime => ParsedDocument?.ProductRfid?.CreationTime;

        [JsonIgnore]
        public DateTime AcquisitionDate => ParsedDocument?.ProductRfid?.AcquisitionDate
                                           ?? ParsedDocument?.ProductRfid?.CreationTime
                                           ?? ParsedDocument?.Product?.CreationTime
                                           ?? DateTime.MinValue;

        [JsonIgnore]
        public string AcquisitionDateFormatted => AcquisitionDate != DateTime.MinValue
            ? AcquisitionDate.ToString("yyyy-MM-dd")
            : string.Empty;

        [JsonIgnore]
        public DateTime DisplayDate => AcquisitionDate;

        [JsonIgnore]
        public string Category => ParsedDocument?.Product?.Category ?? string.Empty;

        [JsonIgnore]
        public string MainCategory => ParsedDocument?.Product?.MainCategory ?? string.Empty;

        [JsonIgnore]
        public string SubCategory => ParsedDocument?.Product?.SubCategory ?? string.Empty;

        [JsonIgnore]
        public string Supplier => ParsedDocument?.Product?.NameOfSupplier ?? string.Empty;

        [JsonIgnore]
        public string Barcode => ParsedDocument?.Product?.Barcode ?? string.Empty;

        [JsonIgnore]
        public string Currency => ParsedDocument?.Product?.Currency ?? string.Empty;

        [JsonIgnore]
        public string ResponsibleEmployee => ParsedDocument?.ProductRfid?.ResponsibleEmployee ?? string.Empty;

        // Indexer for dynamic field access (used by dynamic grid columns)
        [JsonIgnore]
        public string? this[string fieldName] => GetFieldValue(fieldName);

        /// <summary>
        /// Flattened document fields discovered from the raw JSON payload.
        /// Keys are stable JSON paths such as `tms_product.name` or `tms_product_rfid.branch`.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyDictionary<string, string> DocumentFieldValues
            => _documentFieldValues ??= BuildDocumentFieldValues();

        public string? GetFieldValue(string? fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                return null;

            return CanonicalizeFieldName(fieldName).ToUpperInvariant() switch
            {
                "NONE" => null,
                "RFID" => EmptyToNull(Rfid),
                "ITEMCODE" => EmptyToNull(Code),
                "PRODUCTNAME" => EmptyToNull(ProductName),
                "PRICE" => $"MNT {Price:N0}",
                "BRANCH" => EmptyToNull(Branch),
                "STATUS" => EmptyToNull(Status),
                "UNIT" => EmptyToNull(Unit),
                "DATE" => CreationTime != DateTime.MinValue ? CreationTime.ToString("yyyy-MM-dd") : null,
                "ACQUISITIONDATE" => EmptyToNull(AcquisitionDateFormatted),
                "CATEGORY" => EmptyToNull(Category),
                "MAINCATEGORY" => EmptyToNull(MainCategory),
                "SUBCATEGORY" => EmptyToNull(SubCategory),
                "SUPPLIER" => EmptyToNull(Supplier),
                "BARCODE" => EmptyToNull(Barcode),
                "CURRENCY" => EmptyToNull(Currency),
                "BOXNUMBER" => EmptyToNull(BoxNumber),
                "RESPONSIBLEEMPLOYEE" => EmptyToNull(ResponsibleEmployee),
                _ => GetDocumentFieldValue(fieldName)
            };
        }

        public IReadOnlyCollection<string> GetAvailableFieldNames()
        {
            return DocumentFieldValues.Keys
                .Where(key => !string.IsNullOrWhiteSpace(key) && !key.Contains("["))
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string CanonicalizeFieldName(string fieldName)
        {
            var trimmed = fieldName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
                return string.Empty;

            return trimmed.ToLowerInvariant() switch
            {
                "rfid" => "RFID",
                "itemcode" => "ItemCode",
                "name" => "ProductName",
                "productname" => "ProductName",
                "cost" => "Price",
                "price" => "Price",
                "measureunit" => "Unit",
                "unit" => "Unit",
                "creationtime" => "Date",
                "date" => "Date",
                "nameofsupplier" => "Supplier",
                "supplier" => "Supplier",
                "boxnumber" => "BoxNumber",
                "acquisitiondate" => "AcquisitionDate",
                "responsibleemployee" => "ResponsibleEmployee",
                "maincategory" => "MainCategory",
                "subcategory" => "SubCategory",
                _ => char.ToUpperInvariant(trimmed[0]) + trimmed[1..]
            };
        }

        private string? GetDocumentFieldValue(string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                return null;

            var requestedKey = BuildLookupKey(fieldName);

            foreach (var kvp in DocumentFieldValues)
            {
                if (kvp.Key.Contains("["))
                    continue;

                if (BuildLookupKey(kvp.Key).Equals(requestedKey, StringComparison.OrdinalIgnoreCase))
                {
                    return string.IsNullOrWhiteSpace(kvp.Value) ? null : kvp.Value;
                }
            }

            return null;
        }

        private IReadOnlyDictionary<string, string> BuildDocumentFieldValues()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var document = GetDocumentObject();
            if (document == null)
                return values;

            FlattenToken(document, string.Empty, values);
            return values;
        }

        private static void FlattenToken(JToken token, string prefix, IDictionary<string, string> values)
        {
            if (token is JValue scalar)
            {
                if (scalar.Type == JTokenType.String &&
                    TryParseEmbeddedJson(scalar.ToString(), out var embeddedToken))
                {
                    FlattenToken(embeddedToken, prefix, values);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(prefix) && scalar.Value != null)
                {
                    values[prefix] = scalar.Value.ToString() ?? string.Empty;
                }
                return;
            }

            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    var childPrefix = string.IsNullOrWhiteSpace(prefix)
                        ? property.Name
                        : $"{prefix}.{property.Name}";

                    FlattenToken(property.Value, childPrefix, values);
                }
                return;
            }

            if (token is JArray array)
            {
                if (TryFlattenNameValueArray(array, prefix, values))
                    return;

                for (var i = 0; i < array.Count; i++)
                {
                    var childPrefix = string.IsNullOrWhiteSpace(prefix)
                        ? i.ToString()
                        : $"{prefix}[{i}]";

                    FlattenToken(array[i], childPrefix, values);
                }
            }
        }

        private static bool TryParseEmbeddedJson(string? rawValue, out JToken token)
        {
            token = JValue.CreateNull();

            if (string.IsNullOrWhiteSpace(rawValue))
                return false;

            var trimmed = rawValue.Trim();
            if (!(trimmed.StartsWith("[") || trimmed.StartsWith("{")))
                return false;

            try
            {
                token = JToken.Parse(trimmed);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryFlattenNameValueArray(JArray array, string prefix, IDictionary<string, string> values)
        {
            var didFlatten = false;

            foreach (var item in array.OfType<JObject>())
            {
                var name = item["name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                    return false;

                var childPrefix = string.IsNullOrWhiteSpace(prefix)
                    ? name
                    : $"{prefix}.{name}";

                values[childPrefix] = item["value"]?.ToString() ?? string.Empty;
                didFlatten = true;
            }

            return didFlatten;
        }

        private JObject? GetDocumentObject()
        {
            if (_documentObject != null)
                return _documentObject;

            if (string.IsNullOrWhiteSpace(DocumentJson))
                return null;

            try
            {
                _documentObject = JObject.Parse(DocumentJson);
            }
            catch
            {
                return null;
            }

            return _documentObject;
        }

        private static string? EmptyToNull(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static string BuildLookupKey(string fieldName)
        {
            var lastDot = fieldName.LastIndexOf('.');
            var leaf = lastDot >= 0 ? fieldName[(lastDot + 1)..] : fieldName;
            var canonical = CanonicalizeFieldName(leaf);
            return new string(canonical
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());
        }

        // Runtime properties for Print Status logic
        private bool _isPrinted;
        [JsonIgnore]
        public bool IsPrinted
        {
            get => _isPrinted;
            set
            {
                if (SetProperty(ref _isPrinted, value))
                {
                    OnPropertyChanged(nameof(PrintStatus));
                    OnPropertyChanged(nameof(StatusIcon));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusBrush));
                    OnPropertyChanged(nameof(StatusTextBrush));
                    OnPropertyChanged(nameof(StatusDisplay));
                }
            }
        }

        private DateTime? _lastPrintedTime;
        [JsonIgnore]
        public DateTime? LastPrintedTime
        {
            get => _lastPrintedTime;
            set => SetProperty(ref _lastPrintedTime, value);
        }

        // Selection checkbox state
        private bool _isSelected;
        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        // Print error tracking
        private string? _printErrorMessage;
        [JsonIgnore]
        public string? PrintErrorMessage
        {
            get => _printErrorMessage;
            set
            {
                if (SetProperty(ref _printErrorMessage, value))
                {
                    OnPropertyChanged(nameof(PrintStatus));
                    OnPropertyChanged(nameof(StatusIcon));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusBrush));
                    OnPropertyChanged(nameof(StatusTextBrush));
                    OnPropertyChanged(nameof(StatusDisplay));
                }
            }
        }

        // Print status enum (computed)
        [JsonIgnore]
        public PrintStatus PrintStatus
        {
            get
            {
                if (!string.IsNullOrEmpty(PrintErrorMessage)) return PrintStatus.Error;
                if (IsPrinted) return PrintStatus.Printed;
                return PrintStatus.NotPrinted;
            }
        }

        // UI binding properties
        [JsonIgnore]
        public string StatusIcon => PrintStatus switch
        {
            PrintStatus.Printed => "✓",
            PrintStatus.Error => "✗",
            _ => "⚪"
        };

        [JsonIgnore]
        public string StatusText => PrintStatus switch
        {
            PrintStatus.Printed => "Printed",
            PrintStatus.Error => "Error",
            _ => "Not Printed"
        };

        [JsonIgnore]
        public Brush StatusBrush
        {
            get => PrintStatus switch
            {
                PrintStatus.Printed => (Brush)Application.Current.FindResource("SuccessBrush"),
                PrintStatus.Error => (Brush)Application.Current.FindResource("ErrorBrush"),
                _ => Brushes.Transparent
            };
        }

        [JsonIgnore]
        public Brush StatusTextBrush
        {
            get => PrintStatus switch
            {
                PrintStatus.Printed or PrintStatus.Error => Brushes.White,
                _ => (Brush)Application.Current.FindResource("TextSecondaryBrush")
            };
        }

        [JsonIgnore]
        public string StatusDisplay
        {
            get
            {
                if (!string.IsNullOrEmpty(PrintErrorMessage))
                    return $"Error: {PrintErrorMessage}";
                if (IsPrinted && LastPrintedTime.HasValue)
                    return $"Printed at {LastPrintedTime.Value:HH:mm:ss}";
                if (IsPrinted)
                    return "Printed";
                return "Not Printed";
            }
        }

        private void ApplyTopLevelData(ProductDto? product = null, ProductRfidDto? productRfid = null)
        {
            ParsedDocument ??= new ResourceDocument();

            if (product != null)
                ParsedDocument.Product = product;

            if (productRfid != null)
            {
                ParsedDocument.ProductRfid = productRfid;

                if (Id <= 0 && productRfid.Id > 0)
                    Id = productRfid.Id;
            }

            try
            {
                DocumentJson = JsonConvert.SerializeObject(new
                {
                    tms_product = ParsedDocument.Product,
                    tms_product_rfid = ParsedDocument.ProductRfid
                });
            }
            catch
            {
            }
        }
    }

    public class ResourceDocument
    {
        [JsonIgnore]
        public ProductDto Product { get; set; } = new();

        [JsonIgnore]
        public ProductRfidDto ProductRfid { get; set; } = new();

        [JsonProperty("tms_product")]
        public ProductDto? TmsProduct
        {
            set
            {
                if (value != null)
                    Product = value;
            }
        }

        [JsonProperty("product")]
        public ProductDto? ProductAlias
        {
            set
            {
                if (value != null)
                    Product = value;
            }
        }

        [JsonProperty("tms_product_rfid")]
        public ProductRfidDto? TmsProductRfid
        {
            set
            {
                if (value != null)
                    ProductRfid = value;
            }
        }

        [JsonProperty("product_rfid")]
        public ProductRfidDto? ProductRfidAlias
        {
            set
            {
                if (value != null)
                    ProductRfid = value;
            }
        }
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
        public object? CostRaw { get; set; }

        [JsonProperty("price")]
        public object? PriceRaw { get; set; }

        [JsonProperty("currency")]
        public object? CurrencyRaw { get; set; }

        [JsonIgnore]
        public string Currency => CurrencyRaw?.ToString() ?? string.Empty;

        [JsonIgnore]
        public decimal Cost
        {
            get
            {
                var raw = CostRaw ?? PriceRaw;
                if (raw == null) return 0;
                if (decimal.TryParse(raw.ToString(), out var result)) return result;
                return 0;
            }
            set { CostRaw = value; }
        }
        
        [JsonProperty("CreationTime")]
        public object? CreationTimeRaw { get; set; }

        [JsonIgnore]
        public DateTime CreationTime
        {
            get
            {
                if (CreationTimeRaw == null) return DateTime.MinValue;
                var str = CreationTimeRaw.ToString();
                if (DateTime.TryParse(str, out var dt)) return dt;
                // handle special dot format seen in bizpro JSON e.g. "2023.12.08 05:51:31"
                str = str?.Replace(".", "-") ?? "";
                if (DateTime.TryParse(str, out dt)) return dt;
                return DateTime.MinValue;
            }
            set { CreationTimeRaw = value; }
        }

        [JsonProperty("category")]
        public string? Category { get; set; }

        [JsonProperty("mainCategory")]
        public string? MainCategory { get; set; }

        [JsonProperty("subCategory")]
        public string? SubCategory { get; set; }

        [JsonProperty("nameOfSupplier")]
        public string? NameOfSupplier { get; set; }

        [JsonProperty("barcode")]
        public string? Barcode { get; set; }

        [JsonProperty("productInfo")]
        public string? ProductInfoJson { get; set; }
    }

    public class ProductRfidDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("rfid")]
        public string? Rfid { get; set; }

        [JsonIgnore]
        public string? Branch { get; set; }

        [JsonProperty("branch")]
        public object? BranchRaw
        {
            set
            {
                if (value == null)
                {
                    Branch = null;
                    return;
                }

                if (value is string branchName)
                {
                    Branch = branchName;
                    return;
                }

                try
                {
                    var token = value is Newtonsoft.Json.Linq.JToken jToken
                        ? jToken
                        : Newtonsoft.Json.Linq.JToken.FromObject(value);

                    Branch = token["data"]?["name"]?.ToString()
                        ?? token["name"]?.ToString()
                        ?? token.ToString();
                }
                catch
                {
                    Branch = value.ToString();
                }
            }
        }

        [JsonProperty("boxNo")]
        public string? BoxNumber { get; set; }

        [JsonProperty("status")]
        public object? StatusRaw { get; set; }

        [JsonIgnore]
        public int Status
        {
            get
            {
                if (StatusRaw == null) return 0;
                if (int.TryParse(StatusRaw.ToString(), out var result)) return result;
                return 0;
            }
            set { StatusRaw = value; }
        }

        [JsonProperty("CreationTime")]
        public object? CreationTimeRaw { get; set; }

        [JsonIgnore]
        public DateTime? CreationTime
        {
            get
            {
                if (CreationTimeRaw == null) return null;
                var str = CreationTimeRaw.ToString();
                if (string.IsNullOrWhiteSpace(str)) return null;
                if (DateTime.TryParse(str, out var dt)) return dt;
                str = str.Replace(".", "-");
                if (DateTime.TryParse(str, out dt)) return dt;
                return null;
            }
            set { CreationTimeRaw = value; }
        }

        [JsonProperty("acquisitionDate")]
        public object? AcquisitionDateRaw { get; set; }

        [JsonIgnore]
        public DateTime? AcquisitionDate
        {
            get
            {
                if (AcquisitionDateRaw == null) return null;
                var str = AcquisitionDateRaw.ToString();
                if (string.IsNullOrWhiteSpace(str)) return null;
                if (DateTime.TryParse(str, out var dt)) return dt;
                str = str.Replace(".", "-");
                if (DateTime.TryParse(str, out dt)) return dt;
                return null;
            }
            set { AcquisitionDateRaw = value; }
        }

        [JsonProperty("responsibleEmployee")]
        public string? ResponsibleEmployee { get; set; }

        // Print status from backend - comes as "isPrint" with value 2 (printed) or 1 (not printed)
        // CHIPMO Standard: 2 = Printed, 1 = Not Printed
        // Can be either string ("1", "2") or int (1, 2)
        [JsonProperty("isPrint")]
        public object? IsPrintRaw { get; set; }

        // Computed property to convert isPrint to bool
        // Website logic: isPrint=1 means "Үгүй" (NOT printed), isPrint=2 means "Тийм" (PRINTED)
        [JsonIgnore]
        public bool? IsPrinted
        {
            get
            {
                if (IsPrintRaw == null) return null;
                
                // Handle both string and numeric values
                var rawValue = IsPrintRaw.ToString();
                if (rawValue == "1") return false;  // 1 = NOT Printed (Үгүй)
                if (rawValue == "2") return true;   // 2 = Printed (Тийм)
                
                // Try parsing as bool for backwards compatibility
                if (bool.TryParse(rawValue, out var boolValue))
                    return boolValue;
                
                return null;
            }
        }

        [JsonProperty("lastPrintedTime")]
        public object? LastPrintedTimeRaw { get; set; }

        [JsonIgnore]
        public DateTime? LastPrintedTime
        {
            get
            {
                if (LastPrintedTimeRaw == null) return null;
                var str = LastPrintedTimeRaw.ToString();
                if (string.IsNullOrWhiteSpace(str)) return null;
                if (DateTime.TryParse(str, out var dt)) return dt;
                str = str.Replace(".", "-");
                if (DateTime.TryParse(str, out dt)) return dt;
                return null;
            }
            set { LastPrintedTimeRaw = value; }
        }

        [JsonProperty("printErrorMessage")]
        public string? PrintErrorMessage { get; set; }
    }

    public class UpdatePrintStatusRequest
    {
        [JsonProperty("rfid")]
        public string Rfid { get; set; } = string.Empty;

        // Backend expects "isPrint" with value 2 (printed/Тийм) or 1 (not printed/Үгүй)
        [JsonProperty("isPrint")]
        public int IsPrint { get; set; }

        [JsonProperty("lastPrintedTime")]
        public DateTime? LastPrintedTime { get; set; }

        [JsonProperty("printErrorMessage")]
        public string? PrintErrorMessage { get; set; }
    }

    public class UpdatePrintStatusResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }
    }
}
