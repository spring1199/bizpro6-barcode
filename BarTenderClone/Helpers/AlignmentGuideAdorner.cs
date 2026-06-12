using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using BarTenderClone.Models;

namespace BarTenderClone.Helpers
{
    // ──────────────────────────────────────────────────────────────────────
    //  Data types returned by the alignment calculation
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Orientation of a single guide line (horizontal or vertical).
    /// </summary>
    internal enum GuideOrientation
    {
        /// <summary>A vertical line drawn at a specific X coordinate.</summary>
        Vertical,

        /// <summary>A horizontal line drawn at a specific Y coordinate.</summary>
        Horizontal
    }

    /// <summary>
    /// Describes how the dragged element aligns with a reference edge.
    /// </summary>
    internal enum AlignmentEdge
    {
        Left,
        CenterX,
        Right,
        Top,
        CenterY,
        Bottom
    }

    /// <summary>
    /// A single guide line to be rendered on the canvas.
    /// </summary>
    internal sealed class GuideLine
    {
        /// <summary>Orientation (vertical or horizontal).</summary>
        public GuideOrientation Orientation { get; init; }

        /// <summary>
        /// The canvas-space coordinate where the guide is drawn.
        /// For <see cref="GuideOrientation.Vertical"/> this is the X value;
        /// for <see cref="GuideOrientation.Horizontal"/> this is the Y value.
        /// </summary>
        public double Position { get; init; }

        /// <summary>Which edge of the dragged element is aligned.</summary>
        public AlignmentEdge DraggedEdge { get; init; }

        /// <summary>
        /// The signed distance (in canvas units) that the dragged element was
        /// snapped.  Positive = moved right/down; negative = moved left/up.
        /// </summary>
        public double SnapDelta { get; init; }
    }

    /// <summary>
    /// Result of an alignment calculation.  Contains the (possibly snapped)
    /// position and every guide line that should be rendered.
    /// </summary>
    internal sealed class AlignmentResult
    {
        /// <summary>
        /// Snapped X position for the dragged element (visual-bounds left).
        /// <c>null</c> if no horizontal snap occurred.
        /// </summary>
        public double? SnappedX { get; init; }

        /// <summary>
        /// Snapped Y position for the dragged element (visual-bounds top).
        /// <c>null</c> if no vertical snap occurred.
        /// </summary>
        public double? SnappedY { get; init; }

        /// <summary>All active guide lines that should be drawn.</summary>
        public IReadOnlyList<GuideLine> Guides { get; init; } = Array.Empty<GuideLine>();

        /// <summary>An empty result with no guides and no snapping.</summary>
        public static AlignmentResult Empty { get; } = new()
        {
            SnappedX = null,
            SnappedY = null,
            Guides = Array.Empty<GuideLine>()
        };
    }

    // ──────────────────────────────────────────────────────────────────────
    //  The WPF Adorner
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A WPF <see cref="Adorner"/> that draws smart alignment guide lines on
    /// the design canvas while an element is being moved or resized.
    /// <para>
    /// Guide lines appear when the dragged element's edges or centre align
    /// with other elements or with the template boundaries.  Lines are
    /// rendered as magenta/pink dashed lines that extend across the full
    /// canvas, with small diamond-shaped distance indicators at snap points.
    /// </para>
    /// <para>
    /// <b>Usage:</b>
    /// <list type="number">
    ///   <item>Create the adorner once and add it to the adorner layer of
    ///         the design canvas.</item>
    ///   <item>During a drag operation call
    ///         <see cref="UpdateGuides"/> on every mouse-move to recalculate
    ///         and render the guides.</item>
    ///   <item>When the drag ends call <see cref="ClearGuides"/> to hide all
    ///         lines.</item>
    /// </list>
    /// </para>
    /// </summary>
    internal sealed class AlignmentGuideAdorner : Adorner
    {
        // ── Configurable constants ──────────────────────────────────────

        /// <summary>
        /// Default snap threshold in screen (device-independent) pixels.
        /// Edges that are within this distance (divided by the current zoom
        /// level) will be considered aligned.
        /// </summary>
        public const double DefaultSnapThresholdPixels = 4.0;

        // ── Rendering resources (frozen for thread-safety) ──────────────

        private static readonly Pen _guidePen;
        private static readonly Brush _indicatorBrush;
        private static readonly Pen _indicatorPen;
        private static readonly Typeface _labelTypeface;

        /// <summary>Half-size of the diamond indicator drawn at snap points.</summary>
        private const double IndicatorHalfSize = 3.5;

