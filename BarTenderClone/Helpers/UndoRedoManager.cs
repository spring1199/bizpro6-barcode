using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using BarTenderClone.Models;

namespace BarTenderClone.Helpers
{
    /// <summary>
    /// Immutable snapshot of a single <see cref="LabelElement"/>'s state.
    /// Used as the "memento" that captures every designable property so the
    /// element can be restored to a previous state by <see cref="UndoRedoManager"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="LabelElement.IsSelected"/> is intentionally excluded because
    /// it is transient UI state and should not participate in undo/redo.
    /// </remarks>
    public sealed class ElementMemento
    {
        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public double FontSize { get; }
        public string Content { get; }
        public string FieldName { get; }
        public bool IsBold { get; }
        public bool IsCentered { get; }
        public int RotationDegrees { get; }
        public string ImageDataBase64 { get; }
        public string ImageMimeType { get; }
        public string ImageFileName { get; }
        public ElementType Type { get; }

        /// <summary>
        /// Creates a memento by capturing all relevant property values from the
        /// supplied <paramref name="element"/>.
        /// </summary>
        public ElementMemento(LabelElement element)
        {
            ArgumentNullException.ThrowIfNull(element);

            X = element.X;
            Y = element.Y;
            Width = element.Width;
            Height = element.Height;
            FontSize = element.FontSize;
            Content = element.Content;
            FieldName = element.FieldName;
            IsBold = element.IsBold;
            IsCentered = element.IsCentered;
            RotationDegrees = element.RotationDegrees;
            ImageDataBase64 = element.ImageDataBase64;
            ImageMimeType = element.ImageMimeType;
            ImageFileName = element.ImageFileName;
            Type = element.Type;
        }

        /// <summary>
        /// Applies every captured property value back onto the supplied
        /// <paramref name="element"/>, effectively restoring it.
        /// </summary>
        public void ApplyTo(LabelElement element)
        {
            ArgumentNullException.ThrowIfNull(element);

            element.X = X;
            element.Y = Y;
            element.Width = Width;
            element.Height = Height;
            element.FontSize = FontSize;
            element.Content = Content;
            element.FieldName = FieldName;
            element.IsBold = IsBold;
            element.IsCentered = IsCentered;
            element.RotationDegrees = RotationDegrees;
            element.ImageDataBase64 = ImageDataBase64;
            element.ImageMimeType = ImageMimeType;
            element.ImageFileName = ImageFileName;
            element.Type = Type;
        }
    }

    /// <summary>
    /// An immutable snapshot of the entire designer canvas at a single point in
    /// time. Contains the ordered list of <see cref="ElementMemento"/> objects
    /// representing every element that was present on the canvas.
    /// </summary>
    public sealed class DesignerSnapshot
    {
        /// <summary>
        /// Ordered collection of element mementos captured at a point in time.
        /// </summary>
        public IReadOnlyList<ElementMemento> Elements { get; }

        /// <summary>
        /// Optional human-readable description of the action that produced this
        /// snapshot (e.g. "Move element", "Delete element"). Useful for
        /// displaying an undo/redo history list in the UI.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// UTC timestamp of when this snapshot was captured.
        /// </summary>
        public DateTime TimestampUtc { get; }

