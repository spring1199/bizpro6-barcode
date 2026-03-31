using BarTenderClone.Models;
using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace BarTenderClone.Adorners
{
    public class ResizeAdorner : Adorner
    {
        private VisualCollection _visualChildren;
        private Thumb _topLeft, _topRight, _bottomLeft, _bottomRight;
        private Thumb _top, _bottom, _left, _right;
        private LabelElement _element;

        // Constants for handle appearance
        private const double HANDLE_SIZE = 8;
        private const double MIN_WIDTH = 20;
        private const double MIN_HEIGHT = 10;

        public ResizeAdorner(UIElement adornedElement, LabelElement element) : base(adornedElement)
        {
            _element = element;
            _visualChildren = new VisualCollection(this);
            BuildHandles();

            // Subscribe to element property changes to update handle positions
            _element.PropertyChanged += Element_PropertyChanged;
        }

        private void Element_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Update adorner layout when element size changes
            if (e.PropertyName == nameof(LabelElement.Width) ||
                e.PropertyName == nameof(LabelElement.Height))
            {
                InvalidateArrange();
            }
        }

        private void BuildHandles()
        {
            // Create corner handles (diagonal resize)
            _topLeft = CreateHandle(Cursors.SizeNWSE, "TopLeft");
            _topRight = CreateHandle(Cursors.SizeNESW, "TopRight");
            _bottomLeft = CreateHandle(Cursors.SizeNESW, "BottomLeft");
            _bottomRight = CreateHandle(Cursors.SizeNWSE, "BottomRight");

            // Create edge handles (horizontal/vertical resize)
            _top = CreateHandle(Cursors.SizeNS, "Top");
            _bottom = CreateHandle(Cursors.SizeNS, "Bottom");
            _left = CreateHandle(Cursors.SizeWE, "Left");
            _right = CreateHandle(Cursors.SizeWE, "Right");

            // Attach drag handlers
            _topLeft.DragDelta += TopLeft_DragDelta;
            _topRight.DragDelta += TopRight_DragDelta;
            _bottomLeft.DragDelta += BottomLeft_DragDelta;
            _bottomRight.DragDelta += BottomRight_DragDelta;
            _top.DragDelta += Top_DragDelta;
            _bottom.DragDelta += Bottom_DragDelta;
            _left.DragDelta += Left_DragDelta;
            _right.DragDelta += Right_DragDelta;
        }

        private Thumb CreateHandle(Cursor cursor, string tag)
        {
            var thumb = new Thumb
            {
                Width = HANDLE_SIZE,
                Height = HANDLE_SIZE,
                Cursor = cursor,
                Tag = tag,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212)), // #0078D4
                BorderThickness = new Thickness(2),
                Opacity = 1.0
            };

            _visualChildren.Add(thumb);
            return thumb;
        }

        protected override int VisualChildrenCount => _visualChildren.Count;

        protected override Visual GetVisualChild(int index) => _visualChildren[index];

        protected override Size ArrangeOverride(Size finalSize)
        {
            var adornedElement = AdornedElement as FrameworkElement;
            if (adornedElement == null)
                return finalSize;

            double width = adornedElement.ActualWidth;
            double height = adornedElement.ActualHeight;
            double halfHandle = HANDLE_SIZE / 2;

            // Position corner handles
            _topLeft.Arrange(new Rect(-halfHandle, -halfHandle, HANDLE_SIZE, HANDLE_SIZE));
            _topRight.Arrange(new Rect(width - halfHandle, -halfHandle, HANDLE_SIZE, HANDLE_SIZE));
            _bottomLeft.Arrange(new Rect(-halfHandle, height - halfHandle, HANDLE_SIZE, HANDLE_SIZE));
            _bottomRight.Arrange(new Rect(width - halfHandle, height - halfHandle, HANDLE_SIZE, HANDLE_SIZE));

            // Position edge handles (centered on edges)
            _top.Arrange(new Rect(width / 2 - halfHandle, -halfHandle, HANDLE_SIZE, HANDLE_SIZE));
            _bottom.Arrange(new Rect(width / 2 - halfHandle, height - halfHandle, HANDLE_SIZE, HANDLE_SIZE));
            _left.Arrange(new Rect(-halfHandle, height / 2 - halfHandle, HANDLE_SIZE, HANDLE_SIZE));
            _right.Arrange(new Rect(width - halfHandle, height / 2 - halfHandle, HANDLE_SIZE, HANDLE_SIZE));

            return finalSize;
        }

        #region Resize Handlers

        private void TopLeft_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newWidth = _element.Width - e.HorizontalChange;
            double newHeight = _element.Height - e.VerticalChange;

            if (newWidth >= MIN_WIDTH)
            {
                _element.X += e.HorizontalChange;
                _element.Width = newWidth;
            }

            if (newHeight >= MIN_HEIGHT)
            {
                _element.Y += e.VerticalChange;
                _element.Height = newHeight;
            }
        }

        private void TopRight_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newWidth = _element.Width + e.HorizontalChange;
            double newHeight = _element.Height - e.VerticalChange;

            if (newWidth >= MIN_WIDTH)
            {
                _element.Width = newWidth;
            }

            if (newHeight >= MIN_HEIGHT)
            {
                _element.Y += e.VerticalChange;
                _element.Height = newHeight;
            }
        }

        private void BottomLeft_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newWidth = _element.Width - e.HorizontalChange;
            double newHeight = _element.Height + e.VerticalChange;

            if (newWidth >= MIN_WIDTH)
            {
                _element.X += e.HorizontalChange;
                _element.Width = newWidth;
            }

            if (newHeight >= MIN_HEIGHT)
            {
                _element.Height = newHeight;
            }
        }

        private void BottomRight_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newWidth = _element.Width + e.HorizontalChange;
            double newHeight = _element.Height + e.VerticalChange;

            if (newWidth >= MIN_WIDTH)
            {
                _element.Width = newWidth;
            }

            if (newHeight >= MIN_HEIGHT)
            {
                _element.Height = newHeight;
            }
        }

        private void Top_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newHeight = _element.Height - e.VerticalChange;

            if (newHeight >= MIN_HEIGHT)
            {
                _element.Y += e.VerticalChange;
                _element.Height = newHeight;
            }
        }

        private void Bottom_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newHeight = _element.Height + e.VerticalChange;

            if (newHeight >= MIN_HEIGHT)
            {
                _element.Height = newHeight;
            }
        }

        private void Left_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newWidth = _element.Width - e.HorizontalChange;

            if (newWidth >= MIN_WIDTH)
            {
                _element.X += e.HorizontalChange;
                _element.Width = newWidth;
            }
        }

        private void Right_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newWidth = _element.Width + e.HorizontalChange;

            if (newWidth >= MIN_WIDTH)
            {
                _element.Width = newWidth;
            }
        }

        #endregion

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            // Optional: Draw a bounding rectangle around the element
            // This provides additional visual feedback for selection
            var adornedElement = AdornedElement as FrameworkElement;
            if (adornedElement != null)
            {
                var rect = new Rect(0, 0, adornedElement.ActualWidth, adornedElement.ActualHeight);
                var pen = new Pen(new SolidColorBrush(Color.FromRgb(0, 120, 212)), 2) // #0078D4
                {
                    DashStyle = DashStyles.Dash
                };

                drawingContext.DrawRectangle(null, pen, rect);
            }
        }
    }
}