        /// <summary>Font size for the tiny distance labels (in DIPs).</summary>
        private const double LabelFontSize = 9.0;

        /// <summary>Gap between the snap-point indicator and the distance label.</summary>
        private const double LabelOffset = 6.0;

        static AlignmentGuideAdorner()
        {
            // Magenta dashed line ──────────────────────────────────────
            var guideColor = Color.FromArgb(200, 255, 0, 144); // vivid magenta-pink
            var guideBrush = new SolidColorBrush(guideColor);
            guideBrush.Freeze();

            _guidePen = new Pen(guideBrush, 1.0)
            {
                DashStyle = new DashStyle(new double[] { 6, 3 }, 0)
            };
            _guidePen.Freeze();

            // Indicator brush & pen ────────────────────────────────────
            _indicatorBrush = new SolidColorBrush(Color.FromArgb(220, 255, 0, 144));
            _indicatorBrush.Freeze();

            _indicatorPen = new Pen(_indicatorBrush, 1.0);
            _indicatorPen.Freeze();

            _labelTypeface = new Typeface(
                new FontFamily("Segoe UI"),
                FontStyles.Normal,
                FontWeights.Normal,
                FontStretches.Normal);
        }

        // ── Instance state ──────────────────────────────────────────────

        private AlignmentResult _currentResult = AlignmentResult.Empty;
        private double _canvasWidth;
        private double _canvasHeight;
        private double _zoom = 1.0;

        /// <summary>
        /// Creates a new <see cref="AlignmentGuideAdorner"/> attached to the
        /// specified <paramref name="adornedElement"/> (typically the design
        /// canvas or its container).
        /// </summary>
        public AlignmentGuideAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
            // The adorner must not participate in hit-testing so that
            // mouse events pass through to the canvas underneath.
            IsHitTestVisible = false;
        }

        // ── Public API ──────────────────────────────────────────────────

        /// <summary>
        /// Gets or sets the snap threshold in screen pixels.
        /// At higher zoom levels the effective threshold (in canvas units)
        /// is automatically reduced so that snapping feels consistent.
        /// </summary>
        public double SnapThresholdPixels { get; set; } = DefaultSnapThresholdPixels;

        /// <summary>
        /// Calculates alignment guides for the currently dragged element and
        /// triggers a re-render of the adorner.
        /// </summary>
        /// <param name="draggedElement">
        /// The <see cref="LabelElement"/> that is being moved / resized.
        /// </param>
        /// <param name="allElements">
        /// Every <see cref="LabelElement"/> on the canvas (including the
        /// dragged one — it will be automatically excluded from reference
        /// edge collection).
        /// </param>
        /// <param name="templateWidth">
        /// Width of the label template in canvas units.
        /// </param>
        /// <param name="templateHeight">
        /// Height of the label template in canvas units.
        /// </param>
        /// <param name="zoom">
        /// Current zoom / scale factor of the canvas (1.0 = 100 %).
        /// </param>
        /// <returns>
        /// An <see cref="AlignmentResult"/> that contains the snapped position
        /// (if any) and all active guide lines.  The caller can use
        /// <see cref="AlignmentResult.SnappedX"/> and
        /// <see cref="AlignmentResult.SnappedY"/> to apply the snap to the
        /// element's position.
        /// </returns>
        public AlignmentResult UpdateGuides(
            LabelElement draggedElement,
            IEnumerable<LabelElement> allElements,
            double templateWidth,
            double templateHeight,
            double zoom)
        {
            _canvasWidth = templateWidth;
            _canvasHeight = templateHeight;
            _zoom = Math.Max(zoom, 0.01);

            var result = CalculateAlignment(
                draggedElement, allElements, templateWidth, templateHeight, _zoom);

            _currentResult = result;
            InvalidateVisual();

            return result;
        }

        /// <summary>
        /// Hides all guide lines and resets the adorner state.
        /// Call this when the drag operation ends.
        /// </summary>
        public void ClearGuides()
        {
            _currentResult = AlignmentResult.Empty;
            InvalidateVisual();
        }

        // ── Rendering ───────────────────────────────────────────────────

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var guides = _currentResult.Guides;
            if (guides.Count == 0)
                return;

