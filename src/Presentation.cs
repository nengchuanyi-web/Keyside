using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShortcutDock
{
    public static class PanelPalette
    {
        public const string Blue = "#E0EAFE", Pink = "#FEE0EA", Green = "#EAFEE0";
        public static readonly string[] Colors = { Blue, Pink, Green };
        public static readonly string[] Labels = { "PaletteBlue", "PalettePink", "PaletteGreen" };
        public static string Normalize(string hex)
        {
            if (String.IsNullOrEmpty(hex)) return Blue;
            Color source = (Color)ColorConverter.ConvertFromString(hex);
            string closest = Blue; double distance = Double.MaxValue;
            foreach (string preset in Colors)
            {
                Color color = (Color)ColorConverter.ConvertFromString(preset);
                double d = Math.Pow(color.R - source.R, 2) + Math.Pow(color.G - source.G, 2) + Math.Pow(color.B - source.B, 2);
                if (d < distance) { closest = preset; distance = d; }
            }
            return closest;
        }
        public static Color Accent(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex == Pink ? "#956078" : hex == Green ? "#5D7950" : "#3669BC");
        }
    }
    public static class FontSizing
    {
        public static double MinimumHeight(double scale)
        {
            // Header / toolbar + table header + at least one complete shortcut row.
            return Math.Max(450, Math.Ceiling(303 + (scale - 1) * 126 + 28 + 84 * scale + 12));
        }
        public static string Key(double baseline) { return "FontSize" + baseline.ToString("0", CultureInfo.InvariantCulture); }
        public static void Set(FrameworkElement element, double baseline)
        {
            element.SetResourceReference(TextBlock.FontSizeProperty, Key(baseline));
        }
        public static void Apply(MainWindow window)
        {
            double scale = window.State.FontScale;
            foreach (double size in new double[] { 10, 11, 12, 13, 15, 18, 20, 21, 23 }) window.Resources[Key(size)] = size * scale;
            window.Resources["TableRowHeight"] = 46 * scale;
            window.Resources["BrandIconSize"] = 16 * scale;
            double[] heights = { 84, 49, 61, 56, 29, 24 };
            double[] growth = { 44, 18, 18, 26, 10, 10 };
            for (int i = 0; i < heights.Length; i++) window.Resources["PanelRow" + heights[i]] = new GridLength(heights[i] + (scale - 1) * growth[i]);
            window.SetResourceReference(Control.FontSizeProperty, Key(13));
        }
        public static void Adapt(DependencyObject root)
        {
            FrameworkElement element = root as FrameworkElement;
            if (element != null)
            {
                object local = element.ReadLocalValue(TextBlock.FontSizeProperty);
                if (local is double) Set(element, (double)local);
            }
            foreach (object child in LogicalTreeHelper.GetChildren(root))
            { DependencyObject node = child as DependencyObject; if (node != null) Adapt(node); }
        }
    }
    public static class BrandIcon
    {
        static BitmapImage image;
        public static BitmapImage Source
        {
            get
            {
                if (image != null) return image;
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ShortcutDock.Xiaohongshu.png"))
                {
                    image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
                }
            }
        }
    }
}
