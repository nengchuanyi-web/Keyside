using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Windows.Documents;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ShortcutDock
{
    // Integration harness is opt-in and uses an isolated data directory. No global input injection.
    static class Verification
    {
        static readonly List<string> checks = new List<string>();
        static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + name);
            checks.Add("PASS: " + name);
        }
        static void Logic(StateStore store)
        {
            DockStateMachine m = new DockStateMachine(); m.Force(false, 0);
            Check(m.Step(true, false, false, false, true, 10) == DockAction.None, "edge debounce");
            Check(m.Step(true, false, false, false, true, 131) == DockAction.Expand, "edge hover expands at 120 ms");
            Check(m.Step(false, false, false, false, true, 1140) == DockAction.None, "leave starts delay");
            Check(m.Step(false, false, false, false, true, 1791) == DockAction.Collapse, "leave collapses after 650 ms");
            Check(m.Step(true, false, true, false, true, 2000) == DockAction.None, "modal/menu blocks hover");
            Check(m.Step(false, false, false, true, true, 2100) == DockAction.Expand, "pin keeps expanded");
            Check(m.Step(false, false, true, false, true, 5000) == DockAction.None && m.Expanded, "modal blocks collapse");
            m.Force(false, 0);
            Check(m.Step(false, false, false, false, false, 0) == DockAction.Expand, "floating never auto hides");
            m = new DockStateMachine(); m.Force(true, 0);
            Check(m.Step(false, false, false, false, true, 50, true) == DockAction.Collapse, "outside press overrides opening grace");
            Check(m.Step(true, false, false, false, true, 100, true) == DockAction.None, "external mouse drag does not reopen panel");
            m = new DockStateMachine(); m.Force(true, 0);
            Check(m.Step(false, true, false, false, true, 50, true) == DockAction.None && m.Step(false, false, false, false, true, 800, true) == DockAction.None && m.Expanded, "press inside keeps drag interaction open");
            Check(m.Step(false, false, false, false, true, 1100, false) == DockAction.None && m.Step(false, false, false, false, true, 1751, false) == DockAction.Collapse, "release outside resumes automatic collapse");
            m = new DockStateMachine(); m.Force(true, 0);
            Check(m.Step(false, false, false, true, true, 20, true) == DockAction.None && m.Expanded, "pinned panel ignores outside press");
            m = new DockStateMachine(); m.Force(true, 0);
            Check(m.Step(false, false, true, false, true, 20, true) == DockAction.None && m.Expanded, "modal ignores outside press");
            Native.RECT work = Native.RECT.From(-1920, -200, 1920, 1080);
            foreach (string edge in new[] { "Left", "Right", "Top", "Bottom", "Floating" })
            {
                DockPlan p = DockPlan.Calculate(work, edge, 1.5, 0.7, -1000, 10);
                Check(p.Panel.Left >= work.Left && p.Panel.Top >= work.Top && p.Panel.Right <= work.Right && p.Panel.Bottom <= work.Bottom, "negative monitor bounds / 150% DPI / " + edge);
                Check(p.Trigger.Left >= work.Left && p.Trigger.Top >= work.Top && p.Trigger.Right <= work.Right && p.Trigger.Bottom <= work.Bottom, "trigger inside monitor / " + edge);
            }
            DockPlan tiny = DockPlan.Calculate(Native.RECT.From(0, 0, 320, 240), "Bottom", 2, 0.5, 0, 0);
            Check(tiny.Panel.Width == 320 && tiny.Panel.Height == 240, "small work area clamps panel");
            Check(DockPlan.NearestEdge(Native.RECT.From(-1920, -180, 440, 660), work, 30) == "Left", "drag snap to left");
            Check(DockPlan.NearestEdge(Native.RECT.From(-1300, 0, 440, 660), work, 30) == "Floating", "drag away becomes floating");
            Check(DockPlan.NearestEdge(Native.RECT.From(-2100, 0, 440, 660), work, 64) == "Left", "crossed left edge still docks (reported regression)");
            Check(DockPlan.NearestEdge(Native.RECT.From(-240, 0, 440, 660), work, 64) == "Right", "crossed right edge still docks (reported regression)");
            Check(DockPlan.NearestEdge(Native.RECT.From(-1865, 0, 440, 660), work, 64) == "Left", "near left edge uses wider magnetic zone");
            Native.RECT baseline = Native.RECT.From(-1400, 0, 600, 660);
            foreach (ResizeEdges edges in new[] { ResizeEdges.Left, ResizeEdges.Right, ResizeEdges.Top, ResizeEdges.Bottom, ResizeEdges.Left | ResizeEdges.Top, ResizeEdges.Right | ResizeEdges.Bottom })
            {
                Native.RECT resized = ResizeGeometry.Calculate(baseline, edges, 80, 60, work, 1, "Floating");
                Check(resized.Width >= 360 && resized.Height >= 450 && resized.Left >= work.Left && resized.Top >= work.Top && resized.Right <= work.Right && resized.Bottom <= work.Bottom, "resize geometry / " + edges);
            }
            Native.RECT anchored = ResizeGeometry.Calculate(baseline, ResizeEdges.Left, -100, 0, work, 1, "Right");
            Check(anchored.Right == work.Right && anchored.Width == 700, "resize keeps attached edge anchored");
            Native.RECT minimum = ResizeGeometry.Calculate(baseline, ResizeEdges.Left | ResizeEdges.Top, 900, 900, work, 1, "Floating");
            Check(minimum.Width == 360 && minimum.Height == 450, "resize minimum width and height");
            Check(ThemeService.Luminance(255, 255, 255) > 0.99 && ThemeService.Luminance(0, 0, 0) == 0, "relative luminance");
            Check(ThemeService.DarkForBrightness(0.1, false) && !ThemeService.DarkForBrightness(0.8, true), "wallpaper thresholds");
            Check(ThemeService.DarkForBrightness(0.40, true) && !ThemeService.DarkForBrightness(0.40, false), "brightness hysteresis");
            AppState state = AppState.CreateDefault();
            SoftwareTab tab = new SoftwareTab { Name = "测试 / Test" }; tab.Shortcuts.Add(new ShortcutEntry("Ctrl + K, Ctrl + C", "中文功能 / Action"));
            state.Tabs.Add(tab); state.ActiveTabId = tab.Id; state.Language = "en"; state.Edge = "Left"; state.Theme = "Auto";
            state.PanelWidth = 570; state.PanelHeight = 720; state.PanelOpacity = 0.75;
            state.ColumnWeights = new[] { 1.6, 0.95, 0.6 };
            store.Save(state); tab.Name = "重命名 / Renamed"; store.Save(state);
            AppState read = StateStore.Read(store.StatePath);
            Check(read.Tabs[2].Name == tab.Name && read.Tabs[2].Shortcuts[0].Description == "中文功能 / Action" && read.ActiveTabId == tab.Id, "Unicode / tab rename / shortcut JSON round trip");
            Check(read.Language == "en" && read.Theme == "Auto" && read.Edge == "Left", "settings persist");
            Check(read.PanelWidth == 570 && read.PanelHeight == 720 && read.PanelOpacity == 0.75, "size and opacity persist");
            Check(StateStore.Read(store.StatePath).ColumnWeights.SequenceEqual(state.ColumnWeights), "three column proportions persist in JSON");
            string oldColumns = Regex.Replace(File.ReadAllText(store.StatePath), "\\\"ColumnWeights\\\":\\[[^\\]]*\\],?", "");
            string oldColumnsPath = Path.Combine(store.DirectoryPath, "legacy-v0.5.json"); File.WriteAllText(oldColumnsPath, oldColumns);
            Check(StateStore.Read(oldColumnsPath).ColumnWeights.SequenceEqual(new[] { 1.15, 1.15, 0.85 }), "older files receive default column proportions without losing shortcuts");
            AppState invalidColumns = AppState.CreateDefault(); invalidColumns.ColumnWeights = new[] { Double.NaN, -4.0, Double.PositiveInfinity }; invalidColumns.Validate();
            Check(invalidColumns.ColumnWeights.SequenceEqual(new[] { 1.15, 1.15, 0.85 }), "invalid column widths are repaired before WPF layout");
            string legacy = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(store.StatePath), "\\\"Panel(?:Width|Height|Opacity)\\\":[^,}]+,?", "");
            string legacyPath = Path.Combine(store.DirectoryPath, "legacy-v0.1.json"); File.WriteAllText(legacyPath, legacy);
            AppState migrated = StateStore.Read(legacyPath);
            Check(migrated.PanelWidth == 440 && migrated.PanelHeight == 660 && migrated.PanelOpacity == 1 && migrated.Tabs.Count == 3, "v0.1 data migration preserves shortcuts");
            read.Tabs[2].Shortcuts[0].Keys = "Alt + Enter"; read.Tabs[2].Shortcuts.RemoveAt(0); read.Tabs.RemoveAt(2); read.Validate();
            Check(read.Tabs.Count == 2 && read.ActiveTabId == read.Tabs[0].Id, "delete tab and active-tab recovery");
            File.WriteAllText(store.StatePath, "{broken"); AppState recovered = store.Load();
            Check(recovered.Tabs[2].Name == "测试 / Test" && Directory.GetFiles(store.DirectoryPath, "*.corrupt-*").Length > 0, "corrupt primary preserves evidence and recovers backup");
            AppState invalid = AppState.CreateDefault(); invalid.Version = 99;
            bool rejected = false; try { invalid.Validate(); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "unsupported import version rejected");
            string emojiNotes = "提醒 😀 🫩 🧑🏽‍💻 🇨🇳 👨‍👩‍👧‍👦 1️⃣ ❤️ 🏳️‍🌈 🫪\n第二行 🎉";
            state.Tabs[0].Shortcuts[0].Notes = emojiNotes; state.Tabs[0].Shortcuts[0].Pinned = true; state.PanelColor = "#e2ecfa";
            state.FontScale = 1.25; state.NotesCollapsed = true;
            store.Save(state); AppState notesRead = StateStore.Read(store.StatePath);
            Check(notesRead.Tabs[0].Shortcuts[0].Notes == emojiNotes && notesRead.Tabs[0].Shortcuts[0].Pinned && notesRead.PanelColor == PanelPalette.Blue, "emoji notes and pins persist; legacy color migrates to nearest palette");
            Check(notesRead.FontScale == 1.25 && notesRead.NotesCollapsed, "global font scale and notes visibility persist");
            string v03 = Regex.Replace(File.ReadAllText(store.StatePath), "\\\"(?:FontScale|NotesCollapsed)\\\":[^,}]+,?", "");
            string v03Path = Path.Combine(store.DirectoryPath, "legacy-v0.3.json"); File.WriteAllText(v03Path, v03);
            AppState from03 = StateStore.Read(v03Path);
            Check(from03.FontScale == 1 && !from03.NotesCollapsed && from03.Tabs[0].Shortcuts[0].Notes == emojiNotes, "v0.3 migration defaults to normal font and visible notes without losing emoji");
            string older = Regex.Replace(File.ReadAllText(store.StatePath), "\\\"(?:Notes|PanelColor)\\\":(?:\\\"(?:[^\\\"\\\\]|\\\\.)*\\\"|null),?", "");
            string olderPath = Path.Combine(store.DirectoryPath, "legacy-v0.2.json"); File.WriteAllText(olderPath, older);
            Check(StateStore.Read(olderPath).Tabs[0].Shortcuts[0].Notes == "", "legacy notes migrate to empty text");
            int qualified = 0, sequences = 0;
            using (StreamReader reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("ShortcutDock.emoji-test-17.0.txt"), Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Match match = Regex.Match(line, "^([0-9A-F ]+)\\s*;\\s*(fully-qualified|minimally-qualified|unqualified|component)\\s*#");
                    if (!match.Success) continue;
                    string value = EmojiCatalog.FromCodePoints(match.Groups[1].Value.Trim().Replace(' ', '-')); string asset;
                    if (EmojiCatalog.Match(value, 0, out asset) != value.Length) throw new InvalidOperationException("Emoji sequence missing: " + line);
                    sequences++; if (match.Groups[2].Value == "fully-qualified") qualified++;
                }
            }
            Check(qualified == 3944 && sequences == 5225, "Unicode 17 coverage: all 3944 fully-qualified emoji and 5225 presentation sequences");
            EmojiText sample = new EmojiText { Value = emojiNotes };
            Check(sample.Inlines.OfType<InlineUIContainer>().Count() == 10 && sample.Inlines.OfType<InlineUIContainer>().All(c => ((Image)c.Child).Source.Width == 72), "color PNG rendering preserves family, profession, flags, skin tone, keycap and new emoji as whole images");
            List<ShortcutEntry> imported = TextImport.Parse("Ctrl + N@新建文档 / New document\r\nCtrl + O@打开文件 / Open file\r\nCtrl + S@保存 / Save\r\n");
            Check(imported.Count == 3 && imported[1].Keys == "Ctrl + O", "Template.txt format imports three shortcuts");
            Check(TextImport.Parse("\n Ctrl + X @ 联系 user@example.com 😀 \n")[0].Description == "联系 user@example.com 😀", "TXT trims separators and preserves later @ and emoji");
            bool badTxt = false; try { TextImport.Parse("Ctrl + N@新建\ninvalid"); } catch (TxtFormatException e) { badTxt = e.LineNumber == 2; }
            Check(badTxt, "invalid TXT reports exact line before mutation");
            foreach (Encoding encoding in new[] { new UTF8Encoding(false), Encoding.UTF8, Encoding.Unicode, Encoding.GetEncoding(54936) })
            {
                string txtPath = Path.Combine(store.DirectoryPath, "encoding-" + encoding.CodePage + "-" + encoding.GetPreamble().Length + ".txt");
                File.WriteAllText(txtPath, "Ctrl + N@新建文档", encoding);
                Check(TextImport.Read(txtPath)[0].Description == "新建文档", "TXT decoding / " + encoding.CodePage + " / BOM " + encoding.GetPreamble().Length);
            }
            SoftwareTab destination = new SoftwareTab { Name = "Import target" }; destination.Shortcuts.Add(new ShortcutEntry("Ctrl + N", "新建文档 / New document") { Notes = emojiNotes, Pinned = true });
            int skipped; int appended = TextImport.Append(destination, imported.Concat(imported), out skipped);
            Check(appended == 2 && skipped == 4 && destination.Shortcuts[0].Notes == emojiNotes && destination.Shortcuts[0].Pinned, "TXT append deduplicates without overwriting notes or pins");
            destination.Shortcuts = Enumerable.Range(0, 10000).Select(i => new ShortcutEntry(i.ToString(), "Action")).ToList();
            bool exceeded = false; try { TextImport.Append(destination, imported, out skipped); } catch (InvalidDataException) { exceeded = true; }
            Check(exceeded && destination.Shortcuts.Count == 10000, "TXT over-limit import leaves destination unchanged");
            Check(PanelPalette.Colors.SequenceEqual(new[] { "#E0EAFE", "#FEE0EA", "#EAFEE0" }), "only three palette colors match the supplied triadic image");
            Check(PanelPalette.Normalize(null) == PanelPalette.Blue && PanelPalette.Normalize("#FBE8E9") == PanelPalette.Pink && PanelPalette.Normalize("#E0F1E6") == PanelPalette.Green, "legacy custom colors map to the nearest supported palette");
            EmojiText scaledEmoji = new EmojiText { Value = "👨‍👩‍👧‍👦", FontSize = 12 }; scaledEmoji.FontSize = 18;
            Check(((Image)scaledEmoji.Inlines.OfType<InlineUIContainer>().Single().Child).Height == 27, "emoji scales with text while preserving a whole ZWJ sequence");
            Check(BrandIcon.Source.PixelWidth == 200 && BrandIcon.Source.PixelHeight == 200, "uploaded Xiaohongshu icon decodes from the embedded image");
            store.Save(AppState.CreateDefault());
        }
        static void Capture(Window window, string path)
        {
            window.UpdateLayout();
            // WPF render checks actual control layout; DWM's external acrylic layer is not in this bitmap.
            RenderTargetBitmap bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }
        static List<T> Controls<T>(DependencyObject root) where T : DependencyObject
        {
            List<T> result = new List<T>();
            if (root is T) result.Add((T)root);
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) result.AddRange(Controls<T>(VisualTreeHelper.GetChild(root, i)));
            return result;
        }
        static byte RenderedAlpha(Window window)
        {
            window.UpdateLayout();
            RenderTargetBitmap bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window); byte[] pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect(22, 22, 1, 1), pixel, 4, 0); return pixel[3];
        }
        static byte MaximumTextAlpha(Window window, FrameworkElement text)
        {
            window.UpdateLayout();
            RenderTargetBitmap bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
            Point at = text.TranslatePoint(new Point(), window);
            int x = Math.Max(0, (int)at.X), y = Math.Max(0, (int)at.Y);
            int width = Math.Min((int)Math.Ceiling(text.ActualWidth), bitmap.PixelWidth - x), height = Math.Min((int)Math.Ceiling(text.ActualHeight), bitmap.PixelHeight - y);
            byte[] pixels = new byte[width * height * 4]; bitmap.CopyPixels(new Int32Rect(x, y, width, height), pixels, width * 4, 0);
            byte alpha = 0; for (int i = 3; i < pixels.Length; i += 4) alpha = Math.Max(alpha, pixels[i]); return alpha;
        }
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("dwmapi.dll")] static extern int DwmFlush();
        [DllImport("user32.dll")] static extern bool GetWindowDisplayAffinity(IntPtr hwnd, out uint affinity);
        static Window GlassBackdrop(MainWindow panel)
        {
            Grid pattern = new Grid { Background = new LinearGradientBrush(Color.FromRgb(40, 115, 196), Color.FromRgb(187, 75, 127), 0) };
            DrawingGroup stripes = new DrawingGroup();
            stripes.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), null, new RectangleGeometry(new Rect(0, 0, 3, 6))));
            stripes.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)), null, new RectangleGeometry(new Rect(3, 0, 3, 6))));
            pattern.Children.Add(new Border { Background = new DrawingBrush(stripes) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 6, 6), Stretch = Stretch.None } });
            Window backdrop = new Window { WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, ShowActivated = false, Topmost = panel.Topmost, Content = pattern };
            backdrop.Show(); Native.RECT bounds = Native.Bounds(panel); Native.Move(backdrop, bounds);
            SetWindowPos(Native.Handle(backdrop), Native.Handle(panel), 0, 0, 0, 0, 1 | 2 | 16);
            return backdrop;
        }
        static System.Drawing.Bitmap CaptureComposite(MainWindow panel, string path)
        {
            DwmFlush(); Native.RECT bounds = Native.Bounds(panel);
            System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bitmap)) g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bitmap.Size);
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png); return bitmap;
        }
        static double BackdropVariation(System.Drawing.Bitmap bitmap, double scale)
        {
            int y = (int)(96 * scale), left = (int)(160 * scale), right = Math.Min(bitmap.Width - (int)(80 * scale), (int)(390 * scale));
            double sum = 0;
            for (int x = left + 1; x < right; x++)
            {
                var a = bitmap.GetPixel(x - 1, y); var b = bitmap.GetPixel(x, y);
                sum += Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
            }
            return sum / Math.Max(1, right - left - 1);
        }
        static void CompleteDialog(MainWindow owner, string[] values, string screenshot)
        {
            owner.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate {
                Window dialog = Application.Current.Windows.OfType<Window>().First(w => w != owner && w.Owner == owner);
                if (owner.State.Theme == "Glass")
                    Check(((SolidColorBrush)dialog.Background).Color.A == 255 && RenderedAlpha(dialog) == 255, "glass-mode editor keeps a fully opaque background and legible text");
                List<TextBox> boxes = Controls<TextBox>(dialog);
                for (int i = 0; i < values.Length; i++) boxes[i].Text = values[i];
                if (screenshot != null) Capture(dialog, screenshot);
                Controls<Button>(dialog).First(b => b.Content != null && b.Content.ToString() == owner.T["Save"]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }));
        }
        static void CompleteSettings(MainWindow owner, bool accept, string screenshot)
        {
            owner.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate {
                Window dialog = Application.Current.Windows.OfType<Window>().First(w => w != owner && w.Owner == owner);
                Controls<Slider>(dialog).Single(s => s.Name == "PanelTransparency").Value = 25;
                Check(Math.Abs(owner.State.PanelOpacity - 0.75) < 0.001 && owner.Opacity == 1 && Math.Abs(RenderedAlpha(owner) - 191) <= 1, "transparency slider changes rendered background alpha / " + accept + " (opacity=" + owner.State.PanelOpacity + ", alpha=" + RenderedAlpha(owner) + ")");
                List<Button> paletteButtons = Controls<Button>(dialog).Where(b => PanelPalette.Labels.Contains(b.Name)).ToList();
                Check(paletteButtons.Count == 3 && !Controls<TextBox>(dialog).Any(t => t.Name == "ColorHex"), "settings provides exactly three colors and no HEX / wheel picker");
                paletteButtons.Single(b => b.Name == "PalettePink").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(owner.State.PanelColor == PanelPalette.Pink, "palette click previews the selected pink color / " + accept);
                TextBox search = Controls<TextBox>(owner).Single(t => t.Name == "ShortcutSearch");
                Controls<Slider>(dialog).Single(s => s.Name == "PanelFontScale").Value = 125;
                Check(owner.State.FontScale == 1.25 && Controls<TextBlock>(owner).Single(t => t.Name == "PanelTitle").FontSize == 28.75 && Object.ReferenceEquals(search, Controls<TextBox>(owner).Single(t => t.Name == "ShortcutSearch")), "font slider previews all text without rebuilding or losing search / " + accept);
                Controls<CheckBox>(dialog).Single(c => c.Name == "ShowNotesColumn").IsChecked = false;
                Check(owner.State.NotesCollapsed && owner.ShortcutGrid.Columns[2].Visibility == Visibility.Collapsed, "settings checkbox previews collapsed notes / " + accept);
                if (accept)
                {
                    List<ComboBox> choices = Controls<ComboBox>(dialog);
                    choices[0].SelectedIndex = 1; // Light
                    choices[1].SelectedIndex = 1; // Right
                }
                if (screenshot != null) Capture(dialog, screenshot);
                Controls<Button>(dialog).First(b => b.Content != null && b.Content.ToString() == owner.T[accept ? "Save" : "Cancel"]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }));
        }
        static void CancelDialog(MainWindow owner)
        {
            owner.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate {
                Window dialog = Application.Current.Windows.OfType<Window>().First(w => w.Owner == owner);
                Controls<Button>(dialog).First(b => b.Content != null && b.Content.ToString() == owner.T["Cancel"]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }));
        }
        public static int Run(Application app, StateStore store, string output)
        {
            Directory.CreateDirectory(output); Logic(store);
            MainWindow window = new MainWindow(AppState.CreateDefault(), store, true); app.MainWindow = window;
            int code = 1;
            window.Loaded += delegate {
                window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async delegate {
                    try
                    {
                        Check(window.Dock != null, "WPF window and native controller start");
                        Check(window.ShortcutGrid.Columns.Count == 3 && window.ShortcutGrid.Items.Count == 12, "three-column shortcut table renders");
                        Check(Controls<TextBlock>(window).Any(t => t.Text == "食得咸鱼抵得渴") && Controls<Image>(window).Any(i => i.Name == "XiaohongshuIcon" && i.Source == BrandIcon.Source), "uploaded icon and requested subtitle render together");
                        foreach (string theme in new[] { "Light", "Dark", "Glass", "Auto" })
                        {
                            window.State.Theme = theme; window.ApplyTheme();
                            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                            Capture(window, Path.Combine(output, "zh-" + theme.ToLowerInvariant() + ".png"));
                            Check(window.Resources["TextBrush"] is SolidColorBrush, "theme applies / " + theme);
                        }
                        window.State.Language = "en"; window.State.Theme = "Light"; window.Rebuild();
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        Capture(window, Path.Combine(output, "en-light.png"));
                        Check(window.T["Keys"] == "SHORTCUT" && window.ShortcutGrid.Columns[1].Header.ToString() == "ACTION", "English UI rebuild");
                        Check(window.ShortcutGrid.Columns[0].ActualWidth > 120 && window.ShortcutGrid.Columns[1].ActualWidth > 120, "English columns retain width after UI rebuild");
                        CompleteDialog(window, new[] { "测试软件 / Test app" }, null); window.AddTab();
                        Check(window.State.Tabs.Count == 3 && window.ActiveTab().Name == "测试软件 / Test app", "create tab through real modal UI");
                        CompleteDialog(window, new[] { "Ctrl + K, Ctrl + C", "多步快捷键 / Multi-step action" }, Path.Combine(output, "en-editor.png")); window.AddRow();
                        Check(window.ActiveTab().Shortcuts.Count == 1 && window.ActiveTab().Shortcuts[0].Keys == "Ctrl + K, Ctrl + C", "add shortcut through real modal UI");
                        window.ShortcutGrid.SelectedItem = window.ShortcutGrid.Items[0];
                        CompleteDialog(window, new[] { "Alt + Enter", "编辑后功能 / Edited action" }, null); window.EditRow();
                        Check(window.ActiveTab().Shortcuts[0].Keys == "Alt + Enter", "edit shortcut through real modal UI");
                        string noteValue = "😀 🧑🏽‍💻 🇨🇳 👨‍👩‍👧‍👦 1️⃣ 🫪\n支持 emoji 🎉";
                        window.UpdateLayout();
                        DataGridCell noteCell = Controls<DataGridCell>(window.ShortcutGrid).First(c => c.Column.SortMemberPath == "Notes");
                        CompleteDialog(window, new[] { noteValue }, Path.Combine(output, "en-notes-editor.png")); window.EditCell(noteCell);
                        Check(window.ActiveTab().Shortcuts[0].Notes == noteValue, "add emoji notes through real modal UI");
                        window.ToggleRowPin(); Check(window.ActiveTab().Shortcuts[0].Pinned, "context action pins selected shortcut");
                        CompleteDialog(window, new[] { "Alt + Enter", "编辑功能保留备注 / Preserve notes" }, null); window.EditRow();
                        Check(window.ActiveTab().Shortcuts[0].Notes == noteValue && window.ActiveTab().Shortcuts[0].Pinned, "action editor retains notes and shortcut pin");
                        CompleteDialog(window, new[] { "" }, null); window.EditNotes();
                        Check(window.ActiveTab().Shortcuts[0].Notes == "", "empty notes can be saved to clear a cell");
                        CompleteDialog(window, new[] { noteValue }, null); window.EditNotes();
                        CompleteDialog(window, new[] { "重命名软件 / Renamed app" }, null);
                        string renamed = Dialogs.TabName(window, window.ActiveTab().Name); window.ActiveTab().Name = renamed; window.Save();
                        Check(StateStore.Read(store.StatePath).Tabs[2].Name == renamed, "rename dialog and saved state");
                        window.State.Tabs.RemoveAt(2); window.State.Validate(); window.Rebuild();
                        ShortcutEntry first = window.ActiveTab().Shortcuts[0], last = window.ActiveTab().Shortcuts.Last();
                        window.ShortcutGrid.SelectedItem = last; window.ToggleRowPin();
                        Check(Object.ReferenceEquals(window.ShortcutGrid.Items[0], last), "pinned shortcut moves above unpinned rows");
                        window.ShortcutGrid.ContextMenu.IsOpen = true;
                        Check(((MenuItem)window.ShortcutGrid.ContextMenu.Items[0]).Header.ToString() == window.T["UnpinRow"], "right-click menu offers unpin for pinned row");
                        window.ShortcutGrid.ContextMenu.IsOpen = false; window.ToggleRowPin();
                        Check(Object.ReferenceEquals(window.ShortcutGrid.Items[0], first), "unpin restores original relative order");
                        last.Pinned = true; first.Pinned = true; window.RenderRows();
                        Check(Object.ReferenceEquals(window.ShortcutGrid.Items[0], first) && Object.ReferenceEquals(window.ShortcutGrid.Items[1], last), "multiple pinned shortcuts retain source order");
                        first.Pinned = last.Pinned = false; window.RenderRows();
                        first.Notes = "检索备注 🧑🏽‍💻"; window.RenderRows();
                        Controls<TextBox>(window).First(t => t.Name == "ShortcutSearch").Text = "检索备注";
                        Check(window.ShortcutGrid.Items.Count == 1, "search includes notes");
                        Controls<TextBox>(window).First(t => t.Name == "ShortcutSearch").Text = "";
                        string templatePath = Path.Combine(output, "Template.txt"); File.WriteAllText(templatePath, "Ctrl + N@新建文档 / New document\r\nCtrl + O@打开文件 / Open file\r\nCtrl + S@保存 / Save", new UTF8Encoding(false));
                        CompleteDialog(window, new[] { "TXT 导入软件" }, Path.Combine(output, "en-txt-import.png")); window.ImportTextPath(templatePath);
                        Check(window.ActiveTab().Name == "TXT 导入软件" && window.ActiveTab().Shortcuts.Count == 3, "TXT preview creates named software tab through modal UI");
                        window.CommitTextImport(TextImport.Read(templatePath), new TxtImportChoice { TabId = window.State.ActiveTabId });
                        Check(window.ActiveTab().Shortcuts.Count == 3, "repeat TXT import safely skips duplicates");
                        int beforeImport = window.ActiveTab().Shortcuts.Count;
                        CancelDialog(window);
                        window.ImportTextPath(templatePath);
                        Check(window.ActiveTab().Shortcuts.Count == beforeImport && window.State.Tabs.Count == 3, "TXT preview cancel leaves tabs and shortcuts unchanged");
                        window.State.Tabs.RemoveAt(2); window.State.Validate(); window.Rebuild();
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        Check(Controls<Thumb>(window).Count(t => t.ToolTip != null && t.ToolTip.ToString() == window.T["Resize"]) == 8, "four resize edges and four corner grips render");
                        CompleteSettings(window, false, null); Dialogs.Settings(window);
                        Check(window.State.PanelOpacity == 1 && RenderedAlpha(window) == 255 && window.State.PanelColor == PanelPalette.Blue && window.State.FontScale == 1 && !window.State.NotesCollapsed, "settings cancel restores opacity, palette, font size and notes visibility");
                        CompleteSettings(window, true, Path.Combine(output, "en-settings.png")); Dialogs.Settings(window);
                        Check(window.State.PanelOpacity == 0.75 && StateStore.Read(store.StatePath).PanelOpacity == 0.75, "settings save transparency");
                        AppState savedAppearance = StateStore.Read(store.StatePath);
                        Check(savedAppearance.PanelColor == PanelPalette.Pink && savedAppearance.FontScale == 1.25 && savedAppearance.NotesCollapsed, "settings save palette, text size and notes visibility");
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        string existingNotes = window.ActiveTab().Shortcuts[0].Notes; int existingCount = window.ShortcutGrid.Items.Count;
                        window.UpdateLayout(); double collapsedWidth = window.ShortcutGrid.Columns[0].ActualWidth;
                        Controls<Button>(window).Single(b => b.Name == "ToggleNotes").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
                        Check(window.ShortcutGrid.Columns[2].Visibility == Visibility.Visible && window.ShortcutGrid.Columns[0].ActualWidth < collapsedWidth && window.ActiveTab().Shortcuts[0].Notes == existingNotes && window.ShortcutGrid.Items.Count == existingCount, "toolbar expands notes and redistributes columns without changing entries");
                        Controls<Button>(window).Single(b => b.Name == "ToggleNotes").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
                        Check(window.ShortcutGrid.Columns[2].Visibility == Visibility.Collapsed && StateStore.Read(store.StatePath).NotesCollapsed, "toolbar collapses notes and remembers choice");
                        window.Rebuild(); Check(window.ShortcutGrid.Columns[2].Visibility == Visibility.Collapsed, "notes visibility survives tab / language rebuild");
                        window.PreviewNotesVisibility(true); window.PreviewFontScale(1);
                        foreach (string palette in PanelPalette.Colors)
                        {
                            window.State.PanelColor = palette; window.State.Theme = "Light"; window.ApplyTheme();
                            var chosen = ((SolidColorBrush)window.Resources["SurfaceBrush"]).Color;
                            var original = (System.Windows.Media.Color)ColorConverter.ConvertFromString(palette);
                            var neutral = (System.Windows.Media.Color)ColorConverter.ConvertFromString("#F6F7FA");
                            double fromNeutral = Math.Pow(chosen.R - neutral.R, 2) + Math.Pow(chosen.G - neutral.G, 2) + Math.Pow(chosen.B - neutral.B, 2);
                            double originalDistance = Math.Pow(original.R - neutral.R, 2) + Math.Pow(original.G - neutral.G, 2) + Math.Pow(original.B - neutral.B, 2);
                            Check(fromNeutral < originalDistance && ThemeService.Luminance(chosen.R, chosen.G, chosen.B) > 0.85, "light palette uses a softer neutral surface around the supplied color / " + palette);
                            var accent = ((SolidColorBrush)window.Resources["AccentBrush"]).Color;
                            double contrast = (ThemeService.Luminance(chosen.R, chosen.G, chosen.B) + 0.05) / (ThemeService.Luminance(accent.R, accent.G, accent.B) + 0.05);
                            Check(contrast >= 4.5 && ((SolidColorBrush)window.Resources["ControlBrush"]).Color == Colors.White, "soft palette keeps readable shortcut text and white controls / " + palette);
                            window.State.Theme = "Dark"; window.ApplyTheme();
                            chosen = ((SolidColorBrush)window.Resources["SurfaceBrush"]).Color;
                            Check(ThemeService.Luminance(chosen.R, chosen.G, chosen.B) < 0.1, "dark appearance retains a dark variant / " + palette);
                        }
                        window.State.PanelColor = PanelPalette.Blue; window.State.Theme = "Light";
                        window.PreviewOpacity(1); window.ApplyTheme();
                        bool sampled = await ThemeService.SampleAsync(window.Dock.Screen, false);
                        checks.Add("PASS: wallpaper sampling completes (dark=" + sampled + ")");
                        foreach (string edge in new[] { "Left", "Right", "Top", "Bottom" })
                        {
                            EdgeController sameController = window.Dock;
                            window.Dock.ChangeEdge(edge); Native.RECT actual = Native.Bounds(window), expected = window.Dock.Plan.Panel;
                            Check(Math.Abs(actual.Left - expected.Left) <= 1 && Math.Abs(actual.Top - expected.Top) <= 1 && Math.Abs(actual.Width - expected.Width) <= 1 && Math.Abs(actual.Height - expected.Height) <= 1, "native window placement / " + edge);
                            window.Dock.Collapse(); Check(!window.IsVisible && !window.Dock.Expanded, "native collapse / " + edge);
                            window.Dock.Expand(); Check(window.IsVisible && window.Dock.Expanded, "native expand / " + edge);
                            Check(Object.ReferenceEquals(window.Dock, sameController), "reopening retains one edge controller / " + edge);
                        }
                        foreach (string edge in new[] { "Left", "Right" })
                        {
                            window.State.Pinned = false; window.Dock.ChangeEdge(edge);
                            var area = window.Dock.Screen.WorkingArea; Native.RECT actual = Native.Bounds(window);
                            int overrun = (int)(100 * Native.Scale(window.Dock.Screen));
                            Native.Move(window, Native.RECT.From(edge == "Left" ? area.Left - overrun : area.Right - actual.Width + overrun, area.Top + 80, actual.Width, actual.Height));
                            window.Dock.DragFinished();
                            Check(window.State.Edge == edge, "native drag release beyond screen snaps / " + edge);
                            Native.POINT outside = new Native.POINT { X = area.Left + area.Width / 2, Y = area.Top + 10 };
                            window.Dock.ProcessPointer(outside, false, false, 100000);
                            window.Dock.ProcessPointer(outside, false, false, 100651);
                            Check(!window.IsVisible && !window.Dock.Expanded, "production pointer leave auto hides after drag / " + edge);
                            window.Dock.Expand(); window.Dock.ProcessPointer(outside, false, false, 100700);
                            window.Dock.ProcessPointer(outside, true, false, 100701);
                            Check(!window.IsVisible, "production outside click hides after drag / " + edge);
                            window.Dock.Expand();
                            Native.RECT target = ResizeGeometry.Calculate(Native.Bounds(window), ResizeEdges.Right | ResizeEdges.Bottom, 90, 45,
                                Native.RECT.From(area.Left, area.Top, area.Width, area.Height), Native.Scale(window.Dock.Screen), edge);
                            window.Dock.Resizing = true; window.Dock.ResizeTo(target, false);
                            window.Dock.ProcessPointer(outside, false, false, 110000); window.Dock.ProcessPointer(outside, true, false, 110001);
                            Check(window.IsVisible && window.State.Edge == edge, "resizing blocks outside collapse / " + edge);
                            window.Dock.Resizing = false; window.Dock.ResizeTo(target, true);
                            actual = Native.Bounds(window);
                            Check(actual.Width == target.Width && actual.Height == target.Height && (edge == "Left" ? actual.Left == area.Left : actual.Right == area.Right), "native resizing retains attached edge / " + edge);
                            Check(Math.Abs(StateStore.Read(store.StatePath).PanelWidth - target.Width / Native.Scale(window.Dock.Screen)) < 0.01, "resized width saved / " + edge);
                        }
                        window.State.Language = "zh"; window.State.Theme = "Light"; window.State.Edge = "Right"; window.Rebuild(); window.Dock.Place();
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        Capture(window, Path.Combine(output, "zh-resized.png"));
                        window.State.PanelWidth = 740; window.State.PanelHeight = 720; window.State.PanelColor = PanelPalette.Blue;
                        window.ActiveTab().Shortcuts[0].Notes = "🧑🏽‍💻 常用 🎉"; window.ActiveTab().Shortcuts[1].Notes = "🇨🇳 ❤️ 🫪";
                        window.ShortcutGrid.SelectedItem = window.ActiveTab().Shortcuts[1]; window.ToggleRowPin();
                        window.Rebuild(); window.Dock.Place(); window.ApplyTheme();
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        Capture(window, Path.Combine(output, "zh-custom-emoji.png"));
                        foreach (string palette in PanelPalette.Colors)
                        {
                            window.State.PanelColor = palette; window.ApplyTheme();
                            Capture(window, Path.Combine(output, "zh-palette-" + Array.IndexOf(PanelPalette.Colors, palette) + ".png"));
                        }
                        window.PreviewFontScale(1.5); window.PreviewNotesVisibility(true);
                        Capture(window, Path.Combine(output, "zh-large-notes-open.png"));
                        Check(Controls<TextBlock>(window).Single(t => t.Name == "PanelTitle").FontSize == 34.5 && window.ShortcutGrid.RowHeight == 69, "maximum text size resizes title and table row height");
                        EmojiText emojiCell = Controls<EmojiText>(window.ShortcutGrid).First(t => t.Value != null && t.Value.Contains("🧑🏽‍💻"));
                        Check(emojiCell.FontSize == 18 && ((Image)emojiCell.Inlines.OfType<InlineUIContainer>().First().Child).Height == 27, "real table emoji and notes grow with the global text setting");
                        window.ToggleNotes(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        Capture(window, Path.Combine(output, "zh-large-notes-closed.png"));
                        window.State.Language = "en"; window.State.PanelWidth = 360; window.State.PanelHeight = 450; window.Rebuild(); window.Dock.Place();
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        Capture(window, Path.Combine(output, "en-minimum-large.png"));
                        Check(window.ShortcutGrid.ActualHeight >= window.ShortcutGrid.RowHeight + 50 && Controls<Button>(window).Single(b => b.Name == "ToggleNotes").ActualWidth > 70 && Controls<TextBlock>(window.ShortcutGrid).Any(t => t.Text == "↑ Ctrl + O"), "large text keeps at least one readable shortcut row and notes toggle at minimum panel size");
                        Native.RECT fontSized = ResizeGeometry.Calculate(Native.Bounds(window), ResizeEdges.Bottom, 0, -1000,
                            Native.RECT.From(0, 0, 1920, 1080), 1, "Floating", FontSizing.MinimumHeight(1.5));
                        Check(fontSized.Height == 532, "border resize respects minimum height required by large text");
                        window.PreviewFontScale(1); window.State.PanelHeight = 450; window.Dock.Place();
                        CompleteSettings(window, false, null); Dialogs.Settings(window);
                        Check(window.State.PanelHeight == 450 && window.State.FontScale == 1 && Math.Abs(Native.Bounds(window).Height / Native.Scale(window.Dock.Screen) - 450) < 1, "settings cancel restores height after a font preview temporarily enlarges a small panel");
                        window.State.Language = "zh"; window.State.Theme = "Light"; window.State.PanelColor = PanelPalette.Pink;
                        window.State.PanelWidth = 480; window.State.PanelHeight = 780; window.Rebuild(); window.Dock.Place();
                        window.PreviewNotesVisibility(false); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        Capture(window, Path.Combine(output, "zh-refined-unlocked.png"));
                        Check(Controls<Image>(window).Single(i => i.Name == "XiaohongshuIcon").Width == 16 && Controls<TextBlock>(window).Single(t => t.Name == "PanelTitle").Margin.Bottom > 4, "smaller uploaded icon and distinct title / subtitle gap render");
                        window.ShortcutGrid.SelectedItem = window.ShortcutGrid.Items[0];
                        Controls<Button>(window).Single(b => b.Name == "PanelPin").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Check(window.IsEditingLocked && StateStore.Read(store.StatePath).Pinned, "pin button enables and persists editing lock");
                        Check(!Controls<Button>(window).Single(b => b.Name == "AddShortcut").IsEnabled && !Controls<Button>(window).Single(b => b.Name == "AddSoftwareTab").IsEnabled && !Controls<Button>(window).Single(b => b.Name == "PanelSettings").IsEnabled && Controls<Thumb>(window).Where(t => t.ToolTip != null && t.ToolTip.ToString() == window.T["Resize"]).All(t => !t.IsEnabled), "fixed panel disables content, settings and all resize grips");
                        TextBlock lockedFooter = Controls<TextBlock>(window).Single(t => t.Name == "PanelFooter");
                        Check(lockedFooter.Text == "● 已固定 · 保持展开 · 编辑锁定", "fixed footer includes the requested editing-lock status");
                        window.ShortcutGrid.ContextMenu.IsOpen = true;
                        Check(window.ShortcutGrid.ContextMenu.Items.OfType<MenuItem>().All(i => !i.IsEnabled), "fixed row menu disables edit, delete, notes and shortcut pin");
                        window.ShortcutGrid.ContextMenu.IsOpen = false;
                        Button moreButton = Controls<Button>(window).Single(b => b.Name == "MoreOptions");
                        Check(((MenuItem)moreButton.ContextMenu.Items[0]).Header.ToString() == window.T["ImportTxt"] && !((MenuItem)moreButton.ContextMenu.Items[0]).IsEnabled && ((MenuItem)moreButton.ContextMenu.Items[1]).Header.ToString() == window.T["DownloadTemplate"] && ((MenuItem)moreButton.ContextMenu.Items[1]).IsEnabled, "template download sits immediately below TXT import and stays available while fixed");
                        string lockedJson = File.ReadAllText(store.StatePath); Native.RECT fixedBounds = Native.Bounds(window);
                        window.AddTab(); window.AddRow(); window.EditRow(); window.EditNotes(); window.DeleteRow(); window.ToggleRowPin();
                        window.RenameTab(window.ActiveTab()); window.DeleteTab(window.ActiveTab());
                        window.ImportTextPath(Path.Combine(output, "does-not-exist.txt"));
                        window.CommitTextImport(new List<ShortcutEntry> { new ShortcutEntry("Blocked", "Blocked") }, new TxtImportChoice { Name = "Blocked" });
                        window.PreviewFontScale(1.5); window.PreviewOpacity(0.5); window.Dock.ChangeEdge("Left");
                        window.Dock.ResizeTo(Native.RECT.From(fixedBounds.Left, fixedBounds.Top, fixedBounds.Width + 100, fixedBounds.Height - 100), true);
                        Dialogs.Settings(window);
                        Check(Dialogs.Shortcut(window, null) == null && Dialogs.TabName(window, null) == null && Dialogs.Notes(window, "Blocked") == null, "fixed modal entry points also reject editing");
                        Check(File.ReadAllText(store.StatePath) == lockedJson && Native.Bounds(window).Width == fixedBounds.Width && Native.Bounds(window).Height == fixedBounds.Height && window.State.Edge == "Right", "editing commands cannot alter stored content, settings or geometry while fixed");
                        TextBox fixedSearch = Controls<TextBox>(window).Single(t => t.Name == "ShortcutSearch"); int fullCount = window.ShortcutGrid.Items.Count;
                        fixedSearch.Text = "Ctrl + S";
                        Check(fixedSearch.IsEnabled && !fixedSearch.IsReadOnly && window.ShortcutGrid.Items.Count > 0 && window.ShortcutGrid.Items.Count < fullCount, "fixed search remains editable and filters shortcuts");
                        fixedSearch.Text = "";
                        window.SaveTemplateTo(Path.Combine(output, "Downloaded-Template.txt"));
                        Check(File.ReadAllText(Path.Combine(output, "Downloaded-Template.txt"), Encoding.UTF8) == TextImport.TemplateText && TextImport.Read(Path.Combine(output, "Downloaded-Template.txt")).Count == 3 && window.IsEditingLocked, "downloaded UTF-8 template imports successfully and does not unlock the panel");
                        Check(lockedFooter.Text.Contains("编辑锁定"), "download status preserves the pinned editing-lock footer");
                        window.PreviewNotesVisibility(true);
                        Controls<Button>(window).Single(b => (b.Tag as string) == window.State.Tabs[1].Id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        Check(window.State.ActiveTabId == window.State.Tabs[1].Id && window.IsEditingLocked && !Controls<Button>(window).Single(b => b.Name == "AddShortcut").IsEnabled, "fixed tab navigation works and rebuilt controls remain locked");
                        Controls<Button>(window).Single(b => (b.Tag as string) == window.State.Tabs[0].Id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        window.ToggleNotes(); Check(window.State.NotesCollapsed && window.IsEditingLocked, "notes can still be viewed or collapsed without unlocking content");
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); Capture(window, Path.Combine(output, "zh-refined-fixed.png"));
                        window.TogglePanelPin();
                        Check(!window.IsEditingLocked && Controls<Button>(window).Single(b => b.Name == "AddShortcut").IsEnabled && Controls<Button>(window).Single(b => b.Name == "PanelSettings").IsEnabled && Controls<Thumb>(window).Where(t => t.ToolTip != null && t.ToolTip.ToString() == window.T["Resize"]).All(t => t.IsEnabled), "unpin restores editing, settings and resizing");
                        window.ShortcutGrid.SelectedItem = window.ShortcutGrid.Items[0];
                        CompleteDialog(window, new[] { "解锁后备注 🎉" }, null); window.EditNotes();
                        Check(window.ActiveTab().Shortcuts[1].Notes == "解锁后备注 🎉", "notes editor works again after unpin");
                        window.TogglePanelPin(); window.Dock.Collapse();
                        Check(!window.IsEditingLocked && !window.IsVisible, "manual collapse clears pin and editing lock together");
                        window.Dock.Expand();
                        await System.Threading.Tasks.Task.Delay(220);
                        window.PreviewNotesVisibility(true); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        for (int columnIndex = 0; columnIndex < 3; columnIndex++)
                        {
                            DataGridColumn column = window.ShortcutGrid.Columns[columnIndex];
                            DataGridColumnHeader header = Controls<DataGridColumnHeader>(window.ShortcutGrid).Single(h => h.Column == column);
                            Thumb grip = (Thumb)header.Template.FindName("PART_RightHeaderGripper", header);
                            Check(grip != null && grip.IsVisible && column.CanUserResize && window.ShortcutGrid.CanUserResizeColumns, "native header resize handle is available / " + columnIndex);
                            double beforeWidth = column.ActualWidth; int beforeDepth = window.Dock.InteractionDepth;
                            double delta = columnIndex == 2 ? -22 : 28;
                            grip.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
                            Check(window.Dock.InteractionDepth == beforeDepth + 1, "column drag prevents automatic hiding / " + columnIndex);
                            grip.RaiseEvent(new DragDeltaEventArgs(delta, 0) { RoutedEvent = Thumb.DragDeltaEvent });
                            grip.RaiseEvent(new DragCompletedEventArgs(delta, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
                            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
                            Check(Math.Abs(column.ActualWidth - beforeWidth) > 5 && window.Dock.InteractionDepth == beforeDepth && StateStore.Read(store.StatePath).ColumnWeights.SequenceEqual(window.State.ColumnWeights), "real WPF header drag changes and saves width / " + columnIndex + " (before=" + beforeWidth.ToString("0.0") + ", after=" + column.ActualWidth.ToString("0.0") + ", depth=" + window.Dock.InteractionDepth + ")");
                        }
                        double[] savedWeights = (double[])window.State.ColumnWeights.Clone();
                        window.PreviewNotesVisibility(false); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
                        Check(window.State.ColumnWeights.SequenceEqual(savedWeights) && Math.Abs(window.ShortcutGrid.Columns[0].ActualWidth / window.ShortcutGrid.Columns[1].ActualWidth - savedWeights[0] / savedWeights[1]) < 0.02, "hiding notes expands the two remaining columns using their saved proportions");
                        DataGridColumnHeader collapsedHeader = Controls<DataGridColumnHeader>(window.ShortcutGrid).Single(h => h.Column == window.ShortcutGrid.Columns[0]);
                        Thumb collapsedGrip = (Thumb)collapsedHeader.Template.FindName("PART_RightHeaderGripper", collapsedHeader);
                        collapsedGrip.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
                        collapsedGrip.RaiseEvent(new DragDeltaEventArgs(-25, 0) { RoutedEvent = Thumb.DragDeltaEvent });
                        collapsedGrip.RaiseEvent(new DragCompletedEventArgs(-25, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
                        Check(window.State.ColumnWeights[2] == savedWeights[2], "resizing with notes hidden preserves the saved notes width");
                        window.PreviewNotesVisibility(true); window.State.Language = "en"; window.Rebuild(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        Check(Math.Abs(window.ShortcutGrid.Columns[0].ActualWidth / window.ShortcutGrid.Columns[2].ActualWidth - window.State.ColumnWeights[0] / window.State.ColumnWeights[2]) < 0.02, "notes expansion and language rebuild restore custom column proportions");
                        window.TogglePanelPin(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
                        Check(!window.ShortcutGrid.CanUserResizeColumns && window.ShortcutGrid.Columns.All(c => !c.CanUserResize) && Controls<DataGridColumnHeader>(window.ShortcutGrid).Where(h => h.Column != null).All(h => ((Thumb)h.Template.FindName("PART_RightHeaderGripper", h)).Visibility != Visibility.Visible), "pin also locks all three header resize handles");
                        window.TogglePanelPin(); await System.Threading.Tasks.Task.Delay(220);
                        Check(window.ShortcutGrid.CanUserResizeColumns && window.ShortcutGrid.Columns.All(c => c.CanUserResize), "unpin restores column resizing");
                        window.State.Language = "zh"; window.Rebuild(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        foreach (string theme in new[] { "Light", "Dark", "Glass", "Auto" })
                        {
                            window.State.Theme = theme; window.PreviewOpacity(0.35); window.ApplyTheme(); window.UpdateLayout();
                            TextBlock heading = Controls<TextBlock>(window).Single(t => t.Name == "PanelTitle");
                            TextBlock combination = Controls<TextBlock>(window.ShortcutGrid).First(t => t.Text == "↑ Ctrl + O");
                            EmojiText note = Controls<EmojiText>(window.ShortcutGrid).First(t => t.Value != null && t.Value.Contains("🎉"));
                            Check(window.Opacity == 1 && ((SolidColorBrush)window.Resources["SurfaceBrush"]).Color.A < 110 && ((SolidColorBrush)window.Resources["TextBrush"]).Color.A == 255 && MaximumTextAlpha(window, heading) == 255 && MaximumTextAlpha(window, combination) == 255 && MaximumTextAlpha(window, note) == 255, "background becomes translucent while title, keys and emoji remain opaque / " + theme);
                        }
                        window.State.Theme = "Glass"; window.PreviewOpacity(0.6); window.Save();
                        bool wasTopmost = window.Topmost; window.Topmost = true;
                        Window backdrop = GlassBackdrop(window);
                        try
                        {
                            window.State.Theme = "Light"; window.PreviewOpacity(0.35); await System.Threading.Tasks.Task.Delay(250);
                            using (System.Drawing.Bitmap clear = CaptureComposite(window, Path.Combine(output, "system-background-clear.png")))
                            {
                                double clearVariation = BackdropVariation(clear, Native.Scale(window.Dock.Screen));
                                window.State.Theme = "Glass"; window.PreviewOpacity(0.35); await System.Threading.Tasks.Task.Delay(350);
                                Capture(window, Path.Combine(output, "zh-glass-foreground.png"));
                                using (System.Drawing.Bitmap frosted = CaptureComposite(window, Path.Combine(output, "system-background-glass.png")))
                                {
                                    double glassVariation = BackdropVariation(frosted, Native.Scale(window.Dock.Screen));
                                    Check(!window.GlassAvailable || glassVariation < clearVariation * 0.7, "system glass blurs the dedicated test backdrop (clear=" + clearVariation.ToString("0.00") + ", glass=" + glassVariation.ToString("0.00") + ", available=" + window.GlassAvailable + ")");
                                    var blueSide = frosted.GetPixel((int)(40 * Native.Scale(window.Dock.Screen)), (int)(96 * Native.Scale(window.Dock.Screen)));
                                    var pinkSide = frosted.GetPixel(frosted.Width - (int)(40 * Native.Scale(window.Dock.Screen)), (int)(96 * Native.Scale(window.Dock.Screen)));
                                    Check(Math.Abs((blueSide.B - blueSide.R) - (pinkSide.B - pinkSide.R)) > 40, "system glass preserves the test backdrop's different blue and pink regions");
                                    Check(window.GlassBackgroundDark && ThemeService.Luminance(((SolidColorBrush)window.Resources["TextBrush"]).Color.R, ((SolidColorBrush)window.Resources["TextBrush"]).Color.G, ((SolidColorBrush)window.Resources["TextBrush"]).Color.B) > 0.8, "glass uses bright text over the dark colored test backdrop");
                                    uint affinity;
                                    Check(GetWindowDisplayAffinity(Native.Handle(window), out affinity) && affinity == 0, "normal screen capture visibility is restored after taking the glass background");
                                }
                            }
                            ((Grid)backdrop.Content).Background = new LinearGradientBrush(Color.FromRgb(222, 231, 252), Color.FromRgb(255, 224, 235), 0);
                            await System.Threading.Tasks.Task.Delay(850);
                            Check(!window.GlassBackgroundDark && ThemeService.Luminance(((SolidColorBrush)window.Resources["TextBrush"]).Color.R, ((SolidColorBrush)window.Resources["TextBrush"]).Color.G, ((SolidColorBrush)window.Resources["TextBrush"]).Color.B) < 0.1, "visible glass refreshes the changing background and adapts text contrast");
                            using (var updated = CaptureComposite(window, Path.Combine(output, "system-glass-light.png"))) { }
                            window.ShortcutGrid.SelectedItem = window.ShortcutGrid.Items[0];
                            CompleteDialog(window, new[] { "解锁后备注 🎉" }, Path.Combine(output, "glass-notes-editor.png")); window.EditNotes();
                        }
                        finally { backdrop.Close(); window.Topmost = wasTopmost; }
                        code = 0;
                    }
                    catch (Exception e) { checks.Add(e.ToString()); }
                    finally
                    {
                        File.WriteAllLines(Path.Combine(output, "verification.txt"), checks.ToArray());
                        File.WriteAllText(Path.Combine(output, "result.txt"), code == 0 ? "PASS" : "FAIL");
                        window.Close(); app.Shutdown(code);
                    }
                }));
            };
            app.Run(window); return code;
        }
    }
}
