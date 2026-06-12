using BarTenderClone.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace BarTenderClone.Services
{
    public class PrintHistoryService : IPrintHistoryService
    {
        private readonly string _historyFilePath;
        private readonly ILoggingService _logger;
        private readonly System.Threading.SemaphoreSlim _semaphore = new System.Threading.SemaphoreSlim(1, 1);

        public PrintHistoryService(ILoggingService logger)
        {
            _logger = logger;
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appDir = Path.Combine(appData, "BarTenderClone", "History");
            
            if (!Directory.Exists(appDir))
            {
                Directory.CreateDirectory(appDir);
            }
            
            _historyFilePath = Path.Combine(appDir, "print_history.json");
        }

        public async Task SaveEntryAsync(PrintHistoryEntry entry)
        {
            await _semaphore.WaitAsync();
            try
            {
                List<PrintHistoryEntry> entries;
                if (File.Exists(_historyFilePath))
                {
                    var json = await File.ReadAllTextAsync(_historyFilePath);
                    entries = JsonConvert.DeserializeObject<List<PrintHistoryEntry>>(json) ?? new List<PrintHistoryEntry>();
                }
                else
                {
                    entries = new List<PrintHistoryEntry>();
                }

                entries.Insert(0, entry); // Add to the top
                
                // Keep only last 1000 entries to prevent file from getting too large
                if (entries.Count > 1000)
                {
                    entries = entries.GetRange(0, 1000);
                }

                var newJson = JsonConvert.SerializeObject(entries, Formatting.Indented);
                await File.WriteAllTextAsync(_historyFilePath, newJson);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save print history entry.", ex);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<List<PrintHistoryEntry>> GetAllEntriesAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                if (!File.Exists(_historyFilePath))
                {
                    return new List<PrintHistoryEntry>();
                }

                var json = await File.ReadAllTextAsync(_historyFilePath);
                return JsonConvert.DeserializeObject<List<PrintHistoryEntry>>(json) ?? new List<PrintHistoryEntry>();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load print history.", ex);
                return new List<PrintHistoryEntry>();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task ClearHistoryAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    File.Delete(_historyFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to clear print history.", ex);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
