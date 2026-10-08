using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace ShortcutDock
{
    public static class ThemeService
    {
        public static bool SystemDark()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                return key != null && Convert.ToInt32(key.GetValue("AppsUseLightTheme", 1)) == 0;
        }
        public static double Luminance(int r, int g, int b)
        {
            Func<int, double> linear = x => x / 255.0 <= 0.04045 ? x / 255.0 / 12.92 : Math.Pow((x / 255.0 + 0.055) / 1.055, 2.4);
            return 0.2126 * linear(r) + 0.7152 * linear(g) + 0.0722 * linear(b);
        }
        public static bool DarkForBrightness(double brightness, bool previousDark)
        {
            // Hysteresis keeps wallpapers near the threshold from repeatedly changing the theme.
            if (brightness < 0.36) return true;
            if (brightness > 0.44) return false;
            return previousDark;
        }
        public static Task<bool> SampleAsync(Forms.Screen screen, bool previousDark)
        {
            return Task.Factory.StartNew(delegate {
                try
                {
                    string path = null; uint background = 0; bool backgroundAvailable = false;
                    object shell = null;
                    try
                    {
                        shell = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD")));
                        IDesktopWallpaper wallpaper = (IDesktopWallpaper)shell; uint count;
                        wallpaper.GetMonitorDevicePathCount(out count);
                        for (uint i = 0; i < count; i++)
                        {
                            string id; Native.RECT bounds; wallpaper.GetMonitorDevicePathAt(i, out id); wallpaper.GetMonitorRECT(id, out bounds);
                            var area = screen.Bounds;
                            if (bounds.Left == area.Left && bounds.Top == area.Top && bounds.Width == area.Width && bounds.Height == area.Height)
                            { wallpaper.GetWallpaper(id, out path); break; }
                        }
                        wallpaper.GetBackgroundColor(out background); backgroundAvailable = true;
                    }
                    catch (COMException) { } catch (InvalidCastException) { }
                    finally { if (shell != null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell); }
                    if (String.IsNullOrEmpty(path)) path = Native.WallpaperPath();
                    if (!String.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        using (System.Drawing.Image image = System.Drawing.Image.FromStream(stream))
                        using (Bitmap tiny = new Bitmap(64, 64))
                        {
                            using (Graphics g = Graphics.FromImage(tiny)) g.DrawImage(image, 0, 0, 64, 64);
                            double sum = 0;
                            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                            { System.Drawing.Color c = tiny.GetPixel(x, y); sum += Luminance(c.R, c.G, c.B); }
                            return DarkForBrightness(sum / 4096, previousDark);
                        }
                    }
                    if (backgroundAvailable)
                        return DarkForBrightness(Luminance((int)(background & 255), (int)((background >> 8) & 255), (int)((background >> 16) & 255)), previousDark);
                    return SystemDark();
                }
                catch (Exception) { return SystemDark(); }
            });
        }
        static SolidColorBrush Brush(string value) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(value); b.Freeze(); return b; }
        public static void Apply(MainWindow window, bool dark)
        {
            bool glass = window.State.Theme == "Glass";
            bool capturedGlass = window.EnableGlassLayer(glass);
            bool effectiveDark = window.State.Theme == "Dark" || (window.State.Theme == "Auto" && dark) || (glass && (capturedGlass ? window.GlassBackgroundDark : SystemDark()));
            string palette = PanelPalette.Normalize(window.State.PanelColor);
            System.Windows.Media.Color baseColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(palette);
            System.Windows.Media.Color neutral = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(effectiveDark ? "#171A20" : "#F6F7FA");
            System.Windows.Media.Color color = Mix(neutral, baseColor, effectiveDark ? 0.025 : 0.18);
            bool nativeGlass = Native.Backdrop(window, glass && !capturedGlass, effectiveDark, window.State.PanelOpacity, color);
            window.Opacity = 1;
            window.GlassAvailable = capturedGlass || nativeGlass;
            ResourceDictionary r = window.Resources;
            r["DialogSurfaceBrush"] = new SolidColorBrush(color);
            System.Windows.Media.Color surface = color;
            surface.A = (byte)Math.Round(255 * window.State.PanelOpacity * (glass && window.GlassAvailable ? 0.58 : 1));
            r["SurfaceBrush"] = new SolidColorBrush(surface);
            r["TextBrush"] = Brush(effectiveDark ? "#EEF1F8" : "#222A3B");
            r["MutedBrush"] = Brush(effectiveDark ? "#A5AEC0" : "#657186");
            r["LineBrush"] = Brush(effectiveDark ? "#344052" : "#DDE3ED");
            r["ControlBrush"] = Brush(effectiveDark ? "#262C38" : "#FFFFFF");
            System.Windows.Media.Color panelControl = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(effectiveDark ? "#262C38" : "#FFFFFF");
            panelControl.A = (byte)(glass ? Math.Round(255 * 0.6 * window.State.PanelOpacity) : 255);
            r["PanelControlBrush"] = new SolidColorBrush(panelControl);
            r["HoverBrush"] = new SolidColorBrush(Mix((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(effectiveDark ? "#364257" : "#E7EDF7"), baseColor, effectiveDark ? 0.03 : 0.12));
            r["AccentBrush"] = new SolidColorBrush(effectiveDark ? Mix(baseColor, Colors.White, 0.18) : PanelPalette.Accent(palette));
            r["AccentFillBrush"] = new SolidColorBrush(effectiveDark ? Mix((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#262C38"), PanelPalette.Accent(palette), 0.35) : Mix(Colors.White, baseColor, 0.62));
            if (glass)
            {
                System.Windows.Media.Color fill = ((SolidColorBrush)r["AccentFillBrush"]).Color;
                fill.A = (byte)Math.Round(255 * 0.45 * window.State.PanelOpacity); r["AccentFillBrush"] = new SolidColorBrush(fill);
                r["LineBrush"] = Brush(effectiveDark ? "#66FFFFFF" : "#55657186");
            }
            window.RefreshFooter();
        }
        public static System.Windows.Media.Color Mix(System.Windows.Media.Color a, System.Windows.Media.Color b, double amount)
        {
            return System.Windows.Media.Color.FromRgb((byte)Math.Round(a.R + (b.R - a.R) * amount),
                (byte)Math.Round(a.G + (b.G - a.G) * amount), (byte)Math.Round(a.B + (b.B - a.B) * amount));
        }
    }
}
