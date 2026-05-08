using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using BarTenderClone.Models;

namespace BarTenderClone.Helpers
{
    internal static class DesignerInteractionHelper
    {
        public const double MinElementWidth = 20;
        public const double MinElementHeight = 10;
        public const double TextRenderInset = 4;

        private const double SnapThresholdScreenPixels = 6;
        private const double MinTextFontSize = 6;
        private const double MaxTextFontSize = 72;
        private const double TextMeasurePaddingFactor = 0.8;

        public static Vector RotateVector(Vector vector, double degrees)
        {
            var radians = degrees * Math.PI / 180.0;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);

            return new Vector(
                (vector.X * cos) - (vector.Y * sin),
                (vector.X * sin) + (vector.Y * cos));
        }

        public static (double Width, double Height) GetLocalSize(LabelElement element)
        {
            return GetLocalSize(
                element.Width,
                element.Height,
                element.Type,
                element.FontSize,
                element.Content);
        }

        public static (double Width, double Height) GetLocalSize(
            double width,
            double height,
            ElementType type,
            double fontSize,
            string? content)
        {
            var localWidth = type == ElementType.Text
                ? Math.Max(Math.Max(width, MinElementWidth), GetMinimumDisplayWidth(type, fontSize, content))
                : Math.Max(width, MinElementWidth);
            var minimumDisplayHeight = type == ElementType.Text
                ? GetMinimumDisplayHeight(localWidth, type, fontSize, content)
                : height > 0
                    ? GetMinimumLineHeight(type, fontSize)
                    : GetMinimumDisplayHeight(localWidth, type, fontSize, content);
            var localHeight = height > 0
                ? Math.Max(height, minimumDisplayHeight)
                : minimumDisplayHeight;

            return (localWidth, Math.Max(localHeight, MinElementHeight));
        }

        public static Rect GetVisualBounds(LabelElement element)
        {
            var (width, height) = GetLocalSize(element);
            return GetVisualBounds(element.X, element.Y, width, height, element.RotationDegrees);
        }

        public static Rect GetVisualBounds(
            double x,
            double y,
            double width,
            double height,
            int rotationDegrees)
        {
            var bounds = GetRotatedBounds(width, height, rotationDegrees);
            var centerX = x + width / 2;
            var centerY = y + height / 2;

            return new Rect(
                centerX - bounds.Width / 2,
                centerY - bounds.Height / 2,
                bounds.Width,
                bounds.Height);
        }

        public static double GetVisualLeft(
            double x,
            double y,
            double width,
            double height,
            int rotationDegrees,
            ElementType type,
            double fontSize,
            string? content)
        {
            var local = GetLocalSize(width, height, type, fontSize, content);
            return GetVisualBounds(x, y, local.Width, local.Height, rotationDegrees).Left;
        }

        public static double GetVisualTop(
            double x,
            double y,
            double width,
            double height,
            int rotationDegrees,
            ElementType type,
            double fontSize,
            string? content)
        {
            var local = GetLocalSize(width, height, type, fontSize, content);
            return GetVisualBounds(x, y, local.Width, local.Height, rotationDegrees).Top;
        }

        public static double GetVisualWidth(
            double width,
            double height,
            int rotationDegrees,
            ElementType type,
            double fontSize,
            string? content)
        {
            var local = GetLocalSize(width, height, type, fontSize, content);
            return GetRotatedBounds(local.Width, local.Height, rotationDegrees).Width;
        }

        public static double GetVisualHeight(
            double width,
            double height,
            int rotationDegrees,
            ElementType type,
            double fontSize,
            string? content)
        {
            var local = GetLocalSize(width, height, type, fontSize, content);
            return GetRotatedBounds(local.Width, local.Height, rotationDegrees).Height;
        }

        public static Point GetChromePoint(
            double width,
            double height,
            int rotationDegrees,
            ElementType type,
            double fontSize,
            string? content,
            ResizeHandleDirection handle)
        {
            var local = GetLocalSize(width, height, type, fontSize, content);
            var bounds = GetRotatedBounds(local.Width, local.Height, rotationDegrees);
            var localPoint = new Vector(
                GetHandleX(handle) * local.Width / 2,
                GetHandleY(handle) * local.Height / 2);
            var rotatedPoint = RotateVector(localPoint, rotationDegrees);

            return new Point(
                bounds.Width / 2 + rotatedPoint.X,
                bounds.Height / 2 + rotatedPoint.Y);
        }

