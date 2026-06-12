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
            double inverseZoom = 1.0;
            bool isBold = false;
            bool isCentered = false;

            for (int i = 8; i < values.Length; i++)
            {
                if (values[i] is bool b)
                {
                    if (i == 8) isBold = b;
                    else if (i == 9) isCentered = b;
                }
                else if (values[i] is double d)
                {
                    inverseZoom = d;
                }
                else if (values[i] is float f)
                {
                    inverseZoom = f;
                }
            }

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
                    rotateMarkerOffset * inverseZoom,
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
                "TextWrapping" => (width > 0) ? TextWrapping.Wrap : TextWrapping.NoWrap,
                "TextLayoutWidth" => (width > 0)
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

            var local = DesignerInteractionHelper.GetLocalSize(width, height, type, fontSize, content, rotation);

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

            if (parts[0].Equals("RotateLine", StringComparison.OrdinalIgnoreCase))
            {
                var topCenter = DesignerInteractionHelper.GetChromePoint(
                    width,
                    height,
                    rotation,
                    type,
                    fontSize,
                    content,
                    ResizeHandleDirection.Top);

                var marker = DesignerInteractionHelper.GetRotateMarkerPoint(
                    width,
                    height,
                    rotation,
                    type,
                    fontSize,
                    content,
                    rotateMarkerOffset);

                if (parts[1].Equals("X1", StringComparison.OrdinalIgnoreCase))
                {
                    value = topCenter.X;
                    return true;
                }
                if (parts[1].Equals("Y1", StringComparison.OrdinalIgnoreCase))
                {
                    value = topCenter.Y;
                    return true;
                }
                if (parts[1].Equals("X2", StringComparison.OrdinalIgnoreCase))
                {
                    value = marker.X;
                    return true;
                }
                if (parts[1].Equals("Y2", StringComparison.OrdinalIgnoreCase))
                {
                    value = marker.Y;
                    return true;
                }
                return false;
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

        private static int GetHandleX(ResizeHandleDirection handle)
        {
            return handle switch
            {
                ResizeHandleDirection.TopLeft or ResizeHandleDirection.Left or ResizeHandleDirection.BottomLeft => -1,
                ResizeHandleDirection.TopRight or ResizeHandleDirection.Right or ResizeHandleDirection.BottomRight => 1,
                _ => 0
            };
        }

        private static int GetHandleY(ResizeHandleDirection handle)
        {
            return handle switch
            {
                ResizeHandleDirection.TopLeft or ResizeHandleDirection.Top or ResizeHandleDirection.TopRight => -1,
                ResizeHandleDirection.BottomLeft or ResizeHandleDirection.Bottom or ResizeHandleDirection.BottomRight => 1,
                _ => 0
            };
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
