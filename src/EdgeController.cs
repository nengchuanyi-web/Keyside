using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ShortcutDock
{
    public enum DockAction { None, Expand, Collapse }

    // Pure timing logic is separate from HWND geometry so behavior can be verified without moving the user's mouse.
    public sealed class DockStateMachine
    {
        public bool Expanded { get; private set; }
        long hoverSince = -1, leaveSince = -1, graceUntil;
        bool heldBefore, dragFromPanel;
        public DockStateMachine() { Expanded = true; }
        public void Force(bool expanded, long now) { Expanded = expanded; hoverSince = leaveSince = -1; graceUntil = now + 1000; }
        public DockAction Step(bool edgeHovered, bool panelHovered, bool blocked, bool pinned, bool docked, long now, bool mouseHeld = false)
        {
            bool pressedOutside = mouseHeld && !heldBefore && !panelHovered && !edgeHovered;
            if (mouseHeld && !heldBefore) dragFromPanel = panelHovered;
            heldBefore = mouseHeld;
            if (!mouseHeld) dragFromPanel = false;
            if (!docked || pinned)
            {
                hoverSince = leaveSince = -1;
                if (!Expanded) { Expanded = true; return DockAction.Expand; }
                return DockAction.None;
            }
            if (blocked) { leaveSince = hoverSince = -1; return DockAction.None; }
            if (Expanded && pressedOutside && !dragFromPanel)
            { Force(false, now); return DockAction.Collapse; }
            if (mouseHeld && dragFromPanel) { leaveSince = -1; return DockAction.None; }
            if (!Expanded)
            {
                leaveSince = -1;
                if (mouseHeld) { hoverSince = -1; return DockAction.None; }
                if (!edgeHovered) { hoverSince = -1; return DockAction.None; }
                if (hoverSince < 0) hoverSince = now;
                if (now - hoverSince >= 120) { Force(true, now); return DockAction.Expand; }
            }
            else
            {
                hoverSince = -1;
                if (panelHovered || edgeHovered || now < graceUntil) { leaveSince = -1; return DockAction.None; }
                if (leaveSince < 0) leaveSince = now;
                if (now - leaveSince >= 650) { Force(false, now); return DockAction.Collapse; }
            }
            return DockAction.None;
        }
    }

    public sealed class DockPlan
    {
        public Native.RECT Panel, Trigger;
        public static DockPlan Calculate(Native.RECT work, string edge, double scale, double offset, int floatingX, int floatingY, double width = 440, double height = 660)
        {
            int w = Math.Min((int)Math.Round(width * scale), work.Width);
            int h = Math.Min((int)Math.Round(height * scale), work.Height);
            int strip = Math.Max(3, (int)Math.Round(6 * scale));
            int x = work.Left + (int)Math.Round((work.Width - w) * offset);
            int y = work.Top + (int)Math.Round((work.Height - h) * offset);
            if (edge == "Left") x = work.Left;
            if (edge == "Right") x = work.Right - w;
            if (edge == "Top") y = work.Top;
            if (edge == "Bottom") y = work.Bottom - h;
            if (edge == "Floating")
            {
                x = Math.Max(work.Left, Math.Min(work.Right - w, floatingX));
                y = Math.Max(work.Top, Math.Min(work.Bottom - h, floatingY));
            }
            Native.RECT panel = Native.RECT.From(x, y, w, h), trigger;
            if (edge == "Left") trigger = Native.RECT.From(work.Left, y, strip, h);
            else if (edge == "Top") trigger = Native.RECT.From(x, work.Top, w, strip);
            else if (edge == "Bottom") trigger = Native.RECT.From(x, work.Bottom - strip, w, strip);
            else trigger = Native.RECT.From(work.Right - strip, y, strip, h);
            return new DockPlan { Panel = panel, Trigger = trigger };
        }
        public static string NearestEdge(Native.RECT panel, Native.RECT work, double threshold)
        {
            // Crossing a boundary is contact, even when the title grab point leaves half the panel beyond it.
            double[] distances = { Math.Max(0, panel.Left - work.Left), Math.Max(0, work.Right - panel.Right), Math.Max(0, panel.Top - work.Top), Math.Max(0, work.Bottom - panel.Bottom) };
            string[] sides = { "Left", "Right", "Top", "Bottom" };
            int best = 0;
            for (int i = 1; i < 4; i++) if (distances[i] < distances[best]) best = i;
            return distances[best] <= threshold ? sides[best] : "Floating";
        }
    }

    public sealed class EdgeController : IDisposable
    {
        readonly MainWindow window;
        readonly Window trigger;
        readonly Border marker;
        readonly DispatcherTimer timer;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly DockStateMachine machine = new DockStateMachine();
        public DockPlan Plan { get; private set; }
        public Forms.Screen Screen { get; private set; }
        public bool Expanded { get { return machine.Expanded; } }
        public int InteractionDepth;
        public bool Dragging;
        public bool Resizing;
        int displaySignature;
        bool disposed;
        public EdgeController(MainWindow owner, bool startTimer)
        {
            window = owner;
            marker = new Border { Background = new SolidColorBrush(Color.FromRgb(115, 157, 227)), CornerRadius = new CornerRadius(3), Opacity = 0.75 };
            Grid root = new Grid { Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) }; root.Children.Add(marker);
            trigger = new Window {
                Title = "ShortcutDock edge", WindowStyle = WindowStyle.None, AllowsTransparency = true,
                Background = Brushes.Transparent, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
                ShowActivated = false, Topmost = startTimer, Width = 6, Height = 100, Content = root
            };
            trigger.SourceInitialized += delegate { Native.ToolWindow(trigger, true); };
            Place();
            machine.Force(true, clock.ElapsedMilliseconds);
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            timer.Tick += Tick;
            if (startTimer) timer.Start();
        }
        Forms.Screen ResolveScreen()
        {
            foreach (Forms.Screen screen in Forms.Screen.AllScreens)
                if (screen.DeviceName == window.State.Monitor) return screen;
            return Forms.Screen.PrimaryScreen;
        }
        static Native.RECT Work(Forms.Screen screen)
        {
            var area = screen.WorkingArea; return Native.RECT.From(area.Left, area.Top, area.Width, area.Height);
        }
        int Signature()
        {
            int signature = 17;
            foreach (Forms.Screen screen in Forms.Screen.AllScreens) signature = unchecked(signature * 31 + screen.WorkingArea.GetHashCode() + (int)(Native.Scale(screen) * 100));
            return signature;
        }
        public void Place()
        {
            Screen = ResolveScreen(); window.State.Monitor = Screen.DeviceName;
            Plan = DockPlan.Calculate(Work(Screen), window.State.Edge, Native.Scale(Screen), window.State.Offset, (int)window.State.FloatingX, (int)window.State.FloatingY, window.State.PanelWidth, window.State.PanelHeight);
            Native.Move(window, Plan.Panel);
            window.RefreshGlassLayer();
            bool vertical = window.State.Edge == "Left" || window.State.Edge == "Right";
            marker.Width = vertical ? Double.NaN : Math.Min(120, Plan.Trigger.Width / Native.Scale(Screen));
            marker.Height = vertical ? Math.Min(120, Plan.Trigger.Height / Native.Scale(Screen)) : Double.NaN;
            marker.HorizontalAlignment = vertical ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
            marker.VerticalAlignment = vertical ? VerticalAlignment.Center : VerticalAlignment.Stretch;
            Native.Move(trigger, Plan.Trigger);
            if (window.State.Edge == "Floating" || machine.Expanded) trigger.Hide(); else trigger.Show();
            displaySignature = Signature();
        }
        void Tick(object sender, EventArgs e)
        {
            if (disposed) return;
            if (Signature() != displaySignature) { Place(); window.RequestWallpaper(); }
            Native.POINT point;
            if (!Native.GetCursorPos(out point)) return;
            ProcessPointer(point, Native.MouseButtonDown(), Native.IsFullscreenOther(window, Screen), clock.ElapsedMilliseconds);
        }
        // The same production path is used by the integration harness with isolated pointer samples.
        public void ProcessPointer(Native.POINT point, bool mouseHeld, bool fullscreen, long now)
        {
            bool panelHovered = window.IsVisible && Native.Bounds(window).Contains(point);
            bool edgeHovered = Plan.Trigger.Contains(point) && !fullscreen;
            if (mouseHeld && !panelHovered && !edgeHovered) window.ReleaseSearchFocus();
            Apply(machine.Step(edgeHovered, panelHovered,
                InteractionDepth > 0 || Dragging || Resizing || window.HasActiveInteraction,
                window.State.Pinned, window.State.Edge != "Floating", now, mouseHeld));
        }
        void Apply(DockAction action)
        {
            if (action == DockAction.Expand) ShowWindow();
            if (action == DockAction.Collapse)
            {
                window.Hide(); trigger.Show(); Native.Move(trigger, Plan.Trigger);
            }
        }
        void ShowWindow()
        {
            trigger.Hide(); window.Show(); Native.Move(window, Plan.Panel);
            window.RefreshGlassLayer(); window.ApplyTheme();
            // Animate content inside its monitor, never park the HWND on the neighboring monitor.
            TranslateTransform transform = new TranslateTransform(); window.Surface.RenderTransform = transform;
            string edge = window.State.Edge;
            double offset = edge == "Left" || edge == "Top" ? -14 : 14;
            DependencyProperty property = edge == "Top" || edge == "Bottom" ? TranslateTransform.YProperty : TranslateTransform.XProperty;
            transform.BeginAnimation(property, new DoubleAnimation(offset, 0, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            window.Surface.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.25, 1, TimeSpan.FromMilliseconds(160)));
        }
        public void Expand() { machine.Force(true, clock.ElapsedMilliseconds); ShowWindow(); }
        public void Collapse()
        {
            if (window.State.Edge == "Floating") return;
            window.State.Pinned = false; machine.Force(false, clock.ElapsedMilliseconds); Apply(DockAction.Collapse); window.RefreshFooter(); window.Save();
        }
        public void ChangeEdge(string edge)
        {
            if (window.IsEditingLocked) return;
            window.State.Edge = edge; Place(); Expand(); window.RefreshFooter(); window.Save(); window.RequestWallpaper();
        }
        public void DragFinished()
        {
            if (window.IsEditingLocked) return;
            Native.RECT rect = Native.Bounds(window);
            Screen = Forms.Screen.FromRectangle(new System.Drawing.Rectangle(rect.Left, rect.Top, rect.Width, rect.Height));
            window.State.Monitor = Screen.DeviceName;
            Native.RECT work = Work(Screen);
            window.State.Edge = DockPlan.NearestEdge(rect, work, 64 * Native.Scale(Screen));
            bool vertical = window.State.Edge == "Left" || window.State.Edge == "Right";
            window.State.Offset = vertical ? (rect.Top - work.Top) / (double)Math.Max(1, work.Height - rect.Height)
                : (rect.Left - work.Left) / (double)Math.Max(1, work.Width - rect.Width);
            window.State.Offset = Math.Max(0, Math.Min(1, window.State.Offset));
            window.State.FloatingX = rect.Left; window.State.FloatingY = rect.Top;
            Place(); machine.Force(true, clock.ElapsedMilliseconds); window.RefreshFooter(); window.Save(); window.RequestWallpaper();
        }
        public void ResizeTo(Native.RECT rect, bool commit)
        {
            if (window.IsEditingLocked) return;
            double scale = Native.Scale(Screen); Native.RECT work = Work(Screen);
            // Remember preferred DIP sizes independently from work-area clamping.
            window.State.PanelWidth = rect.Width / scale; window.State.PanelHeight = rect.Height / scale;
            window.State.FloatingX = rect.Left; window.State.FloatingY = rect.Top;
            bool vertical = window.State.Edge == "Left" || window.State.Edge == "Right";
            window.State.Offset = Math.Max(0, Math.Min(1, vertical ? (rect.Top - work.Top) / (double)Math.Max(1, work.Height - rect.Height)
                : (rect.Left - work.Left) / (double)Math.Max(1, work.Width - rect.Width)));
            Place();
            if (commit) { machine.Force(true, clock.ElapsedMilliseconds); window.Save(); window.RefreshFooter(); }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; timer.Stop(); trigger.Close();
        }
    }
}