        public static Point GetRotateMarkerPoint(
            double width,
            double height,
            int rotationDegrees,
            ElementType type,
            double fontSize,
            string? content,
            double offset)
        {
            var local = GetLocalSize(width, height, type, fontSize, content);
            var bounds = GetRotatedBounds(local.Width, local.Height, rotationDegrees);
            var localPoint = new Vector(0, -local.Height / 2);
            var localOutward = new Vector(0, -Math.Max(0, offset));
            var rotatedPoint = RotateVector(localPoint + localOutward, rotationDegrees);

            return new Point(
                bounds.Width / 2 + rotatedPoint.X,
                bounds.Height / 2 + rotatedPoint.Y);
        }

        public static void MoveElement(
            LabelElement element,
            LabelTemplate template,
            double deltaX,
            double deltaY,
            double zoom)
        {
            var x = element.X + deltaX;
            var y = element.Y + deltaY;
            var local = GetLocalSize(element);
            var visualBounds = GetVisualBounds(x, y, local.Width, local.Height, element.RotationDegrees);

            var snappedVisual = SnapPosition(visualBounds.Left, visualBounds.Top, visualBounds.Width, visualBounds.Height, template, zoom);
            visualBounds = new Rect(snappedVisual.x, snappedVisual.y, visualBounds.Width, visualBounds.Height);
            visualBounds = ClampPosition(visualBounds, template);

            element.X = x + (visualBounds.Left - GetVisualBounds(x, y, local.Width, local.Height, element.RotationDegrees).Left);
            element.Y = y + (visualBounds.Top - GetVisualBounds(x, y, local.Width, local.Height, element.RotationDegrees).Top);
        }

        public static void ResizeElementFromSnapshot(
            LabelElement element,
            LabelTemplate template,
            ResizeHandleDirection handle,
            double startX,
            double startY,
            double startWidth,
            double startHeight,
            double startFontSize,
            int startRotationDegrees,
            Vector cumulativeScreenDelta,
            double zoom)
        {
            var handleX = GetHandleX(handle);
            var handleY = GetHandleY(handle);
            if (handleX == 0 && handleY == 0)
                return;

            var originalWidth = Math.Max(startWidth, MinElementWidth);
            var originalHeight = Math.Max(startHeight, MinElementHeight);
            var originalCenter = new Point(
                startX + originalWidth / 2,
                startY + originalHeight / 2);

            var localDelta = RotateVector(cumulativeScreenDelta, -startRotationDegrees);
            var newWidth = originalWidth;
            var newHeight = originalHeight;

            if (handleX != 0)
                newWidth = Math.Max(MinElementWidth, originalWidth + (handleX * localDelta.X));

            if (handleY != 0)
                newHeight = Math.Max(MinElementHeight, originalHeight + (handleY * localDelta.Y));

            var widthDelta = newWidth - originalWidth;
            var heightDelta = newHeight - originalHeight;
            var localCenterShift = new Vector(
                handleX == 0 ? 0 : handleX * widthDelta / 2,
                handleY == 0 ? 0 : handleY * heightDelta / 2);
            var screenCenterShift = RotateVector(localCenterShift, startRotationDegrees);
            var newDesignX = originalCenter.X + screenCenterShift.X - newWidth / 2;
            var newDesignY = originalCenter.Y + screenCenterShift.Y - newHeight / 2;
            var visualRect = GetVisualBounds(newDesignX, newDesignY, newWidth, newHeight, startRotationDegrees);
            var visualHandle = RotateVector(new Vector(handleX, handleY), startRotationDegrees);
            var visualHandleX = ToSign(visualHandle.X);
            var visualHandleY = ToSign(visualHandle.Y);

            visualRect = SnapResize(visualRect, visualHandleX, visualHandleY, template, zoom, startRotationDegrees);
            visualRect = ClampResize(visualRect, visualHandleX, visualHandleY, template, startRotationDegrees);
            var finalLocal = GetLocalSizeFromVisualBounds(visualRect, startRotationDegrees);

            element.X = visualRect.Left + visualRect.Width / 2 - finalLocal.Width / 2;
            element.Y = visualRect.Top + visualRect.Height / 2 - finalLocal.Height / 2;
            element.Width = finalLocal.Width;
            element.Height = finalLocal.Height;

            if (element.Type == ElementType.Text && handleX != 0 && handleY != 0 && startFontSize > 0)
            {
                var widthScale = finalLocal.Width / Math.Max(originalWidth, MinElementWidth);
                var heightScale = finalLocal.Height / Math.Max(originalHeight, MinElementHeight);
                var scale = Math.Max(0.01, Math.Min(widthScale, heightScale));
                element.FontSize = Math.Clamp(startFontSize * scale, MinTextFontSize, MaxTextFontSize);
            }
        }

