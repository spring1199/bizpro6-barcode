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

            bool isBold = values.Length > 8 && values[8] is bool b8 ? b8 : false;
            bool isCentered = values.Length > 9 && values[9] is bool b9 ? b9 : false;
            double templateWidth = values.Length > 10 && values[10] is double d10 ? d10 : 0;
            bool isAutoWidth = values.Length > 11 && values[11] is bool b11 ? b11 : (type == ElementType.Text || width <= 0);
            bool isAutoHeight = values.Length > 12 && values[12] is bool b12 ? b12 : (type == ElementType.Text || height <= 0);
            
            int printerDpi = 203;
            if (values.Length > 13 && values[13] is int dpi13)
            {
                printerDpi = dpi13;
            }

            double inverseZoom = 1.0;
            if (values.Length > 14)
            {
                if (values[14] is double d14) inverseZoom = d14;
                else if (values[14] is float f14) inverseZoom = f14;
            }

            var metric = parameter?.ToString() ?? string.Empty;

            var local = DesignerInteractionHelper.GetLocalSize(width, height, type, fontSize, content, isAutoWidth, isAutoHeight, rotation, printerDpi, templateWidth);
            if (isAutoWidth && templateWidth > 0 && local.Width > templateWidth)
            {
                local.Width = templateWidth;
            }
            var visualBounds = DesignerInteractionHelper.GetVisualBounds(x, y, local.Width, local.Height, rotation);
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
                    isAutoWidth,
                    isAutoHeight,
                    printerDpi,
                    handleSize,
                    rotateMarkerSize,
                    rotateMarkerOffset * inverseZoom,
                    templateWidth,
                    out var chromeValue))
            {
                return chromeValue;
            }

            return metric switch
            {
                "VisualLeft" => isCentered && templateWidth > 0
                    ? (templateWidth - visualBounds.Width) / 2
                    : visualBounds.Left,
                "VisualTop" => visualBounds.Top,
                "VisualWidth" => visualBounds.Width,
                "VisualHeight" => visualBounds.Height,
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
            bool isAutoWidth,
            bool isAutoHeight,
            int printerDpi,
            double handleSize,
            double rotateMarkerSize,
            double rotateMarkerOffset,
            double templateWidth,
            out double value)
        {
            value = 0;

            var parts = metric.Split('.');
            if (parts.Length != 2)
                return false;

            var local = DesignerInteractionHelper.GetLocalSize(width, height, type, fontSize, content, isAutoWidth, isAutoHeight, rotation, printerDpi, templateWidth);
            var bounds = DesignerInteractionHelper.GetVisualBounds(0, 0, local.Width, local.Height, rotation);

            if (parts[0].Equals("RotateMarker", StringComparison.OrdinalIgnoreCase))
            {
                var localPoint = new Vector(0, -local.Height / 2);
                var localOutward = new Vector(0, -Math.Max(0, rotateMarkerOffset));
                var rotatedPoint = DesignerInteractionHelper.RotateVector(localPoint + localOutward, rotation);
                var marker = new Point(bounds.Width / 2 + rotatedPoint.X, bounds.Height / 2 + rotatedPoint.Y);

                value = parts[1].Equals("Left", StringComparison.OrdinalIgnoreCase)
                    ? marker.X - rotateMarkerSize / 2
                    : marker.Y - rotateMarkerSize / 2;
                return parts[1].Equals("Left", StringComparison.OrdinalIgnoreCase) ||
                       parts[1].Equals("Top", StringComparison.OrdinalIgnoreCase);
            }

            if (parts[0].Equals("RotateLine", StringComparison.OrdinalIgnoreCase))
            {
                var localTopCenter = new Vector(0, -local.Height / 2);
                var rotatedTopCenter = DesignerInteractionHelper.RotateVector(localTopCenter, rotation);
                var topCenter = new Point(bounds.Width / 2 + rotatedTopCenter.X, bounds.Height / 2 + rotatedTopCenter.Y);

                var localPoint = new Vector(0, -local.Height / 2);
                var localOutward = new Vector(0, -Math.Max(0, rotateMarkerOffset));
                var rotatedPoint = DesignerInteractionHelper.RotateVector(localPoint + localOutward, rotation);
                var marker = new Point(bounds.Width / 2 + rotatedPoint.X, bounds.Height / 2 + rotatedPoint.Y);

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

            var localHandleOffset = new Vector(
                GetHandleX(handle) * local.Width / 2,
                GetHandleY(handle) * local.Height / 2);
            var rotatedHandleOffset = DesignerInteractionHelper.RotateVector(localHandleOffset, rotation);
            var point = new Point(bounds.Width / 2 + rotatedHandleOffset.X, bounds.Height / 2 + rotatedHandleOffset.Y);

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
