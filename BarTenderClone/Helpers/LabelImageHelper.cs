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
            using var rotatedSource = ApplyRotation(source, rotationDegrees);
            using var scaled = DrawUniformOnWhiteCanvas(rotatedSource, targetWidthDots, targetHeightDots);

            return ToZplGraphicField(scaled);
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

            // Use LockBits for bulk pixel access instead of per-pixel GetPixel()
            // This provides 100-1000x speedup by avoiding per-pixel lock/unlock overhead
            var rect = new Rectangle(0, 0, width, height);
            var bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var stride = bitmapData.Stride;
                var pixelData = new byte[stride * height];
                System.Runtime.InteropServices.Marshal.Copy(bitmapData.Scan0, pixelData, 0, pixelData.Length);

                for (var y = 0; y < height; y++)
                {
                    var rowOffset = y * stride;
                    for (var byteIndex = 0; byteIndex < bytesPerRow; byteIndex++)
                    {
                        var value = 0;
                        for (var bit = 0; bit < 8; bit++)
                        {
                            var x = byteIndex * 8 + bit;
                            if (x >= width)
                                continue;

                            var pixelOffset = rowOffset + x * 4; // 4 bytes per pixel (ARGB)
                            var b = pixelData[pixelOffset];
                            var g = pixelData[pixelOffset + 1];
                            var r = pixelData[pixelOffset + 2];
                            var a = pixelData[pixelOffset + 3];

                            var isBlack = a >= 128 && ((r * 299 + g * 587 + b * 114) / 1000) < 180;
                            if (isBlack)
                                value |= 1 << (7 - bit);
                        }

                        hex.Append(value.ToString("X2"));
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            var compressedHex = CompressZplGraphics(hex.ToString(), bytesPerRow);
            return $"^GFA,{totalBytes},{totalBytes},{bytesPerRow},{compressedHex}";
        }

        public static string CompressZplGraphics(string hex, int bytesPerRow)
        {
            var charsPerRow = bytesPerRow * 2;
            var compressed = new StringBuilder(hex.Length);
            
            for (int i = 0; i < hex.Length; i += charsPerRow)
            {
                int length = Math.Min(charsPerRow, hex.Length - i);
                string row = hex.Substring(i, length);
                
                if (IsAllSameChar(row, '0'))
                {
                    compressed.Append(",");
                    continue;
                }
                
                if (IsAllSameChar(row, 'F'))
                {
                    compressed.Append("!");
                    continue;
                }
                
                int col = 0;
                while (col < row.Length)
                {
                    if (IsAllSameChar(row.Substring(col), '0'))
                    {
                        compressed.Append(",");
                        break;
                    }
                    
                    if (IsAllSameChar(row.Substring(col), 'F'))
                    {
                        compressed.Append("!");
                        break;
                    }
                    
                    char c = row[col];
                    int runLength = 1;
                    while (col + runLength < row.Length && row[col + runLength] == c)
                    {
                        runLength++;
                    }
                    
                    if (runLength == 1)
                    {
                        compressed.Append(c);
                    }
                    else
                    {
                        int count = runLength;
                        
                        while (count >= 400)
                        {
                            compressed.Append('z');
                            count -= 400;
                        }
                        
                        if (count >= 20)
                        {
                            int val = (count / 20) * 20;
                            char codeChar = (char)('g' + (val / 20) - 1);
                            compressed.Append(codeChar);
                            count -= val;
                        }
                        
                        if (count > 0)
                        {
                            char codeChar = (char)('G' + count - 1);
                            compressed.Append(codeChar);
                        }
                        
                        compressed.Append(c);
                    }
                    
                    col += runLength;
                }
            }
            
            return compressed.ToString();
        }

        private static bool IsAllSameChar(string str, char target)
        {
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] != target)
                    return false;
            }
            return true;
        }
    }
}