        private static (double x, double y) SnapPosition(
            double x,
            double y,
            double width,
            double height,
            LabelTemplate template,
            double zoom)
        {
            var threshold = GetSnapThreshold(zoom);
            var grid = LabelSizeHelper.MmToScreenPixels(1);

            var snappedX = SnapToGrid(x, grid);
            var snappedY = SnapToGrid(y, grid);

            snappedX = SnapAxisPosition(x, width, GetHorizontalSnapTargets(template), threshold)
                ?? snappedX;
            snappedY = SnapAxisPosition(y, height, GetVerticalSnapTargets(template), threshold)
                ?? snappedY;

            return (snappedX, snappedY);
        }

        private static Rect SnapResize(
            Rect rect,
            int handleX,
            int handleY,
            LabelTemplate template,
            double zoom,
            int rotationDegrees)
        {
            var threshold = GetSnapThreshold(zoom);
            var grid = LabelSizeHelper.MmToScreenPixels(1);
            var left = rect.Left;
            var top = rect.Top;
            var right = rect.Right;
            var bottom = rect.Bottom;
            var minVisual = GetRotatedBounds(MinElementWidth, MinElementHeight, rotationDegrees);

            if (handleX < 0)
                left = SnapEdge(left, right, GetHorizontalSnapTargets(template), grid, threshold, minVisual.Width);
            else if (handleX > 0)
                right = SnapEdge(right, left, GetHorizontalSnapTargets(template), grid, threshold, minVisual.Width);

            if (handleY < 0)
                top = SnapEdge(top, bottom, GetVerticalSnapTargets(template), grid, threshold, minVisual.Height);
            else if (handleY > 0)
                bottom = SnapEdge(bottom, top, GetVerticalSnapTargets(template), grid, threshold, minVisual.Height);

            var width = Math.Max(minVisual.Width, right - left);
            var height = Math.Max(minVisual.Height, bottom - top);

            if (handleX < 0)
                left = right - width;
            else
                right = left + width;

            if (handleY < 0)
                top = bottom - height;
            else
                bottom = top + height;

            return new Rect(left, top, right - left, bottom - top);
        }

        private static Rect ClampPosition(Rect rect, LabelTemplate template)
        {
            var maxX = Math.Max(0, template.Width - rect.Width);
            var maxY = Math.Max(0, template.Height - rect.Height);

            return new Rect(
                Math.Clamp(rect.X, 0, maxX),
                Math.Clamp(rect.Y, 0, maxY),
                rect.Width,
                rect.Height);
        }

        private static Rect ClampResize(Rect rect, int handleX, int handleY, LabelTemplate template, int rotationDegrees)
        {
            var left = rect.Left;
            var top = rect.Top;
            var right = rect.Right;
            var bottom = rect.Bottom;
            var minVisual = GetRotatedBounds(MinElementWidth, MinElementHeight, rotationDegrees);

            if (handleX < 0)
                left = Math.Clamp(left, 0, right - minVisual.Width);
            else if (handleX > 0)
                right = Math.Clamp(right, left + minVisual.Width, template.Width);

            if (handleY < 0)
                top = Math.Clamp(top, 0, bottom - minVisual.Height);
            else if (handleY > 0)
                bottom = Math.Clamp(bottom, top + minVisual.Height, template.Height);

            left = Math.Clamp(left, 0, Math.Max(0, template.Width - minVisual.Width));
            top = Math.Clamp(top, 0, Math.Max(0, template.Height - minVisual.Height));
            right = Math.Clamp(right, left + minVisual.Width, template.Width);
            bottom = Math.Clamp(bottom, top + minVisual.Height, template.Height);

            return new Rect(left, top, right - left, bottom - top);
        }

