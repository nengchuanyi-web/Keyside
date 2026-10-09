using System;
using System.Collections.Generic;
using System.Linq;

namespace ShortcutDock
{
    static class TabOrdering
    {
        internal static string TabKey(string id) { return "t:" + id; }
        internal static string GroupKey(string id) { return "g:" + id; }
        internal static void Normalize(AppState state)
        {
            List<string> available = state.Groups.Select(group => GroupKey(group.Id))
                .Concat(state.Tabs.Where(tab => tab.GroupId == null).Select(tab => TabKey(tab.Id))).ToList();
            HashSet<string> allowed = new HashSet<string>(available), seen = new HashSet<string>();
            state.RootOrder = (state.RootOrder ?? new List<string>()).Concat(available)
                .Where(key => key != null && allowed.Contains(key) && seen.Add(key)).ToList();
        }
        internal static List<string> Visible(AppState state)
        {
            Normalize(state);
            return state.ActiveGroupId == null ? state.RootOrder.ToList() :
                state.Tabs.Where(tab => tab.GroupId == state.ActiveGroupId).Select(tab => TabKey(tab.Id)).ToList();
        }
        internal static List<SoftwareTab> VisibleTabs(AppState state)
        {
            Dictionary<string, SoftwareTab> tabs = state.Tabs.ToDictionary(tab => TabKey(tab.Id));
            return Visible(state).Where(key => tabs.ContainsKey(key)).Select(key => tabs[key]).ToList();
        }
        internal static bool Move(AppState state, string key, int insertion)
        {
            List<string> visible = Visible(state);
            int from = visible.IndexOf(key);
            if (from < 0 || insertion < 0 || insertion > visible.Count) return false;
            int destination = insertion - (from < insertion ? 1 : 0);
            if (from == destination) return false;
            visible.RemoveAt(from); visible.Insert(destination, key);
            if (state.ActiveGroupId == null) state.RootOrder = visible;
            else
            {
                Dictionary<string, SoftwareTab> members = state.Tabs.Where(tab => tab.GroupId == state.ActiveGroupId)
                    .ToDictionary(tab => TabKey(tab.Id));
                int next = 0;
                for (int i = 0; i < state.Tabs.Count; i++)
                    if (state.Tabs[i].GroupId == state.ActiveGroupId) state.Tabs[i] = members[visible[next++]];
            }
            return true;
        }
    }
}
