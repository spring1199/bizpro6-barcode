using System;
using System.Collections.Generic;
using System.Linq;
using BarTenderClone.Models;

namespace BarTenderClone.Services
{
    public class ResourceMetadataService : IResourceMetadataService
    {
        private sealed record FieldDescriptor(
            string Key,
            string DisplayName,
            int Order,
            bool IsCore,
            ProductGridColumnDefinition? GridColumn = null);

        private static readonly FieldDescriptor[] SupportedFields =
        {
            new("RFID", "RFID", 10, true,
                new ProductGridColumnDefinition { Key = "RFID", Header = "RFID", BindingPath = nameof(ResourceItem.Rfid), SortMemberPath = nameof(ResourceItem.Rfid), Order = 20, Width = 100 }),
            new("ItemCode", "ItemCode", 20, true,
                new ProductGridColumnDefinition { Key = "ItemCode", Header = "Code", BindingPath = nameof(ResourceItem.Code), SortMemberPath = nameof(ResourceItem.Code), Order = 30, Width = 70 }),
            new("ProductName", "ProductName", 30, true,
                new ProductGridColumnDefinition { Key = "ProductName", Header = "Product Name", BindingPath = nameof(ResourceItem.ProductName), SortMemberPath = nameof(ResourceItem.ProductName), Order = 40, FillWidth = true, Emphasize = true }),
            new("Price", "Price", 40, true,
                new ProductGridColumnDefinition { Key = "Price", Header = "Price", BindingPath = nameof(ResourceItem.Price), SortMemberPath = nameof(ResourceItem.Price), Order = 60, Width = 60, StringFormat = "N0" }),
            new("Branch", "Branch", 50, true,
                new ProductGridColumnDefinition { Key = "Branch", Header = "Branch", BindingPath = nameof(ResourceItem.Branch), SortMemberPath = nameof(ResourceItem.Branch), Order = 80, Width = 80 }),
            new("Status", "Status", 60, true,
                new ProductGridColumnDefinition { Key = "Status", Header = "Status", BindingPath = nameof(ResourceItem.StatusDisplay), SortMemberPath = nameof(ResourceItem.Status), Order = 10, Width = 80 }),
            new("Unit", "Unit", 70, true,
                new ProductGridColumnDefinition { Key = "Unit", Header = "Unit", BindingPath = nameof(ResourceItem.Unit), SortMemberPath = nameof(ResourceItem.Unit), Order = 50, Width = 50 }),
            new("Date", "Date", 80, true,
                new ProductGridColumnDefinition { Key = "Date", Header = "Date", BindingPath = nameof(ResourceItem.CreationTime), SortMemberPath = nameof(ResourceItem.CreationTime), Order = 70, Width = 55, StringFormat = "MM-dd" }),
            new("AcquisitionDate", "AcquisitionDate", 90, false,
                new ProductGridColumnDefinition { Key = "AcquisitionDate", Header = "Acq. Date", BindingPath = nameof(ResourceItem.AcquisitionDateFormatted), SortMemberPath = nameof(ResourceItem.AcquisitionDate), Order = 100, Width = 80 }),
            new("Category", "Category", 100, false,
                new ProductGridColumnDefinition { Key = "Category", Header = "Category", BindingPath = nameof(ResourceItem.Category), SortMemberPath = nameof(ResourceItem.Category), Order = 120, Width = 80 }),
            new("MainCategory", "MainCategory", 110, false),
            new("SubCategory", "SubCategory", 120, false),
            new("Supplier", "Supplier", 130, false,
                new ProductGridColumnDefinition { Key = "Supplier", Header = "Supplier", BindingPath = nameof(ResourceItem.Supplier), SortMemberPath = nameof(ResourceItem.Supplier), Order = 110, Width = 80 }),
            new("Barcode", "Barcode", 140, false),
            new("Currency", "Currency", 150, false),
            new("BoxNumber", "BoxNumber", 160, false,
                new ProductGridColumnDefinition { Key = "BoxNumber", Header = "Box No", BindingPath = nameof(ResourceItem.BoxNumber), SortMemberPath = nameof(ResourceItem.BoxNumber), Order = 90, Width = 70 }),
            new("ResponsibleEmployee", "ResponsibleEmployee", 170, false,
                new ProductGridColumnDefinition { Key = "ResponsibleEmployee", Header = "Resp. Emp.", BindingPath = nameof(ResourceItem.ResponsibleEmployee), SortMemberPath = nameof(ResourceItem.ResponsibleEmployee), Order = 130, Width = 80 }),
        };

