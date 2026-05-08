using BarTenderClone.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace BarTenderClone.Services
{
    internal static class PrinterCalibrationStore
    {
        private static readonly string StorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BarTenderClone",
            "printer-calibration.json");

        public static PrinterCalibrationProfile Get(string printerName, int fallbackDpi)
        {
            var profiles = LoadAll();
            if (profiles.TryGetValue(printerName, out var profile))
                return Normalize(profile, printerName, fallbackDpi);

            return new PrinterCalibrationProfile
            {
                PrinterName = printerName,
                Dpi = fallbackDpi,
                ScaleX = 1.0,
                ScaleY = 1.0
            };
        }

        public static void Save(PrinterCalibrationProfile profile)
        {
            if (string.IsNullOrWhiteSpace(profile.PrinterName))
                return;

            var profiles = LoadAll();
            profiles[profile.PrinterName] = Normalize(profile, profile.PrinterName, profile.Dpi);

            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            File.WriteAllText(StorePath, JsonConvert.SerializeObject(profiles, Formatting.Indented));
        }

        private static Dictionary<string, PrinterCalibrationProfile> LoadAll()
        {
            try
            {
                if (!File.Exists(StorePath))
                    return new Dictionary<string, PrinterCalibrationProfile>(StringComparer.OrdinalIgnoreCase);

                var profiles = JsonConvert.DeserializeObject<Dictionary<string, PrinterCalibrationProfile>>(
                    File.ReadAllText(StorePath));

                return profiles != null
                    ? new Dictionary<string, PrinterCalibrationProfile>(profiles, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, PrinterCalibrationProfile>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, PrinterCalibrationProfile>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static PrinterCalibrationProfile Normalize(
            PrinterCalibrationProfile profile,
            string printerName,
            int fallbackDpi)
        {
            return new PrinterCalibrationProfile
            {
                PrinterName = printerName,
                Dpi = profile.Dpi > 0 ? profile.Dpi : fallbackDpi,
                OffsetXmm = profile.OffsetXmm,
                OffsetYmm = profile.OffsetYmm,
                ScaleX = NormalizeScale(profile.ScaleX),
                ScaleY = NormalizeScale(profile.ScaleY),
                RotationDegrees = LabelElement.NormalizeRotationDegrees(profile.RotationDegrees)
            };
        }

        private static double NormalizeScale(double value)
            => value > 0.05 && value < 20 ? value : 1.0;
    }
}
