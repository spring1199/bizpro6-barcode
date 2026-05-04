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
            // values[1] = Width (double) - element width in screen pixels
            // values[2] = Height (double)
            // values[3] = PrinterDpi (int)
            // values[4] = IsCentered (bool)
            if (values == null || values.Length < 3)
                return DependencyProperty.UnsetValue;

            var content = values[0] as string;
            if (string.IsNullOrWhiteSpace(content))
                return DependencyProperty.UnsetValue;

            double elementWidth = 200;
            double elementHeight = 40;
            int printerDpi = 203;
            bool isCentered = false;

            if (values[1] is double width && width > 0)
                elementWidth = width;
            if (values[2] is double height && height > 0)
                elementHeight = height;
            if (values.Length > 3 && values[3] is int dpi && dpi > 0)
                printerDpi = dpi;
            if (values.Length > 4 && values[4] is bool centered)
                isCentered = centered;

            try
            {
                int imageWidth = (int)Math.Max(Math.Round(elementWidth), 50);
                int imageHeight = (int)Math.Max(
                    Math.Round(elementHeight),
                    Math.Round(LabelSizeHelper.MmToScreenPixels(5)));

                var layout = LabelSizeHelper.CalculateCode128Layout(content, elementWidth, printerDpi);
                double barcodeWidth = Math.Max(1, Math.Min(layout.ActualWidthPixels, elementWidth));
                var barcodeSource = CreateBarcodeImage(content, barcodeWidth, imageHeight);

                var drawingVisual = new DrawingVisual();
                using (var dc = drawingVisual.RenderOpen())
                {
                    dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, imageWidth, imageHeight));

                    double startX = isCentered
                        ? Math.Max(0, (imageWidth - barcodeSource.Width) / 2)
                        : 0;
                    dc.DrawImage(barcodeSource, new Rect(startX, 0, barcodeSource.Width, barcodeSource.Height));
                }

                var bitmap = new RenderTargetBitmap(imageWidth, imageHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(drawingVisual);
                bitmap.Freeze();
                return bitmap;
            }
            catch
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

            using SKImage image = barcode.Encode(
                BarcodeType.Code128,
                content,
                SKColors.Black,
                SKColors.White,
                (int)Math.Max(Math.Round(width), 1),
                height);

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
