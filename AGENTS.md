# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Project Overview

BarTender Clone is a WPF (.NET 9.0) desktop application for generating and printing ZPL-based barcode labels to Zebra printers. The application features:

- User authentication against a remote API
- Label template design with drag-and-drop elements
- ZPL code generation for Zebra thermal printers
- RFID encoding support
- Batch printing with per-label tracking
- Real-time print job monitoring via Windows Print Spooler API

## Build and Run Commands

```bash
# Build the solution
dotnet build testbartender.sln

# Run the application
dotnet run --project BarTenderClone/BarTenderClone.csproj

# Clean build artifacts
dotnet clean testbartender.sln
```

## Architecture Overview

### Dependency Injection and Service Registration

The application uses Microsoft.Extensions.DependencyInjection with a host-based architecture configured in `App.xaml.cs`:

- **Singleton services**: Session management, API clients, logging, ZPL generation, printing
- **Transient ViewModels**: Fresh instances created per navigation
- **HttpClient integration**: Uses `AddHttpClient<>` for authentication and API services

Service lifecycle is managed through `IHost` with proper startup/shutdown hooks.

### Core Service Layer

**ZplGeneratorService** (`Services/ZplGeneratorService.cs`):
- Converts canvas-based `LabelElement` objects to ZPL commands
- Handles coordinate conversion from screen pixels (96 DPI) to printer dots (203/300/600 DPI)
- Supports field binding from `ResourceItem` data sources (e.g., `{RFID}`, `{PRODUCTNAME}`, `{PRICE}`)
- RFID encoding with configurable data formats (ASCII, Hex, EPC)
- Uses `LabelSizeHelper` for all coordinate/font size calculations

**PrintService** (`Services/PrintService.cs`):
- Two print modes:
  - **Legacy batch mode**: Uses ZPL `^PQ` command for multiple copies
  - **Detailed tracking mode**: Prints labels one-by-one with per-label status tracking
- RFID sequential encoding: Auto-increments RFID data for multiple labels to prevent encoding conflicts
- Print job monitoring via `RawPrinterHelper.WaitForJobCompletionAsync()`
- Integration with `ApiService` for print status updates to backend

**RawPrinterHelper** (`Helpers/RawPrinterHelper.cs`):
- P/Invoke wrapper for Windows Spooler API (`winspool.drv`)
- Raw ZPL data transmission to printer using `WritePrinter()`
- Job status tracking using `GetJob()` and `JOB_INFO_1` structures
- Returns job IDs for asynchronous completion monitoring
- UTF-8 encoding support for Cyrillic/Unicode characters

