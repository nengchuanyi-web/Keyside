using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ShortcutDock
{
    [Flags] public enum ResizeEdges { Left = 1, Right = 2, Top = 4, Bottom = 8 }

    public static class ResizeGeometry
    {
        public static Native.RECT Calculate(Native.RECT start, ResizeEdges edges, int dx, int dy, Native.RECT work, double scale, string dockEdge, double minimumHeight = 450)
        {
            bool left = (edges & ResizeEdges.Left) != 0, right = (edges & ResizeEdges.Right) != 0;
            bool top = (edges & ResizeEdges.Top) != 0, bottom = (edges & ResizeEdges.Bottom) != 0;
            int minW = Math.Min(work.Width, (int)Math.Round(360 * scale));
            int minH = Math.Min(work.Height, (int)Math.Round(minimumHeight * scale));
            int maxW = Math.Min(work.Width, (int)Math.Round(1400 * scale));
            int maxH = Math.Min(work.Height, (int)Math.Round(1600 * scale));
            int w = Math.Max(minW, Math.Min(maxW, start.Width + (right ? dx : left ? -dx : 0)));
            int h = Math.Max(minH, Math.Min(maxH, start.Height + (bottom ? dy : top ? -dy : 0)));
            int x = left ? start.Right - w : start.Left, y = top ? start.Bottom - h : start.Top;
            if (dockEdge == "Left") x = work.Left;
            if (dockEdge == "Right") x = work.Right - w;
            if (dockEdge == "Top") y = work.Top;
            if (dockEdge == "Bottom") y = work.Bottom - h;
            x = Math.Max(work.Left, Math.Min(work.Right - w, x));
            y = Math.Max(work.Top, Math.Min(work.Bottom - h, y));
            return Native.RECT.From(x, y, w, h);
        }
    }

    // WPF thumbs give eight familiar grips without handing resizing to Windows' desktop Snap feature.
    public sealed class ResizeController
    {
        readonly MainWindow window;
        Native.RECT start, work;
        Native.POINT startPointer;
        double scale;
        public ResizeController(MainWindow owner, Grid frame)
        {
            window = owner;
            Add(frame, ResizeEdges.Left, HorizontalAlignment.Left, VerticalAlignment.Stretch, 7, Double.NaN, Cursors.SizeWE);
            Add(frame, ResizeEdges.Right, HorizontalAlignment.Right, VerticalAlignment.Stretch, 7, Double.NaN, Cursors.SizeWE);
            Add(frame, ResizeEdges.Top, HorizontalAlignment.Stretch, VerticalAlignment.Top, Double.NaN, 7, Cursors.SizeNS);
            Add(frame, ResizeEdges.Bottom, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, Double.NaN, 7, Cursors.SizeNS);
            Add(frame, ResizeEdges.Left | ResizeEdges.Top, HorizontalAlignment.Left, VerticalAlignment.Top, 15, 15, Cursors.SizeNWSE);
            Add(frame, ResizeEdges.Right | ResizeEdges.Top, HorizontalAlignment.Right, VerticalAlignment.Top, 15, 15, Cursors.SizeNESW);
            Add(frame, ResizeEdges.Left | ResizeEdges.Bottom, HorizontalAlignment.Left, VerticalAlignment.Bottom, 15, 15, Cursors.SizeNESW);
            Add(frame, ResizeEdges.Right | ResizeEdges.Bottom, HorizontalAlignment.Right, VerticalAlignment.Bottom, 15, 15, Cursors.SizeNWSE);
        }
        void Add(Grid frame, ResizeEdges edges, HorizontalAlignment horizontal, VerticalAlignment vertical, double width, double height, Cursor cursor)
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            Thumb thumb = new Thumb { Width = width, Height = height, HorizontalAlignment = horizontal, VerticalAlignment = vertical,
                Cursor = cursor, ToolTip = window.T["Resize"], Template = new ControlTemplate(typeof(Thumb)) { VisualTree = border } };
            thumb.Tag = cursor; window.RegisterEditingControl(thumb);
            Panel.SetZIndex(thumb, 10); frame.Children.Add(thumb);
            thumb.DragStarted += delegate {
                if (window.Dock == null || window.IsEditingLocked) return;
                window.Dock.Resizing = true; start = Native.Bounds(window); Native.GetCursorPos(out startPointer);
                var area = window.Dock.Screen.WorkingArea; work = Native.RECT.From(area.Left, area.Top, area.Width, area.Height);
                scale = Native.Scale(window.Dock.Screen);
            };
            thumb.DragDelta += delegate {
                if (window.Dock == null || !window.Dock.Resizing || window.IsEditingLocked) return;
                Native.POINT point; if (!Native.GetCursorPos(out point)) return;
                window.Dock.ResizeTo(ResizeGeometry.Calculate(start, edges, point.X - startPointer.X, point.Y - startPointer.Y, work, scale, window.State.Edge, FontSizing.MinimumHeight(window.State.FontScale)), false);
            };
            thumb.DragCompleted += delegate {
                if (window.Dock == null || !window.Dock.Resizing) return;
                window.Dock.Resizing = false; window.Dock.ResizeTo(Native.Bounds(window), true);
            };
        }
    }
}
