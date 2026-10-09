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
            VerifyRowOrderingLogic(store);
            VerifyGroupLogic(store);
            VerifyTabOrderingLogic(store);
            store.Save(AppState.CreateDefault());
        }
        static void VerifyTabOrderingLogic(StateStore store)
        {
            AppState state = AppState.CreateDefault();
            TabGroup design = new TabGroup { Name = "设计" }, work = new TabGroup { Name = "工作" };
            SoftwareTab drawing = new SoftwareTab { Name = "绘图", GroupId = design.Id };
            SoftwareTab notes = new SoftwareTab { Name = "笔记", GroupId = design.Id };
            SoftwareTab other = new SoftwareTab { Name = "其他", GroupId = work.Id };
            notes.Shortcuts.Add(new ShortcutEntry("N", "记录") { Notes = "🧑🏽‍💻", Pinned = true });
            state.Groups.AddRange(new[] { design, work }); state.Tabs.AddRange(new[] { drawing, notes, other });
            state.RootOrder = null; state.Validate();
            string first = TabOrdering.TabKey(state.Tabs[0].Id), second = TabOrdering.TabKey(state.Tabs[1].Id);
            string designKey = TabOrdering.GroupKey(design.Id), workKey = TabOrdering.GroupKey(work.Id);
            Check(TabOrdering.Visible(state).SequenceEqual(new[] { designKey, workKey, first, second }),
                "missing root order preserves legacy groups-first layout and member source order");
            store.Save(state);
            string legacy = Regex.Replace(File.ReadAllText(store.StatePath), "\"RootOrder\":\\[[^\\]]*\\],?", "");
            string path = Path.Combine(store.DirectoryPath, "legacy-v0.8.json"); File.WriteAllText(path, legacy);
            AppState migrated = StateStore.Read(path);
            Check(migrated.RootOrder.SequenceEqual(state.RootOrder) && migrated.Tabs.Count == 5 && migrated.Tabs[3].Shortcuts[0].Notes == "🧑🏽‍💻",
                "v0.8 JSON without RootOrder migrates without losing group members or emoji");
            Check(TabOrdering.Move(state, designKey, 3) && state.RootOrder.SequenceEqual(new[] { workKey, first, designKey, second }),
                "root group moves between software tabs in one mixed order");
            Check(TabOrdering.Move(state, second, 0) && state.RootOrder.SequenceEqual(new[] { second, workKey, first, designKey }),
                "root software tab moves before groups");
            Check(TabOrdering.Move(state, designKey, 1) && state.RootOrder.SequenceEqual(new[] { second, designKey, workKey, first }),
                "group moves back to an earlier root position");
            List<string> before = state.RootOrder.ToList();
            Check(!TabOrdering.Move(state, second, 0) && !TabOrdering.Move(state, second, 1) &&
                !TabOrdering.Move(state, first, -1) && !TabOrdering.Move(state, first, 5) && !TabOrdering.Move(state, "missing", 0) &&
                !TabOrdering.Move(state, TabOrdering.TabKey(notes.Id), 0) && state.RootOrder.SequenceEqual(before),
                "self drops, invalid indices and hidden members leave root order unchanged");
            state.ActiveTabId = "missing"; state.Validate();
            Check(state.ActiveTabId == state.Tabs[1].Id, "active-tab recovery follows saved mixed root order");
            state.ActiveGroupId = design.Id; state.Validate();
            List<SoftwareTab> original = state.Tabs.ToList(); string active = state.ActiveTabId;
            Check(TabOrdering.Move(state, TabOrdering.TabKey(notes.Id), 0) && state.Tabs[2] == notes && state.Tabs[3] == drawing &&
                state.Tabs[0] == original[0] && state.Tabs[1] == original[1] && state.Tabs[4] == other,
                "group member ordering replaces only that group's slots");
            Check(state.RootOrder.SequenceEqual(before) && state.ActiveTabId == active && notes.GroupId == design.Id && notes.Shortcuts[0].Pinned,
                "member sorting retains root order, current tab, membership, notes and pins");
            Check(!TabOrdering.Move(state, first, 0) && !TabOrdering.Move(state, workKey, 0), "group scope rejects outside software and group ordering");
            store.Save(state); AppState read = StateStore.Read(store.StatePath);
            Check(read.RootOrder.SequenceEqual(before) && read.ActiveGroupId == design.Id && read.ActiveTabId == active &&
                TabOrdering.Visible(read).SequenceEqual(new[] { TabOrdering.TabKey(notes.Id), TabOrdering.TabKey(drawing.Id) }),
                "root mixed order and group member order survive JSON save and reload");
            state.ActiveGroupId = null;
            state.RootOrder = new List<string> { second, second, "missing", null, TabOrdering.TabKey(notes.Id), first };
            state.Validate();
            Check(state.RootOrder.SequenceEqual(new[] { second, first, designKey, workKey }),
                "root normalization repairs duplicates, stale keys and hidden members while retaining user order");
            SoftwareTab added = new SoftwareTab { Name = "新增" }; state.Tabs.Add(added); state.Validate();
            Check(state.RootOrder.Last() == TabOrdering.TabKey(added.Id), "new root items append after the existing saved order");
            state.Groups.Remove(work); state.Validate();
            Check(!state.RootOrder.Contains(workKey) && state.RootOrder.Last() == TabOrdering.TabKey(other.Id) && other.GroupId == null,
                "deleted group keys are pruned and recovered members append without data loss");
        }
        static void VerifyGroupLogic(StateStore store)
        {
            AppState state = AppState.CreateDefault();
            store.Save(state);
            string legacy = File.ReadAllText(store.StatePath).Replace(",\"Groups\":[]", "").Replace("\"Groups\":[],", "");
            string path = Path.Combine(store.DirectoryPath, "legacy-v0.7.json"); File.WriteAllText(path, legacy);
            AppState older = StateStore.Read(path);
            Check(older.Groups.Count == 0 && older.ActiveGroupId == null && older.Tabs.All(tab => tab.GroupId == null) &&
                older.Tabs[0].Shortcuts.Count == 12, "old files without group fields retain all tabs and shortcuts at the top level");
            TabGroup group = new TabGroup { Name = "设计 / Design" }; state.Groups.Add(group);
            state.Tabs[1].GroupId = group.Id; state.ActiveGroupId = group.Id; state.Validate();
            Check(state.ActiveTabId == state.Tabs[1].Id, "entering a group selects an existing member rather than an outside tab");
            state.Tabs[1].Shortcuts[0].Notes = "组内备注 🧑🏽‍💻"; state.Tabs[1].Shortcuts[0].Pinned = true;
            store.Save(state); AppState read = StateStore.Read(store.StatePath);
            Check(read.Groups[0].Id == group.Id && read.Groups[0].Name == group.Name && read.ActiveGroupId == group.Id &&
                read.Tabs[1].GroupId == group.Id && read.Tabs[1].Shortcuts[0].Notes == "组内备注 🧑🏽‍💻" &&
                read.Tabs[1].Shortcuts[0].Pinned, "group membership, navigation, IDs, pins and emoji survive JSON round trip");
            TabGroup empty = new TabGroup { Name = "Empty" }; state.Groups.Add(empty); state.ActiveGroupId = empty.Id; state.Validate();
            Check(state.ActiveTabId == null && state.Tabs.Count == 2, "empty group has no active tab and keeps outside tabs intact");
            state.ActiveGroupId = null; state.Validate();
            Check(state.ActiveTabId == state.Tabs[0].Id, "returning to the top level chooses an ungrouped tab");
            state.Groups.Remove(group); state.Tabs[0].GroupId = "missing"; state.ActiveGroupId = "missing"; state.Validate();
            Check(state.Tabs.All(tab => tab.GroupId == null) && state.ActiveGroupId == null && state.Tabs[1].Shortcuts[0].Pinned,
                "orphaned group references recover to the top level without losing content");
            state.Groups.Add(new TabGroup { Id = empty.Id, Name = "Duplicate ID" }); state.Validate();
            Check(state.Groups.Select(item => item.Id).Distinct().Count() == 2, "duplicate group IDs are repaired");
            bool rejected = false; state.Groups.Add(new TabGroup { Name = new string('x', 61) });
            try { state.Validate(); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "group names over 60 characters are rejected before saving");
            state = AppState.CreateDefault(); state.Groups = Enumerable.Range(0, 201).Select(i => new TabGroup { Name = i.ToString() }).ToList();
            rejected = false; try { state.Validate(); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "group count limit is enforced independently of the tab count");
        }
        static SoftwareTab OrderFixture()
        {
            SoftwareTab tab = new SoftwareTab { Name = "Order test" };
            foreach (string key in new[] { "A", "B", "C", "D", "E" }) tab.Shortcuts.Add(new ShortcutEntry(key, key) { Notes = "备注 🎉 " + key });
            return tab;
        }
        static string OrderKeys(SoftwareTab tab) { return String.Join("", tab.Shortcuts.Select(row => row.Keys)); }
        static void VerifyRowOrderingLogic(StateStore store)
        {
            SoftwareTab tab = OrderFixture();
            string[] ids = tab.Shortcuts.Skip(1).Take(2).Select(row => row.Id).ToArray();
            Check(RowOrdering.Move(tab, tab.Shortcuts.ToList(), ids, 5) && OrderKeys(tab) == "ADEBC", "block moves down without reversing selected entries");
            Check(RowOrdering.Move(tab, tab.Shortcuts.ToList(), ids, 1) && OrderKeys(tab) == "ABCDE", "block moves back up to an insertion boundary");
            ids = new[] { tab.Shortcuts[2].Id, tab.Shortcuts[0].Id };
            Check(RowOrdering.Move(tab, tab.Shortcuts.ToList(), ids, 5) && OrderKeys(tab) == "BDEAC", "non-contiguous selection moves in displayed order");
            tab = OrderFixture();
            Check(!RowOrdering.Move(tab, tab.Shortcuts.ToList(), tab.Shortcuts.Skip(1).Take(2).Select(row => row.Id), 2) && OrderKeys(tab) == "ABCDE", "drop within the original block does not change order");
            List<ShortcutEntry> filtered = new List<ShortcutEntry> { tab.Shortcuts[1], tab.Shortcuts[3] };
            Check(RowOrdering.Move(tab, filtered, new[] { filtered[1].Id }, 0) && OrderKeys(tab) == "ADCBE", "filtered reorder preserves the positions of hidden entries");
            tab = OrderFixture(); tab.Shortcuts[0].Pinned = tab.Shortcuts[4].Pinned = true;
            List<ShortcutEntry> visible = tab.Shortcuts.OrderByDescending(row => row.Pinned).ToList();
            Check(RowOrdering.Move(tab, visible, new[] { tab.Shortcuts[4].Id }, 0) && OrderKeys(tab) == "EBCDA" && tab.Shortcuts[0].Pinned && tab.Shortcuts[4].Pinned, "pinned group can be reordered without changing pin flags");
            visible = tab.Shortcuts.OrderByDescending(row => row.Pinned).ToList();
            Check(!RowOrdering.Move(tab, visible, new[] { tab.Shortcuts[0].Id, tab.Shortcuts[1].Id }, 3) && OrderKeys(tab) == "EBCDA", "mixed pin groups are rejected without mutation");
            Check(!RowOrdering.Move(tab, visible, new[] { "foreign-id" }, 0) && !RowOrdering.Move(tab, visible, new[] { tab.Shortcuts[0].Id }, -1), "invalid selections and insertion indices leave order unchanged");
            AppState state = AppState.CreateDefault(); state.Tabs.Add(tab); store.Save(state);
            SoftwareTab reloaded = StateStore.Read(store.StatePath).Tabs.Last();
            Check(OrderKeys(reloaded) == "EBCDA" && reloaded.Shortcuts.Select(row => row.Id).SequenceEqual(tab.Shortcuts.Select(row => row.Id)) && reloaded.Shortcuts.All(row => row.Notes.Contains("🎉")), "manual order, IDs, pin flags and emoji notes survive JSON persistence");
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
        static byte[] SearchPixels(TextBox search)
        {
            search.UpdateLayout();
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext drawing = visual.RenderOpen())
                drawing.DrawRectangle(new VisualBrush(search), null, new Rect(0, 0, search.ActualWidth, search.ActualHeight));
            int width = (int)Math.Ceiling(search.ActualWidth), height = (int)Math.Ceiling(search.ActualHeight);
            RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
            byte[] pixels = new byte[width * height * 4]; bitmap.CopyPixels(pixels, width * 4, 0); return pixels;
        }
        static void VerifySearchRendering(MainWindow window, string output)
        {
            // Check actual rendered glyphs, rather than only Text or filtered rows:
            // both continued to work while the old template clipped all input.
            TextBox search = Controls<TextBox>(window).Single(t => t.Name == "ShortcutSearch");
            foreach (string theme in new[] { "Light", "Dark", "Glass", "Auto" })
            foreach (double scale in new[] { 0.8, 1.0, 1.5 })
            foreach (bool pinned in new[] { false, true })
            {
                if (window.State.Pinned != pinned) window.TogglePanelPin();
                window.State.Theme = theme; window.State.FontScale = scale;
                FontSizing.Apply(window); window.ApplyTheme(); window.UpdateLayout();
                foreach (string query in new[] { "新建", "Ctrl" })
                {
                    search.Text = ""; byte[] blank = SearchPixels(search);
                    search.Text = query; byte[] typed = SearchPixels(search);
                    ScrollViewer host = (ScrollViewer)search.Template.FindName("PART_ContentHost", search);
                    Rect character = search.GetRectFromCharacterIndex(0);
                    string scenario = theme + " / " + scale + " / pinned=" + pinned + " / " + query;
                    Check(host.ViewportHeight + 0.5 >= character.Height && character.Height > 0,
                        "search viewport fits a complete input line / " + scenario);
                    int visiblePixels = 0;
                    for (int i = 0; i < typed.Length; i += 4)
                        if (Math.Abs(typed[i] - blank[i]) + Math.Abs(typed[i + 1] - blank[i + 1]) + Math.Abs(typed[i + 2] - blank[i + 2]) > 40) visiblePixels++;
                    Check(visiblePixels > 40, "Chinese / Latin search glyphs actually render / " + scenario + " (pixels=" + visiblePixels + ")");
                    Check(search.IsEnabled && !search.IsReadOnly && window.ShortcutGrid.Items.Count > 0,
                        "search stays editable and filters matching shortcuts / " + scenario);
                    if (theme == "Light" && scale == 1 && pinned && query == "新建")
                        Capture(window, Path.Combine(output, "search-chinese-pinned.png"));
                    if (theme == "Glass" && scale == 1 && pinned && query == "Ctrl")
                        Capture(window, Path.Combine(output, "search-latin-glass-pinned.png"));
                }
            }
            if (window.State.Pinned) window.TogglePanelPin();
            search.Text = ""; window.State.Theme = "Glass"; window.State.FontScale = 1;
            FontSizing.Apply(window); window.ApplyTheme(); window.UpdateLayout();
        }
        static void VerifyRowReorder(MainWindow window, string output)
        {
            // Keep this control capture deterministic: the preceding synchronous
            // search checks do not advance the opening animation's render clock.
            window.State.Theme = "Light"; window.ApplyTheme();
            window.Surface.BeginAnimation(UIElement.OpacityProperty, null); window.Surface.Opacity = 1;
            window.Surface.RenderTransform = Transform.Identity;
            SoftwareTab tab = window.ActiveTab(); List<ShortcutEntry> original = tab.Shortcuts.ToList();
            DataGrid grid = window.ShortcutGrid; RowReorder controller = window.RowOrder;
            TextBox search = Controls<TextBox>(window).Single(box => box.Name == "ShortcutSearch");
            Check(grid.SelectionMode == DataGridSelectionMode.Extended && grid.SelectionUnit == DataGridSelectionUnit.FullRow, "shortcut rows support extended full-row selection");
            controller.Select(original[1], System.Windows.Input.ModifierKeys.None);
            controller.Select(original[3], System.Windows.Input.ModifierKeys.Shift);
            Check(grid.SelectedItems.Count == 3 && original.Skip(1).Take(3).All(row => grid.SelectedItems.Contains(row)), "Shift-click selects the full anchored range");
            controller.Select(original[5], System.Windows.Input.ModifierKeys.Control);
            Check(grid.SelectedItems.Count == 4 && grid.SelectedItems.Contains(original[5]), "Ctrl-click adds a non-contiguous row");
            controller.Select(original[2], System.Windows.Input.ModifierKeys.Shift);
            Check(grid.SelectedItems.Count == 4 && original.Skip(2).Take(4).All(row => grid.SelectedItems.Contains(row)), "reverse Shift-click range uses the latest selection anchor");
            controller.Select(original[1], System.Windows.Input.ModifierKeys.None); controller.Select(original[3], System.Windows.Input.ModifierKeys.Shift);
            grid.UpdateLayout();
            DataGridRow pressed = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(original[2]);
            int depth = window.Dock.InteractionDepth;
            System.Windows.Input.MouseButtonEventArgs press = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left) { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent };
            pressed.RaiseEvent(press);
            Check(grid.SelectedItems.Count == 3 && window.Dock.InteractionDepth == depth + 1 && window.IsMouseCaptured, "routed press on a selected row retains the group and prevents auto-hide");
            window.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });
            Check(grid.SelectedItems.Count == 1 && grid.SelectedItem == original[2] && !controller.IsInteracting && window.Dock.InteractionDepth == depth, "plain click release selects one row and releases the interaction guard");
            controller.Select(original[1], System.Windows.Input.ModifierKeys.None); controller.Select(original[3], System.Windows.Input.ModifierKeys.Shift);
            pressed.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left) { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent });
            controller.Cancel();
            Check(window.Dock.InteractionDepth == depth && !controller.IsInteracting && !window.IsMouseCaptured, "cancel releases mouse capture and the dock interaction guard");
            Check(!RowReorder.Ready(200, new Point(), new Point(20, 20)) && !RowReorder.Ready(400, new Point(), new Point()) && RowReorder.Ready(400, new Point(), new Point(20, 20)), "drag requires both a long press and actual pointer movement");
            RowDragData payload = controller.CreateData();
            DataGridRow target = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(original[4]);
            int insertion; double lineY;
            Point before = target.TranslatePoint(new Point(12, target.ActualHeight * 0.25), grid);
            Point after = target.TranslatePoint(new Point(12, target.ActualHeight * 0.75), grid);
            Check(controller.Target(before, payload, out insertion, out lineY) && insertion == 4 &&
                controller.Target(after, payload, out insertion, out lineY) && insertion == 5, "real row hit testing resolves insertion before and after the pointed row");
            controller.ShowMarker(lineY); Capture(window, Path.Combine(output, "multi-select-insertion-marker.png"));
            AdornerLayer layer = AdornerLayer.GetAdornerLayer(grid);
            Check(layer != null && layer.GetAdorners(grid) != null && layer.GetAdorners(grid).Any(item => item.GetType().Name == "RowInsertionAdorner"), "drag insertion boundary is drawn over the actual table");
            Check(controller.ApplyDrop(payload, original.Count), "drop commits a multi-row move through the live controller");
            List<ShortcutEntry> expected = original.Take(1).Concat(original.Skip(4)).Concat(original.Skip(1).Take(3)).ToList();
            Check(tab.Shortcuts.SequenceEqual(expected) && grid.Items.OfType<ShortcutEntry>().SequenceEqual(expected) && grid.SelectedItems.Count == 3, "table and stored order update together while moved rows remain selected");
            Check(StateStore.Read(window.Store.StatePath).Tabs[0].Shortcuts.Select(row => row.Id).SequenceEqual(expected.Select(row => row.Id)), "drag order is saved immediately for restart and backup");
            Check(controller.ApplyDrop(controller.CreateData(), 1) && tab.Shortcuts.SequenceEqual(original), "same group can be dragged upward again");
            payload = controller.CreateData(); controller.Cancel();
            Check(!controller.ApplyDrop(payload, original.Count) && tab.Shortcuts.SequenceEqual(original), "cancelled drag cannot later commit a stale drop");
            Check(!controller.ApplyDrop(new RowDragData { Source = null, TabId = tab.Id, Query = "", Ids = payload.Ids }, 0), "external drag data cannot modify shortcut order");
            grid.ContextMenu.IsOpen = true;
            Check(grid.ContextMenu.Items.OfType<MenuItem>().All(item => !item.IsEnabled), "multi-selection disables ambiguous single-row editing and deletion");
            grid.ContextMenu.IsOpen = false;
            search.Text = "Ctrl"; window.UpdateLayout();
            List<ShortcutEntry> filtered = grid.Items.OfType<ShortcutEntry>().ToList();
            controller.Select(filtered.Last(), System.Windows.Input.ModifierKeys.None); RowDragData filteredPayload = controller.CreateData();
            List<ShortcutEntry> hidden = original.Where(row => !filtered.Contains(row)).ToList();
            Check(controller.ApplyDrop(filteredPayload, 0) && grid.Items[0] == filtered.Last() && hidden.All(row => tab.Shortcuts.IndexOf(row) == original.IndexOf(row)), "search-filtered drag moves matches while hidden rows stay put");
            search.Text = "not-a-match";
            Check(!controller.ApplyDrop(filteredPayload, 0), "changing the search query invalidates an outstanding drag");
            search.Text = ""; tab.Shortcuts = original.ToList(); window.RenderRows();
            controller.Select(original[2], System.Windows.Input.ModifierKeys.None); payload = controller.CreateData();
            window.State.ActiveTabId = window.State.Tabs[1].Id;
            Check(!controller.ApplyDrop(payload, 0), "switching software tabs rejects stale drag data");
            window.State.ActiveTabId = tab.Id;
            controller.Select(original[2], System.Windows.Input.ModifierKeys.None);
            grid.UpdateLayout();
            pressed = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(original[2]);
            pressed.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left) { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent });
            window.TogglePanelPin(); string saved = File.ReadAllText(window.Store.StatePath);
            Check(!controller.IsInteracting && !window.IsMouseCaptured && window.Dock.InteractionDepth == depth, "pinning during a pending press cancels capture and releases the dock guard");
            Check(!controller.ApplyDrop(payload, 0) && !window.ReorderRows(payload.Ids, 0) && File.ReadAllText(window.Store.StatePath) == saved, "pinned panel rejects row sorting without changing saved data");
            window.TogglePanelPin();
            original[0].Pinned = true; window.RenderRows(); controller.Select(original[0], System.Windows.Input.ModifierKeys.None); controller.Select(original[1], System.Windows.Input.ModifierKeys.Control);
            Check(!controller.ApplyDrop(controller.CreateData(), 5) && tab.Shortcuts.SequenceEqual(original), "dragging a selection across both pin groups preserves the existing pin behavior");
            original[0].Pinned = false; window.RenderRows(); grid.SelectedItems.Clear(); controller.Cancel(); window.Save();
        }
        static void OpenGroup(Button button)
        {
            button.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount,
                System.Windows.Input.MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent });
        }
        static async System.Threading.Tasks.Task VerifyTabGroups(MainWindow window, string output)
        {
            List<SoftwareTab> original = window.State.Tabs.ToList();
            Button plus = Controls<Button>(window).Single(button => button.Name == "AddSoftwareTab");
            plus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(plus.ContextMenu.IsOpen && plus.ContextMenu.Items.OfType<MenuItem>().Select(item => item.Header.ToString()).SequenceEqual(new[] { window.T["AddTab"], window.T["AddGroup"] }),
                "tab bar plus opens separate create-tab and create-group choices");
            plus.ContextMenu.IsOpen = false;
            CompleteDialog(window, new[] { "设计" }, Path.Combine(output, "group-create-dialog.png"));
            ((MenuItem)plus.ContextMenu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            TabGroup group = window.State.Groups.Single();
            Check(group.Name == "设计" && StateStore.Read(window.Store.StatePath).Groups.Single().Id == group.Id,
                "group is created through its real naming dialog and persisted");
            CancelDialog(window); window.AddGroup();
            Check(window.State.Groups.Count == 1, "cancelling group creation leaves existing groups unchanged");
            CompleteDialog(window, new[] { "空组" }, null); window.AddGroup();
            TabGroup empty = window.State.Groups.Last();
            OpenGroup(Controls<Button>(window).Single(button => button.Name == "TabGroup" && (button.Tag as string) == empty.Id));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(window.State.ActiveGroupId == empty.Id && window.ActiveTab() == null && window.ShortcutGrid.Items.Count == 0 &&
                Controls<TextBlock>(window).Any(block => block.Text == window.T["EmptyGroup"]), "empty group displays its own add-tab guidance");
            Controls<Button>(window).Single(button => button.Name == "BackToTabs").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Button folder = Controls<Button>(window).Single(button => button.Name == "TabGroup" && (button.Tag as string) == group.Id);
            folder.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(window.State.ActiveGroupId == null, "single-click on a group does not navigate; opening requires double-click");
            TabGroupDrag controller = window.GroupDrag;
            Button dragged = Controls<Button>(window).Single(button => button.Name == "SoftwareTab" && (button.Tag as string) == original[1].Id);
            int depth = window.Dock.InteractionDepth; bool guarded = false;
            System.Windows.Input.MouseButtonEventHandler observe = delegate { guarded = controller.IsInteracting && window.Dock.InteractionDepth == depth + 1; };
            dragged.PreviewMouseLeftButtonDown += observe;
            dragged.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount,
                System.Windows.Input.MouseButton.Left) { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent });
            dragged.PreviewMouseLeftButtonDown -= observe; controller.Cancel();
            Check(guarded && !controller.IsInteracting && window.Dock.InteractionDepth == depth,
                "wired tab press blocks auto-hide and cancellation releases the dock guard");
            TabDragData payload = controller.CreateData(original[1].Id);
            Check(folder.AllowDrop && controller.CanDrop(payload, group.Id) && !controller.CanDrop(payload, "missing") &&
                !controller.CanDrop(new TabDragData { Source = null, TabId = original[1].Id }, group.Id), "group target accepts only valid same-controller tab drag data");
            controller.Highlight(folder); window.UpdateLayout(); Capture(window, Path.Combine(output, "group-drop-target.png"));
            Check(Object.ReferenceEquals(folder.BorderBrush, window.Resources["AccentBrush"]), "valid group drop target uses the visible theme accent border");
            List<ShortcutEntry> contents = original[1].Shortcuts.ToList();
            Check(controller.ApplyDrop(payload, group.Id) && original[1].GroupId == group.Id && original[1].Shortcuts.SequenceEqual(contents),
                "dropping an outside tab into a group moves membership without recreating shortcuts");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(Controls<Button>(window).Where(button => button.Name == "SoftwareTab").Select(button => button.Tag as string).SequenceEqual(new[] { original[0].Id }) &&
                StateStore.Read(window.Store.StatePath).Tabs[1].GroupId == group.Id, "grouped tab leaves the root bar and membership is saved immediately");
            Check(!controller.ApplyDrop(payload, empty.Id) && !window.MoveTabToGroup(original[1].Id, group.Id), "disposed drag controller and duplicate group moves cannot mutate tabs");
            folder = Controls<Button>(window).Single(button => button.Name == "TabGroup" && (button.Tag as string) == group.Id);
            OpenGroup(folder); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(window.State.ActiveGroupId == group.Id && window.ActiveTab() == original[1] && window.ShortcutGrid.Items.Count == contents.Count &&
                Controls<Button>(window).Count(button => button.Name == "SoftwareTab") == 1 &&
                Controls<Button>(window).Any(button => button.Name == "BackToTabs"), "wired group double-click opens only member tabs with a back control");
            Capture(window, Path.Combine(output, "group-open.png"));
            AppState savedGroup = StateStore.Read(window.Store.StatePath);
            Check(savedGroup.ActiveGroupId == group.Id && savedGroup.ActiveTabId == original[1].Id, "current group and active member restore from saved data");
            TextBox search = Controls<TextBox>(window).Single(box => box.Name == "ShortcutSearch"); search.Text = "侧栏";
            Check(window.ShortcutGrid.Items.Count == 1 && search.GetRectFromCharacterIndex(0).Height > 0, "search continues to filter the active group member");
            search.Text = "";
            CompleteDialog(window, new[] { "组内新建" }, null);
            Controls<Button>(window).Single(button => button.Name == "AddSoftwareTab").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            SoftwareTab added = window.ActiveTab();
            Check(added.GroupId == group.Id && added.Name == "组内新建" && StateStore.Read(window.Store.StatePath).Tabs.Last().GroupId == group.Id,
                "plus inside a group creates a member tab through the existing dialog");
            window.CommitTextImport(TextImport.Parse("G@组内快捷键"), new TxtImportChoice { Name = "组内 TXT" });
            SoftwareTab imported = window.ActiveTab();
            Check(imported.GroupId == group.Id && imported.Shortcuts.Count == 1, "new TXT imports inherit the current group");
            window.CommitTextImport(TextImport.Parse("G2@追加"), new TxtImportChoice { TabId = original[0].Id });
            ShortcutEntry appended = original[0].Shortcuts.Last();
            Check(window.State.ActiveGroupId == null && window.ActiveTab() == original[0], "TXT append to an outside tab navigates to that tab's scope");
            original[0].Shortcuts.Remove(appended); window.RenderRows();
            controller = window.GroupDrag; payload = controller.CreateData(original[0].Id); controller.Cancel();
            Check(!controller.ApplyDrop(payload, group.Id), "cancelled tab drag cannot later move a tab");
            window.TogglePanelPin(); string locked = File.ReadAllText(window.Store.StatePath);
            window.AddGroup(); window.RenameGroup(group);
            Check(!window.MoveTabToGroup(original[0].Id, group.Id) && !window.Ungroup(group) && Dialogs.GroupName(window, null) == null &&
                File.ReadAllText(window.Store.StatePath) == locked, "fixed panel rejects group edits, moves and naming dialogs without changing saved data");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            folder = Controls<Button>(window).Single(button => button.Name == "TabGroup" && (button.Tag as string) == group.Id);
            Check(folder.ContextMenu.Items.OfType<MenuItem>().All(item => !item.IsEnabled) &&
                !Controls<Button>(window).Single(button => button.Name == "AddSoftwareTab").IsEnabled, "fixed panel disables group management and creation controls");
            OpenGroup(folder); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(window.IsEditingLocked && window.State.ActiveGroupId == group.Id && window.ActiveTab() == original[1], "fixed panel still allows group navigation");
            controller = window.GroupDrag; payload = controller.CreateData(original[1].Id);
            Check(!controller.ApplyDrop(payload, null), "fixed panel also rejects drag-out through the back target");
            Controls<Button>(window).Single(button => button.Name == "BackToTabs").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.TogglePanelPin(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            folder = Controls<Button>(window).Single(button => button.Name == "TabGroup" && (button.Tag as string) == group.Id);
            CompleteDialog(window, new[] { "设计工具" }, null);
            ((MenuItem)folder.ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(group.Name == "设计工具" && StateStore.Read(window.Store.StatePath).Groups[0].Name == group.Name,
                "group context rename updates its caption and saved name");
            OpenGroup(Controls<Button>(window).Single(button => button.Name == "TabGroup" && (button.Tag as string) == group.Id));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Button member = Controls<Button>(window).Single(button => button.Name == "SoftwareTab" && (button.Tag as string) == original[1].Id);
            member.ContextMenu.Items.OfType<MenuItem>().Single(item => item.Header.ToString() == window.T["RemoveFromGroup"]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(original[1].GroupId == null && window.State.ActiveGroupId == group.Id && window.ActiveTab() == added,
                "member context action moves the tab out and selects a remaining group member");
            controller = window.GroupDrag; payload = controller.CreateData(imported.Id);
            Check(controller.CanDrop(payload, null) && Controls<Button>(window).Single(button => button.Name == "BackToTabs").AllowDrop &&
                controller.ApplyDrop(payload, null) && imported.GroupId == null, "dragging a group member onto the back control returns it to the root");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            window.State.Language = "en"; window.Rebuild(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(Controls<Button>(window).Single(button => button.Name == "BackToTabs").ToolTip.ToString() == "Back to top-level tabs" &&
                Controls<Button>(window).Single(button => button.Name == "CurrentTabGroup").ContextMenu.Items.OfType<MenuItem>().First().Header.ToString() == "Rename group",
                "group navigation and management use English resources");
            Capture(window, Path.Combine(output, "en-group-open.png"));
            Check(window.Ungroup(group) && added.GroupId == null && window.State.ActiveGroupId == null && window.State.Tabs.Count == 4 &&
                original[1].Shortcuts.SequenceEqual(contents), "dissolving a group returns member tabs to the root without deleting their contents");
            window.Ungroup(empty); window.State.Tabs = original; window.State.ActiveTabId = original[0].Id;
            window.State.Language = "zh"; window.State.Theme = "Light"; window.Rebuild(); window.Save();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(window.State.Groups.Count == 0 && window.Dock.InteractionDepth == 0, "group workflow releases interaction guards and restores the isolated fixture");
        }
        static async System.Threading.Tasks.Task VerifyTabBarOrdering(MainWindow window, string output)
        {
            List<SoftwareTab> original = window.State.Tabs.ToList(); List<string> originalOrder = window.State.RootOrder.ToList();
            string originalActive = window.State.ActiveTabId; double width = window.State.PanelWidth, height = window.State.PanelHeight;
            TabGroup design = new TabGroup { Name = "设计" }, work = new TabGroup { Name = "工作" };
            SoftwareTab drawing = new SoftwareTab { Name = "绘图", GroupId = design.Id };
            SoftwareTab notes = new SoftwareTab { Name = "笔记", GroupId = design.Id };
            SoftwareTab other = new SoftwareTab { Name = "其他", GroupId = work.Id };
            drawing.Shortcuts.Add(new ShortcutEntry("D", "绘图")); notes.Shortcuts.Add(new ShortcutEntry("N", "笔记") { Notes = "🧑🏽‍💻" });
            window.State.Groups.AddRange(new[] { design, work }); window.State.Tabs.AddRange(new[] { drawing, notes, other });
            string first = TabOrdering.TabKey(original[0].Id), second = TabOrdering.TabKey(original[1].Id);
            string designKey = TabOrdering.GroupKey(design.Id), workKey = TabOrdering.GroupKey(work.Id);
            window.State.RootOrder = new List<string> { first, designKey, second, workKey };
            window.State.PanelWidth = 680; window.State.PanelHeight = 780; window.State.Validate(); window.Rebuild(); window.Dock.Place(); window.Save();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(Controls<Button>(window).Where(button => button.Name == "SoftwareTab" || button.Name == "TabGroup")
                .Select(button => (button.Name == "TabGroup" ? "g:" : "t:") + button.Tag).SequenceEqual(window.State.RootOrder),
                "real tab bar renders saved interleaved software and group order");
            TabGroupDrag controller = window.GroupDrag;
            Button folder = Controls<Button>(window).Single(button => button.Name == "TabGroup" && (button.Tag as string) == work.Id);
            int depth = window.Dock.InteractionDepth; bool guarded = false;
            System.Windows.Input.MouseButtonEventHandler observe = delegate { guarded = controller.IsInteracting && window.Dock.InteractionDepth == depth + 1; };
            folder.PreviewMouseLeftButtonDown += observe;
            folder.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount,
                System.Windows.Input.MouseButton.Left) { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent });
            folder.PreviewMouseLeftButtonDown -= observe; controller.Cancel();
            Check(guarded && !controller.IsInteracting && window.Dock.InteractionDepth == depth,
                "group tab press participates in the long-hold dock guard and releases it on cancel");
            Check(!TabGroupDrag.Ready(200, new Point(), new Point(20, 0)) && !TabGroupDrag.Ready(400, new Point(), new Point()) &&
                TabGroupDrag.Ready(400, new Point(), new Point(20, 0)), "tab ordering waits for both 320 ms hold and pointer movement");
            TabDragData payload = controller.CreateGroupData(work.Id);
            Check(payload.IsGroup && controller.ApplyReorder(payload, 1) && window.State.RootOrder.SequenceEqual(new[] { first, workKey, designKey, second }) &&
                window.State.Groups.SequenceEqual(new[] { design, work }), "group reorder changes root placement independently of group creation order");
            Check(!controller.CanReorder(payload), "rendering the reordered bar invalidates the completed drag payload");
            TextBox search = Controls<TextBox>(window).Single(box => box.Name == "ShortcutSearch"); search.Text = "Ctrl";
            search.Focus(); search.Select(2, 1); DataGrid grid = window.ShortcutGrid;
            grid.SelectedItem = grid.Items[0]; object selected = grid.SelectedItem; string active = window.State.ActiveTabId;
            payload = controller.CreateData(original[1].Id);
            Check(controller.ApplyReorder(payload, 0) && window.State.RootOrder.SequenceEqual(new[] { second, first, workKey, designKey }),
                "software tab can be moved before groups and other software tabs");
            Check(Object.ReferenceEquals(search, Controls<TextBox>(window).Single(box => box.Name == "ShortcutSearch")) && search.Text == "Ctrl" &&
                search.SelectionStart == 2 && search.SelectionLength == 1 && window.ShortcutGrid == grid && grid.SelectedItem == selected && window.State.ActiveTabId == active,
                "bar-only reorder retains the active shortcut list, selection, search text and caret");
            Check(StateStore.Read(window.Store.StatePath).RootOrder.SequenceEqual(window.State.RootOrder), "mixed tab order saves immediately to the real isolated store");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            folder = Controls<Button>(window).Single(button => button.Name == "TabGroup" && (button.Tag as string) == design.Id);
            payload = controller.CreateData(original[0].Id);
            TabDropLocation left = controller.Resolve(payload, folder.TranslatePoint(new Point(folder.ActualWidth * 0.1, folder.ActualHeight / 2), window.TabBarScroll));
            TabDropLocation center = controller.Resolve(payload, folder.TranslatePoint(new Point(folder.ActualWidth * 0.5, folder.ActualHeight / 2), window.TabBarScroll));
            TabDropLocation right = controller.Resolve(payload, folder.TranslatePoint(new Point(folder.ActualWidth * 0.9, folder.ActualHeight / 2), window.TabBarScroll));
            int folderPosition = window.State.RootOrder.IndexOf(designKey);
            Check(left.Kind == TabDropKind.Reorder && left.Insertion == folderPosition && right.Kind == TabDropKind.Reorder && right.Insertion == folderPosition + 1 &&
                center.Kind == TabDropKind.IntoGroup && center.GroupId == design.Id, "real group geometry separates ordering edges from join-group center");
            Check(controller.Resolve(payload, new Point(-1, 10)).Kind == TabDropKind.None &&
                controller.Resolve(payload, new Point(10, window.TabBarScroll.ActualHeight + 1)).Kind == TabDropKind.None,
                "pointer outside the tab scroll viewport has no reorder destination");
            controller.ShowMarker(left.X); window.UpdateLayout(); Capture(window, Path.Combine(output, "tab-order-insertion-line.png"));
            AdornerLayer layer = AdornerLayer.GetAdornerLayer(window.TabBarScroll);
            Check(layer != null && layer.GetAdorners(window.TabBarScroll) != null && layer.GetAdorners(window.TabBarScroll).Length == 1,
                "ordering indicator is a visible adorner attached to the tab bar");
            controller.Highlight(folder); window.UpdateLayout();
            Check(layer.GetAdorners(window.TabBarScroll) == null && Object.ReferenceEquals(folder.BorderBrush, window.Resources["AccentBrush"]),
                "group join highlight replaces the ordering line rather than showing both");
            controller.Cancel();
            Check(layer.GetAdorners(window.TabBarScroll) == null && !Object.ReferenceEquals(folder.BorderBrush, window.Resources["AccentBrush"]),
                "cancelling removes the insertion line and restores the group border");
            TabDragData cancelledPayload = payload; payload = controller.CreateData(original[0].Id);
            Check(!controller.CanReorder(cancelledPayload) && controller.CanReorder(payload), "starting another drag cannot revive an earlier cancelled payload");
            payload = controller.CreateGroupData(work.Id);
            center = controller.Resolve(payload, folder.TranslatePoint(new Point(folder.ActualWidth * 0.5, folder.ActualHeight / 2), window.TabBarScroll));
            Check(center.Kind == TabDropKind.Reorder && !controller.CanDrop(payload, design.Id), "dragging a group over another group reorders without nesting");
            Check(!controller.CanReorder(new TabDragData { Source = null, IsGroup = true, GroupId = work.Id }), "foreign drag payloads cannot sort the tab bar");
            window.TogglePanelPin(); string locked = File.ReadAllText(window.Store.StatePath);
            payload = controller.CreateGroupData(work.Id);
            Check(!controller.ApplyReorder(payload, 0) && !window.ReorderTabBar(first, 0) && File.ReadAllText(window.Store.StatePath) == locked,
                "pinned panel blocks software and group ordering without changing saved state");
            window.TogglePanelPin();
            Button ordinary = Controls<Button>(window).Single(button => button.Name == "SoftwareTab" && (button.Tag as string) == original[1].Id);
            // This check synthesizes only this app's routed release, without a
            // physical mouse press. Capture's synchronous move reports Released;
            // exclude that move while preparing the normal Button pressed state.
            System.Windows.Input.MouseEventHandler dragMove = (System.Windows.Input.MouseEventHandler)Delegate.CreateDelegate(
                typeof(System.Windows.Input.MouseEventHandler), controller, "OnMove");
            ordinary.PreviewMouseMove -= dragMove;
            ordinary.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount,
                System.Windows.Input.MouseButton.Left) { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent });
            Check(ordinary.CaptureMouse(), "ordinary tab press can retain native Button capture");
            typeof(ButtonBase).GetProperty("IsPressed").GetSetMethod(true).Invoke(ordinary, new object[] { true });
            payload = controller.CreateData(original[1].Id); bool releasePreserved = false, releaseObserved = false;
            System.Windows.Input.MouseButtonEventHandler observeRelease = delegate { releaseObserved = true; releasePreserved = ordinary.IsMouseCaptured && ordinary.IsPressed && !controller.IsInteracting; };
            window.PreviewMouseLeftButtonUp += observeRelease;
            ordinary.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount,
                System.Windows.Input.MouseButton.Left) { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseUpEvent });
            window.PreviewMouseLeftButtonUp -= observeRelease;
            ordinary.PreviewMouseMove += dragMove;
            Check(releasePreserved && window.Dock.InteractionDepth == depth, "preview release clears the hold guard without suppressing Button's ordinary click (observed=" + releaseObserved + ", capture=" + ordinary.IsMouseCaptured + ", pressed=" + ordinary.IsPressed + ", interacting=" + controller.IsInteracting + ", depth=" + window.Dock.InteractionDepth + ")");
            ordinary.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount,
                System.Windows.Input.MouseButton.Left) { RoutedEvent = System.Windows.Input.Mouse.MouseUpEvent });
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(window.ActiveTab() == original[1] && !controller.ApplyReorder(payload, 3), "ordinary software clicks still navigate and invalidate the previous drag controller");
            OpenGroup(Controls<Button>(window).Single(button => button.Name == "TabGroup" && (button.Tag as string) == design.Id));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            controller = window.GroupDrag; List<string> root = window.State.RootOrder.ToList(); List<ShortcutEntry> content = notes.Shortcuts.ToList();
            Check(controller.CreateGroupData(work.Id) == null && controller.CreateData(original[0].Id) == null,
                "inside a group only its member tabs can begin ordering");
            payload = controller.CreateData(notes.Id);
            Check(controller.ApplyReorder(payload, 0) && TabOrdering.Visible(window.State).SequenceEqual(new[] { TabOrdering.TabKey(notes.Id), TabOrdering.TabKey(drawing.Id) }) &&
                notes.GroupId == design.Id && drawing.GroupId == design.Id && notes.Shortcuts.SequenceEqual(content) && window.State.RootOrder.SequenceEqual(root),
                "member tabs reorder within their group while preserving contents and root placement");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(Controls<Button>(window).Where(button => button.Name == "SoftwareTab").Select(button => button.Tag as string).SequenceEqual(new[] { notes.Id, drawing.Id }) &&
                Controls<Button>(window).Single(button => button.Name == "BackToTabs").AllowDrop, "sorted member buttons render in order and retain the drag-out target");
            Capture(window, Path.Combine(output, "tab-order-members.png"));
            AppState saved = StateStore.Read(window.Store.StatePath);
            Check(TabOrdering.Visible(saved).SequenceEqual(TabOrdering.Visible(window.State)) && saved.Tabs.Last().Id == other.Id,
                "saved member order persists and leaves other group slots untouched");
            window.LeaveGroup(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            controller = window.GroupDrag; payload = controller.CreateData(original[0].Id);
            Check(controller.ApplyDrop(payload, work.Id) && original[0].GroupId == work.Id && !window.State.RootOrder.Contains(first),
                "joining a group still works after tab and group reordering");
            Check(window.MoveTabToGroup(original[0].Id, null) && window.State.RootOrder.Last() == first,
                "moving out restores a root software tab after the existing sorted items");
            List<string> expanded = window.State.RootOrder.ToList(); int position = expanded.IndexOf(designKey);
            expanded.RemoveAt(position); expanded.InsertRange(position, new[] { TabOrdering.TabKey(notes.Id), TabOrdering.TabKey(drawing.Id) });
            Check(window.Ungroup(design) && window.State.RootOrder.SequenceEqual(expanded) && notes.GroupId == null && drawing.GroupId == null,
                "dissolving a sorted group replaces it in place with its sorted member tabs");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); Capture(window, Path.Combine(output, "tab-order-mixed.png"));
            window.Ungroup(work); window.State.Tabs = original; window.State.RootOrder = originalOrder;
            window.State.ActiveTabId = originalActive; window.State.PanelWidth = width; window.State.PanelHeight = height;
            window.State.Validate(); window.Rebuild(); window.Dock.Place(); window.Save();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(window.Dock.InteractionDepth == depth && window.State.Groups.Count == 0 && window.State.RootOrder.SequenceEqual(originalOrder),
                "tab ordering workflow releases guards and restores the original fixture");
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
                        VerifySearchRendering(window, output);
                        VerifyRowReorder(window, output);
                        await VerifyTabGroups(window, output);
                        await VerifyTabBarOrdering(window, output);
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
