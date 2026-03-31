using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BarTenderClone.Converters
{
    /// <summary>
    /// Converts QR code content, width, and height to a QR code preview image.
    /// Generates a grid pattern that represents the QR code visually.
    /// </summary>
    public class QrCodeToImageConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] = Content (string)
            // values[1] = Width (double)
            // values[2] = Height (double)
            if (values == null || values.Length < 3)
                return null;

            string content = values[0] as string;
            if (string.IsNullOrEmpty(content))
                return null;

            double elementWidth = 100;
            double elementHeight = 100;

            if (values[1] is double w && w > 0)
                elementWidth = w;
            if (values[2] is double h && h > 0)
                elementHeight = h;

            try
            {
                int size = (int)Math.Min(elementWidth, elementHeight);
                size = Math.Max(size, 50);

                // Create a deterministic pattern based on content hash
                int seed = content.GetHashCode();
                Random rng = new Random(seed);

                // Grid size for QR code modules (21x21 is standard QR version 1)
                int moduleCount = 21;
                int moduleSize = size / moduleCount;
                if (moduleSize < 2) moduleSize = 2;
                int actualSize = moduleCount * moduleSize;

                DrawingVisual drawingVisual = new DrawingVisual();
                using (DrawingContext dc = drawingVisual.RenderOpen())
                {
                    // White background
                    dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, actualSize, actualSize));

                    // Draw QR code pattern
                    for (int row = 0; row < moduleCount; row++)
                    {
                        for (int col = 0; col < moduleCount; col++)
                        {
                            bool isBlack = false;

                            // Finder patterns (top-left, top-right, bottom-left corners)
                            if (IsFinderPattern(row, col, moduleCount))
                            {
                                isBlack = IsFinderPatternBlack(row, col, moduleCount);
                            }
                            // Timing patterns
                            else if (row == 6 || col == 6)
                            {
                                isBlack = (row + col) % 2 == 0;
                            }
                            // Data area - use pseudo-random based on content
                            else
                            {
                                isBlack = rng.Next(100) < 45;
                            }

                            if (isBlack)
                            {
                                dc.DrawRectangle(Brushes.Black, null,
                                    new Rect(col * moduleSize, row * moduleSize, moduleSize, moduleSize));
                            }
                        }
                    }
                }

                RenderTargetBitmap bmp = new RenderTargetBitmap(actualSize, actualSize, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(drawingVisual);
                bmp.Freeze();
                return bmp;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private bool IsFinderPattern(int row, int col, int size)
        {
            // Top-left finder pattern
            if (row < 7 && col < 7) return true;
            // Top-right finder pattern
            if (row < 7 && col >= size - 7) return true;
            // Bottom-left finder pattern
            if (row >= size - 7 && col < 7) return true;
            return false;
        }

        private bool IsFinderPatternBlack(int row, int col, int size)
        {
            int r = row;
            int c = col;

            // Normalize to local coordinates for each finder pattern
            if (row >= size - 7) r = row - (size - 7);
            if (col >= size - 7) c = col - (size - 7);

            // Finder pattern: 7x7 with specific pattern
            // Outer ring is black, then white ring, then black center 3x3
            if (r == 0 || r == 6 || c == 0 || c == 6) return true; // Outer ring
            if (r == 1 || r == 5 || c == 1 || c == 5) return false; // White ring
            return true; // Center 3x3
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
