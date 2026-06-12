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

            // Generate hex rows
            var hexRows = new string[height];
            for (var y = 0; y < height; y++)
            {
                var rowBuilder = new StringBuilder(bytesPerRow * 2);
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

                    rowBuilder.Append(value.ToString("X2"));
                }
                hexRows[y] = rowBuilder.ToString();
            }

            // Compress rows
            var compressed = new StringBuilder(totalBytes * 2);
            string? previousRow = null;

            for (var y = 0; y < height; y++)
            {
                string currentRow = hexRows[y];
                if (currentRow == previousRow)
                {
                    compressed.Append(':');
                }
                else
                {
                    compressed.Append(CompressRow(currentRow));
                }
                previousRow = currentRow;
            }

            return $"^GFA,{totalBytes},{totalBytes},{bytesPerRow},{compressed}";
        }

        private static string CompressRow(string row)
        {
            // Find suffix of '0's and 'F's
            int zeroSuffixStart = row.Length;
            while (zeroSuffixStart > 0 && row[zeroSuffixStart - 1] == '0')
            {
                zeroSuffixStart--;
            }

            int fSuffixStart = row.Length;
            while (fSuffixStart > 0 && (row[fSuffixStart - 1] == 'F' || row[fSuffixStart - 1] == 'f'))
            {
                fSuffixStart--;
            }

            string prefix;
            char suffixChar = '\0';

            if (zeroSuffixStart < fSuffixStart)
            {
                prefix = row.Substring(0, zeroSuffixStart);
                suffixChar = ',';
            }
            else if (fSuffixStart < zeroSuffixStart)
            {
                prefix = row.Substring(0, fSuffixStart);
                suffixChar = '!';
            }
            else
            {
                prefix = row;
            }

            var encoded = new StringBuilder();
            int i = 0;
            int length = prefix.Length;
            while (i < length)
            {
                char c = prefix[i];
                int count = 1;
                while (i + count < length && prefix[i + count] == c)
                {
                    count++;
                }

                AppendCompressedRun(encoded, c, count);
                i += count;
            }

            if (suffixChar != '\0')
            {
                encoded.Append(suffixChar);
            }

            return encoded.ToString();
        }

        private static void AppendCompressedRun(StringBuilder sb, char c, int count)
        {
            if (count == 1)
            {
                sb.Append(c);
                return;
            }
            if (count == 2)
            {
                sb.Append(c).Append(c);
                return;
            }

            int rem = count;
            while (rem >= 400)
            {
                sb.Append('z');
                rem -= 400;
            }

            if (rem >= 20)
            {
                int mult20 = (rem / 20) * 20;
                char code = (char)('g' - 1 + (mult20 / 20));
                sb.Append(code);
                rem -= mult20;
            }

            if (rem > 0)
            {
                char code = (char)('G' - 1 + rem);
                sb.Append(code);
            }

            sb.Append(c);
        }
    }
}
