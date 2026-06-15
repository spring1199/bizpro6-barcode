using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using QRCoder;

namespace BarTenderClone.Converters
{
    /// <summary>
    /// Converts QR code content, width, and height to a real QR code preview image using QRCoder.
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

            try
            {
                using (var qrGenerator = new QRCodeGenerator())
                using (var qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M))
                using (var qrCode = new PngByteQRCode(qrCodeData))
                {
                    // 6 pixels per module is plenty for preview size
                    byte[] qrCodeAsPngByteArr = qrCode.GetGraphic(6);
                    using (var ms = new MemoryStream(qrCodeAsPngByteArr))
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = ms;
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
