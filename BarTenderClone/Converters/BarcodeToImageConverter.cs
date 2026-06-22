using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BarTenderClone.Helpers;

namespace BarTenderClone.Converters
{
    /// <summary>
    /// Converts barcode content, width, and height to a barcode image.
    /// Uses the same layout math as ZplGeneratorService so the preview occupies
    /// the same box the printer will use.
    /// Includes a content+size-based cache to avoid re-rendering during drag operations.
    /// </summary>
    public class BarcodeToImageConverter : IMultiValueConverter
    {
        // Cache barcode images by content+dimensions to avoid re-rendering during drag/move
        private static readonly Dictionary<string, BitmapSource> _cache = new();
        private const int MaxCacheSize = 32;

        private static string MakeCacheKey(string content, int width, int height, double barcodeWidth, bool isCentered)
            => $"{content}|{width}|{height}|{barcodeWidth:F1}|{isCentered}";

        public object Convert(object[] values, System.Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] = Content (string)
            // values[1] = Width (double) - user specified element width
            // values[2] = Height (double)
            // values[3] = PrinterDpi (int)
            // values[4] = IsCentered (bool)
            if (values == null || values.Length < 3)
                return DependencyProperty.UnsetValue;

            string? content = values[0] as string;
            if (string.IsNullOrEmpty(content))
                return DependencyProperty.UnsetValue;

            // Get element dimensions (in screen pixels)
            double elementWidth = 200;
            double elementHeight = 40;
            int printerDpi = 203;
            bool isCentered = false;

            if (values[1] is double w && w > 0)
                elementWidth = w;
            if (values[2] is double h && h > 0)
                elementHeight = h;
            if (values.Length > 3 && values[3] is int dpi && dpi > 0)
                printerDpi = dpi;
            if (values.Length > 4 && values[4] is bool centered)
                isCentered = centered;

            try
            {
                int width = (int)Math.Max(Math.Round(elementWidth), 1);
                int height = (int)Math.Max(
                    Math.Round(elementHeight),
                    Math.Round(LabelSizeHelper.MmToScreenPixels(5)));

                // Render through the shared print-engine path so the designer preview matches the
                // printed barcode exactly (same width math + quiet zones, positioned within the box).
                var (barcodeSource, drawWidth) = BarTenderClone.Services.LabelRenderEngine.BuildBarcodeImage(
                    content, elementWidth, elementHeight, printerDpi);

                var cacheKey = MakeCacheKey(content, width, height, drawWidth, isCentered);
                if (_cache.TryGetValue(cacheKey, out var cached))
                    return cached;

                double x = isCentered ? Math.Max(0, (width - drawWidth) / 2) : 0;

                DrawingVisual drawingVisual = new DrawingVisual();
                using (DrawingContext dc = drawingVisual.RenderOpen())
                {
                    dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                    dc.DrawImage(barcodeSource, new Rect(x, 0, drawWidth, height));
                }

                RenderTargetBitmap bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(drawingVisual);
                bmp.Freeze();

                // Store in cache (evict oldest entries if over limit)
                if (_cache.Count >= MaxCacheSize)
                    _cache.Clear();
                _cache[cacheKey] = bmp;

                return bmp;
            }
            catch (Exception)
            {
                return DependencyProperty.UnsetValue;
            }
        }

        public object[] ConvertBack(object value, System.Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
