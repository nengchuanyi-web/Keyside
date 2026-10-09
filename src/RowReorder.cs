using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ShortcutDock
{
    sealed class RowDragData
    {
        internal RowReorder Source;
        internal string TabId, Query;
        internal List<string> Ids;
    }

    sealed class RowReorder : IDisposable
    {
        readonly MainWindow owner;
        readonly DataGrid grid;
        readonly DispatcherTimer hold;
        string anchor, pending, deferredSingle;
        Point down;
        bool armed, dragging, interacting, disposed, cancelRequested, acquiringCapture;
        RowInsertionAdorner marker;
        AdornerLayer layer;
        DateTime lastScroll = DateTime.MinValue;
        internal bool IsInteracting { get { return pending != null || dragging; } }
        internal const int HoldMilliseconds = 320;

        internal RowReorder(MainWindow owner, DataGrid grid)
        {
            this.owner = owner; this.grid = grid; grid.AllowDrop = true;
            hold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(HoldMilliseconds) };
            hold.Tick += OnHold;
            grid.PreviewMouseLeftButtonDown += OnDown;
            owner.PreviewMouseMove += OnMove;
            owner.LostMouseCapture += OnLostCapture;
            owner.PreviewMouseLeftButtonUp += OnUp;
            grid.PreviewDragOver += OnDragOver;
            grid.PreviewDragLeave += OnDragLeave;
            grid.PreviewDrop += OnDrop;
            grid.QueryContinueDrag += OnContinue;
        }
        static T Ancestor<T>(DependencyObject item) where T : DependencyObject
        {
            while (item != null)
            {
                T result = item as T; if (result != null) return result;
                FrameworkContentElement content = item as FrameworkContentElement;
                if (content != null) item = content.Parent;
                else if (item is Visual || item is System.Windows.Media.Media3D.Visual3D) item = VisualTreeHelper.GetParent(item);
                else return null;
            }
            return null;
        }
        internal void Select(ShortcutEntry entry, ModifierKeys modifiers)
        {
            if (entry == null || !grid.Items.Contains(entry)) return;
            List<ShortcutEntry> rows = grid.Items.OfType<ShortcutEntry>().ToList();
            bool shift = (modifiers & ModifierKeys.Shift) != 0, control = (modifiers & ModifierKeys.Control) != 0;
            if (shift)
            {
                int start = rows.FindIndex(row => row.Id == anchor);
                if (start < 0) start = Math.Max(0, rows.IndexOf(grid.SelectedItem as ShortcutEntry));
                int end = rows.IndexOf(entry);
                if (!control) grid.SelectedItems.Clear();
                foreach (ShortcutEntry row in rows.Skip(Math.Min(start, end)).Take(Math.Abs(start - end) + 1))
                    if (!grid.SelectedItems.Contains(row)) grid.SelectedItems.Add(row);
                if (anchor == null) anchor = rows[start].Id;
            }
            else if (control)
            {
                if (grid.SelectedItems.Contains(entry)) grid.SelectedItems.Remove(entry); else grid.SelectedItems.Add(entry);
                anchor = entry.Id;
            }
            else
            {
                grid.SelectedItems.Clear(); grid.SelectedItems.Add(entry); anchor = entry.Id;
            }
            if (grid.Columns.Count > 0) grid.CurrentCell = new DataGridCellInfo(entry, grid.Columns[0]);
        }
        void OnDown(object sender, MouseButtonEventArgs e)
        {
            Cancel();
            DataGridRow row = Ancestor<DataGridRow>(e.OriginalSource as DependencyObject);
            ShortcutEntry entry = row == null ? null : row.Item as ShortcutEntry;
            if (entry == null || e.ClickCount != 1) return;
            ModifierKeys modifiers = Keyboard.Modifiers;
            // Clicking a selected row must not collapse the group before a drag.
            bool preserve = modifiers == ModifierKeys.None && grid.SelectedItems.Count > 1 && grid.SelectedItems.Contains(entry);
            if (preserve) deferredSingle = entry.Id; else Select(entry, modifiers);
            grid.Focus(); owner.Activate(); e.Handled = true;
            if (owner.IsEditingLocked || !grid.SelectedItems.Contains(entry)) return;
            pending = entry.Id; down = e.GetPosition(grid); cancelRequested = false;
            interacting = true; if (owner.Dock != null) owner.Dock.InteractionDepth++;
            // Window capture retains input when the pointer leaves a row. Capture
            // synchronizes the mouse and can re-enter OnMove before setup finishes.
            bool captured; acquiringCapture = true;
            try { captured = Mouse.Capture(owner, CaptureMode.SubTree); }
            finally { acquiringCapture = false; }
            if (!captured || !owner.IsMouseCaptured || pending == null) { Cancel(); return; }
            hold.Start();
        }
        internal static bool Ready(double elapsed, Point start, Point current)
        {
            return elapsed >= HoldMilliseconds &&
                (Math.Abs(current.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(current.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance);
        }
        void OnHold(object sender, EventArgs e)
        {
            hold.Stop(); armed = true; TryStart(Mouse.GetPosition(grid), Mouse.LeftButton == MouseButtonState.Pressed);
        }
        void OnMove(object sender, MouseEventArgs e)
        {
            if (pending == null || dragging || acquiringCapture) return;
            e.Handled = true; TryStart(e.GetPosition(grid), e.LeftButton == MouseButtonState.Pressed);
        }
        void TryStart(Point point, bool pressed)
        {
            if (!pressed || owner.IsEditingLocked || disposed) { Cancel(); return; }
            if (pending == null || !armed || !Ready(HoldMilliseconds, down, point)) return;
            RowDragData data = CreateData();
            if (data.Ids.Count == 0) { Cancel(); return; }
            HashSet<string> ids = new HashSet<string>(data.Ids);
            List<ShortcutEntry> selected = grid.Items.OfType<ShortcutEntry>().Where(row => ids.Contains(row.Id)).ToList();
            if (selected.Any(row => row.Pinned != selected[0].Pinned)) { owner.Report(owner.T["DragPinGroups"]); Cancel(); return; }
            dragging = true; hold.Stop();
            if (owner.IsMouseCaptured) owner.ReleaseMouseCapture();
            try { DragDrop.DoDragDrop(grid, new DataObject(typeof(RowDragData), data), DragDropEffects.Move); }
            finally { dragging = false; Cancel(); }
        }
        internal RowDragData CreateData()
        {
            cancelRequested = false;
            HashSet<string> selected = new HashSet<string>(grid.SelectedItems.OfType<ShortcutEntry>().Select(row => row.Id));
            return new RowDragData { Source = this, TabId = owner.State.ActiveTabId, Query = owner.SearchQuery,
                Ids = grid.Items.OfType<ShortcutEntry>().Where(row => selected.Contains(row.Id)).Select(row => row.Id).ToList() };
        }
        bool Accept(RowDragData data)
        {
            return !disposed && !cancelRequested && !owner.IsEditingLocked && data != null && data.Source == this &&
                data.TabId == owner.State.ActiveTabId && data.Query == owner.SearchQuery && data.Ids != null && data.Ids.Count > 0;
        }
        RowDragData ReadData(DragEventArgs e)
        {
            return e.Data.GetDataPresent(typeof(RowDragData)) ? e.Data.GetData(typeof(RowDragData)) as RowDragData : null;
        }
        internal bool ApplyDrop(RowDragData data, int insertion)
        {
            return Accept(data) && owner.ReorderRows(data.Ids, insertion);
        }
        internal bool Target(Point point, RowDragData data, out int insertion, out double y)
        {
            insertion = 0; y = 0;
            if (!Accept(data)) return false;
            List<ShortcutEntry> rows = grid.Items.OfType<ShortcutEntry>().ToList();
            HashSet<string> ids = new HashSet<string>(data.Ids);
            List<ShortcutEntry> selected = rows.Where(row => ids.Contains(row.Id)).ToList();
            if (selected.Count != data.Ids.Count || selected.Any(row => row.Pinned != selected[0].Pinned)) return false;
            DataGridRow hitRow = Ancestor<DataGridRow>(grid.InputHitTest(point) as DependencyObject);
            if (hitRow != null)
            {
                Point origin = hitRow.TranslatePoint(new Point(), grid);
                insertion = rows.IndexOf(hitRow.Item as ShortcutEntry) + (point.Y >= origin.Y + hitRow.ActualHeight / 2 ? 1 : 0);
            }
            else
            {
                DataGridRow first = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
                double top = first == null ? 32 : first.TranslatePoint(new Point(), grid).Y;
                if (point.X < 0 || point.X > grid.ActualWidth || point.Y < top || point.Y > grid.ActualHeight) return false;
                insertion = rows.Count;
            }
            int firstGroup = rows.FindIndex(item => item.Pinned == selected[0].Pinned);
            int lastGroup = rows.FindLastIndex(item => item.Pinned == selected[0].Pinned) + 1;
            insertion = Math.Max(firstGroup, Math.Min(lastGroup, insertion));
            DataGridRow at = grid.ItemContainerGenerator.ContainerFromIndex(insertion < rows.Count ? insertion : insertion - 1) as DataGridRow;
            if (at == null) return false;
            y = at.TranslatePoint(new Point(), grid).Y + (insertion == rows.Count ? at.ActualHeight : 0);
            return true;
        }
        void OnDragOver(object sender, DragEventArgs e)
        {
            RowDragData data = ReadData(e); Point point = e.GetPosition(grid); int insertion; double y;
            e.Handled = true; e.Effects = DragDropEffects.None;
            if (!Accept(data)) { HideMarker(); return; }
            ScrollViewer scroll = Ancestor<ScrollViewer>(grid.InputHitTest(point) as DependencyObject);
            if (scroll != null && (DateTime.UtcNow - lastScroll).TotalMilliseconds >= 100)
            {
                if (point.Y < 55) { scroll.LineUp(); lastScroll = DateTime.UtcNow; }
                else if (point.Y > grid.ActualHeight - 28) { scroll.LineDown(); lastScroll = DateTime.UtcNow; }
                grid.UpdateLayout();
            }
            if (Target(point, data, out insertion, out y)) { e.Effects = DragDropEffects.Move; ShowMarker(y); }
            else HideMarker();
        }
        void OnDrop(object sender, DragEventArgs e)
        {
            int insertion; double y; RowDragData data = ReadData(e);
            e.Handled = true; e.Effects = DragDropEffects.None;
            if (Target(e.GetPosition(grid), data, out insertion, out y) && ApplyDrop(data, insertion)) e.Effects = DragDropEffects.Move;
            HideMarker();
        }
        void OnDragLeave(object sender, DragEventArgs e) { HideMarker(); }
        void OnContinue(object sender, QueryContinueDragEventArgs e)
        {
            if (e.EscapePressed || cancelRequested || disposed || owner.IsEditingLocked) { e.Action = DragAction.Cancel; e.Handled = true; }
        }
        void OnUp(object sender, MouseButtonEventArgs e)
        {
            if (dragging) return;
            if (deferredSingle != null)
            {
                ShortcutEntry entry = grid.Items.OfType<ShortcutEntry>().FirstOrDefault(row => row.Id == deferredSingle);
                Select(entry, ModifierKeys.None);
            }
            Cancel();
        }
        void OnLostCapture(object sender, MouseEventArgs e)
        {
            if (e.OriginalSource == owner && !dragging && !owner.IsMouseCaptured) Cancel();
        }
        internal void ShowMarker(double y)
        {
            if (marker == null)
            {
                layer = AdornerLayer.GetAdornerLayer(grid);
                if (layer == null) return;
                marker = new RowInsertionAdorner(grid, owner); layer.Add(marker);
            }
            marker.Y = Math.Max(1, Math.Min(grid.ActualHeight - 1, y)); marker.InvalidateVisual();
        }
        void HideMarker() { if (marker != null && layer != null) layer.Remove(marker); marker = null; layer = null; }
        internal void Cancel()
        {
            cancelRequested = true; hold.Stop(); HideMarker();
            if (dragging) return;
            pending = deferredSingle = null; armed = false;
            if (owner.IsMouseCaptured) owner.ReleaseMouseCapture();
            if (interacting && owner.Dock != null) owner.Dock.InteractionDepth = Math.Max(0, owner.Dock.InteractionDepth - 1);
            interacting = false;
        }
        internal void SetLocked(bool locked) { if (locked) Cancel(); }
        public void Dispose()
        {
            disposed = true; Cancel(); hold.Tick -= OnHold;
            grid.PreviewMouseLeftButtonDown -= OnDown; owner.PreviewMouseMove -= OnMove;
            owner.LostMouseCapture -= OnLostCapture; owner.PreviewMouseLeftButtonUp -= OnUp;
            grid.PreviewDragOver -= OnDragOver; grid.PreviewDragLeave -= OnDragLeave;
            grid.PreviewDrop -= OnDrop; grid.QueryContinueDrag -= OnContinue;
        }
        sealed class RowInsertionAdorner : Adorner
        {
            readonly MainWindow owner;
            internal double Y;
            internal RowInsertionAdorner(UIElement element, MainWindow owner) : base(element) { this.owner = owner; IsHitTestVisible = false; }
            protected override void OnRender(DrawingContext drawing)
            {
                Brush brush = (Brush)owner.Resources["AccentBrush"];
                drawing.DrawLine(new Pen(brush, 3), new Point(5, Y), new Point(Math.Max(5, AdornedElement.RenderSize.Width - 5), Y));
            }
        }
    }
}
