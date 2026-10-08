using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ShortcutDock
{
    public sealed class TxtFormatException : Exception
    {
        public int LineNumber { get; private set; }
        public TxtFormatException(int line) : base("Expected shortcut@action at line " + line) { LineNumber = line; }
    }
    public sealed class TxtImportChoice
    {
        public string TabId;
        public string Name;
    }
    public static class TextImport
    {
        public const string TemplateText = "Ctrl + N@新建文档 / New document\r\nCtrl + O@打开文件 / Open file\r\nCtrl + S@保存 / Save\r\n";
        public static void SaveTemplate(string path) { File.WriteAllText(path, TemplateText, new UTF8Encoding(false)); }
        public static List<ShortcutEntry> Read(string path)
        {
            if (new FileInfo(path).Length > 5 * 1024 * 1024) throw new InvalidDataException("TXT exceeds 5 MB.");
            byte[] bytes = File.ReadAllBytes(path); string text;
            if ((bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF))) ||
                (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF))
            {
                using (StreamReader reader = new StreamReader(new MemoryStream(bytes), new UTF8Encoding(false, true), true)) text = reader.ReadToEnd();
            }
            else
            {
                try { text = new UTF8Encoding(false, true).GetString(bytes); }
                catch (DecoderFallbackException) { text = Encoding.GetEncoding(54936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetString(bytes); }
            }
            return Parse(text);
        }
        public static List<ShortcutEntry> Parse(string text)
        {
            List<ShortcutEntry> rows = new List<ShortcutEntry>();
            using (StringReader reader = new StringReader(text ?? ""))
            {
                string line; int number = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    number++; if (String.IsNullOrWhiteSpace(line)) continue;
                    int separator = line.IndexOf('@');
                    if (separator <= 0 || separator == line.Length - 1) throw new TxtFormatException(number);
                    string keys = line.Substring(0, separator).Trim(); string action = line.Substring(separator + 1).Trim();
                    if (keys.Length == 0 || action.Length == 0 || keys.Length > 160 || action.Length > 1000) throw new TxtFormatException(number);
                    rows.Add(new ShortcutEntry(keys, action));
                    if (rows.Count > 10000) throw new InvalidDataException("TXT exceeds 10000 entries.");
                }
            }
            return rows;
        }
        // Build and validate the whole change before mutating the target tab.
        public static int Append(SoftwareTab tab, IEnumerable<ShortcutEntry> rows, out int skipped)
        {
            HashSet<string> known = new HashSet<string>(tab.Shortcuts.Select(r => r.Keys + "\0" + r.Description), StringComparer.Ordinal);
            List<ShortcutEntry> additions = new List<ShortcutEntry>(); skipped = 0;
            foreach (ShortcutEntry row in rows)
            {
                if (!known.Add(row.Keys + "\0" + row.Description)) { skipped++; continue; }
                additions.Add(row);
            }
            if (tab.Shortcuts.Count + additions.Count > 10000) throw new InvalidDataException("Too many shortcuts.");
            tab.Shortcuts.AddRange(additions); return additions.Count;
        }
    }
}
