using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;

namespace BarTenderClone.Models
{
    /// <summary>
    /// Filter criteria for product data table
    /// </summary>
    public partial class ProductFilterCriteria : ObservableObject
    {
        // Status Filter
        [ObservableProperty]
        private PrintStatusFilter _statusFilter = PrintStatusFilter.All;

        // Text Filters
        [ObservableProperty]
        private string _rfidFilter = string.Empty;

        [ObservableProperty]
        private string _codeFilter = string.Empty;

        [ObservableProperty]
        private string _productNameFilter = string.Empty;

        [ObservableProperty]
        private string _unitFilter = "All";

        [ObservableProperty]
        private string _branchFilter = "All";

        [ObservableProperty]
        private string _boxNumberFilter = string.Empty;

        // Price Range Filter
        [ObservableProperty]
        private decimal? _minPrice;

        [ObservableProperty]
        private decimal? _maxPrice;

        // Date Range Filter
        [ObservableProperty]
        private DateTime? _startDate;

        [ObservableProperty]
        private DateTime? _endDate;

        /// <summary>
        /// Count of active filters (for UI badge)
        /// </summary>
        public int ActiveFilterCount
        {
            get
            {
                int count = 0;
                if (StatusFilter != PrintStatusFilter.All) count++;
                if (!string.IsNullOrWhiteSpace(RfidFilter)) count++;
                if (!string.IsNullOrWhiteSpace(CodeFilter)) count++;
                if (!string.IsNullOrWhiteSpace(ProductNameFilter)) count++;
                if (!string.IsNullOrWhiteSpace(UnitFilter) && UnitFilter != "All") count++;
                if (!string.IsNullOrWhiteSpace(BranchFilter) && BranchFilter != "All") count++;
                if (!string.IsNullOrWhiteSpace(BoxNumberFilter)) count++;
                if (MinPrice.HasValue || MaxPrice.HasValue) count++;
                if (StartDate.HasValue || EndDate.HasValue) count++;
                return count;
            }
        }

        /// <summary>
        /// Check if any filter is active
        /// </summary>
        public bool HasActiveFilters => ActiveFilterCount > 0;

        /// <summary>
        /// Clear all filters
        /// </summary>
        public void ClearAll()
        {
            StatusFilter = PrintStatusFilter.All;
            RfidFilter = string.Empty;
            CodeFilter = string.Empty;
            ProductNameFilter = string.Empty;
            UnitFilter = "All";
            BranchFilter = "All";
            BoxNumberFilter = string.Empty;
            MinPrice = null;
            MaxPrice = null;
            StartDate = null;
            EndDate = null;

            // Notify that filter counts have changed
            OnPropertyChanged(nameof(ActiveFilterCount));
            OnPropertyChanged(nameof(HasActiveFilters));
        }

        partial void OnStatusFilterChanged(PrintStatusFilter value)
        {
            OnPropertyChanged(nameof(ActiveFilterCount));
            OnPropertyChanged(nameof(HasActiveFilters));
        }

        partial void OnRfidFilterChanged(string value)
        {
            OnPropertyChanged(nameof(ActiveFilterCount));
            OnPropertyChanged(nameof(HasActiveFilters));
        }

        partial void OnCodeFilterChanged(string value)
        {
            OnPropertyChanged(nameof(ActiveFilterCount));
            OnPropertyChanged(nameof(HasActiveFilters));
        }

        partial void OnProductNameFilterChanged(string value)
        {
            OnPropertyChanged(nameof(ActiveFilterCount));
            OnPropertyChanged(nameof(HasActiveFilters));
        }

        partial void OnUnitFilterChanged(string value)
        {
            OnPropertyChanged(nameof(ActiveFilterCount));
            OnPropertyChanged(nameof(HasActiveFilters));
        }

        partial void OnBranchFilterChanged(string value)
        {
            OnPropertyChanged(nameof(ActiveFilterCount));
            OnPropertyChanged(nameof(HasActiveFilters));
        }

        partial void OnBoxNumberFilterChanged(string value)
        {
            OnPropertyChanged(nameof(ActiveFilterCount));
            OnPropertyChanged(nameof(HasActiveFilters));
        }

