using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using BarTenderClone.Helpers;
using BarTenderClone.Models;
using BarTenderClone.ViewModels;

namespace BarTenderClone.Views
{
    public partial class LabelPreviewView : UserControl
    {
        // Pan state tracking
        private Point? _panStartPoint;
        private Point? _panStartOffset;
        private ResizeDragState? _resizeDragState;
        private bool _moveDragSavedUndo;
        
        // Move drag state tracking
        private double _dragStartX;
        private double _dragStartY;
        private Point _dragStartMouse;

        // Rotation state tracking
        private double _rotateStartAngle;
        private int _rotateStartRotationDegrees;

        // Alignment guide adorner
        private AlignmentGuideAdorner? _alignmentGuideAdorner;

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

            Loaded += (s, e) =>
            {
                Focus();
                var adornerLayer = AdornerLayer.GetAdornerLayer(LabelCard);
                if (adornerLayer != null)
                {
                    _alignmentGuideAdorner = new AlignmentGuideAdorner(LabelCard);
                    adornerLayer.Add(_alignmentGuideAdorner);
                }
            };

            // Attach zoom event handler
            CanvasScrollViewer.PreviewMouseWheel += CanvasScrollViewer_PreviewMouseWheel;

            // Attach pan event handlers
            CanvasScrollViewer.PreviewMouseDown += CanvasScrollViewer_PreviewMouseDown;
            CanvasScrollViewer.PreviewMouseMove += CanvasScrollViewer_PreviewMouseMove;
            CanvasScrollViewer.PreviewMouseUp += CanvasScrollViewer_PreviewMouseUp;
        }

        private double _accumulatedDragX;
        private double _accumulatedDragY;

