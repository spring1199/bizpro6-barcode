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

        [ObservableProperty]
        private bool _isAutoWidth = true;

        [ObservableProperty]
        private bool _isAutoHeight = true;

        private bool _isMeasuring;

        partial void OnWidthChanged(double value)
        {
            if (!_isMeasuring)
            {
                IsAutoWidth = false;
            }
        }

        partial void OnHeightChanged(double value)
        {
            if (!_isMeasuring)
            {
                IsAutoHeight = false;
            }
        }

        partial void OnRotationDegreesChanged(int value)
        {
            var normalized = NormalizeRotationDegrees(value);
            if (normalized != value)
            {
                RotationDegrees = normalized;
            }
            AutoMeasureSize();
        }

        partial void OnContentChanged(string value) => AutoMeasureSize();
        partial void OnFontSizeChanged(double value) => AutoMeasureSize();
        partial void OnIsBoldChanged(bool value) => AutoMeasureSize();
        partial void OnTypeChanged(ElementType value) => AutoMeasureSize();

        public void AutoMeasureSize()
        {
            if (Type != ElementType.Text)
                return;

            if (!IsAutoWidth && !IsAutoHeight)
                return;

            if (System.Windows.Application.Current == null)
            {
                try
                {
                    RunMeasure();
                }
                catch
                {
                    _isMeasuring = true;
                    try
                    {
                        var size = BarTenderClone.Helpers.DesignerInteractionHelper.MeasureActualTextSize(Content, FontSize, IsBold);
                        if (IsAutoWidth) Width = size.Width;
                        if (IsAutoHeight) Height = size.Height;
                    }
                    finally
                    {
                        _isMeasuring = false;
                    }
                }
                return;
            }

            var dispatcher = System.Windows.Application.Current.Dispatcher;
            if (dispatcher.CheckAccess())
            {
                RunMeasure();
            }
            else
            {
                dispatcher.BeginInvoke(new System.Action(RunMeasure));
            }
        }

        private void RunMeasure()
        {
            if (IsAutoWidth || IsAutoHeight)
            {
                _isMeasuring = true;
                try
                {
                    var size = BarTenderClone.Helpers.DesignerInteractionHelper.MeasureActualTextSize(Content, FontSize, IsBold);
                    if (IsAutoWidth) Width = size.Width;
                    if (IsAutoHeight) Height = size.Height;
                }
                finally
                {
                    _isMeasuring = false;
                }
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
