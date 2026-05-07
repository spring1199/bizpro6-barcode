using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BarcodeStandard;
using BarTenderClone.Helpers;
using BarTenderClone.Models;
using SkiaSharp;
using BarcodeType = BarcodeStandard.Type;

namespace BarTenderClone.Services
{
    internal sealed record RasterizedLabel(int WidthDots, int HeightDots, string GraphicField);

    internal static class LabelRasterRenderService
    {
        public static RasterizedLabel RenderToZplGraphic(
            IEnumerable<LabelElement> elements,
            ResourceItem? dataSource,
            LabelTemplate template,
            PrinterConfiguration config)
        {
            var widthDots = Math.Max(1, LabelSizeHelper.ScreenPixelsToDots(template.Width, config.Dpi));
            var heightDots = Math.Max(1, LabelSizeHelper.ScreenPixelsToDots(template.Height, config.Dpi));
            var scale = config.Dpi / LabelSizeHelper.SCREEN_DPI;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.PushTransform(new ScaleTransform(scale, scale));
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, template.Width, template.Height));
                dc.PushClip(new RectangleGeometry(new Rect(0, 0, template.Width, template.Height)));

                foreach (var element in elements.OrderBy(e => e.Y).ThenBy(e => e.X))
                {
                    DrawElement(dc, element, dataSource, config);
                }

