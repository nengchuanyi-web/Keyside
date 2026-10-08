using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace ShortcutDock
{
    // Like Glance, draw a blurred picture of the backdrop as its own layer.
    // Only this layer is blurred; text, controls and emoji keep their original alpha.
    // Captures stay in memory and the panel is excluded only during the synchronous copy.
    sealed class DesktopGlass : IDisposable
    {
        readonly MainWindow owner;
        readonly DispatcherTimer timer;
        readonly Stopwatch clock = Stopwatch.StartNew();
        Image layer;
        Native.RECT lastPanel, lastCapture;
        BitmapSource source;
        bool enabled, disposed, available;
        long capturedAt = -1000;
        public bool Dark { get; private set; }
        public bool Available { get { return available; } }

        [DllImport("user32.dll")] static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);
        [DllImport("user32.dll")] static extern bool GetWindowDisplayAffinity(IntPtr hwnd, out uint affinity);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);
        [DllImport("dwmapi.dll")] static extern int DwmFlush();

        public DesktopGlass(MainWindow owner)
        {
            this.owner = owner;
            Dark = ThemeService.SystemDark();
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            timer.Tick += delegate {
                if (!owner.IsVisible || !enabled || disposed) return;
                bool oldDark = Dark, oldAvailable = available;
                Refresh(true);
                if (Dark != oldDark || available != oldAvailable) owner.ApplyTheme();
            };
        }
        public void Attach(Grid frame)
        {
            layer = new Image { Stretch = Stretch.Fill, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false, Visibility = Visibility.Collapsed,
                Effect = new BlurEffect { Radius = 18, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance } };
            frame.Children.Insert(0, layer);
            if (source != null) ShowSource();
        }
        public bool Enable(bool value)
        {
            enabled = value;
            if (value)
            {
                if (!timer.IsEnabled) { capturedAt = -1000; timer.Start(); }
                Refresh(false);
            }
            else { timer.Stop(); if (layer != null) layer.Visibility = Visibility.Collapsed; }
            return value && available;
        }
        public void Refresh(bool force)
        {
            if (!enabled || disposed) return;
            // Keep owned editors, file dialogs and popup menus out of the backdrop picture.
            if ((owner.Dock != null && owner.Dock.InteractionDepth > 0) || owner.HasActiveInteraction) return;
            Native.RECT panel = Native.Bounds(owner);
            if (panel.Width <= 0 || panel.Height <= 0) return;
            if (!force && clock.ElapsedMilliseconds - capturedAt < 550 && panel.Equals(lastPanel)) { ShowSource(); return; }
            // Older Windows treats 0x11 as a black capture instead of excluding the window.
            if (Environment.OSVersion.Version.Build < 19041) { Unavailable(); return; }
            IntPtr hwnd = Native.Handle(owner); uint previous;
            if (!GetWindowDisplayAffinity(hwnd, out previous) || !SetWindowDisplayAffinity(hwnd, 0x11)) { Unavailable(); return; }
            try
            {
                DwmFlush();
                double scale = HwndSource.FromHwnd(hwnd).CompositionTarget.TransformToDevice.M11;
                int reach = (int)Math.Ceiling(24 * scale);
                var screen = Forms.Screen.FromRectangle(new Drawing.Rectangle(panel.Left, panel.Top, panel.Width, panel.Height)).Bounds;
                Native.RECT capture = Native.RECT.From(Math.Max(screen.Left, panel.Left - reach), Math.Max(screen.Top, panel.Top - reach), 0, 0);
                capture.Right = Math.Min(screen.Right, panel.Right + reach); capture.Bottom = Math.Min(screen.Bottom, panel.Bottom + reach);
                using (Drawing.Bitmap bitmap = new Drawing.Bitmap(capture.Width, capture.Height, Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (Drawing.Graphics graphics = Drawing.Graphics.FromImage(bitmap))
                        graphics.CopyFromScreen(capture.Left, capture.Top, 0, 0, bitmap.Size);
                    double brightness = Brightness(bitmap);
                    Dark = ThemeService.DarkForBrightness(brightness, Dark);
                    IntPtr handle = bitmap.GetHbitmap();
                    try
                    {
                        BitmapSource raw = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        source = new FormatConvertedBitmap(raw, PixelFormats.Bgr32, null, 0); source.Freeze();
                    }
                    finally { DeleteObject(handle); }
                }
                available = true; lastPanel = panel; lastCapture = capture; capturedAt = clock.ElapsedMilliseconds;
                ShowSource();
            }
            catch (Exception) { Unavailable(); }
            finally { SetWindowDisplayAffinity(hwnd, previous); }
        }
        static double Brightness(Drawing.Bitmap bitmap)
        {
            // Bilinear downsampling avoids aliasing thin background stripes into one color.
            using (Drawing.Bitmap sample = new Drawing.Bitmap(32, 32))
            {
                using (Drawing.Graphics graphics = Drawing.Graphics.FromImage(sample))
                {
                    graphics.InterpolationMode = Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                    graphics.DrawImage(bitmap, new Drawing.Rectangle(0, 0, 32, 32));
                }
                double sum = 0;
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                {
                    var c = sample.GetPixel(x, y); sum += ThemeService.Luminance(c.R, c.G, c.B);
                }
                return sum / 1024;
            }
        }
        void ShowSource()
        {
            if (layer == null || !available || !enabled) return;
            double scale = HwndSource.FromHwnd(Native.Handle(owner)).CompositionTarget.TransformToDevice.M11;
            layer.Source = source; layer.Width = lastCapture.Width / scale; layer.Height = lastCapture.Height / scale;
            layer.Margin = new Thickness((lastCapture.Left - lastPanel.Left) / scale, (lastCapture.Top - lastPanel.Top) / scale, 0, 0);
            layer.Visibility = Visibility.Visible;
        }
        void Unavailable() { available = false; if (layer != null) layer.Visibility = Visibility.Collapsed; }
        public void Dispose() { disposed = true; timer.Stop(); source = null; if (layer != null) layer.Source = null; }
    }
}