        public ResourceMetadataProfile BuildProfile(IEnumerable<ResourceItem> items)
        {
            var materializedItems = items?.ToList() ?? new List<ResourceItem>();
            var bindableFields = new List<ResourceFieldOption>
            {
                new() { Key = "None", DisplayName = "None", Order = 0 }
            };

            foreach (var field in SupportedFields.OrderBy(f => f.Order))
            {
                if (field.IsCore || HasMeaningfulValue(materializedItems, field.Key))
                {
                    bindableFields.Add(new ResourceFieldOption
                    {
                        Key = field.Key,
                        DisplayName = field.DisplayName,
                        Order = field.Order
                    });
                }
            }

            var knownKeys = new HashSet<string>(
                bindableFields.Select(f => f.Key),
                StringComparer.OrdinalIgnoreCase);

            var dynamicKeys = DiscoverDynamicKeys(materializedItems, knownKeys);

            foreach (var dynamicField in BuildDynamicFields(dynamicKeys))
            {
                bindableFields.Add(dynamicField);
            }

            var columns = SupportedFields
                .Where(f => f.GridColumn != null && (f.IsCore || HasMeaningfulValue(materializedItems, f.Key)))
                .Select(f => f.GridColumn!)
                .OrderBy(c => c.Order)
                .ToList();

            columns.AddRange(BuildDynamicColumns(dynamicKeys));

            return new ResourceMetadataProfile
            {
                BindableFields = bindableFields.OrderBy(f => f.Order).ToList(),
                GridColumns = columns.OrderBy(c => c.Order).ToList()
            };
        }

        private static bool HasMeaningfulValue(IEnumerable<ResourceItem> items, string fieldKey)
        {
            return items.Any(item => !string.IsNullOrWhiteSpace(item.GetFieldValue(fieldKey)));
        }

        private static IReadOnlyList<string> DiscoverDynamicKeys(
            IEnumerable<ResourceItem> items,
            HashSet<string> knownKeys)
        {
            var discoveredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                foreach (var fieldName in item.GetAvailableFieldNames())
                {
                    var canonicalKey = ResourceItem.CanonicalizeFieldName(GetLeafFieldName(fieldName));
                    if (string.IsNullOrWhiteSpace(canonicalKey) || IsKnownField(canonicalKey, knownKeys))
                        continue;

                    discoveredKeys.Add(canonicalKey);
                }
            }

            return discoveredKeys
                .Where(key => HasMeaningfulValue(items, key))
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IEnumerable<ResourceFieldOption> BuildDynamicFields(IEnumerable<string> dynamicKeys)
        {
            return dynamicKeys
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .Select((key, index) => new ResourceFieldOption
                {
                    Key = key,
                    DisplayName = BuildDisplayName(key),
                    Order = 1000 + index
                });
        }

        private static IEnumerable<ProductGridColumnDefinition> BuildDynamicColumns(IEnumerable<string> dynamicKeys)
        {
            return dynamicKeys
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .Select((key, index) => new ProductGridColumnDefinition
                {
                    Key = key,
                    Header = BuildDisplayName(key),
                    BindingPath = $"[{key}]",
                    SortMemberPath = key,
                    Order = 1000 + index,
                    Width = 110,
                    IsVisibleByDefault = false
                });
        }

        private static bool IsKnownField(string fieldName, HashSet<string> knownKeys)
        {
            if (knownKeys.Contains(fieldName))
                return true;

            var leafName = GetLeafFieldName(fieldName);
            var canonicalLeafName = ResourceItem.CanonicalizeFieldName(leafName);
            return knownKeys.Contains(canonicalLeafName);
        }

        private static string GetLeafFieldName(string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                return string.Empty;

            var lastDot = fieldName.LastIndexOf('.');
            return lastDot >= 0 ? fieldName[(lastDot + 1)..] : fieldName;
        }

        private static string BuildDisplayName(string fieldName)
        {
            var value = GetLeafFieldName(fieldName);
            if (string.IsNullOrWhiteSpace(value))
                return fieldName;

            var chars = new List<char>(value.Length * 2);
            for (var i = 0; i < value.Length; i++)
            {
                var current = value[i];
                if (current == '_' || current == '-')
                {
                    chars.Add(' ');
                    continue;
                }

                if (i > 0 && char.IsUpper(current) && chars.Count > 0 && chars[^1] != ' ')
                    chars.Add(' ');

                chars.Add(i == 0 ? char.ToUpperInvariant(current) : current);
            }

            return new string(chars.ToArray()).Trim();
        }
    }
}
