using System.Collections.Generic;

namespace BarTenderClone.Models
{
    public sealed class ResourceFieldOption
    {
        public string Key { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public int Order { get; init; }
    }

    public sealed class ProductGridColumnDefinition
    {
        public string Key { get; init; } = string.Empty;
        public string Header { get; init; } = string.Empty;
        public string BindingPath { get; init; } = string.Empty;
        public string? SortMemberPath { get; init; }
        public int Order { get; init; }
        public double Width { get; init; } = 80;
        public bool IsVisibleByDefault { get; init; } = true;
        public bool FillWidth { get; init; }
        public string? StringFormat { get; init; }
        public bool Emphasize { get; init; }
    }

    public sealed class ResourceMetadataProfile
    {
        public IReadOnlyList<ResourceFieldOption> BindableFields { get; init; } = new List<ResourceFieldOption>();
        public IReadOnlyList<ProductGridColumnDefinition> GridColumns { get; init; } = new List<ProductGridColumnDefinition>();
    }
}
