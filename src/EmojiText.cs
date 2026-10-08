using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Automation;

namespace ShortcutDock
{
    // Longest-sequence matching keeps ZWJ, flags, modifiers and keycaps together.
    public static class EmojiCatalog
    {
        sealed class Node { public Dictionary<char, Node> Children = new Dictionary<char, Node>(); public string Asset; }
        static readonly Node root = new Node();
        static readonly Dictionary<string, BitmapImage> cache = new Dictionary<string, BitmapImage>();
        static readonly ZipArchive archive;
        public static readonly int SequenceCount;
        static EmojiCatalog()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            archive = new ZipArchive(assembly.GetManifestResourceStream("ShortcutDock.emoji.zip"), ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string key = Path.GetFileNameWithoutExtension(entry.Name);
                Add(FromCodePoints(key), key);
            }
            using (StreamReader reader = new StreamReader(assembly.GetManifestResourceStream("ShortcutDock.emoji-map.txt"), Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] pair = line.Split('\t'); if (pair.Length != 2) continue;
                    Add(FromCodePoints(pair[0]), pair[1]); SequenceCount++;
                }
            }
        }
        public static string FromCodePoints(string points)
        {
            StringBuilder text = new StringBuilder();
            foreach (string point in points.Split('-')) text.Append(Char.ConvertFromUtf32(Convert.ToInt32(point, 16)));
            return text.ToString();
        }
        static void Add(string sequence, string asset)
        {
            Node node = root;
            foreach (char c in sequence)
            {
                Node next; if (!node.Children.TryGetValue(c, out next)) { next = new Node(); node.Children.Add(c, next); }
                node = next;
            }
            node.Asset = asset;
        }
        public static int Match(string text, int start, out string asset)
        {
            Node node = root; int length = 0; asset = null;
            for (int i = start; i < text.Length; i++)
            {
                Node next; if (!node.Children.TryGetValue(text[i], out next)) break;
                node = next; if (node.Asset != null) { length = i - start + 1; asset = node.Asset; }
            }
            // A text presentation selector explicitly requests a normal text glyph.
            if (length > 0 && start + length < text.Length && text[start + length] == '\uFE0E') { asset = null; return 0; }
            return length;
        }
        public static BitmapImage Image(string asset)
        {
            BitmapImage image;
            if (cache.TryGetValue(asset, out image)) return image;
            using (Stream stream = archive.GetEntry(asset + ".png").Open())
            using (MemoryStream seekable = new MemoryStream())
            {
                stream.CopyTo(seekable); seekable.Position = 0;
                image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = seekable; image.EndInit(); image.Freeze();
            }
            cache.Add(asset, image); return image;
        }
    }
    public sealed class EmojiText : TextBlock
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(string), typeof(EmojiText),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure, delegate(DependencyObject d, DependencyPropertyChangedEventArgs e) { ((EmojiText)d).Render(); }));
        public string Value { get { return (string)GetValue(ValueProperty); } set { SetValue(ValueProperty, value); } }
        public bool SuppressTooltip { get; set; }
        static EmojiText()
        {
            FontSizeProperty.OverrideMetadata(typeof(EmojiText), new FrameworkPropertyMetadata(12.0,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure,
                delegate(DependencyObject d, DependencyPropertyChangedEventArgs e) { ((EmojiText)d).ResizeImages(); }));
        }
        public EmojiText() { FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, Segoe UI Emoji"); }
        void ResizeImages()
        {
            foreach (InlineUIContainer container in Inlines.OfType<InlineUIContainer>())
            { Image image = container.Child as Image; if (image != null) image.Width = image.Height = FontSize * 1.5; }
            ToolTip tip = ToolTip as ToolTip; EmojiText preview = tip == null ? null : tip.Content as EmojiText;
            if (preview != null) preview.FontSize = FontSize;
        }
        void Render()
        {
            Inlines.Clear(); string value = Value ?? ""; StringBuilder plain = new StringBuilder();
            Action flush = delegate { if (plain.Length > 0) { Inlines.Add(new Run(plain.ToString())); plain.Clear(); } };
            for (int i = 0; i < value.Length;)
            {
                string asset; int length = EmojiCatalog.Match(value, i, out asset);
                if (length > 0)
                {
                    flush(); string sequence = value.Substring(i, length);
                    Image image = new Image { Source = EmojiCatalog.Image(asset), Width = FontSize * 1.5, Height = FontSize * 1.5,
                        Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 1, 0), ToolTip = sequence };
                    AutomationProperties.SetName(image, sequence);
                    Inlines.Add(new InlineUIContainer(image) { BaselineAlignment = BaselineAlignment.Center }); i += length;
                }
                else { plain.Append(value[i]); i++; }
            }
            flush(); AutomationProperties.SetName(this, value);
            if (!SuppressTooltip) ToolTip = String.IsNullOrEmpty(value) ? null : new ToolTip { Content = new EmojiText { SuppressTooltip = true, FontSize = FontSize, Value = value, TextWrapping = TextWrapping.Wrap, MaxWidth = 380 } };
        }
    }
}