        private static double? SnapAxisPosition(
            double origin,
            double size,
            IReadOnlyList<double> targets,
            double threshold)
        {
            var candidates = new[]
            {
                new SnapCandidate(origin, target => target),
                new SnapCandidate(origin + size / 2, target => target - size / 2),
                new SnapCandidate(origin + size, target => target - size)
            };

            return FindBestSnap(candidates, targets, threshold);
        }

        private static double SnapEdge(
            double activeEdge,
            double fixedEdge,
            IReadOnlyList<double> targets,
            double grid,
            double threshold,
            double minDistance)
        {
            var snappedEdge = SnapToGrid(activeEdge, grid);
            var edgeSnap = FindBestSnap(
                new[] { new SnapCandidate(activeEdge, target => target) },
                targets,
                threshold);

            snappedEdge = edgeSnap ?? snappedEdge;

            if (activeEdge < fixedEdge)
                return Math.Min(snappedEdge, fixedEdge - minDistance);

            return Math.Max(snappedEdge, fixedEdge + minDistance);
        }

        private static double? FindBestSnap(
            IEnumerable<SnapCandidate> candidates,
            IReadOnlyList<double> targets,
            double threshold)
        {
            double? bestValue = null;
            var bestDistance = threshold;

            foreach (var candidate in candidates)
            {
                foreach (var target in targets)
                {
                    var distance = Math.Abs(candidate.CurrentValue - target);
                    if (distance <= bestDistance)
                    {
                        bestDistance = distance;
                        bestValue = candidate.ToOrigin(target);
                    }
                }
            }

            return bestValue;
        }

        private static IReadOnlyList<double> GetHorizontalSnapTargets(LabelTemplate template)
        {
            var margins = LabelSizeHelper.GetSafeMarginsPixels();
            return new[]
            {
                0,
                margins.left,
                template.Width / 2,
                Math.Max(0, template.Width - margins.right),
                template.Width
            };
        }

        private static IReadOnlyList<double> GetVerticalSnapTargets(LabelTemplate template)
        {
            var margins = LabelSizeHelper.GetSafeMarginsPixels();
            return new[]
            {
                0,
                margins.top,
                template.Height / 2,
                Math.Max(0, template.Height - margins.bottom),
                template.Height
            };
        }

        private static double GetSnapThreshold(double zoom)
        {
            return SnapThresholdScreenPixels / Math.Max(zoom, 0.1);
        }

