using BarTenderClone.Models;

namespace BarTenderClone.Helpers
{
    /// <summary>
    /// Resolves label field values consistently for preview and printed visual output.
    /// RFID encoding intentionally uses raw values instead of these display values.
    /// </summary>
    public static class LabelFieldValueResolver
    {
        public static string ResolveVisualValue(LabelElement element, ResourceItem? dataSource)
        {
            var fallback = element.Content ?? string.Empty;

            if (string.IsNullOrWhiteSpace(element.FieldName) ||
                element.FieldName.Equals("None", StringComparison.OrdinalIgnoreCase) ||
                dataSource == null)
            {
                return fallback;
            }

            return ResolveVisualValue(element.FieldName, dataSource, fallback);
        }

        public static string ResolveVisualValue(string fieldName, ResourceItem dataSource, string fallback = "")
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                return fallback;

            var canonicalField = ResourceItem.CanonicalizeFieldName(fieldName);
            var value = dataSource.GetFieldValue(canonicalField);

            if (canonicalField.Equals("RFID", StringComparison.OrdinalIgnoreCase))
                value = StripLeadingZerosForDisplay(value ?? dataSource.Rfid);

            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        public static string ResolveRawValue(LabelElement element, ResourceItem? dataSource)
        {
            var fallback = element.Content ?? string.Empty;

            if (string.IsNullOrWhiteSpace(element.FieldName) ||
                element.FieldName.Equals("None", StringComparison.OrdinalIgnoreCase) ||
                dataSource == null)
            {
                return fallback;
            }

            var canonicalField = ResourceItem.CanonicalizeFieldName(element.FieldName);
            if (canonicalField.Equals("RFID", StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(dataSource.Rfid) ? fallback : dataSource.Rfid;

            return dataSource.GetFieldValue(canonicalField) ?? fallback;
        }

        public static string StripLeadingZerosForDisplay(string? rfid)
        {
            if (string.IsNullOrWhiteSpace(rfid))
                return string.Empty;

            var trimmed = rfid.TrimStart('0');
            return string.IsNullOrEmpty(trimmed) ? "0" : trimmed;
        }
    }
}
