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
    sealed class TabDragData
    {
        internal TabGroupDrag Source;
        internal string TabId, GroupId, Scope;
        internal int Generation;
        internal bool IsGroup;
        internal string Key { get { return IsGroup ? TabOrdering.GroupKey(GroupId) : TabOrdering.TabKey(TabId); } }
    }
    enum TabDropKind { None, Reorder, IntoGroup }
    sealed class TabDropLocation
    {
        internal TabDropKind Kind;
        internal int Insertion;
        internal string GroupId;
        internal double X;
        internal Button Button;
    }

    sealed class TabGroupDrag : IDisposable
    {
        readonly MainWindow owner;
        readonly DispatcherTimer hold;
        readonly Dictionary<Button, string> sources = new Dictionary<Button, string>();
        readonly Dictionary<Button, string> targets = new Dictionary<Button, string>();
        StackPanel bar;
        ScrollViewer scroll;
        Button pending, highlight;
        Point down;
        bool interacting, dragging, disposed, cancelled, armed;
        int generation;
        TabInsertionAdorner marker;
        AdornerLayer layer;
        DateTime lastScroll = DateTime.MinValue;
        internal const int HoldMilliseconds = 320;
        internal bool IsInteracting { get { return pending != null || dragging; } }
        internal TabGroupDrag(MainWindow owner)
        {
            this.owner = owner;
            hold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(HoldMilliseconds) };
            hold.Tick += OnHold;
            owner.PreviewMouseLeftButtonUp += OnUp;
            owner.QueryContinueDrag += OnContinue;
        }
        internal void BindBar(StackPanel bar, ScrollViewer scroll)
        {
            this.bar = bar; this.scroll = scroll; scroll.AllowDrop = true;
            scroll.PreviewDragEnter += OnBarOver; scroll.PreviewDragOver += OnBarOver;
            scroll.PreviewDragLeave += OnLeave; scroll.PreviewDrop += OnBarDrop;
        }
        internal void Source(Button button, string id) { AddSource(button, TabOrdering.TabKey(id)); }
        internal void GroupSource(Button button, string id) { AddSource(button, TabOrdering.GroupKey(id)); }
        void AddSource(Button button, string key)
        {
            sources.Add(button, key); button.AllowDrop = true;
            button.PreviewMouseLeftButtonDown += OnDown;
            button.PreviewMouseMove += OnMove;
            button.LostMouseCapture += OnLost;
        }
        internal void Target(Button button, string groupId)
        {
            targets.Add(button, groupId); button.AllowDrop = true;
            // Groups are handled by the bar, which distinguishes their center
            // from the ordering edges. The back button sits outside the bar.
            if (groupId == null)
            {
                button.PreviewDragEnter += OnOver; button.PreviewDragOver += OnOver;
                button.PreviewDragLeave += OnLeave; button.PreviewDrop += OnDrop;
            }
        }
        void OnDown(object sender, MouseButtonEventArgs e)
        {
            Cancel();
            if (disposed || owner.IsEditingLocked || e.ClickCount != 1) return;
            owner.Activate(); pending = (Button)sender; down = e.GetPosition(owner); cancelled = false; interacting = true;
            if (owner.Dock != null) owner.Dock.InteractionDepth++;
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
            hold.Stop(); armed = true; TryStart(Mouse.GetPosition(owner), Mouse.LeftButton == MouseButtonState.Pressed);
        }
        void OnMove(object sender, MouseEventArgs e)
        {
            if (pending != sender || dragging) return;
            if (TryStart(e.GetPosition(owner), e.LeftButton == MouseButtonState.Pressed)) e.Handled = true;
        }
        bool TryStart(Point point, bool pressed)
        {
            if (!pressed || owner.IsEditingLocked || disposed) { Cancel(); return false; }
            if (pending == null || !armed || !Ready(HoldMilliseconds, down, point)) return false;
            string key = sources[pending];
            TabDragData data = key.StartsWith("g:", StringComparison.Ordinal) ? CreateGroupData(key.Substring(2)) : CreateData(key.Substring(2));
            if (data == null) { Cancel(); return false; }
            Button source = pending; dragging = true;
            if (source.IsMouseCaptured) source.ReleaseMouseCapture();
            try { DragDrop.DoDragDrop(source, new DataObject(typeof(TabDragData), data), DragDropEffects.Move); }
            finally { dragging = false; Cancel(); }
            return true;
        }
        internal TabDragData CreateData(string id)
        {
            SoftwareTab tab = owner.State.Tabs.FirstOrDefault(item => item.Id == id);
            if (disposed || tab == null || tab.GroupId != owner.State.ActiveGroupId) return null;
            cancelled = false;
            return new TabDragData { Source = this, TabId = tab.Id, GroupId = tab.GroupId, Scope = owner.State.ActiveGroupId, Generation = ++generation };
        }
        internal TabDragData CreateGroupData(string id)
        {
            if (disposed || owner.State.ActiveGroupId != null || !owner.State.Groups.Any(group => group.Id == id)) return null;
            cancelled = false;
            return new TabDragData { Source = this, IsGroup = true, GroupId = id, Scope = null, Generation = ++generation };
        }
        bool Accept(TabDragData data)
        {
            if (disposed || cancelled || owner.IsEditingLocked || data == null || data.Source != this || data.Generation != generation ||
                data.Scope != owner.State.ActiveGroupId) return false;
            if (data.IsGroup) return data.Scope == null && owner.State.Groups.Any(group => group.Id == data.GroupId);
            SoftwareTab tab = owner.State.Tabs.FirstOrDefault(item => item.Id == data.TabId);
            return tab != null && tab.GroupId == data.GroupId;
        }
        internal bool CanDrop(TabDragData data, string groupId)
        {
            return Accept(data) && !data.IsGroup && data.GroupId != groupId &&
                (groupId == null || owner.State.Groups.Any(group => group.Id == groupId));
        }
        internal bool ApplyDrop(TabDragData data, string groupId)
        {
            return CanDrop(data, groupId) && owner.MoveTabToGroup(data.TabId, groupId);
        }
        internal bool CanReorder(TabDragData data) { return Accept(data) && TabOrdering.Visible(owner.State).Contains(data.Key); }
        internal bool ApplyReorder(TabDragData data, int insertion)
        {
            return CanReorder(data) && owner.ReorderTabBar(data.Key, insertion);
        }
        internal TabDropLocation Resolve(TabDragData data, Point point)
        {
            TabDropLocation none = new TabDropLocation();
            if (!CanReorder(data) || scroll == null || point.X < 0 || point.X > scroll.ActualWidth ||
                point.Y < 0 || point.Y > scroll.ActualHeight) return none;
            List<string> visible = TabOrdering.Visible(owner.State);
            double end = 0;
            foreach (Button button in bar.Children.OfType<Button>().Where(item => sources.ContainsKey(item)))
            {
                Point at = button.TranslatePoint(new Point(), scroll);
                double right = at.X + button.ActualWidth; end = right;
                int index = visible.IndexOf(sources[button]);
                if (point.X < at.X) return new TabDropLocation { Kind = TabDropKind.Reorder, Insertion = index, X = at.X };
                if (point.X <= right)
                {
                    string groupId;
                    double fraction = (point.X - at.X) / Math.Max(1, button.ActualWidth);
                    if (targets.TryGetValue(button, out groupId) && groupId != null && fraction > 0.25 && fraction < 0.75 && CanDrop(data, groupId))
                        return new TabDropLocation { Kind = TabDropKind.IntoGroup, GroupId = groupId, Button = button };
                    bool after = fraction >= 0.5;
                    return new TabDropLocation { Kind = TabDropKind.Reorder, Insertion = index + (after ? 1 : 0), X = after ? right : at.X };
                }
            }
            return new TabDropLocation { Kind = TabDropKind.Reorder, Insertion = visible.Count, X = end };
        }
        TabDragData Read(DragEventArgs e)
        {
            return e.Data.GetDataPresent(typeof(TabDragData)) ? e.Data.GetData(typeof(TabDragData)) as TabDragData : null;
        }
        internal void Highlight(Button button)
        {
            ClearHighlight(); HideMarker(); if (!targets.ContainsKey(button)) return;
            highlight = button; button.SetResourceReference(Control.BorderBrushProperty, "AccentBrush");
        }
        void ClearHighlight()
        {
            if (highlight != null)
                highlight.SetResourceReference(Control.BorderBrushProperty, targets[highlight] == null ? "TransparentBrush" : "LineBrush");
            highlight = null;
        }
        internal void ShowMarker(double x)
        {
            ClearHighlight();
            if (marker == null)
            {
                layer = AdornerLayer.GetAdornerLayer(scroll); if (layer == null) return;
                marker = new TabInsertionAdorner(scroll, owner); layer.Add(marker);
            }
            marker.X = Math.Max(1, Math.Min(scroll.ActualWidth - 1, x)); marker.InvalidateVisual();
        }
        void HideMarker() { if (marker != null && layer != null) layer.Remove(marker); marker = null; layer = null; }
        void OnBarOver(object sender, DragEventArgs e)
        {
            TabDragData data = Read(e); Point point = e.GetPosition(scroll);
            e.Handled = true; e.Effects = DragDropEffects.None;
            if (!CanReorder(data)) { ClearHighlight(); HideMarker(); return; }
            if ((DateTime.UtcNow - lastScroll).TotalMilliseconds >= 100 && scroll.ScrollableWidth > 0)
            {
                if (point.X < 20) { scroll.LineLeft(); lastScroll = DateTime.UtcNow; }
                else if (point.X > scroll.ActualWidth - 20) { scroll.LineRight(); lastScroll = DateTime.UtcNow; }
                scroll.UpdateLayout();
            }
            TabDropLocation location = Resolve(data, point);
            if (location.Kind == TabDropKind.IntoGroup) { e.Effects = DragDropEffects.Move; Highlight(location.Button); }
            else if (location.Kind == TabDropKind.Reorder) { e.Effects = DragDropEffects.Move; ShowMarker(location.X); }
            else { ClearHighlight(); HideMarker(); }
        }
        void OnBarDrop(object sender, DragEventArgs e)
        {
            TabDragData data = Read(e); TabDropLocation location = Resolve(data, e.GetPosition(scroll));
            ClearHighlight(); HideMarker(); e.Handled = true;
            bool moved = location.Kind == TabDropKind.IntoGroup ? ApplyDrop(data, location.GroupId) :
                location.Kind == TabDropKind.Reorder && ApplyReorder(data, location.Insertion);
            e.Effects = moved ? DragDropEffects.Move : DragDropEffects.None;
        }
        void OnOver(object sender, DragEventArgs e)
        {
            Button target = (Button)sender;
            bool allowed = CanDrop(Read(e), targets[target]); e.Handled = true;
            e.Effects = allowed ? DragDropEffects.Move : DragDropEffects.None;
            if (allowed) Highlight(target); else { ClearHighlight(); HideMarker(); }
        }
        void OnLeave(object sender, DragEventArgs e) { ClearHighlight(); HideMarker(); }
        void OnDrop(object sender, DragEventArgs e)
        {
            string groupId = targets[(Button)sender];
            ClearHighlight(); HideMarker(); e.Handled = true;
            e.Effects = ApplyDrop(Read(e), groupId) ? DragDropEffects.Move : DragDropEffects.None;
        }
        // Leave capture to Button's bubbling mouse-up handler so a short press
        // still fires its normal click after our preview handler clears the hold.
        void OnUp(object sender, MouseButtonEventArgs e) { if (!dragging) Cancel(false); }
        void OnLost(object sender, MouseEventArgs e) { if (!dragging && sender == pending && !pending.IsMouseCaptured) Cancel(); }
        void OnContinue(object sender, QueryContinueDragEventArgs e)
        {
            if (dragging && (e.EscapePressed || cancelled || disposed || owner.IsEditingLocked))
            { e.Action = DragAction.Cancel; e.Handled = true; }
        }
        internal void Cancel(bool releaseCapture = true)
        {
            cancelled = true; generation++; hold.Stop(); armed = false; ClearHighlight(); HideMarker(); if (dragging) return;
            Button source = pending; pending = null;
            if (releaseCapture && source != null && source.IsMouseCaptured) source.ReleaseMouseCapture();
            if (interacting && owner.Dock != null) owner.Dock.InteractionDepth = Math.Max(0, owner.Dock.InteractionDepth - 1);
            interacting = false;
        }
        internal void SetLocked(bool locked) { if (locked) Cancel(); }
        internal void Reset()
        {
            Cancel();
            foreach (Button button in sources.Keys)
            { button.PreviewMouseLeftButtonDown -= OnDown; button.PreviewMouseMove -= OnMove; button.LostMouseCapture -= OnLost; button.AllowDrop = false; }
            foreach (Button button in targets.Keys)
            {
                if (targets[button] == null)
                {
                    button.PreviewDragEnter -= OnOver; button.PreviewDragOver -= OnOver;
                    button.PreviewDragLeave -= OnLeave; button.PreviewDrop -= OnDrop;
                }
                button.AllowDrop = false;
            }
            sources.Clear(); targets.Clear();
        }
        public void Dispose()
        {
            disposed = true; Reset(); hold.Tick -= OnHold;
            owner.PreviewMouseLeftButtonUp -= OnUp; owner.QueryContinueDrag -= OnContinue;
            if (scroll != null)
            {
                scroll.PreviewDragEnter -= OnBarOver; scroll.PreviewDragOver -= OnBarOver;
                scroll.PreviewDragLeave -= OnLeave; scroll.PreviewDrop -= OnBarDrop; scroll.AllowDrop = false;
            }
        }
        sealed class TabInsertionAdorner : Adorner
        {
            readonly MainWindow owner;
            internal double X;
            internal TabInsertionAdorner(UIElement element, MainWindow owner) : base(element) { this.owner = owner; IsHitTestVisible = false; }
            protected override void OnRender(DrawingContext drawing)
            {
                drawing.DrawLine(new Pen((Brush)owner.Resources["AccentBrush"], 3), new Point(X, 5),
                    new Point(X, Math.Max(5, AdornedElement.RenderSize.Height - 5)));
            }
        }
    }
}
