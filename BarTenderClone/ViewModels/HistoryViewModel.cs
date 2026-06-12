using BarTenderClone.Models;
using BarTenderClone.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace BarTenderClone.ViewModels
{
    public partial class HistoryViewModel : ObservableObject
    {
        private readonly IPrintHistoryService _historyService;
        private readonly ILoggingService _logger;

        [ObservableProperty]
        private ObservableCollection<PrintHistoryEntry> _historyEntries = new();

        [ObservableProperty]
        private ObservableCollection<PrintHistoryEntry> _filteredEntries = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private int _totalPrints;

        [ObservableProperty]
        private int _successfulPrints;

        [ObservableProperty]
        private int _failedPrints;

        public HistoryViewModel(IPrintHistoryService historyService, ILoggingService logger)
        {
            _historyService = historyService;
            _logger = logger;
            LoadHistoryCommand.Execute(null);
        }

        [RelayCommand]
        private async Task LoadHistoryAsync()
        {
            try
            {
                var entries = await _historyService.GetAllEntriesAsync();
                
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    HistoryEntries = new ObservableCollection<PrintHistoryEntry>(entries);
                    ApplyFilter();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load history in ViewModel", ex);
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredEntries = new ObservableCollection<PrintHistoryEntry>(HistoryEntries);
            }
            else
            {
                var lowerSearch = SearchText.ToLower();
                var filtered = HistoryEntries.Where(e => 
                    (e.ProductName?.ToLower().Contains(lowerSearch) == true) ||
                    (e.ProductCode?.ToLower().Contains(lowerSearch) == true) ||
                    (e.RfidData?.ToLower().Contains(lowerSearch) == true)
                );
                FilteredEntries = new ObservableCollection<PrintHistoryEntry>(filtered);
            }

            UpdateStatistics();
        }

        private void UpdateStatistics()
        {
            TotalPrints = FilteredEntries.Sum(e => e.QuantityRequested);
            SuccessfulPrints = FilteredEntries.Sum(e => e.QuantitySucceeded);
            FailedPrints = TotalPrints - SuccessfulPrints;
        }

        [RelayCommand]
        private async Task ClearHistoryAsync()
        {
            var result = MessageBox.Show(
                GetResourceString("MsgClearHistoryConfirm", "Түүхийг устгахдаа итгэлтэй байна уу?"), 
                GetResourceString("MsgConfirmTitle", "Баталгаажуулах"), 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await _historyService.ClearHistoryAsync();
                await LoadHistoryAsync();
            }
        }

        private string GetResourceString(string key, string fallback)
        {
            if (System.Windows.Application.Current == null) return fallback;
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                var val = System.Windows.Application.Current.TryFindResource(key);
                return val as string ?? fallback;
            }
            else
            {
                return System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var val = System.Windows.Application.Current.TryFindResource(key);
                    return val as string ?? fallback;
                });
            }
        }
    }
}
