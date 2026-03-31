using System;
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
    /// Uses EXACT same calculation as ZplGeneratorService for WYSIWYG.
    /// </summary>
    public class BarcodeToImageConverter : IMultiValueConverter
    {
        // Must match ZplGeneratorService constants
        private const int MODULES_PER_CHAR = 11;
        private const int OVERHEAD_MODULES = 35;
        private const int MIN_MODULE_WIDTH = 2;
        private const int MAX_MODULE_WIDTH = 10;
        private const int DEFAULT_DPI = 300;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] = Content (string)
            // values[1] = Width (double) - user specified element width
            // values[2] = Height (double)
            if (values == null || values.Length < 3)
                return null;

            string content = values[0] as string;
            if (string.IsNullOrEmpty(content))
                return null;

            // Get element dimensions (in screen pixels)
            double elementWidth = 200;
            double elementHeight = 40;

            if (values[1] is double w && w > 0)
                elementWidth = w;
            if (values[2] is double h && h > 0)
                elementHeight = h;

            try
            {
                // Convert element width to dots (same as ZPL generator)
                int elementWidthDots = LabelSizeHelper.ScreenPixelsToDots(elementWidth, DEFAULT_DPI);
                int height = (int)Math.Max(elementHeight, 20);

                // Calculate EXACTLY like ZplGeneratorService.GenerateBarcodeElement
                int totalModules = (content.Length * MODULES_PER_CHAR) + OVERHEAD_MODULES;
                int moduleWidth = Math.Max(MIN_MODULE_WIDTH, elementWidthDots / totalModules);
                moduleWidth = Math.Clamp(moduleWidth, MIN_MODULE_WIDTH, MAX_MODULE_WIDTH);
                
                // Actual barcode width in dots (what ZPL produces)
                int actualBarcodeWidthDots = totalModules * moduleWidth;
                
                // Convert back to screen pixels for preview
                double actualBarcodeWidthPixels = (double)actualBarcodeWidthDots / DEFAULT_DPI * LabelSizeHelper.SCREEN_DPI;
                int width = (int)Math.Max(actualBarcodeWidthPixels, 50);

                // Generate barcode bars
                var bars = new System.Collections.Generic.List<(double barWidth, double gap)>();
                Random rng = new Random(content.GetHashCode());
                double totalBarsWidth = 0;
                double targetWidth = width * 0.95;

                while (totalBarsWidth < targetWidth)
                {
                    double barWidth = rng.Next(1, 4);
                    double gap = rng.Next(1, 3);
                    if (totalBarsWidth + barWidth > targetWidth) break;
                    bars.Add((barWidth, gap));
                    totalBarsWidth += barWidth + gap;
                }

                if (bars.Count > 0)
                    totalBarsWidth -= bars[bars.Count - 1].gap;
                if (totalBarsWidth <= 0) totalBarsWidth = 10;

                // Create visual
                DrawingVisual drawingVisual = new DrawingVisual();
                using (DrawingContext dc = drawingVisual.RenderOpen())
                {
                    dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));

                    double startX = (width - totalBarsWidth) / 2;
                    double currentX = startX;
                    double barTop = height * 0.1;
                    double barHeight = height * 0.8;

                    foreach (var bar in bars)
                    {
                        dc.DrawRectangle(Brushes.Black, null, new Rect(currentX, barTop, bar.barWidth, barHeight));
                        currentX += bar.barWidth + bar.gap;
                    }
                }

                RenderTargetBitmap bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(drawingVisual);
                bmp.Freeze();
                return bmp;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
