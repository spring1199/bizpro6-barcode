using BarTenderClone.Models;
using System;
using System.Threading.Tasks;

namespace BarTenderClone.Services
{
    public interface IApiService
    {
        Task<ResourceResult?> GetResourcesAsync(int skip = 0, int take = 25, string filter = "");

        /// <summary>
        /// Updates the print status for a specific RFID tag with detailed information
        /// </summary>
        /// <param name="rfid">RFID identifier</param>
        /// <param name="isPrinted">Whether the item was successfully printed</param>
        /// <param name="lastPrintedTime">Timestamp of last print attempt</param>
        /// <param name="errorMessage">Error message if print failed, null if successful</param>
        Task<bool> UpdatePrintStatusAsync(
            ResourceItem item,
            bool isPrinted,
            DateTime? lastPrintedTime = null,
            string? errorMessage = null);

        /// <summary>
        /// Synchronizes and pushes RFID data to SAP
        /// </summary>
        Task<bool> PrintAndPushRfidAsync(long productRfidId);

        /// <summary>
        /// Enqueues a sync job for branches/locations from SAP
        /// </summary>
        Task<bool> EnqueueBranchSyncAsync();

        /// <summary>
        /// Enqueues a sync job for assets/equipments from SAP
        /// </summary>
        Task<bool> EnqueueEquipmentSyncAsync();
    }
}