        private void Thumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (sender is Thumb thumb &&
                thumb.DataContext is LabelElement element)
            {
                _dragStartX = element.X;
                _dragStartY = element.Y;
                _accumulatedDragX = 0;
                _accumulatedDragY = 0;
                _moveDragSavedUndo = false;
            }
        }

        private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb thumb &&
                thumb.DataContext is LabelElement element &&
                DataContext is LabelPreviewViewModel viewModel)
            {
                // Save undo state on first drag delta (before position changes)
                if (!_moveDragSavedUndo)
                {
                    viewModel.SaveUndoState("Move element");
                    _moveDragSavedUndo = true;
                }

                _accumulatedDragX += e.HorizontalChange;
                _accumulatedDragY += e.VerticalChange;

                DesignerInteractionHelper.MoveElementAbsolute(
                    element,
                    viewModel.Template,
                    _dragStartX,
                    _dragStartY,
                    _accumulatedDragX,
                    _accumulatedDragY,
                    viewModel.CurrentZoom,
                    viewModel.PrinterDpi);

                if (_alignmentGuideAdorner != null)
                {
                    // Calculate visual bounds of the moved element
                    var local = DesignerInteractionHelper.GetLocalSize(element);
                    var visualBounds = DesignerInteractionHelper.GetVisualBounds(element.X, element.Y, local.Width, local.Height, element.RotationDegrees);

                    var result = _alignmentGuideAdorner.UpdateGuides(
                        element,
                        viewModel.Elements,
                        viewModel.Template.Width,
                        viewModel.Template.Height,
                        viewModel.CurrentZoom);

                    if (result.SnappedX.HasValue)
                    {
                        var deltaX = result.SnappedX.Value - visualBounds.Left;
                        element.X += deltaX;
                    }
                    if (result.SnappedY.HasValue)
                    {
                        var deltaY = result.SnappedY.Value - visualBounds.Top;
                        element.Y += deltaY;
                    }
                }
            }
        }

        private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _alignmentGuideAdorner?.ClearGuides();
            e.Handled = true;
        }

        private void Thumb_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _moveDragSavedUndo = false; // Reset for new drag
            if (sender is Thumb thumb &&
                thumb.DataContext is LabelElement element &&
                DataContext is LabelPreviewViewModel viewModel)
            {
                // Deselect all elements
                foreach (var el in viewModel.Elements)
                {
                    el.IsSelected = false;
                }

                // Select this element
                element.IsSelected = true;
                viewModel.SelectedElement = element;
                DesignerInteractionHelper.ClampElementToTemplate(element, viewModel.Template);
            }
        }

        private void ResizeHandle_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (sender is Thumb thumb &&
                thumb.DataContext is LabelElement element &&
                thumb.Tag is string tag &&
                Enum.TryParse<ResizeHandleDirection>(tag, out var handle))
            {
                // Save undo state before resize begins
                if (DataContext is LabelPreviewViewModel viewModel)
                {
                    viewModel.SaveUndoState("Resize element");
                    DesignerInteractionHelper.ClampElementToTemplate(element, viewModel.Template);
                }

                DesignerInteractionHelper.CommitMeasuredLocalSize(element);

                var local = DesignerInteractionHelper.GetLocalSize(element);
                _resizeDragState = new ResizeDragState(
                    element,
                    handle,
                    element.X,
                    element.Y,
                    local.Width,
                    local.Height,
                    element.FontSize,
                    element.RotationDegrees);

                _dragStartMouse = Mouse.GetPosition(LabelCard);

                e.Handled = true;
            }
        }

        private void ResizeHandle_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb thumb &&
                thumb.DataContext is LabelElement element &&
                DataContext is LabelPreviewViewModel viewModel)
            {
                if (_resizeDragState == null || !ReferenceEquals(_resizeDragState.Element, element))
                {
                    var fallbackHandle = ResizeHandleDirection.BottomRight;
                    if (thumb.Tag is string tag && Enum.TryParse<ResizeHandleDirection>(tag, out var parsedHandle))
                        fallbackHandle = parsedHandle;

                    var local = DesignerInteractionHelper.GetLocalSize(element);
                    _resizeDragState = new ResizeDragState(
                        element,
                        fallbackHandle,
                        element.X,
                        element.Y,
                        local.Width,
                        local.Height,
                        element.FontSize,
                        element.RotationDegrees);
                }

                Point currentMouse = Mouse.GetPosition(LabelCard);
                Vector cumulativeDelta = currentMouse - _dragStartMouse;

                DesignerInteractionHelper.ResizeElementFromSnapshot(
                    element,
                    viewModel.Template,
                    _resizeDragState.Handle,
                    _resizeDragState.StartX,
                    _resizeDragState.StartY,
                    _resizeDragState.StartWidth,
                    _resizeDragState.StartHeight,
                    _resizeDragState.StartFontSize,
                    _resizeDragState.StartRotationDegrees,
                    cumulativeDelta,
                    viewModel.CurrentZoom,
                    viewModel.PrinterDpi);

                e.Handled = true;
            }
        }

        private void ResizeHandle_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _resizeDragState = null;
            e.Handled = true;
        }

        private void ResizeHandle_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Thumb thumb &&
                thumb.DataContext is LabelElement element &&
                DataContext is LabelPreviewViewModel viewModel)
            {
                var direction = thumb.Tag as string;
                if (string.IsNullOrEmpty(direction))
                {
                    e.Handled = true;
                    return;
                }

                viewModel.SaveUndoState("Reset element size constraints");

                bool isWidth = false;
                bool isHeight = false;

                switch (direction)
                {
                    case "TopLeft":
                    case "TopRight":
                    case "BottomLeft":
                    case "BottomRight":
                        isWidth = true;
                        isHeight = true;
                        break;
                    case "Left":
                    case "Right":
                        isWidth = true;
                        break;
                    case "Top":
                    case "Bottom":
                        isHeight = true;
                        break;
                }

                if (isWidth)
                {
                    element.IsAutoWidth = true;
                }
                if (isHeight)
                {
                    element.IsAutoHeight = true;
                }

                element.AutoMeasureSize();
                e.Handled = true;
            }
        }

        private void RotateHandle_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (sender is Thumb thumb &&
                thumb.DataContext is LabelElement element &&
                DataContext is LabelPreviewViewModel viewModel)
            {
                viewModel.SaveUndoState("Rotate element");
                DesignerInteractionHelper.CommitMeasuredLocalSize(element);

                var local = DesignerInteractionHelper.GetLocalSize(element);
                double centerX = element.X + local.Width / 2;
                double centerY = element.Y + local.Height / 2;

                Point mousePos = Mouse.GetPosition(LabelCard);
                double dx = mousePos.X - centerX;
                double dy = mousePos.Y - centerY;

                _rotateStartAngle = Math.Atan2(dy, dx) * (180.0 / Math.PI);
                _rotateStartRotationDegrees = element.RotationDegrees;

                e.Handled = true;
            }
        }

        private void RotateHandle_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb thumb &&
                thumb.DataContext is LabelElement element &&
                DataContext is LabelPreviewViewModel viewModel)
            {
                var local = DesignerInteractionHelper.GetLocalSize(element);
                double centerX = element.X + local.Width / 2;
                double centerY = element.Y + local.Height / 2;

                Point mousePos = Mouse.GetPosition(LabelCard);
                double dx = mousePos.X - centerX;
                double dy = mousePos.Y - centerY;

                if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5)
                    return;

                double currentAngle = Math.Atan2(dy, dx) * (180.0 / Math.PI);
                double deltaAngle = currentAngle - _rotateStartAngle;

                double targetRotation = (_rotateStartRotationDegrees + deltaAngle) % 360;
                if (targetRotation < 0)
                    targetRotation += 360;

                int snappedRotation = ((int)Math.Round(targetRotation / 90.0) * 90) % 360;
                element.RotationDegrees = snappedRotation;
                e.Handled = true;
            }
        }

        private void RotateHandle_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            e.Handled = true;
        }

        private void UserControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not BarTenderClone.ViewModels.LabelPreviewViewModel viewModel)
                return;

            // Undo: Ctrl+Z
            if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                viewModel.UndoCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Redo: Ctrl+Y or Ctrl+Shift+Z
            if ((e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control) ||
                (e.Key == Key.Z && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)))
            {
                viewModel.RedoCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Delete selected element
            if (e.Key == Key.Delete)
            {
                if (viewModel.SelectedElement != null)
                {
                    viewModel.SaveUndoState("Delete element");
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

            // Get mouse position relative to ScrollViewer viewport
            Point mouseInViewer = e.GetPosition(CanvasScrollViewer);

            // Calculate the content-space point under the cursor before zoom
            double contentX = (CanvasScrollViewer.HorizontalOffset + mouseInViewer.X) / oldZoom;
            double contentY = (CanvasScrollViewer.VerticalOffset + mouseInViewer.Y) / oldZoom;

            // Apply zoom (LayoutTransform updates layout automatically)
            viewModel.CurrentZoom = newZoom;

            // After layout updates, adjust scroll offset to keep the same content point under the cursor
            Dispatcher.BeginInvoke(new Action(() =>
            {
                double newOffsetX = contentX * newZoom - mouseInViewer.X;
                double newOffsetY = contentY * newZoom - mouseInViewer.Y;

                CanvasScrollViewer.ScrollToHorizontalOffset(Math.Max(0, newOffsetX));
                CanvasScrollViewer.ScrollToVerticalOffset(Math.Max(0, newOffsetY));
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // AnimateZoom is no longer needed since LayoutTransform binds directly to CurrentZoom.
        // Zoom changes are instant via the ViewModel property, which is responsive enough.

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


        private sealed class ResizeDragState
        {
            public ResizeDragState(
                LabelElement element,
                ResizeHandleDirection handle,
                double startX,
                double startY,
                double startWidth,
                double startHeight,
                double startFontSize,
                int startRotationDegrees)
            {
                Element = element;
                Handle = handle;
                StartX = startX;
                StartY = startY;
                StartWidth = startWidth;
                StartHeight = startHeight;
                StartFontSize = startFontSize;
                StartRotationDegrees = startRotationDegrees;
            }

            public LabelElement Element { get; }
            public ResizeHandleDirection Handle { get; }
            public double StartX { get; }
            public double StartY { get; }
            public double StartWidth { get; }
            public double StartHeight { get; }
            public double StartFontSize { get; }
            public int StartRotationDegrees { get; }
            public Vector CumulativeDelta { get; set; }
        }


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
                    if (vm != null && item.IsSelected)
                    {
                        vm.SelectedProduct = item;
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
                    foreach (var el in viewModel.Elements)
                    {
                        el.IsSelected = false;
                    }
                    viewModel.SelectedElement = null;
                }
            }
        }

        private void ProductGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            UpdateRowNumber(e.Row);
        }

        private void ProductGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            Dispatcher.BeginInvoke(
                new Action(RefreshVisibleRowNumbers),
                DispatcherPriority.Loaded);
        }

        private void RefreshVisibleRowNumbers()
        {
            if (ProductGrid.Items.Count == 0)
            {
                return;
            }

            for (int i = 0; i < ProductGrid.Items.Count; i++)
            {
                if (ProductGrid.ItemContainerGenerator.ContainerFromIndex(i) is DataGridRow row)
                {
                    UpdateRowNumber(row);
                }
            }
        }

        private void UpdateRowNumber(DataGridRow row)
        {
            if (DataContext is not BarTenderClone.ViewModels.LabelPreviewViewModel viewModel)
            {
                row.Tag = null;
                return;
            }

            var displayedRowNumber = viewModel.Pagination.StartIndex + row.GetIndex() + 1;
            row.Tag = displayedRowNumber.ToString();
        }

        /// <summary>
        /// Clears label element selection when clicking on the product data grid area
        /// </summary>
        private void ClearLabelSelection()
        {
            if (DataContext is BarTenderClone.ViewModels.LabelPreviewViewModel viewModel)
            {
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

        private void FieldBindingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox comboBox)
                return;

            // SelectedValue is the Key string (via SelectedValuePath="Key")
            var fieldName = comboBox.SelectedValue as string;
            if (string.IsNullOrEmpty(fieldName))
                return;

            if (DataContext is not BarTenderClone.ViewModels.LabelPreviewViewModel viewModel || viewModel.SelectedElement == null)
                return;

            // FieldName already updated by TwoWay SelectedValue binding; trigger content refresh
            viewModel.UpdateElementContentFromFieldPublic(viewModel.SelectedElement);
        }

        private void ColumnToggleMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu menu)
            {
                return;
            }

            menu.Items.Clear();

            var header = new MenuItem
            {
                Header = "Column settings",
                IsEnabled = false,
                FontWeight = FontWeights.Bold
            };

            menu.Items.Add(header);
            menu.Items.Add(new Separator());

            foreach (var column in ProductGrid.Columns)
            {
                var headerText = column.Header?.ToString() ?? "Unknown";
                if (headerText == "Select")
                {
                    continue;
                }

                var menuItem = new MenuItem
                {
                    Header = headerText,
                    IsCheckable = true,
                    IsChecked = column.Visibility == Visibility.Visible,
                    Tag = column
                };

                menuItem.Click += (_, _) =>
                {
                    if (menuItem.Tag is DataGridColumn taggedColumn)
                    {
                        taggedColumn.Visibility = menuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;
                    }
                };

                menu.Items.Add(menuItem);
            }

            menu.Items.Add(new Separator());

            var showAll = new MenuItem { Header = "Show all" };
            showAll.Click += (_, _) =>
            {
                foreach (var column in ProductGrid.Columns)
                {
                    column.Visibility = Visibility.Visible;
                }
            };

            menu.Items.Add(showAll);

            var clearAll = new MenuItem { Header = "Clear all" };
            clearAll.Click += (_, _) =>
            {
                foreach (var column in ProductGrid.Columns)
                {
                    if (column.Header?.ToString() != "Select")
                        column.Visibility = Visibility.Collapsed;
                }
            };
            menu.Items.Add(clearAll);
        }
    }
}
