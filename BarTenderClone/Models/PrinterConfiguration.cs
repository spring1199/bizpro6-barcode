using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace BarTenderClone.Models
{
    /// <summary>
    /// Configuration for the currently selected printer and paper size
    /// </summary>
    public partial class PrinterConfiguration : ObservableObject
    {
        [ObservableProperty]
        private int _dpi = 203; // Default to 203 DPI (most common for desktop label printers)

        [ObservableProperty]
        private bool _enableUtf8 = true; // Default to true to fix ???? issues

        [ObservableProperty]
        private int _darkness = 15; // 0-30

        [ObservableProperty]
        private int _printSpeed = 4; // 2-12 ips (4 is optimal for quality on ZT610)

        [ObservableProperty]
        private string _defaultFont = "0"; // Default "0" (Scalable), can be "A", "D", "Q" (Swiss 721), etc.

        [ObservableProperty]
        private PrinterProfile _selectedProfile = PrinterProfiles.ZebraZT610_203;

        [ObservableProperty]
        private PaperSize _selectedPaperSize = PaperSizes.Label54x34;

        [ObservableProperty]
        private MediaType _mediaType = MediaType.DirectThermal; // Default to Direct Thermal

        /// <summary>
        /// Generates ZPL initialization commands based on current configuration
        /// </summary>
        public string GetZplInitCommands()
        {
            var commands = new List<string>();
            
            if (EnableUtf8)
                commands.Add("^CI28"); // UTF-8 encoding for Cyrillic
            
            commands.Add("^MMT");  // Tear-off mode
            commands.Add("^MNW");  // Non-continuous media (gap/notch sensing)
            commands.Add($"^PR{PrintSpeed},{PrintSpeed},{PrintSpeed}"); // Print speed (print, slew, backfeed)
            commands.Add($"^MD{Darkness - 15}"); // Darkness adjustment (-15 to +15)
            
            return string.Join("", commands);
        }
    }

    /// <summary>
    /// Printer profile with hardware-specific settings
    /// </summary>
    public class PrinterProfile
    {
        public string Name { get; init; } = "";
        public PrinterType Type { get; init; }
        public int Dpi { get; init; }
        public int MaxPrintWidthMm { get; init; }
        public string CommandLanguage { get; init; } = "ZPL";
        public bool SupportsRfid { get; init; }
        public int RecommendedSpeed { get; init; } = 4;
        public string Description { get; init; } = "";
    }

    public enum PrinterType
    {
        ZebraZT610,
        CP30,
        Generic
    }

    /// <summary>
    /// Media type for the printer (Direct Thermal vs Thermal Transfer)
    /// </summary>
    public enum MediaType
    {
        /// <summary>
        /// Direct Thermal - No ribbon required, uses heat-sensitive paper
        /// </summary>
        DirectThermal,

        /// <summary>
        /// Thermal Transfer - Uses ribbon to transfer ink onto labels
        /// </summary>
        ThermalTransfer
    }

    /// <summary>
    /// Pre-configured printer profiles for common printers
    /// </summary>
    public static class PrinterProfiles
    {
        public static readonly PrinterProfile ZebraZT610_203 = new()
        {
            Name = "Zebra ZT610 (203 DPI)",
            Type = PrinterType.ZebraZT610,
            Dpi = 203,
            MaxPrintWidthMm = 104,
            CommandLanguage = "ZPL",
            SupportsRfid = true,
            RecommendedSpeed = 4,
            Description = "Industrial printer, 203 DPI, RFID capable"
        };

        public static readonly PrinterProfile ZebraZT610_300 = new()
        {
            Name = "Zebra ZT610 (300 DPI)",
            Type = PrinterType.ZebraZT610,
            Dpi = 300,
            MaxPrintWidthMm = 104,
            CommandLanguage = "ZPL",
            SupportsRfid = true,
            RecommendedSpeed = 4,
            Description = "Industrial printer, 300 DPI, higher quality"
        };

        public static readonly PrinterProfile ZebraZT610_600 = new()
        {
            Name = "Zebra ZT610 (600 DPI)",
            Type = PrinterType.ZebraZT610,
            Dpi = 600,
            MaxPrintWidthMm = 104,
            CommandLanguage = "ZPL",
            SupportsRfid = true,
            RecommendedSpeed = 3,
            Description = "Industrial printer, 600 DPI, micro labels"
        };

        public static readonly PrinterProfile CP30_203 = new()
        {
            Name = "CP30 (203 DPI)",
            Type = PrinterType.CP30,
            Dpi = 203,
            MaxPrintWidthMm = 108,
            CommandLanguage = "ZPL",
            SupportsRfid = true,  // CHANGED: CP30 supports RFID
            RecommendedSpeed = 6,
            Description = "Desktop printer, 203 DPI, high speed, RFID capable"
        };

        public static readonly PrinterProfile CP30_300 = new()
        {
            Name = "CP30 (300 DPI)",
            Type = PrinterType.CP30,
            Dpi = 300,
            MaxPrintWidthMm = 106,
            CommandLanguage = "ZPL",
            SupportsRfid = true,  // CHANGED: CP30 supports RFID
            RecommendedSpeed = 5,
            Description = "Desktop printer, 300 DPI, RFID capable"
        };

        public static readonly PrinterProfile GenericZpl = new()
        {
            Name = "Generic ZPL Printer",
            Type = PrinterType.Generic,
            Dpi = 203,
            MaxPrintWidthMm = 104,
            CommandLanguage = "ZPL",
            SupportsRfid = false,
            RecommendedSpeed = 4,
            Description = "Generic ZPL-compatible printer"
        };

        /// <summary>
        /// All available printer profiles
        /// </summary>
        public static readonly PrinterProfile[] All = new[]
        {
            ZebraZT610_203,
            ZebraZT610_300,
            ZebraZT610_600,
            CP30_203,
            CP30_300,
            GenericZpl
        };
    }

    /// <summary>
    /// Paper/label size definition
    /// </summary>
    public class PaperSize
    {
        public string Name { get; init; } = "";
        public double WidthMm { get; init; }
        public double HeightMm { get; init; }
        public string Category { get; init; } = "Standard";

        public double WidthInches => WidthMm / 25.4;
        public double HeightInches => HeightMm / 25.4;
        
        /// <summary>
        /// Width in screen pixels (96 DPI)
        /// </summary>
        public double WidthPixels => WidthInches * 96;
        
        /// <summary>
        /// Height in screen pixels (96 DPI)
        /// </summary>
        public double HeightPixels => HeightInches * 96;

        public override string ToString() => Name;
    }

    /// <summary>
    /// Pre-configured paper size presets
    /// </summary>
    public static class PaperSizes
    {
        // Current / Primary
        public static readonly PaperSize Label54x34 = new()
        {
            Name = "54×34mm (Current)",
            WidthMm = 54,
            HeightMm = 34,
            Category = "Retail"
        };

        // Common Retail Sizes
        public static readonly PaperSize Label40x30 = new()
        {
            Name = "40×30mm (Small Price)",
            WidthMm = 40,
            HeightMm = 30,
            Category = "Retail"
        };

        public static readonly PaperSize Label50x25 = new()
        {
            Name = "50×25mm (Barcode)",
            WidthMm = 50,
            HeightMm = 25,
            Category = "Retail"
        };

        public static readonly PaperSize Label50x50 = new()
        {
            Name = "50×50mm (Square)",
            WidthMm = 50,
            HeightMm = 50,
            Category = "Retail"
        };

        public static readonly PaperSize Label60x40 = new()
        {
            Name = "60×40mm (Standard)",
            WidthMm = 60,
            HeightMm = 40,
            Category = "Retail"
        };

        public static readonly PaperSize Label70x50 = new()
        {
            Name = "70×50mm (Large)",
            WidthMm = 70,
            HeightMm = 50,
            Category = "Retail"
        };

        // Shelf Labels
        public static readonly PaperSize Label80x30 = new()
        {
            Name = "80×30mm (Shelf)",
            WidthMm = 80,
            HeightMm = 30,
            Category = "Shelf"
        };

        public static readonly PaperSize Label100x50 = new()
        {
            Name = "100×50mm (Wide Shelf)",
            WidthMm = 100,
            HeightMm = 50,
            Category = "Shelf"
        };

        // Shipping
        public static readonly PaperSize Label100x150 = new()
        {
            Name = "100×150mm (Shipping)",
            WidthMm = 100,
            HeightMm = 150,
            Category = "Shipping"
        };

        /// <summary>
        /// All available paper sizes
        /// </summary>
        public static readonly PaperSize[] All = new[]
        {
            Label54x34,
            Label40x30,
            Label50x25,
            Label50x50,
            Label60x40,
            Label70x50,
            Label80x30,
            Label100x50,
            Label100x150
        };
    }
}

