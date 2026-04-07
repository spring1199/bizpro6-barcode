using BarTenderClone.Models;
using System.Collections.Generic;

namespace BarTenderClone.Services
{
    public interface IFieldMetadataService
    {
        TenantUiProfile BuildProfile(IEnumerable<ResourceItem> items, string? tenantName = null);

        IReadOnlyList<TenantBindableFieldDefinition> BuildBindableFields(IEnumerable<ResourceItem> items);

        IReadOnlyList<TenantGridColumnDefinition> BuildGridColumns(IEnumerable<ResourceItem> items);
    }
}
