using System;
using System.Collections.Generic;
using System.Linq;
using BarTenderClone.Models;

namespace BarTenderClone.Services
{
    public class TenantMetadataService : ITenantMetadataService
    {
        private static readonly FieldSpec[] FieldSpecs =
        {
            new("RFID", "RFID", true,
                new TenantGridColumnDefinition
                {
                    Key = "RFID",
                    Header = "RFID",
                    BindingPath = nameof(ResourceItem.Rfid),
                    SortMemberPath = nameof(ResourceItem.Rfid),
                    Width = 100
                }),
            new("ItemCode", "ItemCode", true,
                new TenantGridColumnDefinition
                {
                    Key = "ItemCode",
                    Header = "Code",
                    BindingPath = nameof(ResourceItem.Code),
                    SortMemberPath = nameof(ResourceItem.Code),
                    Width = 70
                }),
            new("ProductName", "ProductName", true,
                new TenantGridColumnDefinition
                {
                    Key = "ProductName",
                    Header = "Product Name",
                    BindingPath = nameof(ResourceItem.ProductName),
                    SortMemberPath = nameof(ResourceItem.ProductName),
                    FillWidth = true,
                    Emphasize = true,
                    Width = 160
                }),
            new("Price", "Price", true,
                new TenantGridColumnDefinition
                {
                    Key = "Price",
                    Header = "Price",
                    BindingPath = nameof(ResourceItem.Price),
                    SortMemberPath = nameof(ResourceItem.Price),
                    Width = 60,
                    StringFormat = "N0",
                    DataKind = TenantFieldDataKind.Number
                }),
            new("Branch", "Branch", true,
                new TenantGridColumnDefinition
                {
                    Key = "Branch",
                    Header = "Branch",
                    BindingPath = nameof(ResourceItem.Branch),
                    SortMemberPath = nameof(ResourceItem.Branch),
                    Width = 80
                }),
            new("Status", "Status", true,
                new TenantGridColumnDefinition
                {
                    Key = "Status",
                    Header = "Status",
                    BindingPath = nameof(ResourceItem.StatusDisplay),
                    SortMemberPath = nameof(ResourceItem.Status),
                    Width = 80
                }),
            new("Unit", "Unit", true,
                new TenantGridColumnDefinition
                {
                    Key = "Unit",
                    Header = "Unit",
                    BindingPath = nameof(ResourceItem.Unit),
                    SortMemberPath = nameof(ResourceItem.Unit),
                    Width = 50
                }),
            new("Date", "Date", true,
                new TenantGridColumnDefinition
                {
                    Key = "Date",
                    Header = "Date",
                    BindingPath = nameof(ResourceItem.CreationTime),
                    SortMemberPath = nameof(ResourceItem.CreationTime),
                    Width = 55,
                    StringFormat = "MM-dd",
                    DataKind = TenantFieldDataKind.Date
                }),
            new("BoxNumber", "BoxNumber", false,
                new TenantGridColumnDefinition
                {
                    Key = "BoxNumber",
                    Header = "Box No",
                    BindingPath = nameof(ResourceItem.BoxNumber),
                    SortMemberPath = nameof(ResourceItem.BoxNumber),
                    Width = 70
                }),
            new("AcquisitionDate", "AcquisitionDate", false,
                new TenantGridColumnDefinition
                {
                    Key = "AcquisitionDate",
                    Header = "Acq. Date",
                    BindingPath = nameof(ResourceItem.AcquisitionDateFormatted),
                    SortMemberPath = nameof(ResourceItem.AcquisitionDate),
                    Width = 80
                }),
            new("Supplier", "Supplier", false,
                new TenantGridColumnDefinition
                {
                    Key = "Supplier",
                    Header = "Supplier",
                    BindingPath = nameof(ResourceItem.Supplier),
                    SortMemberPath = nameof(ResourceItem.Supplier),
                    Width = 80
                }),
            new("Category", "Category", false,
                new TenantGridColumnDefinition
                {
                    Key = "Category",
                    Header = "Category",
                    BindingPath = nameof(ResourceItem.Category),
                    SortMemberPath = nameof(ResourceItem.Category),
                    Width = 80
                }),
            new("MainCategory", "MainCategory", false),
            new("SubCategory", "SubCategory", false),
            new("Barcode", "Barcode", false),
            new("Currency", "Currency", false),
            new("ResponsibleEmployee", "ResponsibleEmployee", false,
                new TenantGridColumnDefinition
                {
                    Key = "ResponsibleEmployee",
                    Header = "Resp. Emp.",
                    BindingPath = nameof(ResourceItem.ResponsibleEmployee),
                    SortMemberPath = nameof(ResourceItem.ResponsibleEmployee),
                    Width = 90
                })
        };

