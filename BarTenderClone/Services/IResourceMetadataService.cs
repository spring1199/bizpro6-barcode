using System.Collections.Generic;
using BarTenderClone.Models;

namespace BarTenderClone.Services
{
    public interface IResourceMetadataService
    {
        ResourceMetadataProfile BuildProfile(IEnumerable<ResourceItem> items);
    }
}
