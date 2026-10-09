using System;
using System.Collections.Generic;
using System.Linq;

namespace ShortcutDock
{
    static class RowOrdering
    {
        // Replace visible slots only. Hidden search results and the other pin group
        // retain their positions; the JSON list remains the source of manual order.
        internal static bool Move(SoftwareTab tab, IList<ShortcutEntry> visible, IEnumerable<string> selection, int insertion)
        {
            if (tab == null || visible == null || insertion < 0 || insertion > visible.Count) return false;
            HashSet<ShortcutEntry> source = new HashSet<ShortcutEntry>(tab.Shortcuts);
            HashSet<string> ids = new HashSet<string>(selection ?? Enumerable.Empty<string>());
            List<ShortcutEntry> moved = visible.Where(row => ids.Contains(row.Id)).ToList();
            if (moved.Count == 0 || moved.Count != ids.Count || moved.Any(row => !source.Contains(row))) return false;
            bool pinned = moved[0].Pinned;
            if (moved.Any(row => row.Pinned != pinned)) return false;
            List<ShortcutEntry> group = visible.Where(row => row.Pinned == pinned).ToList();
            if (group.Distinct().Count() != group.Count || group.Any(row => !source.Contains(row))) return false;
            List<ShortcutEntry> remaining = group.Where(row => !ids.Contains(row.Id)).ToList();
            int destination = visible.Take(insertion).Count(row => row.Pinned == pinned && !ids.Contains(row.Id));
            remaining.InsertRange(destination, moved);
            if (group.SequenceEqual(remaining)) return false;
            HashSet<string> slots = new HashSet<string>(group.Select(row => row.Id));
            int next = 0;
            for (int i = 0; i < tab.Shortcuts.Count; i++)
                if (slots.Contains(tab.Shortcuts[i].Id)) tab.Shortcuts[i] = remaining[next++];
            return true;
        }
    }
}
