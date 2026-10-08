using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ShortcutDock
{
    // Preserve the user's proportions while the panel and notes visibility change.
    // WPF owns the header grippers and their live resizing; this class persists the result.
    sealed class ColumnSizing : IDisposable
    {
        readonly MainWindow owner;
        readonly DataGrid grid;
        bool dragging, disposed;
        Thumb activeGrip;
        readonly DragStartedEventHandler started;
        readonly DragCompletedEventHandler completed;
        readonly MouseButtonEventHandler doubleClicked;

        public ColumnSizing(MainWindow owner, DataGrid grid)
        {
            this.owner = owner; this.grid = grid;
            started = OnStarted; completed = OnCompleted; doubleClicked = OnDoubleClick;
            grid.AddHandler(Thumb.DragStartedEvent, started, true);
            grid.AddHandler(Thumb.DragCompletedEvent, completed, true);
            grid.AddHandler(Control.MouseDoubleClickEvent, doubleClicked, true);
            Style headers = new Style(typeof(DataGridColumnHeader), (Style)owner.Resources[typeof(DataGridColumnHeader)]);
            headers.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, owner.T["ColumnResizeHint"]));
            grid.ColumnHeaderStyle = headers;
            Restore(); SetLocked(owner.IsEditingLocked);
        }
        public void Restore()
        {
            for (int i = 0; i < grid.Columns.Count; i++)
            {
                grid.Columns[i].MinWidth = 48 * owner.State.FontScale;
                grid.Columns[i].Width = new DataGridLength(owner.State.ColumnWeights[i], DataGridLengthUnitType.Star);
            }
        }
        public void SetLocked(bool locked)
        {
            grid.CanUserResizeColumns = !locked;
            foreach (DataGridColumn column in grid.Columns) column.CanUserResize = !locked;
            if (locked && activeGrip != null && activeGrip.IsDragging) activeGrip.CancelDrag();
        }
        DataGridColumnHeader HeaderGrip(DependencyObject source)
        {
            DependencyObject item = source;
            while (item != null && !(item is Thumb) && !(item is DataGridColumnHeader)) item = VisualTreeHelper.GetParent(item);
            Thumb grip = item as Thumb;
            if (grip == null || (grip.Name != "PART_LeftHeaderGripper" && grip.Name != "PART_RightHeaderGripper")) return null;
            item = grip;
            while (item != null && !(item is DataGridColumnHeader)) item = VisualTreeHelper.GetParent(item);
            DataGridColumnHeader header = item as DataGridColumnHeader;
            return header != null && grid.Columns.Contains(header.Column) ? header : null;
        }
        void OnStarted(object sender, DragStartedEventArgs e)
        {
            DataGridColumnHeader header = HeaderGrip(e.OriginalSource as DependencyObject);
            if (disposed || owner.IsEditingLocked || header == null) return;
            if (dragging) return;
            dragging = true; activeGrip = e.OriginalSource as Thumb;
            int index = grid.Columns.IndexOf(header.Column);
            if (activeGrip.Name == "PART_LeftHeaderGripper")
                do { index--; } while (index >= 0 && grid.Columns[index].Visibility != Visibility.Visible);
            // A final star column consumes leftover space, so WPF otherwise ignores dragging
            // its outer boundary. Make only the dragged column pixel-sized during the drag;
            // the remaining star columns absorb the difference. Commit restores proportions.
            if (index >= 0)
                grid.Columns[index].Width = new DataGridLength(grid.Columns[index].ActualWidth, DataGridLengthUnitType.Pixel);
            if (owner.Dock != null) owner.Dock.InteractionDepth++;
        }
        void Release()
        {
            if (dragging && owner.Dock != null) owner.Dock.InteractionDepth = Math.Max(0, owner.Dock.InteractionDepth - 1);
            dragging = false; activeGrip = null;
        }
        void OnCompleted(object sender, DragCompletedEventArgs e)
        {
            if (!dragging) return;
            Release();
            if (!disposed && !e.Canceled && !owner.IsEditingLocked) Commit();
        }
        void OnDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (disposed || owner.IsEditingLocked || HeaderGrip(e.OriginalSource as DependencyObject) == null) return;
            // WPF auto-sizes the column before this queued capture runs.
            owner.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate {
                if (!disposed && !owner.IsEditingLocked) Commit();
            }));
        }
        public void Commit()
        {
            if (disposed || owner.IsEditingLocked) return;
            grid.UpdateLayout();
            int[] visible = Enumerable.Range(0, grid.Columns.Count).Where(i => grid.Columns[i].Visibility == Visibility.Visible).ToArray();
            double pixels = visible.Sum(i => grid.Columns[i].ActualWidth), weights = visible.Sum(i => owner.State.ColumnWeights[i]);
            if (pixels <= 0) return;
            foreach (int i in visible) owner.State.ColumnWeights[i] = grid.Columns[i].ActualWidth / pixels * weights;
            Restore(); owner.Save();
        }
        public void Dispose()
        {
            disposed = true; Release();
            grid.RemoveHandler(Thumb.DragStartedEvent, started);
            grid.RemoveHandler(Thumb.DragCompletedEvent, completed);
            grid.RemoveHandler(Control.MouseDoubleClickEvent, doubleClicked);
        }
    }
}
