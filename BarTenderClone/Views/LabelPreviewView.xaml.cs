using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Animation;
using BarTenderClone.Adorners;

namespace BarTenderClone.Views
{
    public partial class LabelPreviewView : UserControl
    {
        // Pan state tracking
        private Point? _panStartPoint;
        private Point? _panStartOffset;

        // Adorner management
        private ResizeAdorner? _currentAdorner;
        private AdornerLayer? _adornerLayer;

        // Ctrl+Click range select tracking
        private int _lastSelectedIndex = -1;

        public LabelPreviewView()
        {
            InitializeComponent();
            
            // Failsafe: Force AutoGenerateColumns to false to prevent JSON dump view
            if (ProductGrid != null)
            {
                ProductGrid.AutoGenerateColumns = false;
            }

            Loaded += (s, e) => Focus();

            // Attach zoom event handler
            CanvasScrollViewer.PreviewMouseWheel += CanvasScrollViewer_PreviewMouseWheel;

            // Attach pan event handlers
            CanvasScrollViewer.PreviewMouseDown += CanvasScrollViewer_PreviewMouseDown;
            CanvasScrollViewer.PreviewMouseMove += CanvasScrollViewer_PreviewMouseMove;
            CanvasScrollViewer.PreviewMouseUp += CanvasScrollViewer_PreviewMouseUp;
        }

