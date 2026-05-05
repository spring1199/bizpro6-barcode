using CommunityToolkit.Mvvm.ComponentModel;

namespace BarTenderClone.Models
{
    public enum ElementType
    {
        Text,
        Barcode,
        QRCode,
        Image
    }

    public partial class LabelElement : ObservableObject
    {
        [ObservableProperty]
        private double _x;

        [ObservableProperty]
        private double _y;

        [ObservableProperty]
        private double _width;

        [ObservableProperty]
        private double _height;

        [ObservableProperty]
        private string _content = string.Empty;

        [ObservableProperty]
        private string _fieldName = string.Empty; // For data binding (e.g., "ProductName")

        [ObservableProperty]
        private ElementType _type;

        [ObservableProperty]
        private double _fontSize = 12;

        [ObservableProperty]
        private bool _isBold;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isCentered;

        [ObservableProperty]
        private int _rotationDegrees;

        [ObservableProperty]
        private string _imageDataBase64 = string.Empty;

        [ObservableProperty]
        private string _imageMimeType = string.Empty;

        [ObservableProperty]
        private string _imageFileName = string.Empty;

        partial void OnRotationDegreesChanged(int value)
        {
            var normalized = NormalizeRotationDegrees(value);
            if (normalized != value)
            {
                RotationDegrees = normalized;
            }
        }

        public static int NormalizeRotationDegrees(int value)
        {
            var normalized = ((value % 360) + 360) % 360;
            return normalized switch
            {
                0 or 90 or 180 or 270 => normalized,
                < 45 => 0,
                < 135 => 90,
                < 225 => 180,
                < 315 => 270,
                _ => 0
            };
        }
    }

    public partial class LabelTemplate : ObservableObject
    {
        [ObservableProperty]
        private double _width = 400; // pixels (approx 4 inches at 100dpi for view)

        [ObservableProperty]
        private double _height = 400; // pixels

        [ObservableProperty]
        private string _name = "New Template";
    }

    /// <summary>
    /// Preset label sizes for quick selection.
    /// Common label dimensions for retail and shipping applications.
    /// </summary>
    public class LabelSizePreset
    {
        public string Name { get; set; } = string.Empty;
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }

        public override string ToString() => Name;
    }
}
