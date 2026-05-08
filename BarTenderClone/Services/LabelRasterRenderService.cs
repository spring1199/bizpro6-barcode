using System.Text;
using System.Windows.Media.Imaging;
using BarTenderClone.Models;

namespace BarTenderClone.Services
{
    internal sealed record RasterizedLabel(
        int WidthDots,
        int HeightDots,
        string GraphicField,
        string DiagnosticSummary = "");

    internal static class LabelRasterRenderService
    {
        public static RasterizedLabel RenderToZplGraphic(
            IEnumerable<LabelElement> elements,
            ResourceItem? dataSource,
            LabelTemplate template,
            PrinterConfiguration config)
        {
            var rendered = LabelRenderEngine.RenderPrintBitmap(elements, dataSource, template, config);
            return new RasterizedLabel(
                rendered.WidthDots,
                rendered.HeightDots,
                ToZplGraphicField(rendered.Bitmap),
                rendered.DiagnosticSummary);
        }

        public static RasterizedLabel RenderCalibrationToZplGraphic(
            LabelTemplate template,
            PrinterConfiguration config)
        {
            var rendered = LabelRenderEngine.RenderCalibrationBitmap(template, config);
            return new RasterizedLabel(
                rendered.WidthDots,
                rendered.HeightDots,
                ToZplGraphicField(rendered.Bitmap),
                rendered.DiagnosticSummary);
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
    }
}