**ApiService** (`Services/ApiService.cs`):
- Fetches `ResourceItem` data from backend API (https://app.chipmo.mn)
- Handles ABP framework response wrapper deserialization
- Updates print status back to server after successful printing
- Bearer token authentication via `IAuthenticationService`

### Coordinate System and Scaling

**Critical concept**: The application maintains two coordinate systems that must be properly converted:

1. **Screen coordinates** (96 DPI): Used for WPF canvas preview
2. **Printer coordinates** (203/300/600 DPI): Used in generated ZPL

All conversions are centralized in `LabelSizeHelper`:

- `ScreenPixelsToDots()`: Converts element positions (X, Y) and dimensions (Width, Height)
- `FontSizeToZplHeight()`: Converts font sizes with 1.0x scaling factor (configurable via `FONT_SCALING_FACTOR`)
- `CalculateBarcodeModuleWidth()`: Targets 10 mil (0.01 inches) for reliable scanning
- `GetSafeMarginsDots()`: 2mm margins on all sides to prevent edge cropping

**Font Scaling**: Currently set to 1.0x (`FONT_SCALING_FACTOR = 1.0`) for WYSIWYG behavior. Adjust this constant in `LabelSizeHelper` if printed text appears too small/large.

### MVVM Pattern

- **ViewModels**: Use CommunityToolkit.Mvvm for `ObservableObject` and `ObservableProperty` attributes
- **Navigation**: `MainViewModel` acts as a shell, swapping `CurrentView` between `LoginViewModel` and `LabelPreviewViewModel`
- **Data binding**: Two-way binding for template design elements, one-way binding for print status display
- **Commands**: RelayCommand pattern for button actions (print, add element, save template)

### Models and Data Flow

**LabelElement** (`Models/DesignerModels.cs`):
- Represents a single element on the label canvas (Text, Barcode, Image)
- Properties: X, Y, Width, Height, Content, FieldName, FontSize, IsCentered
- `FieldName` enables data binding (e.g., "PRODUCTNAME" resolves from ResourceItem.ProductName)

**ResourceItem** (`Models/ResourceModels.cs`):
- Data source for label printing
- Contains nested `ParsedDocument` with `Product` and `ProductRfid` sub-objects
- Fields: Code, ProductName, Price, Rfid, Branch, Status, Unit, CreationTime

**PrintResult/BatchPrintResult** (`Models/PrintResults.cs`):
- Detailed tracking of print job outcomes
- Includes per-label results with `LabelResult[]` when detailed tracking is enabled
- Error categorization: `PrintErrorType` enum (PrinterNotFound, SpoolerError, InvalidData, etc.)

## Important Implementation Details

### Print Service Usage

When calling `PrintLabelWithRfidAsync()`, use the `PrintOptions` parameter to control behavior:

```csharp
var options = new PrintOptions
{
    EnableDetailedTracking = true,  // Per-label status tracking
    StopOnFirstFailure = true,      // Stop batch on first error
    DelayBetweenLabelsMs = 500      // Delay between labels (RFID encoding)
};

var result = await printService.PrintLabelWithRfidAsync(
    elements, dataSource, template, printerName,
    rfidConfig, quantity: 10, options, printerConfig
);
```

**Without detailed tracking**: Uses legacy `^PQ10` command (all labels in one job)
**With detailed tracking**: Sends 10 separate print jobs with sequential RFID data

### RFID Sequential Encoding

When printing multiple labels with RFID, the `PrintService` automatically generates sequential RFID data:

- Input: Base RFID "1234567890ABCDEF"
- Label 1: "1234567890ABCDEF" (original)
- Label 2: "1234567890ABC0F0" (incremented last 4 hex digits)
- Label 3: "1234567890ABC0F1"

This prevents RFID encoding conflicts when the same tag is encoded multiple times.

### ZPL Field Binding

Elements can reference data fields using the `FieldName` property:

- **RFID**: Maps to `dataSource.Rfid`
- **ITEMCODE**: Maps to `dataSource.Code`
- **PRODUCTNAME**: Maps to `dataSource.ProductName`
- **PRICE**: Maps to `dataSource.Price` (formatted as "1,234 MNT")
- **BRANCH**, **STATUS**, **UNIT**, **DATE**: Additional fields

Resolution happens in `ZplGeneratorService.ResolveFieldValue()`.

### Logging Service

The application uses a custom file-based logger (`FileLoggingService`):

- Logs stored in: `C:\BarTenderClone\Logs\` directory
- Daily log rotation with filename pattern: `log_YYYY-MM-DD.txt`
- Log levels: Debug, Info, Warning, Error
- All print operations, API calls, and errors are logged

Inject `ILoggingService` into any service to add logging.

### Template Persistence

Templates are saved/loaded via `ITemplateService`:

- Storage location: `C:\BarTenderClone\Templates\` directory
- Format: JSON serialization of `LabelTemplate` with `LabelElement[]` array
- File extension: `.btl` (BarTender Label)

## Common Gotchas

1. **Printer must support ZPL**: This application only works with Zebra-compatible printers (ZPL II language)

2. **UTF-8 encoding**: The `PrinterConfiguration.EnableUtf8` flag must be `true` for Cyrillic characters. This adds `^CI28` to ZPL output.

3. **DPI mismatch**: If labels print at wrong scale, verify `PrinterConfiguration.Dpi` matches your physical printer (typically 203 for desktop printers, 300/600 for industrial).

4. **Font size scaling**: If text is too small on physical labels, increase `FONT_SCALING_FACTOR` in `LabelSizeHelper.cs`. Current value is 1.0 for 1:1 preview-to-print matching.

5. **Barcode centering**: Centering is approximate. Code 128 barcode width = `((data.Length * 11) + 35) * moduleWidth` dots. Adjust formula in `GenerateBarcodeElement()` if needed.

6. **Print job tracking limitations**: Windows Spooler API may not reliably track all job states. The code uses "optimistic" completion checks with 5-second timeouts and soft-failure logic.

7. **RFID verification**: Enable `RfidConfiguration.EnableVerification` to add `^HV1` command for read-after-write verification (requires RFID-capable printer).

## API Integration

Backend API base URL: `https://app.chipmo.mn/api/services/app/`

Key endpoints:
- `POST /Resource/Resources`: Fetch paginated resources with filtering
- `POST /Resource/UpdatePrintStatus`: Update print status after successful print

Authentication: Bearer token stored in `ISessionService.AccessToken`

Response format: ABP framework wrapper `{ "result": { "items": [...] } }`

## Extending the Application

### Adding a new label element type

1. Add enum value to `ElementType` in `Models/DesignerModels.cs`
2. Add generation logic in `ZplGeneratorService.GenerateZplInternal()` switch statement
3. Update UI in `LabelPreviewView.xaml` to support drag-and-drop for new type
4. Add corresponding converter if visual preview is needed

### Supporting additional printers

For non-ZPL printers, create a new implementation of `IZplGeneratorService` (rename interface to `ILabelGeneratorService`) that generates the appropriate printer command language (EPL, ESC/POS, etc.).

### Adding new data fields

1. Add property to `ResourceItem` or `ResourceDocument` in `Models/ResourceModels.cs`
2. Update `ZplGeneratorService.ResolveFieldValue()` to handle new field name
3. API must return the new field in the response

## Technology Stack

- **Framework**: .NET 9.0 Windows (WPF)
- **UI Library**: ModernWpfUI (modern Windows 10/11 styling)
- **MVVM Toolkit**: CommunityToolkit.Mvvm
- **DI Container**: Microsoft.Extensions.DependencyInjection
- **Barcode Generation**: BarcodeLib (for preview only; ZPL generates actual barcode)
- **JSON**: Newtonsoft.Json
- **Printer Communication**: P/Invoke to winspool.drv (Windows Spooler API)
