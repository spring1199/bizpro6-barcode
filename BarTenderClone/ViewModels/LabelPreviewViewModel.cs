using BarTenderClone.Models;
using BarTenderClone.Services;
using BarTenderClone.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BarTenderClone.Views;

namespace BarTenderClone.ViewModels
{
    public partial class LabelPreviewViewModel : ObservableObject
    {
        private readonly IApiService _apiService;
        private readonly IPrintService _printService;
        private readonly ISessionService _sessionService;
        private readonly ITemplateService _templateService;

        [ObservableProperty]
        private LabelTemplate _template = new LabelTemplate();

        [ObservableProperty]
        public ObservableCollection<LabelElement> _elements = new ObservableCollection<LabelElement>();

        [ObservableProperty]
        private LabelElement? _selectedElement;

        [ObservableProperty]
        private bool _isDirty = false;

        [ObservableProperty]
        private string? _currentFilePath;

        [ObservableProperty]
        private ObservableCollection<ResourceItem> _products = new();

        [ObservableProperty]
        private ResourceItem? _selectedProduct;

        [ObservableProperty]
        private ObservableCollection<ResourceItem> _selectedProducts = new();

        [ObservableProperty]
        private bool _isMultiSelect = false;

        [ObservableProperty]
        private string _selectionSummary = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> _availablePrinters = new();

        [ObservableProperty]
        private string? _selectedPrinter;

        [ObservableProperty]
        private int _printerDpi = 203;

        [ObservableProperty]
        private ObservableCollection<int> _availableDpis = new ObservableCollection<int> { 203, 300, 600 };

        [ObservableProperty]
        private int _quantity = 1;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isAllSelected = false;

        partial void OnIsAllSelectedChanged(bool value)
        {
            foreach (var product in Products)
            {
                product.IsSelected = value;
            }
            UpdateSelectedItemsFromCheckboxes();
        }

        [RelayCommand]
        private void SelectAllVisible()
        {
            foreach (var product in Products)
            {
                product.IsSelected = true;
            }
            UpdateSelectedItemsFromCheckboxes();
        }

        [RelayCommand]
        private void UnselectAll()
        {
            foreach (var product in AllProducts)
            {
                product.IsSelected = false;
            }
            IsAllSelected = false;
            UpdateSelectedItemsFromCheckboxes();
        }

        // ===== FILTERING & PAGINATION =====

        [ObservableProperty]
        private ProductFilterCriteria _filterCriteria = new();

        partial void OnFilterCriteriaChanged(ProductFilterCriteria? oldValue, ProductFilterCriteria newValue)
        {
            // Unsubscribe from old
            if (oldValue != null)
                oldValue.PropertyChanged -= FilterCriteria_PropertyChanged;
            
            // Subscribe to new
            if (newValue != null)
                newValue.PropertyChanged += FilterCriteria_PropertyChanged;
        }

        private void FilterCriteria_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // When any filter property changes, re-apply filters
            ApplyFiltersAndPagination();
        }

        [ObservableProperty]
        private PaginationState _pagination = new();

        [ObservableProperty]
        private ObservableCollection<int> _availablePageSizes = new(PaginationConstants.PageSizes);

        [ObservableProperty]
        private ObservableCollection<ResourceItem> _allProducts = new(); // Full dataset from API

        [ObservableProperty]
        private ObservableCollection<ResourceItem> _filteredProducts = new(); // After filters, before pagination

        [ObservableProperty]
        private bool _isLoadingData = false;

        [ObservableProperty]
        private bool _isFilterExpanded = false; // Collapsed by default

        [ObservableProperty]
        private string _quickSearchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> _availableUnits = new() { "All" };

        [ObservableProperty]
        private ObservableCollection<string> _availableBranches = new() { "All" };

        partial void OnQuickSearchTextChanged(string value)
        {
            ApplyFiltersAndPagination();
        }

        [ObservableProperty]
        private double _templateWidthInches = 2.126; // Default 54mm

        [ObservableProperty]
        private double _templateHeightInches = 1.339; // Default 34mm

        // Helper properties for MM binding
        public double TemplateWidthMm
        {
            get => Math.Round(TemplateWidthInches * LabelSizeHelper.MM_PER_INCH, 1);
            set
            {
                if (Math.Abs(TemplateWidthMm - value) > 0.1)
                {
                    TemplateWidthInches = LabelSizeHelper.MmToInches(value);
                    OnPropertyChanged();
                }
            }
        }

        public double TemplateHeightMm
        {
            get => Math.Round(TemplateHeightInches * LabelSizeHelper.MM_PER_INCH, 1);
            set
            {
                if (Math.Abs(TemplateHeightMm - value) > 0.1)
                {
                    TemplateHeightInches = LabelSizeHelper.MmToInches(value);
                    OnPropertyChanged();
                }
            }
        }

        public (double left, double top, double right, double bottom) SafeMargins
            => LabelSizeHelper.GetSafeMarginsPixels();

        // Individual margin properties for XAML binding (visual margin indicators)
        public double SafeMarginLeft => LabelSizeHelper.GetSafeMarginsPixels().left;
        public double SafeMarginTop => LabelSizeHelper.GetSafeMarginsPixels().top;
        public double SafeMarginRight => Template.Width - LabelSizeHelper.GetSafeMarginsPixels().right;
        public double SafeMarginBottom => Template.Height - LabelSizeHelper.GetSafeMarginsPixels().bottom;

        // Label size presets for quick selection
        [ObservableProperty]
        private ObservableCollection<LabelSizePreset> _labelSizePresets = new()
        {
            new LabelSizePreset { Name = "54mm × 34mm (Default)", WidthMm = 54, HeightMm = 34 },
            new LabelSizePreset { Name = "60mm × 40mm", WidthMm = 60, HeightMm = 40 },
            new LabelSizePreset { Name = "100mm × 50mm", WidthMm = 100, HeightMm = 50 },
            new LabelSizePreset { Name = "100mm × 150mm", WidthMm = 100, HeightMm = 150 },
            new LabelSizePreset { Name = "4\" × 6\" (Shipping)", WidthMm = 101.6, HeightMm = 152.4 }
        };

        [ObservableProperty]
        private LabelSizePreset? _selectedPreset;

        partial void OnSelectedPresetChanged(LabelSizePreset? value)
        {
            if (value != null && value.WidthMm > 0)
            {
                TemplateWidthInches = LabelSizeHelper.MmToInches(value.WidthMm);
                TemplateHeightInches = LabelSizeHelper.MmToInches(value.HeightMm);
            }
        }

        [ObservableProperty]
        private bool _autoPopulateOnSelect = true; // Enable auto-populate by default

        [ObservableProperty]
        private bool _enableRfidEncoding = true;

        [ObservableProperty]
        private RfidDataFormat _selectedRfidFormat = RfidDataFormat.Hexadecimal;

        [ObservableProperty]
        private string _lastPrintStatus = string.Empty;

        [ObservableProperty]
        private bool _isPrinting = false;

        // Media Type selection
        [ObservableProperty]
        private MediaType _selectedMediaType = MediaType.DirectThermal;

        public ObservableCollection<MediaType> AvailableMediaTypes { get; } = new()
        {
            MediaType.DirectThermal,
            MediaType.ThermalTransfer
        };

        // Element type options for dynamic switching
        public ObservableCollection<ElementType> AvailableElementTypes { get; } = new()
        {
            ElementType.Text,
            ElementType.Barcode,
            ElementType.QRCode
        };

