namespace BarTenderClone.Models
{
    public class PrinterCalibrationProfile
    {
        public string PrinterName { get; set; } = string.Empty;
        public int Dpi { get; set; } = 203;
        public double OffsetXmm { get; set; }
        public double OffsetYmm { get; set; }
        public double ScaleX { get; set; } = 1.0;
        public double ScaleY { get; set; } = 1.0;
        public int RotationDegrees { get; set; }
    }
}