        partial void OnMinPriceChanged(decimal? value)
        {
            OnPropertyChanged(nameof(ActiveFilterCount));
            OnPropertyChanged(nameof(HasActiveFilters));
        }

        partial void OnMaxPriceChanged(decimal? value)
        {
            OnPropertyChanged(nameof(ActiveFilterCount));
            OnPropertyChanged(nameof(HasActiveFilters));
        }

        partial void OnStartDateChanged(DateTime? value)
        {
            OnPropertyChanged(nameof(ActiveFilterCount));
            OnPropertyChanged(nameof(HasActiveFilters));
        }

        partial void OnEndDateChanged(DateTime? value)
        {
            OnPropertyChanged(nameof(ActiveFilterCount));
            OnPropertyChanged(nameof(HasActiveFilters));
        }
    }

    /// <summary>
    /// Print status filter options
    /// </summary>
    public enum PrintStatusFilter
    {
        All,
        Printed,
        NotPrinted,
        Error
    }

    /// <summary>
    /// Pagination state management
    /// </summary>
    public partial class PaginationState : ObservableObject
    {
        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _pageSize = 50; // Default: 50 items per page

        [ObservableProperty]
        private int _totalItems;

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages => TotalItems == 0 ? 1 : (int)Math.Ceiling((double)TotalItems / PageSize);

        /// <summary>
        /// Whether there is a previous page
        /// </summary>
        public bool HasPreviousPage => CurrentPage > 1;

        /// <summary>
        /// Whether there is a next page
        /// </summary>
        public bool HasNextPage => CurrentPage < TotalPages;

        /// <summary>
        /// Starting index for current page (0-based)
        /// </summary>
        public int StartIndex => (CurrentPage - 1) * PageSize;

        /// <summary>
        /// Ending index for current page (1-based, inclusive)
        /// </summary>
        public int EndIndex => Math.Min(StartIndex + PageSize, TotalItems);

        /// <summary>
        /// Page information string (e.g., "1-50 of 237")
        /// </summary>
        public string PageInfo
        {
            get
            {
                if (TotalItems == 0)
                    return "No items";

                return $"{StartIndex + 1}-{EndIndex} of {TotalItems}";
            }
        }

        /// <summary>
        /// Navigate to next page
        /// </summary>
        public void NextPage()
        {
            if (HasNextPage)
                CurrentPage++;
        }

        /// <summary>
        /// Navigate to previous page
        /// </summary>
        public void PreviousPage()
        {
            if (HasPreviousPage)
                CurrentPage--;
        }

        /// <summary>
        /// Navigate to first page
        /// </summary>
        public void GoToFirstPage()
        {
            CurrentPage = 1;
        }

        /// <summary>
        /// Navigate to last page
        /// </summary>
        public void GoToLastPage()
        {
            CurrentPage = TotalPages;
        }

        partial void OnCurrentPageChanged(int value)
        {
            OnPropertyChanged(nameof(HasPreviousPage));
            OnPropertyChanged(nameof(HasNextPage));
            OnPropertyChanged(nameof(StartIndex));
            OnPropertyChanged(nameof(EndIndex));
            OnPropertyChanged(nameof(PageInfo));
        }

        partial void OnPageSizeChanged(int value)
        {
            // Reset to first page when page size changes
            CurrentPage = 1;
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(HasPreviousPage));
            OnPropertyChanged(nameof(HasNextPage));
            OnPropertyChanged(nameof(StartIndex));
            OnPropertyChanged(nameof(EndIndex));
            OnPropertyChanged(nameof(PageInfo));
        }

        partial void OnTotalItemsChanged(int value)
        {
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(HasPreviousPage));
            OnPropertyChanged(nameof(HasNextPage));
            OnPropertyChanged(nameof(EndIndex));
            OnPropertyChanged(nameof(PageInfo));
        }
    }

    /// <summary>
    /// Pagination constants
    /// </summary>
    public static class PaginationConstants
    {
        public static readonly int[] PageSizes = { 25, 50, 100, 200, 500 };
        public const int DefaultPageSize = 50;
    }
}