            foreach (var guide in guides)
            {
                DrawGuide(drawingContext, guide);
            }
        }

        /// <summary>
        /// Draws a single guide line across the full canvas extent, plus a
        /// small snap-point indicator and optional distance label.
        /// </summary>
        private void DrawGuide(DrawingContext dc, GuideLine guide)
        {
            Point start, end;

            if (guide.Orientation == GuideOrientation.Vertical)
            {
                // Vertical line at X = guide.Position, spanning full height
                start = new Point(guide.Position, 0);
                end = new Point(guide.Position, _canvasHeight);
            }
            else
            {
                // Horizontal line at Y = guide.Position, spanning full width
                start = new Point(0, guide.Position);
                end = new Point(_canvasWidth, guide.Position);
            }

            dc.DrawLine(_guidePen, start, end);

            // Draw a small diamond indicator at the guide's snap coordinate
            DrawSnapIndicator(dc, guide);

            // Draw distance label when a non-trivial snap occurred
            if (Math.Abs(guide.SnapDelta) > 0.1)
            {
                DrawDistanceLabel(dc, guide);
            }
        }

        /// <summary>
        /// Draws a small filled diamond at the point where the guide line
        /// intersects the dragged element's aligned edge.
        /// </summary>
        private void DrawSnapIndicator(DrawingContext dc, GuideLine guide)
        {
            // The indicator sits at the guide position on the relevant axis
            // and at the canvas centre on the other axis (so it's always
            // visible somewhere along the line).
            Point centre;
            if (guide.Orientation == GuideOrientation.Vertical)
                centre = new Point(guide.Position, _canvasHeight / 2);
            else
                centre = new Point(_canvasWidth / 2, guide.Position);

            // Adjust indicator size for zoom so it has a consistent screen size
            var halfSize = IndicatorHalfSize / Math.Max(_zoom, 0.1);

            var diamond = new StreamGeometry();
            using (var ctx = diamond.Open())
            {
                ctx.BeginFigure(new Point(centre.X, centre.Y - halfSize), true, true);
                ctx.LineTo(new Point(centre.X + halfSize, centre.Y), true, false);
                ctx.LineTo(new Point(centre.X, centre.Y + halfSize), true, false);
                ctx.LineTo(new Point(centre.X - halfSize, centre.Y), true, false);
            }
            diamond.Freeze();

            dc.DrawGeometry(_indicatorBrush, _indicatorPen, diamond);
        }

        /// <summary>
        /// Draws a tiny text label showing the snap distance in pixels next to
        /// the indicator.
        /// </summary>
        private void DrawDistanceLabel(DrawingContext dc, GuideLine guide)
        {
            var text = $"{Math.Abs(guide.SnapDelta):F1}px";
            var fontSize = LabelFontSize / Math.Max(_zoom, 0.1);

            var formattedText = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                _labelTypeface,
                fontSize,
                _indicatorBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            Point labelPosition;
            var offset = LabelOffset / Math.Max(_zoom, 0.1);

            if (guide.Orientation == GuideOrientation.Vertical)
            {
                labelPosition = new Point(
                    guide.Position + offset,
                    _canvasHeight / 2 - formattedText.Height / 2);
            }
            else
            {
                labelPosition = new Point(
                    _canvasWidth / 2 + offset,
                    guide.Position - formattedText.Height - offset / 2);
            }

            dc.DrawText(formattedText, labelPosition);
        }

        // ── Alignment calculation engine ────────────────────────────────

        /// <summary>
        /// Core alignment algorithm.  Collects reference edges from all
        /// non-dragged elements and the template boundaries, then finds the
        /// closest match for each axis.
        /// </summary>
        private AlignmentResult CalculateAlignment(
            LabelElement draggedElement,
            IEnumerable<LabelElement> allElements,
            double templateWidth,
            double templateHeight,
            double zoom)
        {
            // Effective snap threshold in canvas-space units
            var threshold = SnapThresholdPixels / zoom;

            // Compute visual bounds of the dragged element
            var draggedBounds = DesignerInteractionHelper.GetVisualBounds(draggedElement);

            // ── Collect reference edges ─────────────────────────────────
            var refXEdges = new List<double>();   // vertical guides (X positions)
            var refYEdges = new List<double>();   // horizontal guides (Y positions)

            // Template edges + centres
            refXEdges.Add(0);                              // left edge
            refXEdges.Add(templateWidth / 2);              // centre X
            refXEdges.Add(templateWidth);                  // right edge

            refYEdges.Add(0);                              // top edge
            refYEdges.Add(templateHeight / 2);             // centre Y
            refYEdges.Add(templateHeight);                 // bottom edge

            // Other elements' edges
            foreach (var element in allElements)
            {
                // Skip the element being dragged
                if (ReferenceEquals(element, draggedElement))
                    continue;

                var bounds = DesignerInteractionHelper.GetVisualBounds(element);

                refXEdges.Add(bounds.Left);
                refXEdges.Add(bounds.Left + bounds.Width / 2);  // centre X
                refXEdges.Add(bounds.Right);

                refYEdges.Add(bounds.Top);
                refYEdges.Add(bounds.Top + bounds.Height / 2);  // centre Y
                refYEdges.Add(bounds.Bottom);
            }

            // ── Find best X snap ────────────────────────────────────────
            var draggedLeft   = draggedBounds.Left;
            var draggedRight  = draggedBounds.Right;
            var draggedCenterX = draggedBounds.Left + draggedBounds.Width / 2;

            var bestXSnap = FindBestAxisSnap(
                new (double value, AlignmentEdge edge)[]
                {
                    (draggedLeft,    AlignmentEdge.Left),
                    (draggedCenterX, AlignmentEdge.CenterX),
                    (draggedRight,   AlignmentEdge.Right)
                },
                refXEdges,
                threshold);

            // ── Find best Y snap ────────────────────────────────────────
            var draggedTop    = draggedBounds.Top;
            var draggedBottom = draggedBounds.Bottom;
            var draggedCenterY = draggedBounds.Top + draggedBounds.Height / 2;

            var bestYSnap = FindBestAxisSnap(
                new (double value, AlignmentEdge edge)[]
                {
                    (draggedTop,     AlignmentEdge.Top),
                    (draggedCenterY, AlignmentEdge.CenterY),
                    (draggedBottom,  AlignmentEdge.Bottom)
                },
                refYEdges,
                threshold);

            // ── Build guide lines ───────────────────────────────────────
            var guides = new List<GuideLine>();

            double? snappedX = null;
            if (bestXSnap.HasValue)
            {
                var (refPosition, matchedEdge, delta) = bestXSnap.Value;

                // Convert the reference position to a visual-bounds-left value
                snappedX = matchedEdge switch
                {
                    AlignmentEdge.Left    => refPosition,
                    AlignmentEdge.CenterX => refPosition - draggedBounds.Width / 2,
                    AlignmentEdge.Right   => refPosition - draggedBounds.Width,
                    _ => refPosition
                };

                guides.Add(new GuideLine
                {
                    Orientation = GuideOrientation.Vertical,
                    Position    = refPosition,
                    DraggedEdge = matchedEdge,
                    SnapDelta   = delta
                });
            }

            double? snappedY = null;
            if (bestYSnap.HasValue)
            {
                var (refPosition, matchedEdge, delta) = bestYSnap.Value;

                snappedY = matchedEdge switch
                {
                    AlignmentEdge.Top     => refPosition,
                    AlignmentEdge.CenterY => refPosition - draggedBounds.Height / 2,
                    AlignmentEdge.Bottom  => refPosition - draggedBounds.Height,
                    _ => refPosition
                };

                guides.Add(new GuideLine
                {
                    Orientation = GuideOrientation.Horizontal,
                    Position    = refPosition,
                    DraggedEdge = matchedEdge,
                    SnapDelta   = delta
                });
            }

            return new AlignmentResult
            {
                SnappedX = snappedX,
                SnappedY = snappedY,
                Guides   = guides
            };
        }

        /// <summary>
        /// For a given axis, finds the reference edge that is closest to any
        /// of the dragged element's edges/centre within the snap threshold.
        /// Returns the reference position, the matched dragged edge, and the
        /// signed delta.
        /// </summary>
        private static (double refPosition, AlignmentEdge edge, double delta)?
            FindBestAxisSnap(
                ReadOnlySpan<(double value, AlignmentEdge edge)> draggedEdges,
                List<double> referencePositions,
                double threshold)
        {
            double bestDistance = threshold;
            double bestRef     = 0;
            double bestDelta   = 0;
            AlignmentEdge bestEdge = default;
            bool found = false;

            foreach (var (draggedValue, draggedEdge) in draggedEdges)
            {
                foreach (var refPos in referencePositions)
                {
                    var distance = Math.Abs(draggedValue - refPos);
                    if (distance <= bestDistance)
                    {
                        bestDistance = distance;
                        bestRef     = refPos;
                        bestDelta   = refPos - draggedValue;   // signed
                        bestEdge    = draggedEdge;
                        found       = true;
                    }
                }
            }

            return found
                ? (bestRef, bestEdge, bestDelta)
                : null;
        }
    }
}
