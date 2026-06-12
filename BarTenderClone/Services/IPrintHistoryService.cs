using BarTenderClone.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BarTenderClone.Services
{
    public interface IPrintHistoryService
    {
        Task SaveEntryAsync(PrintHistoryEntry entry);
        Task<List<PrintHistoryEntry>> GetAllEntriesAsync();
        Task ClearHistoryAsync();
    }
}
