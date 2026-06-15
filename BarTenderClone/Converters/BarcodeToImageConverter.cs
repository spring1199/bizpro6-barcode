using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BarcodeStandard;
using BarTenderClone.Helpers;
using SkiaSharp;
using BarcodeType = BarcodeStandard.Type;

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
                int width = (int)Math.Max(Math.Round(elementWidth), 50);
                int height = (int)Math.Max(
                    Math.Round(elementHeight),
                    Math.Round(LabelSizeHelper.MmToScreenPixels(5)));

                double barcodeWidth = elementWidth;

                // Check cache first — avoid expensive re-render during drag/move
                var cacheKey = MakeCacheKey(content, width, height, barcodeWidth, isCentered);
                if (_cache.TryGetValue(cacheKey, out var cached))
                    return cached;

                var barcodeSource = CreateBarcodeImage(content, barcodeWidth, height);

                // Create visual
                DrawingVisual drawingVisual = new DrawingVisual();
                using (DrawingContext dc = drawingVisual.RenderOpen())
                {
                    dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));

                    // Stretch the barcode to fill the entire element width and height (no gaps)
                    dc.DrawImage(barcodeSource, new Rect(0, 0, width, height));
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

        private static BitmapSource CreateBarcodeImage(string content, double width, int height)
        {
            var barcode = new Barcode
            {
                IncludeLabel = false,
                Alignment = AlignmentPositions.Left
            };

            var targetWidth = (int)Math.Max(Math.Round(width), 1);
            var targetHeight = Math.Max(height, 1);
            SKImage? image = null;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    image = barcode.Encode(
                        BarcodeType.Code128,
                        content,
                        SKColors.Black,
                        SKColors.White,
                        targetWidth,
                        targetHeight);
                    break;
                }
                catch when (attempt < 4)
                {
                    targetWidth *= 2;
                }
            }

            if (image == null)
                throw new InvalidOperationException("Failed to render Code 128 barcode.");

            using (image)
            {
                using var bitmap = SKBitmap.FromImage(image);

                // Find horizontal bounds of the black barcode bars to crop quiet zones and gaps
                int left = bitmap.Width;
                int right = -1;

                for (int y = 0; y < bitmap.Height; y++)
                {
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        var color = bitmap.GetPixel(x, y);
                        // Check if pixel is dark (part of the barcode bars)
                        if (color.Red < 128 && color.Green < 128 && color.Blue < 128)
                        {
                            if (x < left) left = x;
                            if (x > right) right = x;
                        }
                    }
                }

                SKBitmap finalBitmap = bitmap;
                bool isCropped = false;

                if (right >= left && (left > 0 || right < bitmap.Width - 1))
                {
                    int croppedWidth = right - left + 1;
                    var croppedBitmap = new SKBitmap(croppedWidth, bitmap.Height);
                    using (var canvas = new SKCanvas(croppedBitmap))
                    {
                        canvas.Clear(SKColors.White);
                        var srcRect = new SKRect(left, 0, right + 1, bitmap.Height);
                        var destRect = new SKRect(0, 0, croppedWidth, bitmap.Height);
                        canvas.DrawBitmap(bitmap, srcRect, destRect);
                    }
                    finalBitmap = croppedBitmap;
                    isCropped = true;
                }

                try
                {
                    using SKData data = finalBitmap.Encode(SKEncodedImageFormat.Png, 100);
                    using var stream = data.AsStream();

                    var wpfBitmap = new BitmapImage();
                    wpfBitmap.BeginInit();
                    wpfBitmap.CacheOption = BitmapCacheOption.OnLoad;
                    wpfBitmap.StreamSource = stream;
                    wpfBitmap.EndInit();
                    wpfBitmap.Freeze();

                    return wpfBitmap;
                }
                finally
                {
                    if (isCropped)
                    {
                        finalBitmap.Dispose();
                    }
                }
            }
        }
    }
}
