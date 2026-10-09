using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ShortcutDock
{
    public sealed partial class MainWindow
    {
        internal void SelectTab(SoftwareTab tab)
        {
            if (tab == null || !State.Tabs.Contains(tab) || tab.GroupId != State.ActiveGroupId) return;
            State.ActiveTabId = tab.Id; Rebuild(); Save();
        }
        void RegisterTabAction(MenuItem item, ContextMenu menu)
        {
            item.Tag = "TabManagement"; RegisterEditingControl(item); menu.Items.Add(item);
        }
        ContextMenu GroupMenu(TabGroup group)
        {
            ContextMenu menu = Menu();
            RegisterTabAction(Item(T["RenameGroup"], delegate { RenameGroup(group); }), menu);
            RegisterTabAction(Item(T["DissolveGroup"], delegate { DissolveGroup(group); }), menu);
            return menu;
        }
        void RenderTabs()
        {
            double scrollOffset = tabScroll.HorizontalOffset;
            tabDrag.Reset();
            editingActions.RemoveAll(a => (a.Control.Tag as string) == "TabManagement");
            tabs.Children.Clear();
            foreach (string key in TabOrdering.Visible(State))
            {
                if (key.StartsWith("g:", StringComparison.Ordinal))
                {
                    TabGroup group = State.Groups.First(item => TabOrdering.GroupKey(item.Id) == key);
                    TabGroup captured = group;
                    Button button = Button("", T["GroupHint"], delegate { });
                    button.Name = "TabGroup"; button.Tag = group.Id; button.MaxWidth = 160;
                    button.Margin = new Thickness(0, 0, 5, 0);
                    button.Content = new TextBlock { Text = "▸ " + group.Name + " · " + State.Tabs.Count(member => member.GroupId == group.Id),
                        TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 134 };
                    BindBrush(button, Control.BorderBrushProperty, "LineBrush");
                    button.MouseDoubleClick += delegate(object sender, MouseButtonEventArgs e) {
                        if (e.ChangedButton == MouseButton.Left) { e.Handled = true; EnterGroup(captured.Id); }
                    };
                    button.ContextMenu = GroupMenu(group); tabDrag.GroupSource(button, group.Id); tabDrag.Target(button, group.Id); tabs.Children.Add(button);
                    continue;
                }
                SoftwareTab tab = State.Tabs.First(item => TabOrdering.TabKey(item.Id) == key);
                AddTabButton(tab);
            }
            if (State.ActiveGroupId != null)
            {
                TabGroup group = State.Groups.First(item => item.Id == State.ActiveGroupId);
                Button breadcrumb = Button(group.Name + " /", group.Name, delegate { }); breadcrumb.Name = "CurrentTabGroup";
                breadcrumb.MaxWidth = 110; breadcrumb.Content = new TextBlock { Text = group.Name + " /", MaxWidth = 85, TextTrimming = TextTrimming.CharacterEllipsis };
                BindBrush(breadcrumb, Control.ForegroundProperty, "MutedBrush");
                breadcrumb.ContextMenu = GroupMenu(group); tabs.Children.Insert(0, breadcrumb);
            }
            if (tabBack != null) tabDrag.Target(tabBack, null);
            tabScroll.ScrollToHorizontalOffset(scrollOffset);
        }
        void AddTabButton(SoftwareTab tab)
        {
            SoftwareTab captured = tab;
            Button button = Button(tab.Name, tab.Name + "\n" + T["TabOrderHint"], delegate { SelectTab(captured); });
            button.Tag = tab.Id; button.Name = "SoftwareTab";
            button.MaxWidth = 160; button.Margin = new Thickness(0, 0, 4, 0);
            button.Content = new TextBlock { Text = tab.Name, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 130 };
            if (tab.Id == State.ActiveTabId) { BindBrush(button, Control.BackgroundProperty, "AccentFillBrush"); BindBrush(button, Control.ForegroundProperty, "AccentBrush"); }
            ContextMenu menu = Menu();
            RegisterTabAction(Item(T["Rename"], delegate { RenameTab(captured); }), menu);
            if (tab.GroupId != null)
                RegisterTabAction(Item(T["RemoveFromGroup"], delegate { MoveTabToGroup(captured.Id, null); }), menu);
            RegisterTabAction(Item(T["DeleteTab"], delegate { DeleteTab(captured); }), menu);
            button.ContextMenu = menu; tabDrag.Source(button, tab.Id); tabs.Children.Add(button);
        }
        internal bool ReorderTabBar(string key, int insertion)
        {
            if (IsEditingLocked || !TabOrdering.Move(State, key, insertion)) return false;
            RenderTabs(); Save(); return true;
        }
        internal void AddGroup()
        {
            if (IsEditingLocked || State.ActiveGroupId != null) return;
            if (State.Groups.Count >= 200) { Report(T["LimitGroups"]); return; }
            string name = Dialogs.GroupName(this, null); if (name == null) return;
            State.Groups.Add(new TabGroup { Name = name }); RenderTabs(); Save();
        }
        internal void RenameGroup(TabGroup group)
        {
            if (IsEditingLocked || group == null || !State.Groups.Contains(group)) return;
            string name = Dialogs.GroupName(this, group.Name);
            if (name != null) { group.Name = name; RenderTabs(); Save(); }
        }
        internal void EnterGroup(string id)
        {
            if (!State.Groups.Any(group => group.Id == id)) return;
            State.ActiveGroupId = id; State.Validate(); Rebuild(); Save();
        }
        internal void LeaveGroup()
        {
            State.ActiveGroupId = null; State.Validate(); Rebuild(); Save();
        }
        internal bool MoveTabToGroup(string tabId, string groupId)
        {
            if (IsEditingLocked || (groupId != null && !State.Groups.Any(group => group.Id == groupId))) return false;
            SoftwareTab tab = State.Tabs.FirstOrDefault(item => item.Id == tabId);
            if (tab == null || tab.GroupId == groupId) return false;
            tab.GroupId = groupId; State.Validate(); Rebuild(); Save(); return true;
        }
        internal void DissolveGroup(TabGroup group)
        {
            if (IsEditingLocked || group == null || !State.Groups.Contains(group) ||
                !Dialogs.Confirm(this, String.Format(T["DissolveQuestion"], group.Name))) return;
            Ungroup(group);
        }
        internal bool Ungroup(TabGroup group)
        {
            if (IsEditingLocked || group == null || !State.Groups.Contains(group)) return false;
            TabOrdering.Normalize(State);
            int position = State.RootOrder.IndexOf(TabOrdering.GroupKey(group.Id));
            if (position >= 0)
            {
                State.RootOrder.RemoveAt(position);
                State.RootOrder.InsertRange(position, State.Tabs.Where(tab => tab.GroupId == group.Id).Select(tab => TabOrdering.TabKey(tab.Id)));
            }
            foreach (SoftwareTab tab in State.Tabs.Where(item => item.GroupId == group.Id)) tab.GroupId = null;
            if (State.ActiveGroupId == group.Id) State.ActiveGroupId = null;
            State.Groups.Remove(group); State.Validate(); Rebuild(); Save(); return true;
        }
    }
}
