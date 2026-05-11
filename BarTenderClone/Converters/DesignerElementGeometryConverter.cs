using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using BarTenderClone.Helpers;
using BarTenderClone.Models;

namespace BarTenderClone.Converters
{
    public sealed class DesignerElementGeometryConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 8)
                return DependencyProperty.UnsetValue;

            var x = values[0] is double elementX ? elementX : 0;
            var y = values[1] is double elementY ? elementY : 0;
            var width = values[2] is double elementWidth ? elementWidth : 0;
            var height = values[3] is double elementHeight ? elementHeight : 0;
            var rotation = values[4] is int elementRotation ? elementRotation : 0;
            var type = values[5] is ElementType elementType ? elementType : ElementType.Text;
            var fontSize = values[6] is double elementFontSize ? elementFontSize : 12;
            var content = values[7] as string ?? string.Empty;
            var isBold = values.Length > 8 && values[8] is bool bold && bold;
            var isCentered = values.Length > 9 && values[9] is bool centered && centered;
            var metric = parameter?.ToString() ?? string.Empty;

            var local = DesignerInteractionHelper.GetLocalSize(width, height, type, fontSize, content, rotation);
            const double handleSize = 8;
            const double rotateMarkerSize = 10;
            const double rotateMarkerOffset = 28;

            if (TryConvertCursorMetric(metric, rotation, out var cursorValue))
            {
                return cursorValue;
            }

            if (TryConvertChromeMetric(
                    metric,
                    width,
                    height,
                    rotation,
                    type,
                    fontSize,
                    content,
                    handleSize,
                    rotateMarkerSize,
                    rotateMarkerOffset,
                    out var chromeValue))
            {
                return chromeValue;
            }

            return metric switch
            {
                "VisualLeft" => DesignerInteractionHelper.GetVisualLeft(x, y, width, height, rotation, type, fontSize, content),
                "VisualTop" => DesignerInteractionHelper.GetVisualTop(x, y, width, height, rotation, type, fontSize, content),
                "VisualWidth" => DesignerInteractionHelper.GetVisualWidth(width, height, rotation, type, fontSize, content),
                "VisualHeight" => DesignerInteractionHelper.GetVisualHeight(width, height, rotation, type, fontSize, content),
                "LocalWidth" => local.Width,
                "LocalHeight" => local.Height,
                "TextFitFontSize" => DesignerInteractionHelper.MeasureTextLayout(
                    local.Width,
                    local.Height,
                    fontSize,
                    content,
                    isBold,
                    isCentered,
                    rotation).FontSize,
                "TextWrapping" => DesignerInteractionHelper.ShouldWrapText(rotation) ? TextWrapping.Wrap : TextWrapping.NoWrap,
                "TextLayoutWidth" => DesignerInteractionHelper.ShouldWrapText(rotation)
                    ? DesignerInteractionHelper.MeasureTextLayout(
                        local.Width,
                        local.Height,
                        fontSize,
                        content,
                        isBold,
                        isCentered,
                        rotation).ContentWidth
                    : double.NaN,
                _ => DependencyProperty.UnsetValue
            };
        }

        private static bool TryConvertChromeMetric(
            string metric,
            double width,
            double height,
            int rotation,
            ElementType type,
            double fontSize,
            string content,
            double handleSize,
            double rotateMarkerSize,
            double rotateMarkerOffset,
            out double value)
        {
            value = 0;

            var parts = metric.Split('.');
            if (parts.Length != 2)
                return false;

            if (parts[0].Equals("RotateMarker", StringComparison.OrdinalIgnoreCase))
            {
                var marker = DesignerInteractionHelper.GetRotateMarkerPoint(
                    width,
                    height,
                    rotation,
                    type,
                    fontSize,
                    content,
                    rotateMarkerOffset);

                value = parts[1].Equals("Left", StringComparison.OrdinalIgnoreCase)
                    ? marker.X - rotateMarkerSize / 2
                    : marker.Y - rotateMarkerSize / 2;
                return parts[1].Equals("Left", StringComparison.OrdinalIgnoreCase) ||
                       parts[1].Equals("Top", StringComparison.OrdinalIgnoreCase);
            }

            if (!Enum.TryParse<ResizeHandleDirection>(parts[0], out var handle))
                return false;

            if (parts[1].Equals("Cursor", StringComparison.OrdinalIgnoreCase))
            {
                value = 0;
                return false;
            }

            var point = DesignerInteractionHelper.GetChromePoint(
                width,
                height,
                rotation,
                type,
                fontSize,
                content,
                handle);

            value = parts[1].Equals("Left", StringComparison.OrdinalIgnoreCase)
                ? point.X - handleSize / 2
                : point.Y - handleSize / 2;

            return parts[1].Equals("Left", StringComparison.OrdinalIgnoreCase) ||
                   parts[1].Equals("Top", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryConvertCursorMetric(string metric, int rotation, out object value)
        {
            value = DependencyProperty.UnsetValue;
            var parts = metric.Split('.');
            if (parts.Length != 2 || !parts[1].Equals("Cursor", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!Enum.TryParse<ResizeHandleDirection>(parts[0], out var handle))
                return false;

            value = DesignerInteractionHelper.GetResizeCursor(handle, rotation);
            return true;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
