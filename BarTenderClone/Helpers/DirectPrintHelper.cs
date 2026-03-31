using System;
using System.IO;
using System.Text;

namespace BarTenderClone.Helpers
{
    /// <summary>
    /// Direct port printing helper - bypasses Windows spooler entirely.
    /// Useful for diagnosing driver/spooler issues.
    /// </summary>
    public static class DirectPrintHelper
    {
        /// <summary>
        /// Sends ZPL directly to printer port (e.g., "USB002", "COM1", "LPT1")
        /// </summary>
        public static bool SendToPort(string portName, string zplData)
        {
            try
            {
                // Ensure port name is properly formatted
                if (!portName.StartsWith("\\\\.\\"))
                {
                    portName = "\\\\.\\" + portName;
                }

                // Convert ZPL to bytes (UTF-8)
                byte[] bytes = Encoding.UTF8.GetBytes(zplData);

                // Open port as file stream
                using (FileStream fs = new FileStream(portName, FileMode.Open, FileAccess.Write))
                {
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush();
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Direct port print failed: {ex.Message}");
                return false;
            }
        }
    }
}
