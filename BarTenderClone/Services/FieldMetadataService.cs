using BarTenderClone.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BarTenderClone.Services
{
    public sealed class FieldMetadataService : IFieldMetadataService
    {
        private static readonly FieldSeed[] KnownFieldSeeds =
        {
            new("RFID", "RFID", "Rfid", TenantFieldDataKind.Text, 100),
            new("ItemCode", "Item Code", "Code", TenantFieldDataKind.Text, 90),
            new("ProductName", "Product Name", "ProductName", TenantFieldDataKind.Text, 180),
            new("Price", "Price", "Price", TenantFieldDataKind.Number, 90),
            new("Branch", "Branch", "Branch", TenantFieldDataKind.Text, 110),
            new("Status", "Status", "Status", TenantFieldDataKind.Text, 80),
            new("Unit", "Unit", "Unit", TenantFieldDataKind.Text, 70),
            new("Date", "Date", "CreationTime", TenantFieldDataKind.Date, 90),
            new("AcquisitionDate", "Acq. Date", "AcquisitionDateFormatted", TenantFieldDataKind.Date, 100),
            new("Category", "Category", "Category", TenantFieldDataKind.Text, 110),
            new("MainCategory", "Main Category", "MainCategory", TenantFieldDataKind.Text, 130),
            new("SubCategory", "Sub Category", "SubCategory", TenantFieldDataKind.Text, 130),
            new("Supplier", "Supplier", "Supplier", TenantFieldDataKind.Text, 120),
            new("Barcode", "Barcode", "Barcode", TenantFieldDataKind.Text, 120),
            new("Currency", "Currency", "Currency", TenantFieldDataKind.Text, 90),
            new("BoxNumber", "Box No", "BoxNumber", TenantFieldDataKind.Text, 90),
            new("ResponsibleEmployee", "Resp. Emp.", "ResponsibleEmployee", TenantFieldDataKind.Text, 130)
        };

        public TenantUiProfile BuildProfile(IEnumerable<ResourceItem> items, string? tenantName = null)
        {
            var materialized = Materialize(items);
            var bindableFields = BuildBindableFields(materialized);
            var gridColumns = BuildGridColumns(materialized);

            return new TenantUiProfile
            {
                TenantName = tenantName ?? string.Empty,
                BindableFields = bindableFields,
                GridColumns = gridColumns
            };
        }

        public IReadOnlyList<TenantBindableFieldDefinition> BuildBindableFields(IEnumerable<ResourceItem> items)
        {
            var materialized = Materialize(items);
            var definitions = new List<TenantBindableFieldDefinition>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var seed in KnownFieldSeeds)
            {
                if (!TryGetSampleValue(materialized, seed.Key, out var sampleValue))
                    continue;

                definitions.Add(new TenantBindableFieldDefinition
                {
                    Key = seed.Key,
                    DisplayName = seed.DisplayName,
                    BindingPath = seed.BindingPath,
                    Source = TenantFieldValueSource.KnownField,
                    DataKind = seed.DataKind,
                    IsVisibleByDefault = true,
                    HasSampleValue = true,
                    SampleValue = sampleValue
                });
                seenKeys.Add(seed.Key);
            }

            foreach (var field in DiscoverDynamicFieldPaths(materialized))
            {
                if (seenKeys.Contains(field))
                    continue;

                if (!TryGetSampleValue(materialized, field, out var sampleValue))
                    continue;

                definitions.Add(new TenantBindableFieldDefinition
                {
                    Key = field,
                    DisplayName = HumanizePath(field),
                    BindingPath = $"DocumentFieldValues[{field}]",
                    Source = TenantFieldValueSource.DocumentJson,
                    DataKind = InferDataKind(sampleValue),
                    IsVisibleByDefault = false,
                    HasSampleValue = true,
                    SampleValue = sampleValue
                });
            }

            return definitions
                .OrderBy(field => field.Source)
                .ThenBy(field => GetKnownFieldOrder(field.Key))
                .ThenBy(field => field.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public IReadOnlyList<TenantGridColumnDefinition> BuildGridColumns(IEnumerable<ResourceItem> items)
        {
            var materialized = Materialize(items);
            var columns = new List<TenantGridColumnDefinition>();
            var knownFieldNames = new HashSet<string>(
                BuildBindableFields(materialized)
                    .Where(field => field.Source == TenantFieldValueSource.KnownField)
                    .Select(field => field.Key),
                StringComparer.OrdinalIgnoreCase);

            foreach (var seed in KnownFieldSeeds)
            {
                if (!knownFieldNames.Contains(seed.Key))
                    continue;

                columns.Add(new TenantGridColumnDefinition
                {
                    Key = seed.Key,
                    Header = seed.DisplayName,
                    BindingPath = seed.BindingPath,
                    DataKind = seed.DataKind,
                    IsVisibleByDefault = true,
                    IsDynamic = false,
                    Width = seed.Width
                });
            }

            foreach (var field in DiscoverDynamicFieldPaths(materialized))
            {
                if (!TryGetSampleValue(materialized, field, out var sampleValue))
                    continue;

                columns.Add(new TenantGridColumnDefinition
                {
                    Key = field,
                    Header = HumanizePath(field),
                    BindingPath = $"DocumentFieldValues[{field}]",
                    DataKind = InferDataKind(sampleValue),
                    IsVisibleByDefault = false,
                    IsDynamic = true,
                    Width = 120
                });
            }

            return columns
                .OrderBy(column => column.IsDynamic)
                .ThenBy(column => GetKnownFieldOrder(column.Key))
                .ThenBy(column => column.Header, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<ResourceItem> Materialize(IEnumerable<ResourceItem> items)
        {
            return items?.Where(item => item != null).ToList() ?? new List<ResourceItem>();
        }

        private static IEnumerable<string> DiscoverDynamicFieldPaths(IEnumerable<ResourceItem> items)
        {
            return items
                .SelectMany(item => item.GetAvailableFieldNames())
                .Where(field => !string.IsNullOrWhiteSpace(field) && !field.Contains("[") && !IsKnownLeafField(field))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(field => field, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool TryGetSampleValue(IEnumerable<ResourceItem> items, string fieldName, out string value)
        {
            foreach (var item in items)
            {
                var candidate = item.GetFieldValue(fieldName);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    value = candidate!;
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }

        private static TenantFieldDataKind InferDataKind(string sampleValue)
        {
            if (bool.TryParse(sampleValue, out _))
                return TenantFieldDataKind.Boolean;

            if (DateTime.TryParse(sampleValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                return TenantFieldDataKind.Date;

            if (decimal.TryParse(sampleValue, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                return TenantFieldDataKind.Number;

            return TenantFieldDataKind.Text;
        }

        private static int GetKnownFieldOrder(string key)
        {
            for (var i = 0; i < KnownFieldSeeds.Length; i++)
            {
                if (KnownFieldSeeds[i].Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return int.MaxValue;
        }

        private static string HumanizePath(string path)
        {
            var segments = path
                .Split(new[] { '.', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(segment => !segment.All(char.IsDigit))
                .Select(segment => HumanizeSegment(segment));

            return string.Join(" / ", segments);
        }

        private static bool IsKnownLeafField(string path)
        {
            var leaf = GetLeafSegment(path);
            if (string.IsNullOrWhiteSpace(leaf))
                return false;

            var canonical = ResourceItem.CanonicalizeFieldName(leaf);
            return KnownFieldSeeds.Any(seed => seed.Key.Equals(canonical, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetLeafSegment(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            var trimmed = path.Trim();
            var dotIndex = trimmed.LastIndexOf('.');
            var segment = dotIndex >= 0 ? trimmed[(dotIndex + 1)..] : trimmed;
            var bracketIndex = segment.LastIndexOf(']');
            if (bracketIndex >= 0 && bracketIndex < segment.Length - 1)
                segment = segment[(bracketIndex + 1)..];

            return segment.Trim('[', ']');
        }

        private static string HumanizeSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
                return string.Empty;

            var cleaned = segment.Replace("_", " ").Trim();
            if (cleaned.Length == 1)
                return cleaned.ToUpperInvariant();

            return char.ToUpperInvariant(cleaned[0]) + cleaned[1..];
        }

        private sealed record FieldSeed(string Key, string DisplayName, string BindingPath, TenantFieldDataKind DataKind, double Width);
    }
}