        private void Thumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.Thumb thumb && thumb.DataContext is BarTenderClone.Models.LabelElement element)
            {
                element.X += e.HorizontalChange;
                element.Y += e.VerticalChange;
                
                // Restrict movement: cannot move left of canvas (X >= 0)
                if (element.X < 0) element.X = 0;
                if (element.Y < 0) element.Y = 0;
            }
        }

        private void Thumb_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.Thumb thumb &&
                thumb.DataContext is BarTenderClone.Models.LabelElement element &&
                DataContext is BarTenderClone.ViewModels.LabelPreviewViewModel viewModel)
            {
                // Remove old adorner if exists
                RemoveAdorner();

                // Deselect all elements
                foreach (var el in viewModel.Elements)
                {
                    el.IsSelected = false;
                }

                // Select this element
                element.IsSelected = true;
                viewModel.SelectedElement = element;

                // Add adorner to selected element
                AddAdorner(thumb, element);
            }
        }

        private void AddAdorner(System.Windows.Controls.Primitives.Thumb thumb, BarTenderClone.Models.LabelElement element)
        {
            // Get adorner layer
            _adornerLayer = AdornerLayer.GetAdornerLayer(thumb);

            if (_adornerLayer != null)
            {
                // Create and add new adorner
                _currentAdorner = new ResizeAdorner(thumb, element);
                _adornerLayer.Add(_currentAdorner);
            }
        }

        private void RemoveAdorner()
        {
            if (_currentAdorner != null && _adornerLayer != null)
            {
                _adornerLayer.Remove(_currentAdorner);
                _currentAdorner = null;
            }
        }

        private void UserControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && DataContext is BarTenderClone.ViewModels.LabelPreviewViewModel viewModel)
            {
                if (viewModel.SelectedElement != null)
                {
                    // Remove adorner before deleting element
                    RemoveAdorner();

                    viewModel.Elements.Remove(viewModel.SelectedElement);
                    viewModel.SelectedElement = null;
                    e.Handled = true;
                }
            }
        }

        // private void DataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        // {
        //     if (sender is System.Windows.Controls.DataGrid dataGrid &&
        //         DataContext is BarTenderClone.ViewModels.LabelPreviewViewModel viewModel)
        //     {
        //         viewModel.UpdateSelectedItems(dataGrid.SelectedItems);
        //     }
        // }

        #region Zoom Functionality

        private void CanvasScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Only handle Ctrl+Wheel for zoom
            if (Keyboard.Modifiers != ModifierKeys.Control)
                return;

            e.Handled = true;

            if (DataContext is not BarTenderClone.ViewModels.LabelPreviewViewModel viewModel)
                return;

            const double ZOOM_FACTOR = 1.1;
            double oldZoom = viewModel.CurrentZoom;
            double newZoom = e.Delta > 0
                ? oldZoom * ZOOM_FACTOR
                : oldZoom / ZOOM_FACTOR;

            newZoom = Math.Clamp(newZoom, viewModel.MinZoom, viewModel.MaxZoom);

            if (Math.Abs(newZoom - oldZoom) < 0.001)
                return; // Already at min/max

            // Get mouse position relative to label card BEFORE zoom
            Point mousePos = e.GetPosition(LabelCard);

            // Calculate the content point under the cursor
            double contentX = mousePos.X / oldZoom;
            double contentY = mousePos.Y / oldZoom;

            // Apply zoom with smooth animation
            AnimateZoom(newZoom);

            // After animation starts, adjust scroll offset to maintain cursor position
            // We need to do this after a short delay to let the animation begin
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Calculate how much the content point has moved in screen space
                double newMouseX = contentX * newZoom;
                double newMouseY = contentY * newZoom;

                // Calculate the scroll offset adjustment needed
                double offsetX = newMouseX - mousePos.X;
                double offsetY = newMouseY - mousePos.Y;

                // Adjust scroll position
                CanvasScrollViewer.ScrollToHorizontalOffset(CanvasScrollViewer.HorizontalOffset + offsetX);
                CanvasScrollViewer.ScrollToVerticalOffset(CanvasScrollViewer.VerticalOffset + offsetY);
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void AnimateZoom(double targetZoom)
        {
            var duration = TimeSpan.FromMilliseconds(150);
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

            var animX = new DoubleAnimation
            {
                To = targetZoom,
                Duration = duration,
                EasingFunction = easing
            };

            var animY = new DoubleAnimation
            {
                To = targetZoom,
                Duration = duration,
                EasingFunction = easing
            };

            ZoomTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, animX);
            ZoomTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, animY);

            // Update ViewModel (triggers ZoomPercentage update)
            if (DataContext is BarTenderClone.ViewModels.LabelPreviewViewModel viewModel)
            {
                viewModel.CurrentZoom = targetZoom;
            }
        }

        #endregion

        #region Pan Functionality

        private void CanvasScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Middle mouse OR Space+Left mouse for panning
            bool isPanTrigger = e.MiddleButton == MouseButtonState.Pressed ||
                               (e.LeftButton == MouseButtonState.Pressed &&
                                Keyboard.IsKeyDown(Key.Space));

            if (!isPanTrigger)
                return;

            // Don't start pan if clicking on an element
            if (e.OriginalSource is FrameworkElement element)
            {
                // Check if we're clicking on the ScrollViewer background or Grid, not on a Thumb
                if (element is System.Windows.Controls.Primitives.Thumb)
                    return;
            }

            _panStartPoint = e.GetPosition(CanvasScrollViewer);
            _panStartOffset = new Point(
                CanvasScrollViewer.HorizontalOffset,
                CanvasScrollViewer.VerticalOffset);

            CanvasScrollViewer.Cursor = Cursors.Hand;
            CanvasScrollViewer.CaptureMouse();
            e.Handled = true;
        }

        private void CanvasScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_panStartPoint.HasValue || !_panStartOffset.HasValue)
                return;

            Point currentPoint = e.GetPosition(CanvasScrollViewer);
            Vector delta = _panStartPoint.Value - currentPoint;

            CanvasScrollViewer.ScrollToHorizontalOffset(_panStartOffset.Value.X + delta.X);
            CanvasScrollViewer.ScrollToVerticalOffset(_panStartOffset.Value.Y + delta.Y);
        }

        private void CanvasScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_panStartPoint.HasValue)
            {
                _panStartPoint = null;
                _panStartOffset = null;
                CanvasScrollViewer.Cursor = Cursors.Arrow;
                CanvasScrollViewer.ReleaseMouseCapture();
            }
        }

        #endregion



        private void ProductDataPanel_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Clear label designer selection when clicking anywhere on product data panel
            ClearLabelSelection();
        }

        private void DataGridRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.DataContext is BarTenderClone.Models.ResourceItem item)
            {
                // Check if the click was on the actual checkbox column (or its visual children)
                var originalSource = e.OriginalSource as DependencyObject;
                bool isCheckbox = false;
                
                while (originalSource != null && originalSource != row)
                {
                    if (originalSource is CheckBox)
                    {
                        isCheckbox = true;
                        break;
                    }
                    originalSource = System.Windows.Media.VisualTreeHelper.GetParent(originalSource);
                }

                // Get ViewModel and Products collection
                var vm = DataContext as BarTenderClone.ViewModels.LabelPreviewViewModel;
                var products = vm?.Products;
                
                if (isCheckbox)
                {
                    // Checkbox clicked: Handle Ctrl+Click range select
                    if (Keyboard.Modifiers == ModifierKeys.Control && _lastSelectedIndex >= 0 && products != null)
                    {
                        int currentIndex = products.IndexOf(item);
                        if (currentIndex >= 0)
                        {
                            int start = Math.Min(_lastSelectedIndex, currentIndex);
                            int end = Math.Max(_lastSelectedIndex, currentIndex);
                            
                            for (int i = start; i <= end; i++)
                            {
                                products[i].IsSelected = true;
                            }
                            _lastSelectedIndex = currentIndex;
                            e.Handled = true;
                            return;
                        }
                    }
                    
                    // Normal checkbox click - update last selected index
                    if (products != null)
                    {
                        _lastSelectedIndex = products.IndexOf(item);
                    }
                    e.Handled = true;
                }
                else
                {
                    // Row clicked: Toggle selection manually and track index
                    item.IsSelected = !item.IsSelected;
                    if (products != null)
                    {
                        _lastSelectedIndex = products.IndexOf(item);
                    }
                    e.Handled = true;
                }
            }
        }

        private void CheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is CheckBox checkbox && checkbox.DataContext is BarTenderClone.Models.ResourceItem item)
            {
                var vm = DataContext as BarTenderClone.ViewModels.LabelPreviewViewModel;
                var products = vm?.Products;
                
                if (products == null) return;
                
                int currentIndex = products.IndexOf(item);
                
                // Ctrl+Click: Range select
                if (Keyboard.Modifiers == ModifierKeys.Control && _lastSelectedIndex >= 0 && currentIndex >= 0)
                {
                    int start = Math.Min(_lastSelectedIndex, currentIndex);
                    int end = Math.Max(_lastSelectedIndex, currentIndex);
                    
                    for (int i = start; i <= end; i++)
                    {
                        products[i].IsSelected = true;
                    }
                    _lastSelectedIndex = currentIndex;
                    e.Handled = true; // Prevent checkbox from toggling again
                    return;
                }
                
                // Normal click: Let checkbox handle it, but track index
                _lastSelectedIndex = currentIndex;
            }
        }

        private void LabelCard_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Clear DataGrid selection when clicking on canvas area
            ProductGrid.SelectedItem = null;
            ProductGrid.UnselectAll();
            
            // Move focus to LabelCard so Delete key affects canvas elements
            LabelCard.Focus();
            
            // Check if clicked on empty canvas area (not on an element)
            if (e.OriginalSource is Border border && border == LabelCard)
            {
                // Clear label element selection when clicking empty canvas
                if (DataContext is BarTenderClone.ViewModels.LabelPreviewViewModel viewModel && viewModel.SelectedElement != null)
                {
                    RemoveAdorner();
                    foreach (var el in viewModel.Elements)
                    {
                        el.IsSelected = false;
                    }
                    viewModel.SelectedElement = null;
                }
            }
        }

        /// <summary>
        /// Clears label element selection when clicking on the product data grid area
        /// </summary>
        private void ClearLabelSelection()
        {
            if (DataContext is BarTenderClone.ViewModels.LabelPreviewViewModel viewModel)
            {
                // Remove adorner
                RemoveAdorner();
                
                // Deselect all label elements
                if (viewModel.SelectedElement != null)
                {
                    foreach (var el in viewModel.Elements)
                    {
                        el.IsSelected = false;
                    }
                    viewModel.SelectedElement = null;
                }
            }
        }
    }
}
