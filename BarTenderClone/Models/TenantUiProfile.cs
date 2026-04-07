using System;
using System.Collections.Generic;
using System.Linq;

namespace BarTenderClone.Models
{
    public enum TenantFieldValueSource
    {
        KnownField,
        DocumentJson
    }

    public enum TenantFieldDataKind
    {
        Unknown,
        Text,
        Number,
        Date,
        Boolean
    }

    public sealed class TenantBindableFieldDefinition
    {
        public string Key { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string BindingPath { get; init; } = string.Empty;
        public TenantFieldValueSource Source { get; init; } = TenantFieldValueSource.KnownField;
        public TenantFieldDataKind DataKind { get; init; } = TenantFieldDataKind.Text;
        public bool IsVisibleByDefault { get; init; }
        public bool HasSampleValue { get; init; }
        public string? SampleValue { get; init; }
    }

    public sealed class TenantGridColumnDefinition
    {
        public string Key { get; init; } = string.Empty;
        public string Header { get; init; } = string.Empty;
        public string BindingPath { get; init; } = string.Empty;
        public string? SortMemberPath { get; init; }
        public TenantFieldDataKind DataKind { get; init; } = TenantFieldDataKind.Text;
        public bool IsVisibleByDefault { get; init; } = true;
        public bool IsDynamic { get; init; }
        public double Width { get; init; } = 100;
        public bool FillWidth { get; init; }
        public bool Emphasize { get; init; }
        public string? StringFormat { get; init; }
    }

    public sealed class TenantUiProfile
    {
        public string TenantName { get; init; } = string.Empty;
        public IReadOnlyList<TenantBindableFieldDefinition> BindableFields { get; init; } = Array.Empty<TenantBindableFieldDefinition>();
        public IReadOnlyList<TenantGridColumnDefinition> GridColumns { get; init; } = Array.Empty<TenantGridColumnDefinition>();

        public IReadOnlyList<string> AvailableFields
            => BindableFields.Select(field => field.Key).ToList();

        public IReadOnlyCollection<string> VisibleColumnKeys
            => GridColumns.Where(column => column.IsVisibleByDefault).Select(column => column.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
