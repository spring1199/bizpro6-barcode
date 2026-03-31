namespace BarTenderClone.Models
{
    /// <summary>
    /// RFID encoding configuration for Zebra ZT610
    /// </summary>
    public class RfidConfiguration
    {
        /// <summary>
        /// Enable RFID encoding (vs. barcode-only printing)
        /// </summary>
        public bool EnableRfidEncoding { get; set; } = true;

        /// <summary>
        /// Data format: ASCII, Hex, or EPC
        /// </summary>
        public RfidDataFormat DataFormat { get; set; } = RfidDataFormat.Hexadecimal;

        /// <summary>
        /// Memory bank to write to
        /// </summary>
        public RfidMemoryBank MemoryBank { get; set; } = RfidMemoryBank.EPC;

        /// <summary>
        /// Starting block number for write operation
        /// </summary>
        public int StartingBlock { get; set; } = 2; // Block 2 for EPC data (blocks 0-1 are CRC/PC)

        /// <summary>
        /// Number of retry attempts on RFID encoding failure
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// Action to take on void label (RFID encoding failure)
        /// </summary>
        public VoidLabelAction VoidAction { get; set; } = VoidLabelAction.Print;

        /// <summary>
        /// Enable post-encoding verification
        /// </summary>
        public bool EnableVerification { get; set; } = true;

        /// <summary>
        /// RFID position relative to label (in dots from label start)
        /// </summary>
        public int RfidPositionDots { get; set; } = 50;

        /// <summary>
        /// RFID write power level (0-30, higher = stronger signal)
        /// </summary>
        public int WritePower { get; set; } = 20;

        /// <summary>
        /// RFID read power level (0-30, higher = stronger signal)
        /// </summary>
        public int ReadPower { get; set; } = 20;
    }

    public enum RfidDataFormat
    {
        ASCII,      // A - ASCII text
        Hexadecimal, // H - Hex string
        EPC         // E - EPC format (requires ^RB setup)
    }

    public enum RfidMemoryBank
    {
        EPC = 1,
        TID = 2,
        User = 3,
        Reserved = 0
    }

    public enum VoidLabelAction
    {
        Mark,       // M - Print void mark on failed labels
        Eject,      // E - Eject failed labels without printing
        Print       // P - Continue printing even if RFID fails (not recommended for production)
    }
}