        private static readonly HashSet<string> IgnoredDynamicFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "Id",
            "TenantId",
            "ProductId",
            "ResourceId"
        };

        public TenantUiProfile BuildProfile(string? tenancyName, IEnumerable<ResourceItem> products)
        {
            var productList = products?.ToList() ?? new List<ResourceItem>();
            var bindableFields = new List<TenantBindableFieldDefinition>
            {
                new()
                {
                    Key = "None",
                    DisplayName = "None",
                    BindingPath = string.Empty,
                    Source = TenantFieldValueSource.KnownField,
                    IsVisibleByDefault = true
                }
            };

            foreach (var spec in FieldSpecs)
            {
                if (!IsFieldAvailable(spec, productList))
                    continue;

                bindableFields.Add(new TenantBindableFieldDefinition
                {
                    Key = spec.FieldKey,
                    DisplayName = spec.DisplayName,
                    BindingPath = spec.GridColumn?.BindingPath ?? $"[{spec.FieldKey}]",
                    Source = TenantFieldValueSource.KnownField,
                    DataKind = spec.GridColumn?.DataKind ?? TenantFieldDataKind.Text,
                    IsVisibleByDefault = spec.IsCore,
                    HasSampleValue = true,
                    SampleValue = productList.Select(item => item.GetFieldValue(spec.FieldKey)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                });
            }

            var knownKeys = new HashSet<string>(bindableFields.Select(field => field.Key), StringComparer.OrdinalIgnoreCase);
            foreach (var dynamicField in BuildDynamicFields(productList, knownKeys))
            {
                bindableFields.Add(dynamicField);
            }

            var gridColumns = FieldSpecs
                .Where(spec => spec.GridColumn != null && IsFieldAvailable(spec, productList))
                .Select(spec => spec.GridColumn!)
                .ToList();

            var dynamicColumns = bindableFields
                .Where(field => field.Source == TenantFieldValueSource.DocumentJson)
                .Select((field, index) => new TenantGridColumnDefinition
                {
                    Key = field.Key,
                    Header = field.DisplayName,
                    BindingPath = $"[{field.Key}]",
                    SortMemberPath = field.Key,
                    DataKind = field.DataKind,
                    IsVisibleByDefault = false,
                    IsDynamic = true,
                    Width = 120 + (index % 2 == 0 ? 0 : 20)
                });

            return new TenantUiProfile
            {
                TenantName = tenancyName ?? string.Empty,
                BindableFields = bindableFields,
                GridColumns = gridColumns.Concat(dynamicColumns).ToList()
            };
        }

        private static bool IsFieldAvailable(FieldSpec spec, IReadOnlyCollection<ResourceItem> products)
        {
            if (spec.IsCore)
                return true;

            return products.Any(product => !string.IsNullOrWhiteSpace(product.GetFieldValue(spec.FieldKey)));
        }

        private static IEnumerable<TenantBindableFieldDefinition> BuildDynamicFields(
            IReadOnlyCollection<ResourceItem> products,
            HashSet<string> knownKeys)
        {
            var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var product in products)
            {
                foreach (var fieldName in product.GetAvailableFieldNames())
                {
                    if (!knownKeys.Contains(fieldName) && !IgnoredDynamicFields.Contains(fieldName))
                    {
                        discovered.Add(fieldName);
                    }
                }
            }

            return discovered
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(name => new TenantBindableFieldDefinition
                {
                    Key = name,
                    DisplayName = HumanizeKey(name),
                    BindingPath = $"[{name}]",
                    Source = TenantFieldValueSource.DocumentJson,
                    DataKind = TenantFieldDataKind.Text,
                    HasSampleValue = true,
                    SampleValue = products.Select(item => item.GetFieldValue(name)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                });
        }

        private sealed record FieldSpec(
            string FieldKey,
            string DisplayName,
            bool IsCore,
            TenantGridColumnDefinition? GridColumn = null);

        private static string HumanizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return key;

            var chars = new List<char> { key[0] };
            for (int i = 1; i < key.Length; i++)
            {
                if (char.IsUpper(key[i]) && !char.IsWhiteSpace(key[i - 1]))
                {
                    chars.Add(' ');
                }

                chars.Add(key[i]);
            }

            return new string(chars.ToArray());
        }
    }
}