        // Computed property to enable/disable Print Selected button
        public bool HasSelectedItems => SelectedProducts.Count > 0;
        public int SelectedItemCount => SelectedProducts.Count;

        // Zoom properties
        [ObservableProperty]
        private double _currentZoom = 1.0;

        [ObservableProperty]
        private double _minZoom = 0.25;

        [ObservableProperty]
        private double _maxZoom = 4.0;

        // Computed property for zoom percentage display
        public string ZoomPercentage => $"{Math.Round(CurrentZoom * 100)}%";

        partial void OnCurrentZoomChanged(double value)
        {
            OnPropertyChanged(nameof(ZoomPercentage));
        }

        public LabelPreviewViewModel(IApiService apiService, IPrintService printService, ISessionService sessionService, ITemplateService templateService)
        {
            _apiService = apiService;
            _printService = printService;
            _sessionService = sessionService;
            _templateService = templateService;

            LoadPrinters();
            UpdateTemplateSize(); // Init defaults

            // Subscribe to elements collection changes to track dirty state
            Elements.CollectionChanged += (s, e) => IsDirty = true;

            // Subscribe to filter property changes
            FilterCriteria.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != nameof(ProductFilterCriteria.ActiveFilterCount) &&
                    e.PropertyName != nameof(ProductFilterCriteria.HasActiveFilters))
                {
                    ApplyFiltersAndPagination();
                }
            };

            // Subscribe to pagination changes
            Pagination.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PaginationState.CurrentPage) ||
                    e.PropertyName == nameof(PaginationState.PageSize))
                {
                    UpdatePagedView();
                }
            };
        }

        partial void OnTemplateWidthInchesChanged(double value) 
        {
            UpdateTemplateSize();
            OnPropertyChanged(nameof(TemplateWidthMm));
        }

        partial void OnTemplateHeightInchesChanged(double value) 
        {
            UpdateTemplateSize();
            OnPropertyChanged(nameof(TemplateHeightMm));
        }

        partial void OnSelectedProductChanged(ResourceItem? value)
        {
            // When a product is selected, update the canvas accordingly
            if (value == null || IsMultiSelect) return;

            // If there are existing elements (loaded template), just refresh their content
            // This preserves the template layout while updating the data
            if (Elements.Count > 0)
            {
                RefreshElementsFromProduct(value);
            }
            // If canvas is empty and auto-populate is enabled, create default layout
            else if (AutoPopulateOnSelect)
            {
                PopulateCanvasWithProductData(value);
            }
        }

        partial void OnSelectedProductsChanged(ObservableCollection<ResourceItem> value)
        {
            IsMultiSelect = value.Count > 1;
            // SelectionSummary is now handled by UpdateSelectionSummary() for better cross-page feedback
            SelectedProduct = value.FirstOrDefault();
        }

        private LabelElement? _previousSelectedElement;

        partial void OnSelectedElementChanged(LabelElement? value)
        {
            // Unsubscribe from previous element to avoid memory leaks
            if (_previousSelectedElement != null)
            {
                _previousSelectedElement.PropertyChanged -= OnElementPropertyChanged;
            }

            // Subscribe to new element's PropertyChanged
            if (value != null)
            {
                value.PropertyChanged += OnElementPropertyChanged;
            }

            _previousSelectedElement = value;
        }

        private void OnElementPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // User edited an element property manually
            // Set IsDirty to true to prevent auto-populate from overwriting changes
            if (e.PropertyName != nameof(LabelElement.IsSelected))
            {
                IsDirty = true;
            }
        }

        public void UpdateSelectedItems(System.Collections.IList selectedItems)
        {
            // Sync checkbox state with DataGrid selection
            foreach (var product in Products)
            {
                product.IsSelected = selectedItems.Contains(product);
            }

            SelectedProducts.Clear();
            foreach (ResourceItem item in selectedItems)
            {
                SelectedProducts.Add(item);
            }

            // Update select-all checkbox state
            UpdateSelectAllCheckboxState();
            UpdateSelectionSummary();
        }

        [RelayCommand]
        private void UpdateSelectedItemsFromCheckboxes()
        {
            // CRITICAL FIX: Scan AllProducts instead of just Products (current page)
            var selectedItems = AllProducts.Where(p => p.IsSelected).ToList();

            SelectedProducts.Clear();
            foreach (var item in selectedItems)
            {
                SelectedProducts.Add(item);
            }

            // Update IsAllSelected state based on CURRENT PAGE only
            UpdateSelectAllCheckboxState();
            UpdateSelectionSummary();
        }

        private void UpdateSelectAllCheckboxState()
        {
            if (Products.Count == 0)
            {
                IsAllSelected = false;
            }
            else
            {
                // Check if all items on current page are selected
                IsAllSelected = Products.All(p => p.IsSelected);
            }
        }

        private void UpdateSelectionSummary()
        {
            int totalSelected = SelectedProducts.Count; // All selected across all pages
            int onCurrentPage = Products.Count(p => p.IsSelected); // Selected on current page

            if (totalSelected == 0)
            {
                SelectionSummary = "";
            }
            else if (totalSelected == 1)
            {
                SelectionSummary = "1 item selected";
            }
            else if (totalSelected == onCurrentPage)
            {
                // All selections are on current page
                SelectionSummary = $"{totalSelected} items selected";
            }
            else
            {
                // Selections span multiple pages
                SelectionSummary = $"{totalSelected} selected ({onCurrentPage} on this page)";
            }

            // Notify button state properties
            OnPropertyChanged(nameof(HasSelectedItems));
            OnPropertyChanged(nameof(SelectedItemCount));
        }

        private void OnResourceItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ResourceItem.IsSelected))
            {
                UpdateSelectedItemsFromCheckboxes();
            }
        }

        private void UpdateTemplateSize()
        {
             // Store old dimensions for scaling calculation
             double oldWidth = Template.Width;
             double oldHeight = Template.Height;

             // Approx 96 DPI for screen preview
             Template.Width = TemplateWidthInches * 96;
             Template.Height = TemplateHeightInches * 96;

             // Scale elements proportionally when template size changes
             if (oldWidth > 0 && oldHeight > 0 && Elements.Count > 0)
             {
                 double scaleX = Template.Width / oldWidth;
                 double scaleY = Template.Height / oldHeight;
                 
                 // Only scale if there's a significant change (>1%)
                 if (Math.Abs(scaleX - 1.0) > 0.01 || Math.Abs(scaleY - 1.0) > 0.01)
                 {
                     ScaleElements(scaleX, scaleY);
                 }
             }

             // Notify margin properties that depend on template size
             OnPropertyChanged(nameof(SafeMarginRight));
             OnPropertyChanged(nameof(SafeMarginBottom));
        }

        /// <summary>
        /// Scales all elements proportionally when label size changes.
        /// This ensures elements remain visible and properly positioned.
        /// </summary>
        private void ScaleElements(double scaleX, double scaleY)
        {
            // Use the smaller scale factor for font size to maintain readability
            double fontScale = Math.Min(scaleX, scaleY);

            foreach (var element in Elements)
            {
                // Scale position
                element.X *= scaleX;
                element.Y *= scaleY;
                
                // Scale dimensions
                element.Width *= scaleX;
                element.Height *= scaleY;
                
                // Scale font size (use uniform scale to maintain aspect ratio)
                if (element.Type == ElementType.Text)
                {
                    element.FontSize = Math.Max(8, element.FontSize * fontScale);
                }
            }
        }

        [RelayCommand]
        private void RefreshPrinters()
        {
            LoadPrinters();
            StatusMessage = "Printer list refreshed.";
        }

        private void LoadPrinters()
        {
            var savedPrinter = SelectedPrinter;
            var printers = _printService.GetInstalledPrinters();
            
            AvailablePrinters.Clear();
            foreach (var printer in printers)
            {
                AvailablePrinters.Add(printer);
            }

            // Restore selection or default to first
            if (savedPrinter != null && AvailablePrinters.Contains(savedPrinter))
            {
                SelectedPrinter = savedPrinter;
            }
            else if (AvailablePrinters.Count > 0)
            {
                SelectedPrinter = AvailablePrinters[0];
            }
        }

        partial void OnSelectedPrinterChanged(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;

            // Smart DPI Detection
            // Checks for common DPI indicators in printer name
            int newDpi = 0;

            if (value.Contains("600")) newDpi = 600;
            else if (value.Contains("300")) newDpi = 300;
            else if (value.Contains("203")) newDpi = 203;

            // Only update if we found a match and it's different
            if (newDpi > 0 && PrinterDpi != newDpi)
            {
                PrinterDpi = newDpi;
                StatusMessage = $"Auto-detected {newDpi} DPI from printer.";
            }
        }

        /// <summary>
        /// Automatically populates the canvas with label elements based on selected product data.
        /// Creates a professional label layout with product information.
        /// Adapts to Compact (4x6cm) or Standard sizes.
        /// </summary>
        private void PopulateCanvasWithProductData(ResourceItem product)
        {
            // Validate that we have parsed document data
            if (product == null)
            {
                StatusMessage = "Error: No product selected";
                return;
            }

            if (product.ParsedDocument == null)
            {
                StatusMessage = "Error: Product data not properly loaded. Please try fetching data again.";
                System.Windows.MessageBox.Show(
                    "The selected product data is not properly loaded.\n\nPlease try:\n1. Click 'Fetch Data' again\n2. Select a different product",
                    "Data Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Clear existing elements (optional - you can comment this out if you want to keep existing elements)
            Elements.Clear();
            SelectedElement = null;

            // Determine Layout Mode
            bool isCompact = (TemplateWidthInches * TemplateHeightInches) < 6.0; // Less than 6 sq inches (~38cm2) -> Compact

            if (isCompact)
            {
                PopulateCompactLayout(product);
            }
            else
            {
                PopulateStandardLayout(product);
            }

            // Reset dirty flag since this was programmatic population, not user edit
            // IsDirty should only be true for manual user changes
            IsDirty = false;
            StatusMessage = $"Auto-populated label with {product.ProductName}";
        }

        private void PopulateCompactLayout(ResourceItem product)
        {
            // The "Compact" and "Standard" layouts are merged into a single "Retail" style
            // as per user request to match the specific "Left" image reference.
            PopulateStandardLayout(product);
        }

        /// <summary>
        /// Refreshes existing label elements with data from the selected product.
        /// Preserves the template layout (positions, fonts, sizes) while updating content values.
        /// This is called when user selects a different product after loading a template.
        /// </summary>
        private void RefreshElementsFromProduct(ResourceItem product)
        {
            if (product == null) return;

            foreach (var element in Elements)
            {
                // Only update elements that have a FieldName (data-bound elements)
                if (string.IsNullOrWhiteSpace(element.FieldName)) continue;

                // Resolve the field value based on FieldName
                string newContent = element.FieldName.ToUpperInvariant() switch
                {
                    "RFID" => product.Rfid ?? element.Content,
                    "ITEMCODE" => product.Code ?? element.Content,
                    "PRODUCTNAME" => product.ProductName ?? element.Content,
                    "PRICE" => $"MNT {product.Price:N0}",
                    "BRANCH" => product.Branch ?? element.Content,
                    "STATUS" => product.Status ?? element.Content,
                    "UNIT" => product.Unit ?? element.Content,
                    "DATE" => product.CreationTime.ToString("yyyy-MM-dd"),
                    _ => element.Content // Keep original if unknown field
                };

                element.Content = newContent;
            }

            StatusMessage = $"Өгөгдөл шинэчлэгдлээ: {product.ProductName}";
        }

        private void PopulateStandardLayout(ResourceItem product)
        {
            // PROFESSIONAL RETAIL LABEL LAYOUT
            // Uses mm-based sizing for consistent output across all printers

            var margins = LabelSizeHelper.GetSafeMarginsPixels();
            double leftMargin = margins.left;
            double rightMargin = margins.right;
            double availableWidth = Template.Width - leftMargin - rightMargin;
            
            // Starting Y position
            double currentY = margins.top;

            // Define sizes in MILLIMETERS for consistent physical output
            const double CODE_FONT_MM = 2.5;      // ~2.5mm font height
            const double NAME_FONT_MM = 3.0;      // ~3mm font height
            const double PRICE_FONT_MM = 3.5;     // ~3.5mm font height
            const double BARCODE_HEIGHT_MM = 8.0; // ~8mm barcode height
            const double LINE_SPACING_MM = 1.0;   // ~1mm between lines
            const double SECTION_SPACING_MM = 2.0; // ~2mm between sections

            // Convert mm to screen pixels for UI display
            double codeFontPx = LabelSizeHelper.MmToScreenPixels(CODE_FONT_MM);
            double nameFontPx = LabelSizeHelper.MmToScreenPixels(NAME_FONT_MM);
            double priceFontPx = LabelSizeHelper.MmToScreenPixels(PRICE_FONT_MM);
            double barcodeHeightPx = LabelSizeHelper.MmToScreenPixels(BARCODE_HEIGHT_MM);
            double lineSpacingPx = LabelSizeHelper.MmToScreenPixels(LINE_SPACING_MM);
            double sectionSpacingPx = LabelSizeHelper.MmToScreenPixels(SECTION_SPACING_MM);

            // === SECTION 1: Product Code ===
            Elements.Add(new LabelElement
            {
                Type = ElementType.Text,
                Content = product.Code ?? "N/A",
                FieldName = "ItemCode",
                X = leftMargin,
                Y = currentY,
                FontSize = codeFontPx,
                Width = availableWidth
            });
            currentY += codeFontPx + sectionSpacingPx;

            // === SECTION 2: Product Name ===
            Elements.Add(new LabelElement
            {
                Type = ElementType.Text,
                Content = product.ProductName ?? "Product",
                FieldName = "ProductName",
                X = leftMargin,
                Y = currentY,
                FontSize = nameFontPx,
                IsBold = false,
                Width = availableWidth
            });
            currentY += nameFontPx + lineSpacingPx;

            // === SECTION 3: Price (Bold) ===
            Elements.Add(new LabelElement
            {
                Type = ElementType.Text,
                Content = $"MNT {product.Price:N0}",
                X = leftMargin,
                Y = currentY,
                FontSize = priceFontPx,
                IsBold = true,
                Width = availableWidth
            });
            currentY += priceFontPx + sectionSpacingPx;

            // === SECTION 4: Barcode (Let ZplGeneratorService handle centering) ===
            // Ensure barcode doesn't exceed bottom margin
            double maxBarcodeHeight = Template.Height - currentY - margins.bottom;
            double effectiveHeight = Math.Min(barcodeHeightPx, maxBarcodeHeight);
            effectiveHeight = Math.Max(effectiveHeight, LabelSizeHelper.MmToScreenPixels(5)); // Min 5mm

            Elements.Add(new LabelElement
            {
                Type = ElementType.Barcode,
                Content = product.Rfid ?? "12345678",
                FieldName = "RFID",
                X = leftMargin,  // Start from left margin
                Y = currentY,
                Width = availableWidth,  // Full available width for centering calculation
                Height = effectiveHeight,
                IsCentered = true  // ZplGeneratorService will center within Width
            });
        }

        [RelayCommand]
        private void AddText()
        {
            var margins = LabelSizeHelper.GetSafeMarginsPixels();
            double defaultFontSize = LabelSizeHelper.CalculateResponsiveFontSize(
                TemplateWidthInches, TemplateHeightInches, FontSizeCategory.Medium);

            Elements.Add(new LabelElement
            {
                Type = ElementType.Text,
                Content = "Sample Text",
                X = margins.left,
                Y = margins.top,
                FontSize = defaultFontSize,
                Width = Template.Width - margins.left - margins.right
            });
            IsDirty = true;
        }

        [RelayCommand]
        private void AddBarcode()
        {
            var margins = LabelSizeHelper.GetSafeMarginsPixels();
            var (_, height) = LabelSizeHelper.CalculateResponsiveBarcodeSize(
                TemplateWidthInches, TemplateHeightInches);
            
            double width = LabelSizeHelper.CalculateCode128Width("12345678", PrinterDpi);

            Elements.Add(new LabelElement
            {
                Type = ElementType.Barcode,
                Content = "12345678",
                X = margins.left,
                Y = Template.Height / 2,
                Width = width,
                Height = height
            });
            IsDirty = true;
        }

        [RelayCommand]
        private void ClearCanvas()
        {
            if (IsDirty && Elements.Count > 0)
            {
                var result = System.Windows.MessageBox.Show(
                    "You have unsaved changes. Are you sure you want to clear the canvas?",
                    "Unsaved Changes",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result != System.Windows.MessageBoxResult.Yes)
                    return;
            }

            Elements.Clear();
            SelectedElement = null;
            IsDirty = false;
            CurrentFilePath = null;
            Template.Name = "New Template";
        }

        [RelayCommand]
        private void DeleteElement()
        {
            if (SelectedElement != null)
            {
                Elements.Remove(SelectedElement);
                SelectedElement = null;
                IsDirty = true;
            }
        }

        [RelayCommand]
        private void NewTemplate()
        {
            ClearCanvas(); // Reuses dirty check logic
        }

        [RelayCommand]
        private async Task SaveTemplateAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(CurrentFilePath))
                {
                    await SaveTemplateAsAsync();
                    return;
                }

                StatusMessage = "Saving template...";

                await _templateService.SaveTemplateAsync(
                    Template,
                    Elements,
                    TemplateWidthInches,
                    TemplateHeightInches,
                    CurrentFilePath); // CurrentFilePath holds the Name now

                IsDirty = false;
                StatusMessage = $"Template saved: {CurrentFilePath}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Save failed: {ex.Message}";
                System.Windows.MessageBox.Show(
                    $"Failed to save template:\n\n{ex.Message}",
                    "Save Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task SaveTemplateAsAsync()
        {
            try
            {
                var dialog = new SaveTemplateWindow(Template.Name == "New Template" ? "" : Template.Name);
                if (dialog.ShowDialog() == true)
                {
                    var name = dialog.TemplateName;
                    StatusMessage = "Saving template...";

                    await _templateService.SaveTemplateAsync(
                        Template,
                        Elements,
                        TemplateWidthInches,
                        TemplateHeightInches,
                        name);

                    CurrentFilePath = name;
                    Template.Name = name;
                    IsDirty = false;
                    StatusMessage = $"Template saved: {name}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Save failed: {ex.Message}";
                System.Windows.MessageBox.Show(
                    $"Failed to save template:\n\n{ex.Message}",
                    "Save Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task LoadTemplateAsync()
        {
            try
            {
                // Check for unsaved changes
                if (IsDirty && Elements.Count > 0)
                {
                    var result = System.Windows.MessageBox.Show(
                        "You have unsaved changes. Do you want to save before loading a new template?",
                        "Unsaved Changes",
                        System.Windows.MessageBoxButton.YesNoCancel,
                        System.Windows.MessageBoxImage.Warning);

                    if (result == System.Windows.MessageBoxResult.Cancel)
                        return;

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        await SaveTemplateAsync();
                        if (IsDirty) // Save was cancelled or failed
                            return;
                    }
                }

                bool selecting = true;
                while (selecting)
                {
                    var templates = _templateService.GetTemplateNames().ToList();
                    var dialog = new LoadTemplateWindow(templates);
                    
                    if (dialog.ShowDialog() == true)
                    {
                        if (dialog.IsDeleteRequested)
                        {
                            _templateService.DeleteTemplate(dialog.SelectedTemplate);
                            continue; // Refresh list
                        }

                        StatusMessage = "Loading template...";
                        var name = dialog.SelectedTemplate;

                        var (template, elements, widthInches, heightInches) = await _templateService.LoadTemplateAsync(name);

                        // Clear current state
                        Elements.Clear();
                        SelectedElement = null;

                        // Load new template
                        Template.Name = template.Name;
                        Template.Width = template.Width;
                        Template.Height = template.Height;
                        TemplateWidthInches = widthInches;
                        TemplateHeightInches = heightInches;

                        // Load elements
                        foreach (var element in elements)
                        {
                            Elements.Add(element);
                        }

                        IsDirty = false;
                        CurrentFilePath = name;
                        StatusMessage = $"Template loaded: {template.Name} ({elements.Count} elements)";
                        selecting = false;
                    }
                    else
                    {
                        selecting = false;
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load failed: {ex.Message}";
                System.Windows.MessageBox.Show(
                    $"Failed to load data:\n\n{ex.Message}",
                    "Load Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        // ===== FILTERING & PAGINATION METHODS =====

        /// <summary>
        /// Applies all active filters and updates pagination
        /// </summary>
        private void ApplyFiltersAndPagination()
        {
            // Start with all products
            IEnumerable<ResourceItem> filtered = AllProducts;

            // Quick Search (multi-field)
            if (!string.IsNullOrWhiteSpace(QuickSearchText))
            {
                var search = QuickSearchText.ToLowerInvariant();
                filtered = filtered.Where(p =>
                    (p.ProductName?.ToLowerInvariant().Contains(search) ?? false) ||
                    (p.Code?.ToLowerInvariant().Contains(search) ?? false) ||
                    (p.Rfid?.ToLowerInvariant().Contains(search) ?? false));
            }

            // Status Filter
            if (FilterCriteria.StatusFilter != PrintStatusFilter.All)
            {
                filtered = FilterCriteria.StatusFilter switch
                {
                    PrintStatusFilter.Printed => filtered.Where(p => p.IsPrinted),
                    PrintStatusFilter.NotPrinted => filtered.Where(p => !p.IsPrinted && string.IsNullOrEmpty(p.PrintErrorMessage)),
                    PrintStatusFilter.Error => filtered.Where(p => !string.IsNullOrEmpty(p.PrintErrorMessage)),
                    _ => filtered
                };
            }

            // RFID Filter
            if (!string.IsNullOrWhiteSpace(FilterCriteria.RfidFilter))
            {
                var rfidSearch = FilterCriteria.RfidFilter.ToLowerInvariant();
                filtered = filtered.Where(p => p.Rfid?.ToLowerInvariant().Contains(rfidSearch) ?? false);
            }

            // Code Filter
            if (!string.IsNullOrWhiteSpace(FilterCriteria.CodeFilter))
            {
                var codeSearch = FilterCriteria.CodeFilter.ToLowerInvariant();
                filtered = filtered.Where(p => p.Code?.ToLowerInvariant().Contains(codeSearch) ?? false);
            }

            // Product Name Filter
            if (!string.IsNullOrWhiteSpace(FilterCriteria.ProductNameFilter))
            {
                var nameSearch = FilterCriteria.ProductNameFilter.ToLowerInvariant();
                filtered = filtered.Where(p => p.ProductName?.ToLowerInvariant().Contains(nameSearch) ?? false);
            }

            // Unit Filter
            if (!string.IsNullOrWhiteSpace(FilterCriteria.UnitFilter) && FilterCriteria.UnitFilter != "All")
            {
                filtered = filtered.Where(p => p.Unit == FilterCriteria.UnitFilter);
            }

            // Branch Filter
            if (!string.IsNullOrWhiteSpace(FilterCriteria.BranchFilter) && FilterCriteria.BranchFilter != "All")
            {
                filtered = filtered.Where(p => p.Branch == FilterCriteria.BranchFilter);
            }

            // Box Number Filter
            if (!string.IsNullOrWhiteSpace(FilterCriteria.BoxNumberFilter))
            {
                var boxSearch = FilterCriteria.BoxNumberFilter.ToLowerInvariant();
                filtered = filtered.Where(p => p.BoxNumber?.ToLowerInvariant().Contains(boxSearch) ?? false);
            }

            // Price Range Filter
            if (FilterCriteria.MinPrice.HasValue)
            {
                filtered = filtered.Where(p => p.Price >= FilterCriteria.MinPrice.Value);
            }
            if (FilterCriteria.MaxPrice.HasValue)
            {
                filtered = filtered.Where(p => p.Price <= FilterCriteria.MaxPrice.Value);
            }

            // Date Range Filter
            if (FilterCriteria.StartDate.HasValue)
            {
                filtered = filtered.Where(p => p.CreationTime.Date >= FilterCriteria.StartDate.Value.Date);
            }
            if (FilterCriteria.EndDate.HasValue)
            {
                filtered = filtered.Where(p => p.CreationTime.Date <= FilterCriteria.EndDate.Value.Date);
            }

            // Update filtered collection
            FilteredProducts.Clear();
            foreach (var item in filtered)
            {
                FilteredProducts.Add(item);
            }

            // Update pagination total
            Pagination.TotalItems = FilteredProducts.Count;

            // Update paged view
            UpdatePagedView();

            // Update status message
            if (FilterCriteria.HasActiveFilters || !string.IsNullOrWhiteSpace(QuickSearchText))
            {
                StatusMessage = $"Showing {Pagination.TotalItems} of {AllProducts.Count} items";
            }
            else
            {
                StatusMessage = $"Loaded {AllProducts.Count} items";
            }
        }

        /// <summary>
        /// Updates the displayed page of products
        /// </summary>
        private void UpdatePagedView()
        {
            Products.Clear();

            var pagedItems = FilteredProducts
                .Skip(Pagination.StartIndex)
                .Take(Pagination.PageSize);

            foreach (var item in pagedItems)
            {
                Products.Add(item);
            }

            // Update select-all state based on current page
            UpdateSelectAllCheckboxState();
            UpdateSelectionSummary();
        }

        [RelayCommand]
        private void ClearAllFilters()
        {
            QuickSearchText = string.Empty;
            FilterCriteria.ClearAll();
            StatusMessage = "Filters cleared";
        }

        [RelayCommand]
        private void ToggleFilterPanel()
        {
            IsFilterExpanded = !IsFilterExpanded;
        }

        // Pagination Commands
        [RelayCommand]
        private void GoToFirstPage() => Pagination.GoToFirstPage();

        [RelayCommand]
        private void GoToPreviousPage() => Pagination.PreviousPage();

        [RelayCommand]
        private void GoToNextPage() => Pagination.NextPage();

        [RelayCommand]
        private void GoToLastPage() => Pagination.GoToLastPage();

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            StatusMessage = "Loading...";
            IsLoadingData = true;
            try
            {
                // Check authentication first
                if (!_sessionService.IsAuthenticated)
                {
                    StatusMessage = "Error: Not authenticated. Please log in first.";
                    System.Windows.MessageBox.Show(
                        "You are not logged in. Please restart the application and log in.",
                        "Authentication Required",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // Warn if there are active selections
                if (SelectedProducts.Count > 0)
                {
                    var dialogResult = System.Windows.MessageBox.Show(
                        $"You have {SelectedProducts.Count} item(s) selected.\n\n" +
                        "Loading new data will clear all selections. Continue?",
                        "Active Selections",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning);

                    if (dialogResult != System.Windows.MessageBoxResult.Yes)
                    {
                        StatusMessage = "Data load cancelled.";
                        IsLoadingData = false;
                        return;
                    }
                }

                // Clear existing data
                AllProducts.Clear();
                FilteredProducts.Clear();
                Products.Clear();
                SelectedProducts.Clear();

                // Fetch all data in chunks of 1000
                const int chunkSize = 1000;
                int skip = 0;
                int totalCount = 0;

                do
                {
                    var result = await _apiService.GetResourcesAsync(skip, chunkSize);

                    if (result == null)
                        break;

                    totalCount = result.TotalCount;

                    foreach (var item in result.Items)
                    {
                        // Subscribe to property changes to track selection
                        item.PropertyChanged += OnResourceItemPropertyChanged;
                        AllProducts.Add(item);

                        // Initialize print status from backend if available
                        if (item.ParsedDocument?.ProductRfid != null)
                        {
                            var rfidDto = item.ParsedDocument.ProductRfid;
                            if (rfidDto.IsPrinted.HasValue)
                                item.IsPrinted = rfidDto.IsPrinted.Value;
                            if (rfidDto.LastPrintedTime.HasValue)
                                item.LastPrintedTime = rfidDto.LastPrintedTime.Value;
                            if (!string.IsNullOrEmpty(rfidDto.PrintErrorMessage))
                                item.PrintErrorMessage = rfidDto.PrintErrorMessage;
                        }
                    }

                    skip += chunkSize;

                } while (skip < totalCount);

                if (AllProducts.Count > 0)
                {
                    // Populate available units for filter dropdown
                    var units = AllProducts
                        .Select(p => p.Unit)
                        .Where(u => !string.IsNullOrWhiteSpace(u))
                        .Distinct()
                        .OrderBy(u => u)
                        .ToList();

                    AvailableUnits.Clear();
                    AvailableUnits.Add("All");
                    foreach (var unit in units)
                    {
                        AvailableUnits.Add(unit);
                    }

                    // Populate available branches for filter dropdown
                    var branches = AllProducts
                        .Select(p => p.Branch)
                        .Where(b => !string.IsNullOrWhiteSpace(b))
                        .Distinct()
                        .OrderBy(b => b)
                        .ToList();

                    AvailableBranches.Clear();
                    AvailableBranches.Add("All");
                    foreach (var branch in branches)
                    {
                        AvailableBranches.Add(branch);
                    }

                    // Reset pagination
                    Pagination.CurrentPage = 1;
                    Pagination.TotalItems = AllProducts.Count;

                    // Apply filters and pagination
                    ApplyFiltersAndPagination();

                    StatusMessage = $"Loaded {AllProducts.Count} items.";
                }
                else
                {
                    StatusMessage = "No items found or API error.";
                }
            }
            catch (System.Net.Http.HttpRequestException httpEx)
            {
                StatusMessage = $"Network error: {httpEx.Message}";
                System.Windows.MessageBox.Show(
                    $"Network error: {httpEx.Message}\n\nPlease check your internet connection.",
                    "Network Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                System.Windows.MessageBox.Show(
                    $"Failed to load data:\n\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoadingData = false;
            }
        }

        [RelayCommand]
        private async Task SyncLocationAsync()
        {
            try
            {
                StatusMessage = "Байршлын мэдээлэлг SAP-аас синк хийж байна...";
                var success = await _apiService.EnqueueBranchSyncAsync();
                if (success)
                {
                    StatusMessage = "Байршил синк хийх хүсэлт амжилттай илгээгдлээ.";
                    System.Windows.MessageBox.Show("Байршлын мэдээллийг SAP-аас синк хийх процесс эхэллээ. Түр хүлээгээд 'Fetch Data' дарж шалгана уу.", 
                        "Синк эхэллээ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = "Байршил синк хийхэд алдаа гарлаа.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Алдаа: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task SyncAssetAsync()
        {
            try
            {
                StatusMessage = "Хөрөнгийн мэдээллийг SAP-аас синк хийж байна...";
                var success = await _apiService.EnqueueEquipmentSyncAsync();
                if (success)
                {
                    StatusMessage = "Хөрөнгө синк хийх хүсэлт амжилттай илгээгдлээ.";
                    System.Windows.MessageBox.Show("Хөрөнгийн мэдээллийг SAP-аас синк хийх процесс эхэллээ. Түр хүлээгээд 'Fetch Data' дарж шалгана уу.", 
                        "Синк эхэллээ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = "Хөрөнгө синк хийхэд алдаа гарлаа.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Алдаа: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task MarkAsPrintedAsync()
        {
            if (!SelectedProducts.Any() && SelectedProduct == null) 
            {
                StatusMessage = "Бараа сонгогдоогүй байна.";
                return;
            }

            try
            {
                StatusMessage = "Хэвлэсэн төлөвт оруулж байна...";
                var items = SelectedProducts.Any() ? SelectedProducts.ToList() : new List<ResourceItem> { SelectedProduct! };
                
                int successCount = 0;
                foreach (var item in items)
                {
                    var success = await _apiService.UpdatePrintStatusAsync(item, true);
                    if (success)
                    {
                        item.IsPrinted = true;
                        item.PrintErrorMessage = null;
                        successCount++;
                    }
                }
                
                StatusMessage = $"{successCount} барааг хэвлэсэн төлөвт орууллаа.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Алдаа: {ex.Message}";
            }
        }

        /// <summary>
        /// Validates label elements to ensure they fit within printable area.
        /// Returns list of warning messages for elements that may be cropped.
        /// Uses relaxed checks for WYSIWYG mode (preview = print output).
        /// </summary>
        private List<string> ValidateLabelElements()
        {
            var warnings = new List<string>();

            foreach (var element in Elements)
            {
                // Get a short description for the warning message
                string elementDesc = element.Type == ElementType.Barcode
                    ? "Barcode"
                    : (element.Content?.Length > 20
                        ? element.Content.Substring(0, 20) + "..."
                        : element.Content ?? "Text element");

                // WYSIWYG mode: Only warn if element is COMPLETELY outside the template
                // (not just touching margins, since preview shows exact print area)
                if (element.X + element.Width > Template.Width + 10)
                    warnings.Add($"'{elementDesc}' extends beyond right edge (may be cropped)");

                if (element.Y + element.Height > Template.Height + 10)
                    warnings.Add($"'{elementDesc}' extends beyond bottom edge (may be cropped)");

                // Check font size minimum - only warn if extremely small (<4px after scale)
                if (element.Type == ElementType.Text && element.FontSize < 4)
                    warnings.Add($"'{elementDesc}' has font size {element.FontSize:F0}px (may be unreadable)");
            }

            return warnings;
        }

        [RelayCommand]
        private async Task TestConnectionAsync()
        {
            if (string.IsNullOrEmpty(SelectedPrinter))
            {
                StatusMessage = "Please select a printer first.";
                return;
            }

            try
            {
                StatusMessage = "Sending test print...";
                // Absolute minimal ZPL - no special modes, just text.
                string testZpl = "^XA^FO50,50^A0N,50,50^FDTEST OK^FS^XZ";
                
                var (success, jobId) = RawPrinterHelper.SendStringToPrinterWithJobTracking(SelectedPrinter, testZpl);
                
                if (success)
                {
                    StatusMessage = "Test print sent (Job " + jobId + ")";
                    System.Windows.MessageBox.Show($"Test print document sent to {SelectedPrinter}.\n\nJob ID: {jobId}\n\nZPL Sent:\n{testZpl}", 
                        "Test Sent", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = "Failed to send test print.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Test error: " + ex.Message;
            }
        }

        [RelayCommand]
        private async Task SwitchToZplModeAsync()
        {
            if (string.IsNullOrEmpty(SelectedPrinter)) return;

            try
            {
                StatusMessage = "Switching printer to ZPL mode...";
                // CPCL command to switch device language to ZPL
                string switchCmd = "! U1 setvar \"device.languages\" \"zpl\"\r\n";
                
                var (success, _) = RawPrinterHelper.SendStringToPrinterWithJobTracking(SelectedPrinter, switchCmd);
                
                if (success)
                {
                    StatusMessage = "Switch command sent. Try 'Test' again.";
                    System.Windows.MessageBox.Show("Sent ZPL-Mode switch command. Please WAIT 5 seconds and then try the 'Test' button again.", 
                        "Command Sent", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Switch error: " + ex.Message;
            }
        }

        [RelayCommand]
        private async Task DirectTestConnectionAsync()
        {
            if (string.IsNullOrEmpty(SelectedPrinter))
            {
                StatusMessage = "Please select a printer first.";
                return;
            }

            try
            {
                StatusMessage = "Sending DIRECT port test...";
                string testZpl = "^XA^FO50,50^A0N,50,50^FDDIRECT PORT TEST^FS^PQ1^XZ";
                
                // Try to send directly to USB002 (CP30's port)
                bool success = DirectPrintHelper.SendToPort("USB002", testZpl);
                
                if (success)
                {
                    StatusMessage = "Direct test sent to USB002";
                    System.Windows.MessageBox.Show($"Direct port test sent to USB002.\n\nZPL:\n{testZpl}\n\nCheck if printer outputs paper.\n\nIf this works but normal Test doesn't, the issue is with the Windows driver.", 
                        "Direct Test Sent", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = "Failed to send direct test.";
                    System.Windows.MessageBox.Show("Failed to send to USB002. The port may be locked by the driver or the printer is off.", 
                        "Direct Test Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Direct test error: " + ex.Message;
                System.Windows.MessageBox.Show($"Direct port test failed:\n\n{ex.Message}\n\nThis usually means the port is locked by the Windows driver.", 
                    "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task PrintAsync()
        {
            if (string.IsNullOrEmpty(SelectedPrinter))
            {
                StatusMessage = "Please select a printer.";
                System.Windows.MessageBox.Show("Please select a printer first.", "No Printer",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!Elements.Any())
            {
                StatusMessage = "Please add elements to the canvas before printing.";
                System.Windows.MessageBox.Show("Please add elements to the label canvas first.", "No Elements",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Validate layout before printing
            var warnings = ValidateLabelElements();
            if (warnings.Any())
            {
                var warningMessage = "Layout validation detected potential issues:\n\n" +
                                   string.Join("\n", warnings.Take(5));

                if (warnings.Count > 5)
                {
                    warningMessage += $"\n\n...and {warnings.Count - 5} more issue(s)";
                }

                warningMessage += "\n\nContinue printing anyway?";

                var result = System.Windows.MessageBox.Show(
                    warningMessage,
                    "Layout Validation Warning",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result != System.Windows.MessageBoxResult.Yes)
                {
                    StatusMessage = "Print cancelled due to layout warnings.";
                    return;
                }
            }

            try
            {
                // Check if any selected items are already printed
                var printedItems = SelectedProducts.Count > 0 
                    ? SelectedProducts.Where(p => p.IsPrinted).ToList() 
                    : (SelectedProduct != null && SelectedProduct.IsPrinted ? new List<ResourceItem> { SelectedProduct } : new List<ResourceItem>());

                if (printedItems.Any())
                {
                    string message = printedItems.Count == 1
                        ? $"'{printedItems[0].ProductName}' нь өмнө нь хэвлэгдсэн байна.\n\nДахин хэвлэх үү?"
                        : $"Сонгосон бараануудын дотор өмнө нь хэвлэгдсэн {printedItems.Count} бараа байна.\n\nБүгдийг нь дахин хэвлэх үү?";

                    var result = System.Windows.MessageBox.Show(
                        message,
                        "Хэвлэгдсэн бараа",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);

                    if (result != System.Windows.MessageBoxResult.Yes)
                    {
                        StatusMessage = "Print cancelled (already printed items).";
                        return;
                    }
                }

                IsPrinting = true;

                if (SelectedProducts.Count <= 1)
                {
                    await PrintSingleWithRfidAsync();
                }
                else
                {
                    await PrintBatchWithRfidAsync();
                }
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Print error: {ex.Message}";
                System.Windows.MessageBox.Show($"Print operation failed:\n\n{ex.Message}", "Print Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsPrinting = false;
            }
        }

        private async Task PrintSingleWithRfidAsync()
        {
            StatusMessage = "Printing...";

            var rfidConfig = new RfidConfiguration
            {
                EnableRfidEncoding = EnableRfidEncoding,
                DataFormat = SelectedRfidFormat,
                EnableVerification = true,
                RetryCount = 3
            };

            // Create print options with detailed tracking enabled for multi-label prints
            var printOptions = new PrintOptions
            {
                EnableDetailedTracking = Quantity > 1, // Only enable for multi-label prints
                StopOnFirstFailure = false, // Continue printing all labels
                DelayBetweenLabelsMs = EnableRfidEncoding ? 300 : 100, // Longer delay for RFID
                MaxParallelLabels = 1 // Sequential for safety
            };

            var printerConfig = new PrinterConfiguration
            {
                Dpi = PrinterDpi,
                EnableUtf8 = true,
                Darkness = 15,
                PrintSpeed = 3,
                MediaType = SelectedMediaType
            };

            var result = await _printService.PrintLabelWithRfidAsync(
                Elements,
                SelectedProduct,
                Template,
                SelectedPrinter,
                rfidConfig,
                Quantity,
                printOptions,
                printerConfig
            );

            if (result.Success)
            {
                if (result.HasDetailedTracking)
                {
                    // Show detailed per-label results
                    int succeeded = result.LabelsSucceeded;
                    int failed = result.LabelsFailed;

                    // Update Local State - зөвхөн бүх label амжилттай хэвлэгдсэн үед
                    if (SelectedProduct != null)
                    {
                        if (failed == 0)
                        {
                            SelectedProduct.IsPrinted = true;
                            SelectedProduct.LastPrintedTime = DateTime.Now;
                            SelectedProduct.PrintErrorMessage = null;

                            // Persist to backend and sync with SAP
                            try { await _apiService.PrintAndPushRfidAsync(SelectedProduct.Id); } catch { }
                        }
                        else
                        {
                            // Partial failure - хэвлэгдсэн гэж тэмдэглэхгүй
                            SelectedProduct.IsPrinted = false;
                            SelectedProduct.PrintErrorMessage = $"Partial: {succeeded}/{Quantity} labels printed";
                        }
                    }

                    if (succeeded == Quantity)
                    {
                        StatusMessage = $"Successfully printed all {Quantity} labels!";
                    }
                    else
                    {
                        StatusMessage = $"Partial success: {succeeded}/{Quantity} labels printed";
                    }

                    LastPrintStatus = $"✓ Print completed at {result.Timestamp:HH:mm:ss} ({succeeded}/{Quantity})";

                    // Build detailed message
                    var detailsBuilder = new StringBuilder();
                    detailsBuilder.AppendLine($"Print Summary:");
                    detailsBuilder.AppendLine($"Total Labels: {Quantity}");
                    detailsBuilder.AppendLine($"Succeeded: {succeeded}");
                    if (failed > 0)
                    {
                        detailsBuilder.AppendLine($"Failed: {failed}");
                        detailsBuilder.AppendLine();
                        detailsBuilder.AppendLine("Failed Labels:");
                        foreach (var label in result.LabelResults.Where(l => !l.Success))
                        {
                            detailsBuilder.AppendLine($"  Label {label.LabelNumber}: {label.ErrorMessage}");
                        }
                    }
                    detailsBuilder.AppendLine();
                    detailsBuilder.AppendLine($"RFID Encoding: {(result.RfidEncoded == true ? "Yes" : "No")}");

                    if (result.RfidEncoded == true && result.LabelResults != null)
                    {
                        detailsBuilder.AppendLine();
                        detailsBuilder.AppendLine("RFID Data by Label:");
                        foreach (var label in result.LabelResults.Where(l => l.Success))
                        {
                            detailsBuilder.AppendLine($"  Label {label.LabelNumber}: {label.RfidData}");
                        }
                    }

                    System.Windows.MessageBox.Show(
                        detailsBuilder.ToString(),
                        failed > 0 ? "Print Partially Completed" : "Print Success",
                        System.Windows.MessageBoxButton.OK,
                        failed > 0 ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information
                    );
                }
                else
                {
                    // Legacy single-label or batch mode
                    // Update Local State
                    if (SelectedProduct != null)
                    {
                        SelectedProduct.IsPrinted = true;
                        SelectedProduct.LastPrintedTime = DateTime.Now;
                        SelectedProduct.PrintErrorMessage = null;

                        try { await _apiService.PrintAndPushRfidAsync(SelectedProduct.Id); } catch { }
                    }

                    StatusMessage = $"Successfully printed {Quantity} label(s)!";
                    LastPrintStatus = $"✓ Print completed at {result.Timestamp:HH:mm:ss}";

                    if (result.RfidEncoded == true)
                    {
                        StatusMessage += " (RFID encoded)";
                    }

                    System.Windows.MessageBox.Show(
                        $"Print completed successfully!\n\nQuantity: {Quantity}\nRFID Encoding: {(result.RfidEncoded == true ? "Yes" : "No")}\nJob ID: {result.JobId}",
                        "Print Success",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information
                    );
                }
            }
            else
            {
                // Update Local State with error
                if (SelectedProduct != null)
                {
                    SelectedProduct.IsPrinted = false;
                    SelectedProduct.PrintErrorMessage = result.ErrorMessage;

                    try
                    {
                        await _apiService.UpdatePrintStatusAsync(
                            SelectedProduct,
                            isPrinted: false,
                            lastPrintedTime: DateTime.Now,
                            errorMessage: result.ErrorMessage);
                    }
                    catch { }
                }

                StatusMessage = $"Print failed: {result.ErrorMessage}";
                LastPrintStatus = $"✗ Print failed at {result.Timestamp:HH:mm:ss}";

                System.Windows.MessageBox.Show(
                    $"Print operation failed:\n\n{result.ErrorMessage}\n\nError Type: {result.ErrorType}",
                    "Print Failed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error
                );
            }
        }

        private async Task PrintBatchWithRfidAsync()
        {
            int totalItems = SelectedProducts.Count;
            int totalLabels = totalItems * Quantity;

            // Confirm large batch
            if (totalLabels > 50)
            {
                var result = System.Windows.MessageBox.Show(
                    $"You are about to print {totalLabels} labels ({totalItems} items × {Quantity} qty).\n\n" +
                    $"RFID Encoding: {(EnableRfidEncoding ? "ENABLED" : "DISABLED")}\n" +
                    $"Detailed Tracking: {(Quantity > 1 ? "ENABLED (per-label)" : "STANDARD")}\n\n" +  // NEW
                    "The batch will stop on the first error. Continue?",
                    "Large Batch Warning",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result != System.Windows.MessageBoxResult.Yes)
                {
                    StatusMessage = "Print cancelled.";
                    return;
                }
            }

            StatusMessage = $"Printing batch: {totalItems} items...";

            var rfidConfig = new RfidConfiguration
            {
                EnableRfidEncoding = EnableRfidEncoding,
                DataFormat = SelectedRfidFormat,
                EnableVerification = true,
                RetryCount = 3
            };

            // Create print options
            var printOptions = new PrintOptions
            {
                EnableDetailedTracking = Quantity > 1,
                StopOnFirstFailure = false,
                DelayBetweenLabelsMs = EnableRfidEncoding ? 300 : 100,
                MaxParallelLabels = 1
            };

            var printerConfig = new PrinterConfiguration
            {
                Dpi = PrinterDpi,
                EnableUtf8 = true,
                Darkness = 15,
                PrintSpeed = 3,
                MediaType = SelectedMediaType
            };

            var batchResult = await _printService.PrintBatchWithRfidAsync(
                Elements,
                SelectedProducts,
                Template,
                SelectedPrinter,
                rfidConfig,
                Quantity,
                printOptions,
                printerConfig
            );

            // Calculate totals using per-label tracking
            int totalLabelsSucceeded = batchResult.ItemResults.Sum(ir => ir.QuantitySucceeded);
            int totalLabelsFailed = batchResult.ItemResults.Sum(ir => ir.QuantityRequested - ir.QuantitySucceeded);

            if (batchResult.AllSucceeded)
            {
                StatusMessage = $"Batch complete: {totalLabelsSucceeded} labels printed successfully!";
                LastPrintStatus = $"✓ Batch completed at {batchResult.EndTime:HH:mm:ss}";

                // Update status for all succeeded items
                foreach (var item in batchResult.ItemResults.Where(r => r.Result.Success))
                {
                    var originalItem = SelectedProducts.FirstOrDefault(p => p.Rfid == item.Item.Rfid);
                    if (originalItem != null)
                    {
                        originalItem.IsPrinted = true;
                        originalItem.LastPrintedTime = DateTime.Now;
                        originalItem.PrintErrorMessage = null;

                        try { await _apiService.PrintAndPushRfidAsync(originalItem.Id); } catch { }
                    }
                }

                System.Windows.MessageBox.Show(
                    $"Batch print completed successfully!\n\n" +
                    $"Total Items: {batchResult.SuccessCount}\n" +
                    $"Total Labels: {totalLabelsSucceeded}\n" +
                    $"RFID Encoding: {(EnableRfidEncoding ? "Yes" : "No")}\n" +
                    $"Duration: {(batchResult.EndTime - batchResult.StartTime).TotalSeconds:F1} seconds",
                    "Batch Success",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information
                );
            }
            else
            {
                // Update succeeded items
                foreach (var item in batchResult.ItemResults.Where(r => r.Result.Success))
                {
                    var originalItem = SelectedProducts.FirstOrDefault(p => p.Rfid == item.Item.Rfid);
                    if (originalItem != null)
                    {
                        originalItem.IsPrinted = true;
                        originalItem.LastPrintedTime = DateTime.Now;
                        originalItem.PrintErrorMessage = null;

                        try { await _apiService.PrintAndPushRfidAsync(originalItem.Id); } catch { }
                    }
                }

                // Update failed items
                foreach (var item in batchResult.ItemResults.Where(r => !r.Result.Success))
                {
                    var originalItem = SelectedProducts.FirstOrDefault(p => p.Rfid == item.Item.Rfid);
                    if (originalItem != null)
                    {
                        originalItem.IsPrinted = false;
                        originalItem.PrintErrorMessage = item.Result.ErrorMessage;

                        try
                        {
                            await _apiService.UpdatePrintStatusAsync(
                                originalItem, false, DateTime.Now, item.Result.ErrorMessage);
                        }
                        catch { }
                    }
                }

                StatusMessage = $"Batch stopped: {batchResult.SuccessCount} succeeded, {batchResult.FailureCount} failed";
                LastPrintStatus = $"✗ Batch stopped at {batchResult.EndTime:HH:mm:ss}";

                // Find first failure for details
                var firstFailure = batchResult.ItemResults.FirstOrDefault(r => !r.Result.Success);
                var failureDetails = "";

                if (firstFailure != null)
                {
                    failureDetails = $"\n\nFirst Failure:\nItem: {firstFailure.Item.ProductName}\nError: {firstFailure.Result.ErrorMessage}";

                    // Show per-label details if available
                    if (firstFailure.LabelResults != null && firstFailure.LabelResults.Any(l => !l.Success))
                    {
                        var failedLabel = firstFailure.LabelResults.First(l => !l.Success);
                        failureDetails += $"\nFailed at Label {failedLabel.LabelNumber}/{firstFailure.QuantityRequested}";
                    }
                }

                System.Windows.MessageBox.Show(
                    $"Batch print stopped due to error!\n\n" +
                    $"Succeeded: {batchResult.SuccessCount} items ({totalLabelsSucceeded} labels)\n" +
                    $"Failed: {batchResult.FailureCount} items ({totalLabelsFailed} labels)\n" +
                    $"Not Attempted: {batchResult.TotalItems - batchResult.SuccessCount - batchResult.FailureCount} items" +
                    failureDetails,
                    "Batch Stopped",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning
                );
            }
        }

        // Available field names for binding
        public List<string> AvailableFields { get; } = new List<string>
        {
            "None",
            "RFID",
            "ItemCode",
            "ProductName",
            "Price",
            "Branch",
            "Status",
            "Unit",
            "Date"
        };
    }
}
