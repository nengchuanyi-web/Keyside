using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace ShortcutDock
{
    [DataContract]
    public sealed class ShortcutEntry
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Keys { get; set; }
        [DataMember] public string Description { get; set; }
        [DataMember] public string Notes { get; set; }
        [DataMember] public bool Pinned { get; set; }
        public string DisplayKeys { get { return (Pinned ? "↑ " : "") + Keys; } }
        public ShortcutEntry() { Id = Guid.NewGuid().ToString("N"); Notes = ""; }
        public ShortcutEntry(string keys, string description) : this() { Keys = keys; Description = description; }
    }

    [DataContract]
    public sealed class SoftwareTab
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public List<ShortcutEntry> Shortcuts { get; set; }
        public SoftwareTab() { Id = Guid.NewGuid().ToString("N"); Shortcuts = new List<ShortcutEntry>(); }
    }

    [DataContract]
    public sealed class AppState
    {
        [DataMember] public int Version { get; set; }
        [DataMember] public string Language { get; set; }
        [DataMember] public string Theme { get; set; }
        [DataMember] public string Edge { get; set; }
        [DataMember] public string Monitor { get; set; }
        [DataMember] public double Offset { get; set; }
        [DataMember] public double FloatingX { get; set; }
        [DataMember] public double FloatingY { get; set; }
        [DataMember] public bool Pinned { get; set; }
        [DataMember] public double PanelWidth { get; set; }
        [DataMember] public double PanelHeight { get; set; }
        [DataMember] public double PanelOpacity { get; set; }
        [DataMember] public string PanelColor { get; set; }
        [DataMember] public double FontScale { get; set; }
        [DataMember] public bool NotesCollapsed { get; set; }
        [DataMember] public double[] ColumnWeights { get; set; }
        [DataMember] public string ActiveTabId { get; set; }
        [DataMember] public List<SoftwareTab> Tabs { get; set; }
        public AppState()
        {
            Version = 1; Language = "zh"; Theme = "Glass"; Edge = "Right";
            Offset = 0.35; FloatingX = 100; FloatingY = 100; Tabs = new List<SoftwareTab>();
            PanelWidth = 440; PanelHeight = 660; PanelOpacity = 1;
            PanelColor = PanelPalette.Blue; FontScale = 1;
            ColumnWeights = new[] { 1.15, 1.15, 0.85 };
        }
        public static AppState CreateDefault()
        {
            AppState state = new AppState();
            SoftwareTab ps = new SoftwareTab { Name = "Photoshop" };
            ps.Shortcuts.AddRange(new[] {
                new ShortcutEntry("Ctrl + N", "新建文档 / New document"),
                new ShortcutEntry("Ctrl + O", "打开文件 / Open file"),
                new ShortcutEntry("Ctrl + S", "保存 / Save"),
                new ShortcutEntry("Ctrl + Shift + S", "另存为 / Save as"),
                new ShortcutEntry("Ctrl + Z", "撤销 / Undo"),
                new ShortcutEntry("Ctrl + T", "自由变换 / Free transform"),
                new ShortcutEntry("Ctrl + J", "复制图层 / Duplicate layer"),
                new ShortcutEntry("Ctrl + Shift + N", "新建图层 / New layer"),
                new ShortcutEntry("V", "移动工具 / Move tool"),
                new ShortcutEntry("B", "画笔工具 / Brush tool"),
                new ShortcutEntry("E", "橡皮擦 / Eraser"),
                new ShortcutEntry("Space", "临时抓手 / Pan")
            });
            SoftwareTab code = new SoftwareTab { Name = "VS Code" };
            code.Shortcuts.AddRange(new[] {
                new ShortcutEntry("Ctrl + Shift + P", "命令面板 / Command palette"),
                new ShortcutEntry("Ctrl + P", "快速打开 / Quick open"),
                new ShortcutEntry("Ctrl + B", "切换侧栏 / Toggle sidebar"),
                new ShortcutEntry("Ctrl + /", "行注释 / Toggle comment"),
                new ShortcutEntry("Alt + Shift + F", "格式化 / Format document")
            });
            state.Tabs.Add(ps); state.Tabs.Add(code); state.ActiveTabId = ps.Id;
            return state;
        }
        public void Validate()
        {
            if (Version != 1) throw new InvalidDataException("Unsupported data version.");
            if (Language != "zh" && Language != "en") Language = "zh";
            if (!new List<string> { "Glass", "Light", "Dark", "Auto" }.Contains(Theme)) Theme = "Glass";
            if (!new List<string> { "Left", "Right", "Top", "Bottom", "Floating" }.Contains(Edge)) Edge = "Right";
            if (Double.IsNaN(Offset) || Double.IsInfinity(Offset)) Offset = 0.35;
            Offset = Math.Max(0, Math.Min(1, Offset));
            if (Double.IsNaN(FloatingX) || Double.IsInfinity(FloatingX)) FloatingX = 100;
            if (Double.IsNaN(FloatingY) || Double.IsInfinity(FloatingY)) FloatingY = 100;
            // DataContract does not run this constructor when reading older JSON: missing fields are zero.
            if (!ValidPositive(PanelWidth)) PanelWidth = 440;
            if (!ValidPositive(PanelHeight)) PanelHeight = 660;
            if (!ValidPositive(PanelOpacity)) PanelOpacity = 1;
            PanelWidth = Math.Max(360, Math.Min(1400, PanelWidth));
            PanelHeight = Math.Max(450, Math.Min(1600, PanelHeight));
            PanelOpacity = Math.Max(0.35, Math.Min(1, PanelOpacity));
            if (!ValidPositive(FontScale)) FontScale = 1;
            FontScale = Math.Max(0.8, Math.Min(1.5, FontScale));
            PanelHeight = Math.Max(PanelHeight, FontSizing.MinimumHeight(FontScale));
            if (ColumnWeights == null || ColumnWeights.Length != 3) ColumnWeights = new[] { 1.15, 1.15, 0.85 };
            for (int i = 0; i < ColumnWeights.Length; i++)
                ColumnWeights[i] = ValidPositive(ColumnWeights[i]) ? Math.Max(0.05, Math.Min(100, ColumnWeights[i])) : (i == 2 ? 0.85 : 1.15);
            if (!String.IsNullOrEmpty(PanelColor))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(PanelColor, "^#[0-9a-fA-F]{6}$"))
                    throw new InvalidDataException("Invalid panel color.");
                PanelColor = PanelColor.ToUpperInvariant();
            }
            PanelColor = PanelPalette.Normalize(PanelColor);
            if (Tabs == null) Tabs = new List<SoftwareTab>();
            if (Tabs.Count > 200) throw new InvalidDataException("Too many tabs (maximum 200).");
            HashSet<string> ids = new HashSet<string>();
            foreach (SoftwareTab tab in Tabs)
            {
                if (tab == null || String.IsNullOrWhiteSpace(tab.Name)) throw new InvalidDataException("A tab needs a name.");
                if (tab.Name.Length > 60) throw new InvalidDataException("Tab name exceeds 60 characters.");
                if (String.IsNullOrEmpty(tab.Id) || !ids.Add(tab.Id)) { tab.Id = Guid.NewGuid().ToString("N"); ids.Add(tab.Id); }
                if (tab.Shortcuts == null) tab.Shortcuts = new List<ShortcutEntry>();
                if (tab.Shortcuts.Count > 10000) throw new InvalidDataException("Too many shortcuts.");
                foreach (ShortcutEntry row in tab.Shortcuts)
                {
                    if (row == null || String.IsNullOrWhiteSpace(row.Keys) || String.IsNullOrWhiteSpace(row.Description))
                        throw new InvalidDataException("Shortcut keys and description are required.");
                    if (row.Keys.Length > 160 || row.Description.Length > 1000) throw new InvalidDataException("Shortcut text is too long.");
                    if (row.Notes == null) row.Notes = "";
                    if (row.Notes.Length > 4000) throw new InvalidDataException("Notes exceed 4000 characters.");
                    if (String.IsNullOrEmpty(row.Id)) row.Id = Guid.NewGuid().ToString("N");
                }
            }
            if (!Tabs.Exists(t => t.Id == ActiveTabId)) ActiveTabId = Tabs.Count > 0 ? Tabs[0].Id : null;
        }
        static bool ValidPositive(double value) { return value > 0 && !Double.IsNaN(value) && !Double.IsInfinity(value); }
    }

    public sealed class StateStore
    {
        public string DirectoryPath { get; private set; }
        public string StatePath { get { return Path.Combine(DirectoryPath, "state.json"); } }
        public string Warning { get; private set; }
        public StateStore(string directory) { DirectoryPath = directory; Directory.CreateDirectory(directory); }
        public static AppState Read(string path)
        {
            if (new FileInfo(path).Length > 20 * 1024 * 1024) throw new InvalidDataException("JSON exceeds 20 MB.");
            using (FileStream stream = File.OpenRead(path))
            {
                AppState state = (AppState)new DataContractJsonSerializer(typeof(AppState)).ReadObject(stream);
                if (state == null) throw new InvalidDataException("Empty data.");
                state.Validate(); return state;
            }
        }
        public AppState Load()
        {
            if (!File.Exists(StatePath)) return AppState.CreateDefault();
            try { return Read(StatePath); }
            catch (Exception)
            {
                string damaged = StatePath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
                File.Copy(StatePath, damaged, false);
                Warning = "RecoveredData";
                // Remove only the damaged primary, after preserving it. Never replace a good backup with it.
                File.Delete(StatePath);
                if (File.Exists(StatePath + ".bak"))
                {
                    try { return Read(StatePath + ".bak"); } catch (Exception) { }
                }
                return AppState.CreateDefault();
            }
        }
        public void Save(AppState state) { Write(state, StatePath); }
        public static void Write(AppState state, string path)
        {
            state.Validate();
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { new DataContractJsonSerializer(typeof(AppState)).WriteObject(stream, state); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak", true);
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
