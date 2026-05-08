using BarcodeStandard;
using BarTenderClone.Helpers;
using BarTenderClone.Models;
using SkiaSharp;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BarcodeType = BarcodeStandard.Type;

namespace BarTenderClone.Services
{
    internal sealed record RenderedLabelBitmap(
        int WidthDots,
        int HeightDots,
        BitmapSource Bitmap,
        string DiagnosticSummary);

    internal static class LabelRenderEngine
    {
        public static RenderedLabelBitmap RenderDesignerBitmap(
            IEnumerable<LabelElement> elements,
            ResourceItem? dataSource,
            LabelTemplate template,
            PrinterConfiguration config)
        {
            var bitmap = RenderTemplateBitmap(
                template,
                config,
                applyCalibration: false,
                dc => DrawElements(dc, elements, dataSource, config));

            return BuildRenderedLabel(bitmap, template, config, "Designer");
        }

        public static RenderedLabelBitmap RenderPrintBitmap(
            IEnumerable<LabelElement> elements,
            ResourceItem? dataSource,
            LabelTemplate template,
            PrinterConfiguration config)
        {
            var bitmap = RenderTemplateBitmap(
                template,
                config,
                applyCalibration: true,
                dc => DrawElements(dc, elements, dataSource, config));

            bitmap = ApplyRightAngleRotation(bitmap, config.PrintRotationDegrees);
            return BuildRenderedLabel(bitmap, template, config, "Print");
        }

        public static RenderedLabelBitmap RenderCalibrationBitmap(
            LabelTemplate template,
            PrinterConfiguration config)
        {
            var bitmap = RenderTemplateBitmap(
                template,
                config,
                applyCalibration: true,
                dc => DrawCalibration(dc, template));

            bitmap = ApplyRightAngleRotation(bitmap, config.PrintRotationDegrees);
            return BuildRenderedLabel(bitmap, template, config, "Calibration");
        }

        private static void DrawElements(
            DrawingContext dc,
            IEnumerable<LabelElement> elements,
            ResourceItem? dataSource,
            PrinterConfiguration config)
        {
            foreach (var element in elements.OrderBy(e => e.Y).ThenBy(e => e.X))
            {
                DrawElement(dc, element, dataSource, config);
            }
        }

        internal static void DrawElement(
            DrawingContext dc,
            LabelElement element,
            ResourceItem? dataSource,
            PrinterConfiguration config)
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

