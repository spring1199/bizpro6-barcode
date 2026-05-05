using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace BarTenderClone.Helpers
{
    public sealed record EmbeddedLabelImage(string Base64Data, string MimeType, int Width, int Height);

    public static class LabelImageHelper
    {
        private const int DefaultMaxTemplateImageDimension = 800;

        public static EmbeddedLabelImage LoadAndNormalizeForTemplate(string filePath, int maxDimension = DefaultMaxTemplateImageDimension)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Image path is required.", nameof(filePath));

            using var source = new Bitmap(filePath);
            var scale = Math.Min(1.0, maxDimension / (double)Math.Max(source.Width, source.Height));
            var targetWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
            var targetHeight = Math.Max(1, (int)Math.Round(source.Height * scale));

            using var target = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
            target.SetResolution(96, 96);

            using (var graphics = Graphics.FromImage(target))
            {
                graphics.Clear(Color.Transparent);
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, 0, 0, targetWidth, targetHeight);
            }

            using var stream = new MemoryStream();
            target.Save(stream, ImageFormat.Png);
            return new EmbeddedLabelImage(Convert.ToBase64String(stream.ToArray()), "image/png", targetWidth, targetHeight);
        }

        public static string GenerateZplGraphic(string base64Data, int targetWidthDots, int targetHeightDots, int rotationDegrees)
        {
            if (string.IsNullOrWhiteSpace(base64Data) || targetWidthDots <= 0 || targetHeightDots <= 0)
                return string.Empty;

            using var sourceStream = new MemoryStream(Convert.FromBase64String(base64Data));
            using var source = new Bitmap(sourceStream);
            using var scaled = DrawUniformOnWhiteCanvas(source, targetWidthDots, targetHeightDots);
            using var rotated = ApplyRotation(scaled, rotationDegrees);

            return ToZplGraphicField(rotated);
        }

        private static Bitmap DrawUniformOnWhiteCanvas(Bitmap source, int targetWidth, int targetHeight)
        {
            var target = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
            target.SetResolution(96, 96);

            var scale = Math.Min(targetWidth / (double)source.Width, targetHeight / (double)source.Height);
            var drawWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
            var drawHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
            var drawX = (targetWidth - drawWidth) / 2;
            var drawY = (targetHeight - drawHeight) / 2;

            using var graphics = Graphics.FromImage(target);
            graphics.Clear(Color.White);
            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(source, drawX, drawY, drawWidth, drawHeight);

            return target;
        }

        private static Bitmap ApplyRotation(Bitmap source, int rotationDegrees)
        {
            var rotated = new Bitmap(source);
            switch (((rotationDegrees % 360) + 360) % 360)
            {
                case 90:
                    rotated.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    break;
                case 180:
                    rotated.RotateFlip(RotateFlipType.Rotate180FlipNone);
                    break;
                case 270:
                    rotated.RotateFlip(RotateFlipType.Rotate270FlipNone);
                    break;
            }

            return rotated;
        }

        private static string ToZplGraphicField(Bitmap bitmap)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
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

                        var color = bitmap.GetPixel(x, y);
                        var isBlack = color.A >= 128 && ((color.R * 299 + color.G * 587 + color.B * 114) / 1000) < 180;
                        if (isBlack)
                            value |= 1 << (7 - bit);
                    }

                    hex.Append(value.ToString("X2"));
                }
            }

            return $"^GFA,{totalBytes},{totalBytes},{bytesPerRow},{hex}";
        }
    }
}