        private static double SnapToGrid(double value, double gridSize)
        {
            if (gridSize <= 0)
                return value;

            return Math.Round(value / gridSize) * gridSize;
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

        private static (double Width, double Height) GetRotatedBounds(double width, double height, int rotationDegrees)
        {
            var rotation = LabelElement.NormalizeRotationDegrees(rotationDegrees);
            return rotation is 90 or 270
                ? (Math.Max(height, MinElementHeight), Math.Max(width, MinElementWidth))
                : (Math.Max(width, MinElementWidth), Math.Max(height, MinElementHeight));
        }

        private static (double Width, double Height) GetLocalSizeFromVisualBounds(Rect visualBounds, int rotationDegrees)
        {
            var rotation = LabelElement.NormalizeRotationDegrees(rotationDegrees);
            return rotation is 90 or 270
                ? (Math.Max(visualBounds.Height, MinElementWidth), Math.Max(visualBounds.Width, MinElementHeight))
                : (Math.Max(visualBounds.Width, MinElementWidth), Math.Max(visualBounds.Height, MinElementHeight));
        }

        public static double GetMinimumDisplayHeight(ElementType type, double fontSize, double width, string? content = null)
        {
            return GetMinimumDisplayHeight(Math.Max(width, MinElementWidth), type, fontSize, content);
        }

        public static double GetMinimumLineHeight(ElementType type, double fontSize)
        {
            if (type != ElementType.Text)
                return MinElementHeight;

            return Math.Max(8, fontSize) * LabelSizeHelper.FONT_SCALING_FACTOR * 1.35;
        }

        private static double GetMinimumDisplayWidth(ElementType type, double fontSize, string? content)
        {
            if (type != ElementType.Text)
                return MinElementWidth;

            var measuredWidth = MeasureLongestUnwrappedTextWidth(fontSize, content);
            return Math.Max(MinElementWidth, measuredWidth);
        }

        private static double GetMinimumDisplayHeight(double width, ElementType type, double fontSize, string? content)
        {
            if (type != ElementType.Text)
                return MinElementHeight;

            var lineHeight = GetMinimumLineHeight(type, fontSize);
            var measuredHeight = MeasureWrappedTextHeight(width, fontSize, content);

            return Math.Max(lineHeight, measuredHeight);
        }

        private static double MeasureWrappedTextHeight(double width, double fontSize, string? content)
        {
            var effectiveFontSize = Math.Max(8, fontSize) * LabelSizeHelper.FONT_SCALING_FACTOR;
            var safeWidth = Math.Max(MinElementWidth, width);
            var contentWidth = Math.Max(1, safeWidth - TextRenderInset * 2);
            var safeContent = string.IsNullOrEmpty(content) ? " " : content;

            try
            {
                var formattedText = new FormattedText(
                    safeContent,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    new Typeface(
                        new FontFamily("Segoe UI"),
                        FontStyles.Normal,
                        FontWeights.Bold,
                        FontStretches.Normal),
                    effectiveFontSize,
                    Brushes.Black,
                    1.0)
                {
                    MaxTextWidth = contentWidth,
                    Trimming = TextTrimming.None
                };

                return Math.Ceiling(formattedText.Height + GetTextMeasurePadding(effectiveFontSize) + TextRenderInset * 2);
            }
            catch
            {
                var averageCharWidth = Math.Max(4, effectiveFontSize * 0.55);
                var charsPerLine = Math.Max(1, (int)Math.Floor(safeWidth / averageCharWidth));
                var estimatedLines = Math.Max(1, (int)Math.Ceiling((double)safeContent.Length / charsPerLine));
                return GetMinimumLineHeight(ElementType.Text, fontSize) * estimatedLines;
            }
        }

        private static double MeasureLongestUnwrappedTextWidth(double fontSize, string? content)
        {
            var effectiveFontSize = Math.Max(8, fontSize) * LabelSizeHelper.FONT_SCALING_FACTOR;
            var padding = GetTextMeasurePadding(effectiveFontSize);
            var safeContent = string.IsNullOrWhiteSpace(content) ? " " : content;
            var tokens = safeContent
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .DefaultIfEmpty(safeContent);

            try
            {
                var maxWidth = 0.0;
                foreach (var token in tokens)
                {
                    var formattedText = new FormattedText(
                        token,
                        CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight,
                        new Typeface(
                            new FontFamily("Segoe UI"),
                            FontStyles.Normal,
                            FontWeights.Bold,
                            FontStretches.Normal),
                        effectiveFontSize,
                        Brushes.Black,
                        1.0)
                    {
                        Trimming = TextTrimming.None
                    };

                    maxWidth = Math.Max(maxWidth, formattedText.WidthIncludingTrailingWhitespace);
                }

                return Math.Ceiling(maxWidth + padding);
            }
            catch
            {
                var longestTokenLength = tokens.Max(token => token.Length);
                return Math.Ceiling(longestTokenLength * Math.Max(4, effectiveFontSize * 0.58) + padding);
            }
        }

        private static double GetTextMeasurePadding(double effectiveFontSize)
        {
            return Math.Max(4, effectiveFontSize * TextMeasurePaddingFactor);
        }

        private static int ToSign(double value)
        {
            if (value > 0.1)
                return 1;

            if (value < -0.1)
                return -1;

            return 0;
        }

        private sealed record SnapCandidate(double CurrentValue, Func<double, double> ToOrigin);
    }

    internal enum ResizeHandleDirection
    {
        TopLeft,
        Top,
        TopRight,
        Right,
        BottomRight,
        Bottom,
        BottomLeft,
        Left
    }
}