        private static BitmapSource RenderTemplateBitmap(
            LabelTemplate template,
            PrinterConfiguration config,
            bool applyCalibration,
            Action<DrawingContext> drawContent)
        {
            var widthDots = Math.Max(1, LabelSizeHelper.ScreenPixelsToDots(template.Width, config.Dpi));
            var heightDots = Math.Max(1, LabelSizeHelper.ScreenPixelsToDots(template.Height, config.Dpi));
            var screenToDots = config.Dpi / LabelSizeHelper.SCREEN_DPI;
            var scaleX = applyCalibration ? NormalizeScale(config.CalibrationScaleX) : 1.0;
            var scaleY = applyCalibration ? NormalizeScale(config.CalibrationScaleY) : 1.0;
            var offsetX = applyCalibration ? LabelSizeHelper.MmToDots(config.CalibrationOffsetXmm, config.Dpi) : 0;
            var offsetY = applyCalibration ? LabelSizeHelper.MmToDots(config.CalibrationOffsetYmm, config.Dpi) : 0;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, widthDots, heightDots));
                dc.PushClip(new RectangleGeometry(new Rect(0, 0, widthDots, heightDots)));
                dc.PushTransform(new TranslateTransform(offsetX, offsetY));
                dc.PushTransform(new ScaleTransform(screenToDots * scaleX, screenToDots * scaleY));
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, template.Width, template.Height));
                dc.PushClip(new RectangleGeometry(new Rect(0, 0, template.Width, template.Height)));

                drawContent(dc);

                dc.Pop();
                dc.Pop();
                dc.Pop();
                dc.Pop();
            }

            var bitmap = new RenderTargetBitmap(
                widthDots,
                heightDots,
                LabelSizeHelper.SCREEN_DPI,
                LabelSizeHelper.SCREEN_DPI,
                PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private static void DrawText(DrawingContext dc, LabelElement element, ResourceItem? dataSource, double width, double height)
        {
            var content = LabelFieldValueResolver.ResolveVisualValue(element, dataSource);
            if (string.IsNullOrEmpty(content))
                return;

            var inset = Math.Min(
                DesignerInteractionHelper.TextRenderInset,
                Math.Max(0, Math.Min(width, height) / 4));
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
                MaxTextWidth = Math.Max(1, width - inset * 2),
                MaxTextHeight = Math.Max(1, height - inset * 2),
                TextAlignment = element.IsCentered ? TextAlignment.Center : TextAlignment.Left,
                Trimming = TextTrimming.None
            };

            dc.DrawText(formattedText, new Point(element.X + inset, element.Y + inset));
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
            var barcodeWidth = Math.Max(
                layout.ActualWidthPixels,
                LabelSizeHelper.CalculateCode128Width(content, config.Dpi));
            var image = CreateBarcodeImage(content, barcodeWidth, Math.Max(height, LabelSizeHelper.MmToScreenPixels(5)));
            var drawWidth = Math.Max(barcodeWidth, image.Width);
            var x = element.IsCentered
                ? element.X + Math.Max(0, (width - drawWidth) / 2)
                : element.X;

            dc.DrawRectangle(Brushes.White, null, new Rect(element.X, element.Y, width, height));
            dc.DrawImage(image, new Rect(x, element.Y, drawWidth, height));
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
                // Invalid embedded image data should not block the whole label render.
            }
        }

        private static void DrawCalibration(DrawingContext dc, LabelTemplate template)
        {
            var thin = new Pen(Brushes.Black, 1);
            var heavy = new Pen(Brushes.Black, 2);
            var width = Math.Max(1, template.Width);
            var height = Math.Max(1, template.Height);
            var tick = LabelSizeHelper.MmToScreenPixels(10);
            var shortTick = LabelSizeHelper.MmToScreenPixels(2);

            dc.DrawRectangle(null, heavy, new Rect(1, 1, Math.Max(1, width - 2), Math.Max(1, height - 2)));
            dc.DrawLine(thin, new Point(width / 2, 0), new Point(width / 2, height));
            dc.DrawLine(thin, new Point(0, height / 2), new Point(width, height / 2));

            for (var x = 0.0; x <= width + 0.1; x += tick)
            {
                dc.DrawLine(heavy, new Point(x, 0), new Point(x, shortTick));
                dc.DrawLine(heavy, new Point(x, height), new Point(x, height - shortTick));
            }

            for (var y = 0.0; y <= height + 0.1; y += tick)
            {
                dc.DrawLine(heavy, new Point(0, y), new Point(shortTick, y));
                dc.DrawLine(heavy, new Point(width, y), new Point(width - shortTick, y));
            }

            var label = $"{Math.Round(LabelSizeHelper.ScreenPixelsToInches(width) * LabelSizeHelper.MM_PER_INCH, 1)}x{Math.Round(LabelSizeHelper.ScreenPixelsToInches(height) * LabelSizeHelper.MM_PER_INCH, 1)}mm";
            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var text = new FormattedText(
                label,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                10,
                Brushes.Black,
                1.0);
            dc.DrawText(text, new Point(6, 6));
            dc.DrawText(text, new Point(Math.Max(6, width - text.Width - 6), Math.Max(6, height - text.Height - 6)));
        }

        private static BitmapSource CreateBarcodeImage(string content, double width, double height)
        {
            var barcode = new Barcode
            {
                IncludeLabel = false,
                Alignment = AlignmentPositions.Left
            };

            var targetWidth = (int)Math.Max(Math.Round(width), 1);
            var targetHeight = (int)Math.Max(Math.Round(height), 1);
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

        private static BitmapSource ApplyRightAngleRotation(BitmapSource source, int rotationDegrees)
        {
            var normalized = LabelElement.NormalizeRotationDegrees(rotationDegrees);
            if (normalized == 0)
                return source;

            var rotated = new TransformedBitmap(source, new RotateTransform(normalized));
            rotated.Freeze();
            return rotated;
        }

        private static RenderedLabelBitmap BuildRenderedLabel(
            BitmapSource bitmap,
            LabelTemplate template,
            PrinterConfiguration config,
            string stage)
        {
            var diagnostic = string.Format(
                CultureInfo.InvariantCulture,
                "{0} WYSIWYG raster: Template={1:0.0}x{2:0.0}mm; Dpi={3}; Raster={4}x{5}; Offset={6:0.###},{7:0.###}mm; Scale={8:0.####},{9:0.####}; Rotation={10}",
                stage,
                LabelSizeHelper.ScreenPixelsToInches(template.Width) * LabelSizeHelper.MM_PER_INCH,
                LabelSizeHelper.ScreenPixelsToInches(template.Height) * LabelSizeHelper.MM_PER_INCH,
                config.Dpi,
                bitmap.PixelWidth,
                bitmap.PixelHeight,
                config.CalibrationOffsetXmm,
                config.CalibrationOffsetYmm,
                NormalizeScale(config.CalibrationScaleX),
                NormalizeScale(config.CalibrationScaleY),
                LabelElement.NormalizeRotationDegrees(config.PrintRotationDegrees));

            return new RenderedLabelBitmap(bitmap.PixelWidth, bitmap.PixelHeight, bitmap, diagnostic);
        }

        private static double NormalizeScale(double value)
            => value > 0.05 && value < 20 ? value : 1.0;

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
