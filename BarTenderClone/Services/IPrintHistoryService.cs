using BarTenderClone.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BarTenderClone.Services
{
    public interface IPrintHistoryService
    {
        Task SaveEntryAsync(PrintHistoryEntry entry);
        Task SaveEntriesAsync(IReadOnlyCollection<PrintHistoryEntry> entries);
        Task<List<PrintHistoryEntry>> GetAllEntriesAsync();
        Task ClearHistoryAsync();
    }
}
