using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace ShortcutDock
{
    public static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct RECT
        {
            public int Left, Top, Right, Bottom;
            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }
            public bool Contains(POINT p) { return p.X >= Left && p.X < Right && p.Y >= Top && p.Y < Bottom; }
            public static RECT From(int x, int y, int w, int h) { return new RECT { Left = x, Top = y, Right = x + w, Bottom = y + h }; }
        }
        [StructLayout(LayoutKind.Sequential)] struct MARGINS { public int Left, Right, Top, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct ACCENT { public int State, Flags, Color, Animation; }
        [StructLayout(LayoutKind.Sequential)] struct COMPOSITION { public int Attribute; public IntPtr Data; public int Size; }
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder name, int count);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);
        [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref COMPOSITION data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SystemParametersInfo(uint action, uint param, StringBuilder value, uint flags);
        [DllImport("user32.dll", EntryPoint="GetWindowLongW")] static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint="SetWindowLongW")] static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        public static IntPtr Handle(Window window) { return new WindowInteropHelper(window).EnsureHandle(); }
        public static void ToolWindow(Window window, bool neverActivate)
        {
            IntPtr hwnd = Handle(window);
            int style = GetWindowLong(hwnd, -20) | 0x80;
            if (neverActivate) style |= 0x08000000;
            SetWindowLong(hwnd, -20, style);
            HwndSource source = HwndSource.FromHwnd(hwnd);
            source.AddHook(delegate(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) {
                if (msg == 0x21) { handled = true; return new IntPtr(3); } // Clicks work, hover/click do not steal activation.
                return IntPtr.Zero;
            });
        }
        public static void Move(Window window, RECT rect)
        {
            SetWindowPos(Handle(window), window.Topmost ? new IntPtr(-1) : IntPtr.Zero,
                rect.Left, rect.Top, rect.Width, rect.Height, 0x10 | (window.Topmost ? 0u : 4u));
        }
        public static RECT Bounds(Window window) { RECT r; GetWindowRect(Handle(window), out r); return r; }
        public static bool MouseButtonDown()
        {
            return GetAsyncKeyState(1) < 0 || GetAsyncKeyState(2) < 0 || GetAsyncKeyState(4) < 0;
        }
        public static void RoundedRegion(Window window)
        {
            if (!window.AllowsTransparency) return;
            RECT rect = Bounds(window);
            if (rect.Width <= 0 || rect.Height <= 0) return;
            double scale = HwndSource.FromHwnd(Handle(window)).CompositionTarget.TransformToDevice.M11;
            int diameter = (int)Math.Round(30 * scale);
            IntPtr region = CreateRoundRectRgn(0, 0, rect.Width + 1, rect.Height + 1, diameter, diameter);
            if (region != IntPtr.Zero && SetWindowRgn(Handle(window), region, true) == 0) DeleteObject(region);
        }
        public static double Scale(Forms.Screen screen)
        {
            try
            {
                uint x, y; var r = screen.Bounds;
                if (GetDpiForMonitor(MonitorFromPoint(new POINT { X = r.Left + r.Width / 2, Y = r.Top + r.Height / 2 }, 2), 0, out x, out y) == 0)
                    return x / 96.0;
            } catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
            return 1;
        }
        public static bool IsFullscreenOther(Window window, Forms.Screen screen)
        {
            IntPtr front = GetForegroundWindow(); RECT r;
            if (front == IntPtr.Zero || front == Handle(window) || !GetWindowRect(front, out r)) return false;
            StringBuilder className = new StringBuilder(256); GetClassName(front, className, className.Capacity);
            if (className.ToString() == "Progman" || className.ToString() == "WorkerW" || className.ToString() == "Shell_TrayWnd") return false;
            var b = screen.Bounds;
            // Maximized normal apps use the work area; only exclusive/borderless full-screen blocks hover.
            return r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
        }
        public static string WallpaperPath()
        {
            StringBuilder path = new StringBuilder(32768);
            return SystemParametersInfo(0x0073, (uint)path.Capacity, path, 0) ? path.ToString() : null;
        }
        public static bool Backdrop(Window window, bool glass, bool dark, double opacity = 1, Color? tintColor = null)
        {
            IntPtr hwnd = Handle(window);
            try
            {
                int corner = 2, mode = dark ? 1 : 0, kind = glass && !window.AllowsTransparency ? 3 : 1;
                DwmSetWindowAttribute(hwnd, 33, ref corner, 4);
                DwmSetWindowAttribute(hwnd, 20, ref mode, 4);
                HwndSource source = HwndSource.FromHwnd(hwnd);
                source.CompositionTarget.BackgroundColor = Colors.Transparent;
                MARGINS margins = glass && !window.AllowsTransparency ? new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 } : new MARGINS();
                DwmExtendFrameIntoClientArea(hwnd, ref margins);
                int result = DwmSetWindowAttribute(hwnd, 38, ref kind, 4);
                if (result == 0 && glass && !window.AllowsTransparency) return true;
                // Native acrylic compatibility fallback; DWM system-backdrop does not support layered WPF.
                // Keep the compositor tint subtle. The WPF background supplies the adjustable tint;
                // its foreground stays opaque, without any whole-window alpha reduction.
                uint tint = (uint)Math.Max(1, Math.Round(26 * opacity)) << 24;
                uint rgb = tintColor.HasValue ? (uint)(tintColor.Value.R | tintColor.Value.G << 8 | tintColor.Value.B << 16) : dark ? 0x00201A17u : 0x00DDF0F5u;
                // Compatibility fallback when a separate captured-glass layer is unavailable.
                ACCENT accent = new ACCENT { State = glass ? 4 : 0, Flags = 2, Color = unchecked((int)(tint | rgb)) };
                IntPtr memory = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ACCENT)));
                try
                {
                    Marshal.StructureToPtr(accent, memory, false);
                    COMPOSITION data = new COMPOSITION { Attribute = 19, Data = memory, Size = Marshal.SizeOf(typeof(ACCENT)) };
                    bool enabled = SetWindowCompositionAttribute(hwnd, ref data) != 0 && glass;
                    RoundedRegion(window); return enabled;
                } finally { Marshal.FreeHGlobal(memory); }
            }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }
    }

    // Only the leading vtable entries are needed; order follows shobjidl_core.h.
    [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitor, [MarshalAs(UnmanagedType.LPWStr)] string path);
        void GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitor, [MarshalAs(UnmanagedType.LPWStr)] out string path);
        void GetMonitorDevicePathAt(uint index, [MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetMonitorDevicePathCount(out uint count);
        void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitor, out Native.RECT rect);
        void SetBackgroundColor(uint color);
        void GetBackgroundColor(out uint color);
    }
}
