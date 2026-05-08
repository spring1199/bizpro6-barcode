using System;
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
    /// </summary>
    public class BarcodeToImageConverter : IMultiValueConverter
    {
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

                var layout = LabelSizeHelper.CalculateCode128Layout(content, elementWidth, printerDpi);
                double barcodeWidth = Math.Max(
                    layout.ActualWidthPixels,
                    LabelSizeHelper.CalculateCode128Width(content, printerDpi));
                var barcodeSource = CreateBarcodeImage(content, barcodeWidth, height);

                // Create visual
                DrawingVisual drawingVisual = new DrawingVisual();
                using (DrawingContext dc = drawingVisual.RenderOpen())
                {
                    dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));

                    double totalBarsWidth = barcodeSource.Width;
                    double startX = isCentered
                        ? Math.Max(0, (width - totalBarsWidth) / 2)
                        : 0;
                    dc.DrawImage(barcodeSource, new Rect(startX, 0, barcodeSource.Width, barcodeSource.Height));
                }

                RenderTargetBitmap bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(drawingVisual);
                bmp.Freeze();
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
                using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = data.AsStream();

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();

                return bitmap;
            }
        }
    }
}
