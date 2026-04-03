using BarTenderClone.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace BarTenderClone.Helpers
{
    public static class RawPrinterHelper
    {
        // Structure and API declarions:
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public class DOCINFOW
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? pDocName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? pOutputFile;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? pDataType;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct JOB_INFO_1
        {
            public int JobId;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pPrinterName;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pMachineName;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pUserName;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pDocument;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pDatatype;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pStatus;
            public int Status;
            public int Priority;
            public int Position;
            public int TotalPages;
            public int PagesPrinted;
            public SYSTEMTIME Submitted;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEMTIME
        {
            public short wYear;
            public short wMonth;
            public short wDayOfWeek;
            public short wDay;
            public short wHour;
            public short wMinute;
            public short wSecond;
            public short wMilliseconds;
        }

        // Job status flags
        public const int JOB_STATUS_PAUSED = 0x00000001;
        public const int JOB_STATUS_ERROR = 0x00000002;
        public const int JOB_STATUS_DELETING = 0x00000004;
        public const int JOB_STATUS_SPOOLING = 0x00000008;
        public const int JOB_STATUS_PRINTING = 0x00000010;
        public const int JOB_STATUS_OFFLINE = 0x00000020;
        public const int JOB_STATUS_PAPEROUT = 0x00000040;
        public const int JOB_STATUS_PRINTED = 0x00000080;
        public const int JOB_STATUS_DELETED = 0x00000100;

        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPWStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern int StartDocPrinter(IntPtr hPrinter, Int32 level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOW di);

        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, Int32 dwCount, out Int32 dwWritten);

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool GetJob(IntPtr hPrinter, int JobId, int Level, IntPtr pJob, int cbBuf, out int pcbNeeded);

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool EnumJobs(IntPtr hPrinter, int FirstJob, int NoJobs, int Level, IntPtr pJob, int cbBuf, out int pcbNeeded, out int pcReturned);

        // SendBytesToPrinter() - helper function
        public static bool SendBytesToPrinter(string szPrinterName, IntPtr pBytes, Int32 dwCount)
        {
            Int32 dwError = 0, dwWritten = 0;
            IntPtr hPrinter = new IntPtr(0);
            DOCINFOW di = new DOCINFOW();
            bool bSuccess = false; // Assume failure unless you specifically succeed.

            di.pDocName = "BarTender Clone Document";
            di.pDataType = "RAW";

            if (OpenPrinter(szPrinterName, out hPrinter, IntPtr.Zero))
            {
                int jobId = StartDocPrinter(hPrinter, 1, di);
                if (jobId > 0)
                {
                    if (StartPagePrinter(hPrinter))
                    {
                        bSuccess = WritePrinter(hPrinter, pBytes, dwCount, out dwWritten);
                        EndPagePrinter(hPrinter);
                    }
                    EndDocPrinter(hPrinter);
                }
                ClosePrinter(hPrinter);
            }
            if (!bSuccess)
            {
                dwError = Marshal.GetLastWin32Error();
            }
            return bSuccess;
        }

        public static bool SendStringToPrinter(string szPrinterName, string szString)
        {
            // Convert to UTF-8 bytes to support Cyrillic/Unicode characters
            // ZPL printer needs ^CI28 enabled for this to work (handled in generator)
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(szString);
            IntPtr pBytes = Marshal.AllocCoTaskMem(bytes.Length);
            bool bSuccess = false;

            try
            {
                Marshal.Copy(bytes, 0, pBytes, bytes.Length);
                bSuccess = SendBytesToPrinter(szPrinterName, pBytes, bytes.Length);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pBytes);
            }
            
            return bSuccess;
        }

        /// <summary>
        /// Sends string to printer and returns the job ID for monitoring
        /// </summary>
        public static (bool success, int jobId) SendStringToPrinterWithJobTracking(string szPrinterName, string szString)
        {
            IntPtr pBytes = IntPtr.Zero;
            IntPtr hPrinter = IntPtr.Zero;
            bool bSuccess = false;
            int jobId = 0;

            try
            {
                // Convert to UTF-8 bytes
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(szString);
                Int32 dwCount = bytes.Length;
                
                pBytes = Marshal.AllocCoTaskMem(dwCount);
                Marshal.Copy(bytes, 0, pBytes, dwCount);

                DOCINFOW di = new DOCINFOW
                {
                    pDocName = "BarTender Clone RFID Document",
                    pDataType = "RAW"
                };

                if (OpenPrinter(szPrinterName, out hPrinter, IntPtr.Zero))
                {
                    jobId = StartDocPrinter(hPrinter, 1, di);
                    if (jobId > 0)
                    {
                        if (StartPagePrinter(hPrinter))
                        {
                            bSuccess = WritePrinter(hPrinter, pBytes, dwCount, out int dwWritten);
                            EndPagePrinter(hPrinter);
                        }
                        EndDocPrinter(hPrinter);
                    }
                    ClosePrinter(hPrinter);
                }
            }
            finally
            {
                if (pBytes != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pBytes);
            }

            return (bSuccess, jobId);
        }

        /// <summary>
        /// Gets the status of a specific print job
        /// </summary>
        public static (bool success, int status, string statusText) GetJobStatus(string printerName, int jobId)
        {
            IntPtr hPrinter = IntPtr.Zero;
            IntPtr pJob = IntPtr.Zero;

            try
            {
                if (!OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
                    return (false, 0, "Failed to open printer");

                // First call to get required buffer size
                GetJob(hPrinter, jobId, 1, IntPtr.Zero, 0, out int cbNeeded);

                pJob = Marshal.AllocHGlobal(cbNeeded);

                if (GetJob(hPrinter, jobId, 1, pJob, cbNeeded, out _))
                {
                    JOB_INFO_1 jobInfo = Marshal.PtrToStructure<JOB_INFO_1>(pJob);
                    string statusText = GetJobStatusText(jobInfo.Status);
                    return (true, jobInfo.Status, statusText);
                }

                return (false, 0, "Failed to get job info");
            }
            finally
            {
                if (pJob != IntPtr.Zero)
                    Marshal.FreeHGlobal(pJob);
                if (hPrinter != IntPtr.Zero)
                    ClosePrinter(hPrinter);
            }
        }

        private static string GetJobStatusText(int status)
        {
            if (status == 0) return "Queued";

            var statuses = new List<string>();
            if ((status & JOB_STATUS_PAUSED) != 0) statuses.Add("Paused");
            if ((status & JOB_STATUS_ERROR) != 0) statuses.Add("Error");
            if ((status & JOB_STATUS_DELETING) != 0) statuses.Add("Deleting");
            if ((status & JOB_STATUS_SPOOLING) != 0) statuses.Add("Spooling");
            if ((status & JOB_STATUS_PRINTING) != 0) statuses.Add("Printing");
            if ((status & JOB_STATUS_OFFLINE) != 0) statuses.Add("Offline");
            if ((status & JOB_STATUS_PAPEROUT) != 0) statuses.Add("Paper Out");
            if ((status & JOB_STATUS_PRINTED) != 0) statuses.Add("Printed");
            if ((status & JOB_STATUS_DELETED) != 0) statuses.Add("Deleted");

            return statuses.Count > 0 ? string.Join(", ", statuses) : "Unknown";
        }

        /// <summary>
        /// Waits for a print job to complete and returns final status
        /// </summary>
        public static async Task<PrintResult> WaitForJobCompletionAsync(string printerName, int jobId, int timeoutMs = 60000)
        {
            var startTime = DateTime.Now;
            var pollInterval = 500; // Poll every 500ms

            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                var (success, status, statusText) = GetJobStatus(printerName, jobId);

                if (!success)
                {
                    return new PrintResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to query job status",
                        ErrorType = PrintErrorType.SpoolerError
                    };
                }

                // Check for completion or error states
                // JOB_STATUS_PRINTED (0x80) means it finished printing
                // JOB_STATUS_DELETED (0x100) means it was removed (usually after finishing or manual cancel)
                if ((status & JOB_STATUS_PRINTED) != 0 || (status & JOB_STATUS_DELETED) != 0)
                {
                    return new PrintResult
                    {
                        Success = true,
                        JobId = jobId
                    };
                }

                if ((status & JOB_STATUS_ERROR) != 0)
                {
                    return new PrintResult
                    {
                        Success = false,
                        ErrorMessage = $"Print job error: {statusText}",
                        ErrorType = PrintErrorType.SpoolerError,
                        JobId = jobId
                    };
                }

                await Task.Delay(pollInterval);
            }

            // Timeout reached - printer likely printed successfully but job tracking couldn't confirm.
            // Treat as SpoolerError so callers use optimistic/soft-success path.
            return new PrintResult
            {
                Success = false,
                ErrorMessage = "Print job tracking timeout (print likely succeeded)",
                ErrorType = PrintErrorType.SpoolerError,
                JobId = jobId
            };
        }
    }
}