                dc.Pop(); // clip
                dc.Pop(); // scale
            }

            var bitmap = new RenderTargetBitmap(
                widthDots,
                heightDots,
                LabelSizeHelper.SCREEN_DPI,
                LabelSizeHelper.SCREEN_DPI,
                PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();

            return new RasterizedLabel(widthDots, heightDots, ToZplGraphicField(bitmap));
        }

        private static void DrawElement(DrawingContext dc, LabelElement element, ResourceItem? dataSource, PrinterConfiguration config)
        {
            var (width, height) = DesignerInteractionHelper.GetLocalSize(element);
            if (width <= 0 || height <= 0)
                return;

            var center = new Point(element.X + width / 2, element.Y + height / 2);
            dc.PushTransform(new RotateTransform(LabelElement.NormalizeRotationDegrees(element.RotationDegrees), center.X, center.Y));
            dc.PushClip(new RectangleGeometry(new Rect(element.X, element.Y, width, height)));

            switch (element.Type)
            {
                case ElementType.Text:
                    DrawText(dc, element, dataSource, width, height);
                    break;
                case ElementType.Barcode:
                    DrawBarcode(dc, element, dataSource, config, width, height);
                    break;
                case ElementType.QRCode:
                    DrawQrCode(dc, element, dataSource, width, height);
                    break;
                case ElementType.Image:
                    DrawImage(dc, element, width, height);
                    break;
            }

            dc.Pop();
            dc.Pop();
        }

        private static void DrawText(DrawingContext dc, LabelElement element, ResourceItem? dataSource, double width, double height)
        {
            var content = LabelFieldValueResolver.ResolveVisualValue(element, dataSource);
            if (string.IsNullOrEmpty(content))
                return;

            var typeface = new Typeface(
                new FontFamily("Segoe UI"),
                FontStyles.Normal,
                element.IsBold ? FontWeights.Bold : FontWeights.Normal,
                FontStretches.Normal);
            var formattedText = new FormattedText(
                content,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                Math.Max(1, element.FontSize * LabelSizeHelper.FONT_SCALING_FACTOR),
                Brushes.Black,
                1.0)
            {
                MaxTextWidth = Math.Max(1, width),
                MaxTextHeight = Math.Max(1, height),
                TextAlignment = element.IsCentered ? TextAlignment.Center : TextAlignment.Left,
                Trimming = TextTrimming.None
            };

            dc.DrawText(formattedText, new Point(element.X, element.Y));
        }

        private static void DrawBarcode(
            DrawingContext dc,
            LabelElement element,
            ResourceItem? dataSource,
            PrinterConfiguration config,
            double width,
            double height)
        {
            var content = SanitizeBarcodeData(LabelFieldValueResolver.ResolveVisualValue(element, dataSource));
            var layout = LabelSizeHelper.CalculateCode128Layout(content, width, config.Dpi);
            var barcodeWidth = Math.Max(1, Math.Min(layout.ActualWidthPixels, width));
            var image = CreateBarcodeImage(content, barcodeWidth, Math.Max(height, LabelSizeHelper.MmToScreenPixels(5)));
            var x = element.IsCentered
                ? element.X + Math.Max(0, (width - barcodeWidth) / 2)
                : element.X;

            dc.DrawRectangle(Brushes.White, null, new Rect(element.X, element.Y, width, height));
            dc.DrawImage(image, new Rect(x, element.Y, barcodeWidth, height));
        }

        private static void DrawQrCode(DrawingContext dc, LabelElement element, ResourceItem? dataSource, double width, double height)
        {
            var content = LabelFieldValueResolver.ResolveVisualValue(element, dataSource);
            if (string.IsNullOrEmpty(content))
                return;

            var size = Math.Max(1, Math.Min(width, height));
            var moduleCount = 21;
            var moduleSize = Math.Max(1, Math.Floor(size / moduleCount));
            var actualSize = moduleCount * moduleSize;
            var x0 = element.X + (width - actualSize) / 2;
            var y0 = element.Y + (height - actualSize) / 2;
            var rng = new Random(content.GetHashCode());

            dc.DrawRectangle(Brushes.White, null, new Rect(x0, y0, actualSize, actualSize));

            for (var row = 0; row < moduleCount; row++)
            {
                for (var col = 0; col < moduleCount; col++)
                {
                    var isBlack = IsFinderPattern(row, col, moduleCount)
                        ? IsFinderPatternBlack(row, col, moduleCount)
                        : row == 6 || col == 6
                            ? (row + col) % 2 == 0
                            : rng.Next(100) < 45;

                    if (isBlack)
                    {
                        dc.DrawRectangle(
                            Brushes.Black,
                            null,
                            new Rect(x0 + col * moduleSize, y0 + row * moduleSize, moduleSize, moduleSize));
                    }
                }
            }
        }

        private static void DrawImage(DrawingContext dc, LabelElement element, double width, double height)
        {
            if (string.IsNullOrWhiteSpace(element.ImageDataBase64))
                return;

            try
            {
                var bytes = Convert.FromBase64String(element.ImageDataBase64);
                var bitmap = new BitmapImage();
                using var stream = new MemoryStream(bytes);
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();

                var scale = Math.Min(width / bitmap.PixelWidth, height / bitmap.PixelHeight);
                var drawWidth = Math.Max(1, bitmap.PixelWidth * scale);
                var drawHeight = Math.Max(1, bitmap.PixelHeight * scale);
                var x = element.X + (width - drawWidth) / 2;
                var y = element.Y + (height - drawHeight) / 2;
                dc.DrawImage(bitmap, new Rect(x, y, drawWidth, drawHeight));
            }
            catch
            {
                // Invalid embedded image data should not block the whole print job.
            }
        }

        private static BitmapSource CreateBarcodeImage(string content, double width, double height)
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
                (int)Math.Max(Math.Round(height), 1));
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

        private static string ToZplGraphicField(BitmapSource bitmap)
        {
            var width = bitmap.PixelWidth;
            var height = bitmap.PixelHeight;
            var stride = width * 4;
            var pixels = new byte[stride * height];
            bitmap.CopyPixels(pixels, stride, 0);

            var bytesPerRow = (width + 7) / 8;
            var totalBytes = bytesPerRow * height;
            var hex = new StringBuilder(totalBytes * 2);

            for (var y = 0; y < height; y++)
            {
                for (var byteIndex = 0; byteIndex < bytesPerRow; byteIndex++)
                {
                    var value = 0;
                    for (var bit = 0; bit < 8; bit++)
                    {
                        var x = byteIndex * 8 + bit;
                        if (x >= width)
                            continue;

                        var offset = y * stride + x * 4;
                        var b = pixels[offset];
                        var g = pixels[offset + 1];
                        var r = pixels[offset + 2];
                        var a = pixels[offset + 3];
                        var luminance = (r * 299 + g * 587 + b * 114) / 1000;
                        if (a >= 128 && luminance < 180)
                            value |= 1 << (7 - bit);
                    }

                    hex.Append(value.ToString("X2"));
                }
            }

            return $"^GFA,{totalBytes},{totalBytes},{bytesPerRow},{hex}";
        }

        private static string SanitizeBarcodeData(string data)
        {
            if (string.IsNullOrEmpty(data)) return "0000";
            var sanitized = new string(data.Where(c =>
                char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.' || c == ' ').ToArray());
            if (string.IsNullOrEmpty(sanitized)) return "0000";
            return sanitized.Length > 50 ? sanitized[..50] : sanitized;
        }

        private static bool IsFinderPattern(int row, int col, int size)
        {
            return row < 7 && col < 7
                   || row < 7 && col >= size - 7
                   || row >= size - 7 && col < 7;
        }

        private static bool IsFinderPatternBlack(int row, int col, int size)
        {
            var r = row >= size - 7 ? row - (size - 7) : row;
            var c = col >= size - 7 ? col - (size - 7) : col;

            if (r == 0 || r == 6 || c == 0 || c == 6) return true;
            if (r == 1 || r == 5 || c == 1 || c == 5) return false;
            return true;
        }
    }
}
