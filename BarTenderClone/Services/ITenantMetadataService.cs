using System.Collections.Generic;
using BarTenderClone.Models;

namespace BarTenderClone.Services
{
    public interface ITenantMetadataService
    {
        TenantUiProfile BuildProfile(string? tenancyName, IEnumerable<ResourceItem> products);
    }
}
