using System;
using System.IO;

namespace BarTenderClone.Services
{
    /// <summary>
    /// Simple file-based logging service for production diagnostics
    /// </summary>
    public class FileLoggingService : ILoggingService
    {
        private readonly string _logFilePath;
        private readonly object _lockObject = new object();

        public FileLoggingService()
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BarTenderClone",
                "Logs"
            );
            Directory.CreateDirectory(logDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd");
            _logFilePath = Path.Combine(logDir, $"print_{timestamp}.log");
        }

        public void LogInfo(string message) => Log("INFO", message);
        public void LogWarning(string message) => Log("WARN", message);
        public void LogError(string message, Exception? ex = null)
        {
            var fullMessage = ex != null ? $"{message}\nException: {ex}" : message;
            Log("ERROR", fullMessage);
        }
        public void LogDebug(string message) => Log("DEBUG", message);

        private void Log(string level, string message)
        {
            lock (_lockObject)
            {
                try
                {
                    var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }
                catch
                {
                    // Logging should never crash the application
                }
            }
        }
    }
}
