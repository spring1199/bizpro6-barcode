using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BarTenderClone.Models;

namespace BarTenderClone.Helpers
{
    internal static class DesignerInteractionHelper
    {
        public const double MinElementWidth = 20;
        public const double MinElementHeight = 10;
        public const double TextRenderInset = 4;
        public const double SelectionChromePadding = 8;

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
                element.Content,
                element.RotationDegrees);
        }

        public static (double Width, double Height) GetLocalSize(
            double width,
            double height,
            ElementType type,
            double fontSize,
            string? content,
            int rotationDegrees = 0)
        {
            var localWidth = type == ElementType.Text && width <= 0
                ? ShouldWrapText(rotationDegrees)
                    ? Math.Max(GetMinimumDisplayWidth(type, fontSize, content), MinElementWidth)
                    : Math.Max(MeasureFullTextLineWidth(fontSize, content), MinElementWidth)
                : Math.Max(width, MinElementWidth);
            var minimumDisplayHeight = type == ElementType.Text
                ? height > 0
                    ? MinElementHeight
                    : GetMinimumLineHeight(type, fontSize)
                : height > 0
                    ? MinElementHeight
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

        public static void CommitMeasuredLocalSize(LabelElement element)
        {
            var local = GetLocalSize(element);
            if (Math.Abs(element.Width - local.Width) > 0.01)
                element.Width = local.Width;

            if (Math.Abs(element.Height - local.Height) > 0.01)
                element.Height = local.Height;
        }

        public static void ClampElementToTemplate(LabelElement element, LabelTemplate template, double padding = SelectionChromePadding)
        {
            var local = GetLocalSize(element);
            var bounds = GetVisualBounds(element.X, element.Y, local.Width, local.Height, element.RotationDegrees);
            var minLeft = Math.Min(padding, Math.Max(0, template.Width - bounds.Width));
            var minTop = Math.Min(padding, Math.Max(0, template.Height - bounds.Height));
            var maxRight = Math.Max(minLeft, template.Width - padding);
            var maxBottom = Math.Max(minTop, template.Height - padding);

            var deltaX = 0.0;
            var deltaY = 0.0;

            if (bounds.Width + padding * 2 <= template.Width)
            {
                if (bounds.Left < minLeft)
                    deltaX = minLeft - bounds.Left;
                else if (bounds.Right > maxRight)
                    deltaX = maxRight - bounds.Right;
            }
            else
            {
                deltaX = (template.Width - bounds.Width) / 2 - bounds.Left;
            }

            if (bounds.Height + padding * 2 <= template.Height)
            {
                if (bounds.Top < minTop)
                    deltaY = minTop - bounds.Top;
                else if (bounds.Bottom > maxBottom)
                    deltaY = maxBottom - bounds.Bottom;
            }
            else
            {
                deltaY = (template.Height - bounds.Height) / 2 - bounds.Top;
            }

            if (Math.Abs(deltaX) > 0.01)
                element.X += deltaX;

            if (Math.Abs(deltaY) > 0.01)
                element.Y += deltaY;
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
            var local = GetLocalSize(width, height, type, fontSize, content, rotationDegrees);
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
            var local = GetLocalSize(width, height, type, fontSize, content, rotationDegrees);
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
            var local = GetLocalSize(width, height, type, fontSize, content, rotationDegrees);
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
            var local = GetLocalSize(width, height, type, fontSize, content, rotationDegrees);
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
            var local = GetLocalSize(width, height, type, fontSize, content, rotationDegrees);
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
            var local = GetLocalSize(width, height, type, fontSize, content, rotationDegrees);
            var bounds = GetRotatedBounds(local.Width, local.Height, rotationDegrees);
            var localPoint = new Vector(0, -local.Height / 2);
            var localOutward = new Vector(0, -Math.Max(0, offset));
            var rotatedPoint = RotateVector(localPoint + localOutward, rotationDegrees);

            return new Point(
                bounds.Width / 2 + rotatedPoint.X,
                bounds.Height / 2 + rotatedPoint.Y);
        }

        public static Cursor GetResizeCursor(ResizeHandleDirection handle, int rotationDegrees)
        {
            var handleVector = RotateVector(new Vector(GetHandleX(handle), GetHandleY(handle)), rotationDegrees);
            var x = ToSign(handleVector.X);
            var y = ToSign(handleVector.Y);

            if (x != 0 && y != 0)
                return x == y ? Cursors.SizeNWSE : Cursors.SizeNESW;

            if (x != 0)
                return Cursors.SizeWE;

            return Cursors.SizeNS;
        }

        public static TextLayoutResult MeasureTextLayout(
            double width,
            double height,
            double fontSize,
            string? content,
            bool isBold = false,
            bool isCentered = false,
            int rotationDegrees = 0)
        {
            var localWidth = Math.Max(width, MinElementWidth);
            var localHeight = Math.Max(height, MinElementHeight);
            var inset = Math.Min(TextRenderInset, Math.Max(0, Math.Min(localWidth, localHeight) / 4));
            var contentWidth = Math.Max(1, localWidth - inset * 2);
            var contentHeight = Math.Max(1, localHeight - inset * 2);
            var maxFontSize = Math.Max(1, fontSize * LabelSizeHelper.FONT_SCALING_FACTOR);
            var minReadableFontSize = Math.Max(1, MinTextFontSize * LabelSizeHelper.FONT_SCALING_FACTOR);
            var safeContent = string.IsNullOrEmpty(content) ? " " : content;
            var wrapText = ShouldWrapText(rotationDegrees);

            var best = MeasureText(safeContent, maxFontSize, contentWidth, isBold, isCentered, wrapText);
            if (TextFits(best, contentWidth, contentHeight))
            {
                return new TextLayoutResult(maxFontSize, inset, contentWidth, contentHeight, best.Width, best.Height, true, wrapText);
            }

            var low = 1.0;
            var high = maxFontSize;
            var bestFont = low;
            TextMeasurement bestMeasurement = MeasureText(safeContent, low, contentWidth, isBold, isCentered, wrapText);
            for (var i = 0; i < 24; i++)
            {
                var mid = (low + high) / 2;
                var measurement = MeasureText(safeContent, mid, contentWidth, isBold, isCentered, wrapText);
                if (TextFits(measurement, contentWidth, contentHeight))
                {
                    bestFont = mid;
                    bestMeasurement = measurement;
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            if (bestFont < minReadableFontSize)
            {
                var readableMeasurement = MeasureText(safeContent, minReadableFontSize, contentWidth, isBold, isCentered, wrapText);
                if (TextFits(readableMeasurement, contentWidth, contentHeight))
                {
                    bestFont = minReadableFontSize;
                    bestMeasurement = readableMeasurement;
                }
            }

            return new TextLayoutResult(
                bestFont,
                inset,
                contentWidth,
                contentHeight,
                bestMeasurement.Width,
                bestMeasurement.Height,
                TextFits(bestMeasurement, contentWidth, contentHeight),
                wrapText);
        }

        public static bool ShouldWrapText(int rotationDegrees) => true;

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
            var candidate = BuildResizeCandidate(
                element,
                handleX,
                handleY,
                startX,
                startY,
                originalWidth,
                originalHeight,
                startFontSize,
                startRotationDegrees,
                cumulativeScreenDelta);

            if (!IsInsideTemplate(candidate.VisualBounds, template))
            {
                var low = 0.0;
                var high = 1.0;
                var best = BuildResizeCandidate(
                    element,
                    handleX,
                    handleY,
                    startX,
                    startY,
                    originalWidth,
                    originalHeight,
                    startFontSize,
                    startRotationDegrees,
                    new Vector());

                for (var i = 0; i < 18; i++)
                {
                    var mid = (low + high) / 2;
                    var probe = BuildResizeCandidate(
                        element,
                        handleX,
                        handleY,
                        startX,
                        startY,
                        originalWidth,
                        originalHeight,
                        startFontSize,
                        startRotationDegrees,
                        cumulativeScreenDelta * mid);

                    if (IsInsideTemplate(probe.VisualBounds, template))
                    {
                        best = probe;
                        low = mid;
                    }
                    else
                    {
                        high = mid;
                    }
                }

                candidate = best;
            }

            element.X = candidate.X;
            element.Y = candidate.Y;
            element.Width = candidate.Width;
            element.Height = candidate.Height;
            element.FontSize = candidate.FontSize;
        }

        private static ResizeCandidate BuildResizeCandidate(
            LabelElement element,
            int handleX,
            int handleY,
            double startX,
            double startY,
            double originalWidth,
            double originalHeight,
            double startFontSize,
            int startRotationDegrees,
            Vector cumulativeScreenDelta)
        {
            var originalCenter = new Point(
                startX + originalWidth / 2,
                startY + originalHeight / 2);
            var localDelta = RotateVector(cumulativeScreenDelta, -startRotationDegrees);

            var requestedWidth = handleX == 0
                ? originalWidth
                : Math.Max(MinElementWidth, originalWidth + (handleX * localDelta.X));
            var requestedHeight = handleY == 0
                ? originalHeight
                : Math.Max(MinElementHeight, originalHeight + (handleY * localDelta.Y));

            var fontSize = startFontSize;
            if (element.Type == ElementType.Text && handleX != 0 && handleY != 0 && startFontSize > 0)
            {
                var widthScale = requestedWidth / Math.Max(originalWidth, MinElementWidth);
                var heightScale = requestedHeight / Math.Max(originalHeight, MinElementHeight);
                var scale = Math.Max(0.01, Math.Min(widthScale, heightScale));
                fontSize = Math.Clamp(startFontSize * scale, MinTextFontSize, MaxTextFontSize);
            }

            var finalLocal = GetLocalSize(requestedWidth, requestedHeight, element.Type, fontSize, element.Content);
            var fixedLocal = new Vector(
                handleX == 0 ? 0 : -handleX * originalWidth / 2,
                handleY == 0 ? 0 : -handleY * originalHeight / 2);
            var activeCenterOffset = new Vector(
                handleX == 0 ? 0 : handleX * finalLocal.Width / 2,
                handleY == 0 ? 0 : handleY * finalLocal.Height / 2);
            var fixedScreen = originalCenter + RotateVector(fixedLocal, startRotationDegrees);
            var newCenter = fixedScreen + RotateVector(activeCenterOffset, startRotationDegrees);
            var x = newCenter.X - finalLocal.Width / 2;
            var y = newCenter.Y - finalLocal.Height / 2;

            return new ResizeCandidate(
                x,
                y,
                finalLocal.Width,
                finalLocal.Height,
                fontSize,
                GetVisualBounds(x, y, finalLocal.Width, finalLocal.Height, startRotationDegrees));
        }

        private static bool IsInsideTemplate(Rect rect, LabelTemplate template)
        {
            const double tolerance = 0.5;
            return rect.Left >= -tolerance &&
                   rect.Top >= -tolerance &&
                   rect.Right <= template.Width + tolerance &&
                   rect.Bottom <= template.Height + tolerance;
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

        private static double MeasureFullTextLineWidth(double fontSize, string? content)
        {
            var effectiveFontSize = Math.Max(8, fontSize) * LabelSizeHelper.FONT_SCALING_FACTOR;
            var padding = GetTextMeasurePadding(effectiveFontSize);
            var safeContent = string.IsNullOrWhiteSpace(content) ? " " : content;

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
                    Trimming = TextTrimming.None
                };
                return Math.Ceiling(formattedText.WidthIncludingTrailingWhitespace + padding);
            }
            catch
            {
                return Math.Ceiling(safeContent.Length * Math.Max(4, effectiveFontSize * 0.55) + padding);
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

        private static bool TextFits(TextMeasurement measurement, double contentWidth, double contentHeight)
        {
            const double tolerance = 0.75;
            return measurement.Width <= contentWidth + tolerance &&
                   measurement.Height <= contentHeight + tolerance;
        }

        private static TextMeasurement MeasureText(
            string content,
            double effectiveFontSize,
            double maxTextWidth,
            bool isBold,
            bool isCentered,
            bool wrapText)
        {
            try
            {
                var formattedText = new FormattedText(
                    content,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    new Typeface(
                        new FontFamily("Segoe UI"),
                        FontStyles.Normal,
                        isBold ? FontWeights.Bold : FontWeights.Normal,
                        FontStretches.Normal),
                    Math.Max(1, effectiveFontSize),
                    Brushes.Black,
                    1.0)
                {
                    TextAlignment = isCentered ? TextAlignment.Center : TextAlignment.Left,
                    Trimming = TextTrimming.None
                };
                if (wrapText)
                {
                    formattedText.MaxTextWidth = Math.Max(1, maxTextWidth);
                }

                return new TextMeasurement(
                    Math.Ceiling(formattedText.WidthIncludingTrailingWhitespace),
                    Math.Ceiling(formattedText.Height));
            }
            catch
            {
                var averageCharWidth = Math.Max(1, effectiveFontSize * 0.55);
                var lines = Math.Max(1, content.Count(c => c == '\n') + 1);
                if (wrapText)
                {
                    var charsPerLine = Math.Max(1, (int)Math.Floor(Math.Max(1, maxTextWidth) / averageCharWidth));
                    lines = Math.Max(1, (int)Math.Ceiling((double)Math.Max(1, content.Length) / charsPerLine));
                }

                return new TextMeasurement(
                    wrapText ? Math.Min(maxTextWidth, content.Length * averageCharWidth) : content.Length * averageCharWidth,
                    Math.Ceiling(effectiveFontSize * 1.35 * lines));
            }
        }

        private sealed record ResizeCandidate(
            double X,
            double Y,
            double Width,
            double Height,
            double FontSize,
            Rect VisualBounds);

        private sealed record TextMeasurement(double Width, double Height);
    }

    internal sealed record TextLayoutResult(
        double FontSize,
        double Inset,
        double ContentWidth,
        double ContentHeight,
        double MeasuredWidth,
        double MeasuredHeight,
        bool Fits,
        bool WrapText);

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