        /// <summary>
        /// Creates a snapshot by capturing the state of every element in the
        /// supplied collection.
        /// </summary>
        /// <param name="elements">The current canvas elements.</param>
        /// <param name="description">
        /// A short description of the action being recorded.
        /// </param>
        public DesignerSnapshot(
            IEnumerable<LabelElement> elements,
            string description = "")
        {
            ArgumentNullException.ThrowIfNull(elements);

            Elements = elements
                .Select(e => new ElementMemento(e))
                .ToList()
                .AsReadOnly();

            Description = description ?? string.Empty;
            TimestampUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments raised when the undo/redo state changes (e.g. after an
    /// undo, redo, or new state save).
    /// </summary>
    public sealed class UndoRedoStateChangedEventArgs : EventArgs
    {
        /// <summary>Whether an undo operation is available.</summary>
        public bool CanUndo { get; }

        /// <summary>Whether a redo operation is available.</summary>
        public bool CanRedo { get; }

        /// <summary>Number of states on the undo stack.</summary>
        public int UndoCount { get; }

        /// <summary>Number of states on the redo stack.</summary>
        public int RedoCount { get; }

        public UndoRedoStateChangedEventArgs(
            bool canUndo,
            bool canRedo,
            int undoCount,
            int redoCount)
        {
            CanUndo = canUndo;
            CanRedo = canRedo;
            UndoCount = undoCount;
            RedoCount = redoCount;
        }
    }

    /// <summary>
    /// Manages undo/redo history for the label designer canvas using the
    /// Memento pattern.
    /// <para>
    /// <b>Usage:</b><br/>
    /// 1. Call <see cref="SaveState"/> before (or after) every user action that
    ///    should be undoable.<br/>
    /// 2. Call <see cref="Undo"/> / <see cref="Redo"/> to navigate history.<br/>
    /// 3. Bind UI controls to <see cref="CanUndo"/> / <see cref="CanRedo"/>.
    /// </para>
    /// <para>
    /// The manager is designed to be used exclusively on the UI thread. A
    /// re-entrancy guard prevents nested calls (e.g. property-change handlers
    /// triggering additional saves during an undo).
    /// </para>
    /// </summary>
    public sealed class UndoRedoManager
    {
        // -----------------------------------------------------------------
        // Fields
        // -----------------------------------------------------------------

        private readonly Stack<DesignerSnapshot> _undoStack = new();
        private readonly Stack<DesignerSnapshot> _redoStack = new();
        private readonly int _maxDepth;

        /// <summary>
        /// Re-entrancy guard. When <c>true</c> the manager is currently
        /// applying a snapshot (undo/redo) and should ignore any
        /// <see cref="SaveState"/> calls that might be triggered by property
        /// change handlers.
        /// </summary>
        private bool _isApplying;

        // -----------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------

        /// <summary>
        /// Creates a new <see cref="UndoRedoManager"/> with the specified
        /// maximum history depth.
        /// </summary>
        /// <param name="maxDepth">
        /// Maximum number of undo states to keep. When exceeded, the oldest
        /// state is discarded. Must be at least 1. Default is 50.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="maxDepth"/> is less than 1.
        /// </exception>
        public UndoRedoManager(int maxDepth = 50)
        {
            if (maxDepth < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(maxDepth),
                    maxDepth,
                    "Max depth must be at least 1.");

            _maxDepth = maxDepth;
        }

        // -----------------------------------------------------------------
        // Events
        // -----------------------------------------------------------------

        /// <summary>
        /// Raised whenever the undo/redo state changes (after save, undo, redo,
        /// or clear). Subscribe to this event to update UI command states.
        /// </summary>
        public event EventHandler<UndoRedoStateChangedEventArgs>? StateChanged;

        // -----------------------------------------------------------------
        // Properties
        // -----------------------------------------------------------------

        /// <summary>
        /// Returns <c>true</c> when there is at least one state on the undo
        /// stack that can be restored.
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// Returns <c>true</c> when there is at least one state on the redo
        /// stack that can be re-applied.
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Number of snapshots currently on the undo stack.
        /// </summary>
        public int UndoCount => _undoStack.Count;

        /// <summary>
        /// Number of snapshots currently on the redo stack.
        /// </summary>
        public int RedoCount => _redoStack.Count;

        /// <summary>
        /// Returns <c>true</c> while the manager is restoring a snapshot.
        /// External code can check this to suppress save calls that would be
        /// triggered by property-change handlers during undo/redo.
        /// </summary>
        public bool IsApplying => _isApplying;

        // -----------------------------------------------------------------
        // Public Methods
        // -----------------------------------------------------------------

        /// <summary>
        /// Captures a snapshot of the current canvas state and pushes it onto
        /// the undo stack. Clears the redo stack because a new action
        /// invalidates the forward history.
        /// </summary>
        /// <param name="elements">
        /// The current collection of <see cref="LabelElement"/> instances on
        /// the designer canvas.
        /// </param>
        /// <param name="description">
        /// Optional human-readable description of the action being recorded
        /// (e.g. "Move element", "Change font size").
        /// </param>
        /// <remarks>
        /// This method is a no-op when the manager is currently applying a
        /// snapshot (re-entrancy guard), preventing infinite loops when
        /// property-change handlers call back into the manager.
        /// </remarks>
        public void SaveState(
            ObservableCollection<LabelElement> elements,
            string description = "")
        {
            ArgumentNullException.ThrowIfNull(elements);

            // Prevent re-entrant saves during undo/redo application.
            if (_isApplying)
                return;

            var snapshot = new DesignerSnapshot(elements, description);

            _undoStack.Push(snapshot);

            // Trim oldest entries when the stack exceeds the configured depth.
            TrimStack(_undoStack, _maxDepth);

            // Any new action invalidates the redo history.
            _redoStack.Clear();

            RaiseStateChanged();
        }

        /// <summary>
        /// Restores the most recent snapshot from the undo stack and pushes the
        /// <em>current</em> state onto the redo stack so it can be re-applied.
        /// </summary>
        /// <param name="elements">
        /// The live canvas element collection. Its contents will be replaced
        /// with the elements from the most recent undo snapshot.
        /// </param>
        /// <returns>
        /// <c>true</c> if a state was restored; <c>false</c> if the undo stack
        /// was empty.
        /// </returns>
        public bool Undo(ObservableCollection<LabelElement> elements)
        {
            ArgumentNullException.ThrowIfNull(elements);

            if (_undoStack.Count == 0)
                return false;

            // Save the current state onto the redo stack before we overwrite it.
            _redoStack.Push(new DesignerSnapshot(elements, "Before undo"));

            var snapshot = _undoStack.Pop();
            ApplySnapshot(snapshot, elements);

            RaiseStateChanged();
            return true;
        }

        /// <summary>
        /// Re-applies the most recent snapshot from the redo stack and pushes
        /// the <em>current</em> state back onto the undo stack.
        /// </summary>
        /// <param name="elements">
        /// The live canvas element collection. Its contents will be replaced
        /// with the elements from the most recent redo snapshot.
        /// </param>
        /// <returns>
        /// <c>true</c> if a state was re-applied; <c>false</c> if the redo
        /// stack was empty.
        /// </returns>
        public bool Redo(ObservableCollection<LabelElement> elements)
        {
            ArgumentNullException.ThrowIfNull(elements);

            if (_redoStack.Count == 0)
                return false;

            // Save the current state onto the undo stack before we overwrite it.
            _undoStack.Push(new DesignerSnapshot(elements, "Before redo"));
            TrimStack(_undoStack, _maxDepth);

            var snapshot = _redoStack.Pop();
            ApplySnapshot(snapshot, elements);

            RaiseStateChanged();
            return true;
        }

        /// <summary>
        /// Clears all undo and redo history. Call this when loading a new
        /// template or resetting the designer.
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            RaiseStateChanged();
        }

        /// <summary>
        /// Returns the description of the action that would be undone by the
        /// next <see cref="Undo"/> call, or <c>null</c> if the stack is empty.
        /// Useful for showing "Undo: Move element" style tooltips.
        /// </summary>
        public string? PeekUndoDescription()
        {
            return _undoStack.Count > 0
                ? _undoStack.Peek().Description
                : null;
        }

        /// <summary>
        /// Returns the description of the action that would be re-applied by
        /// the next <see cref="Redo"/> call, or <c>null</c> if the stack is
        /// empty.
        /// </summary>
        public string? PeekRedoDescription()
        {
            return _redoStack.Count > 0
                ? _redoStack.Peek().Description
                : null;
        }

        // -----------------------------------------------------------------
        // Private Helpers
        // -----------------------------------------------------------------

        /// <summary>
        /// Replaces the contents of <paramref name="elements"/> with brand-new
        /// <see cref="LabelElement"/> instances restored from the given
        /// <paramref name="snapshot"/>.
        /// </summary>
        private void ApplySnapshot(
            DesignerSnapshot snapshot,
            ObservableCollection<LabelElement> elements)
        {
            _isApplying = true;
            try
            {
                elements.Clear();

                foreach (var memento in snapshot.Elements)
                {
                    var element = new LabelElement();
                    memento.ApplyTo(element);
                    elements.Add(element);
                }
            }
            finally
            {
                _isApplying = false;
            }
        }

        /// <summary>
        /// Trims the bottom (oldest) entries from a stack when its count
        /// exceeds <paramref name="maxDepth"/>. Because <see cref="Stack{T}"/>
        /// does not support direct bottom-removal we rebuild it.
        /// </summary>
        private static void TrimStack(Stack<DesignerSnapshot> stack, int maxDepth)
        {
            if (stack.Count <= maxDepth)
                return;

            // Keep only the newest <maxDepth> items.
            var keep = stack.Take(maxDepth).ToArray();

            stack.Clear();

            // Re-push in reverse order so the newest item is on top again.
            for (int i = keep.Length - 1; i >= 0; i--)
            {
                stack.Push(keep[i]);
            }
        }

        /// <summary>
        /// Raises the <see cref="StateChanged"/> event with current stack info.
        /// </summary>
        private void RaiseStateChanged()
        {
            StateChanged?.Invoke(
                this,
                new UndoRedoStateChangedEventArgs(
                    CanUndo,
                    CanRedo,
                    UndoCount,
                    RedoCount));
        }
    }
}
