using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace ShortcutDock
{
    public sealed partial class MainWindow : Window
    {
        public AppState State { get; private set; }
        public Strings T { get; private set; }
        public StateStore Store { get; private set; }
        public EdgeController Dock { get; private set; }
        public Border Surface { get; private set; }
        public bool GlassAvailable;
        DesktopGlass desktopGlass;
        internal bool GlassBackgroundDark { get { return desktopGlass != null && desktopGlass.Dark; } }
        public bool HasActiveInteraction { get { return menusOpen > 0 || (search != null && search.IsKeyboardFocusWithin && inputClock.ElapsedMilliseconds - lastSearchInput < 1500); } }
        public DataGrid ShortcutGrid { get { return grid; } }
        public bool IsEditingLocked { get { return State.Pinned; } }
        internal string SearchQuery { get { return search == null ? "" : search.Text.Trim(); } }
        internal RowReorder RowOrder { get { return rowReorder; } }
        RowReorder rowReorder;
        TabGroupDrag tabDrag;
        Button tabBack;
        ScrollViewer tabScroll;
        internal TabGroupDrag GroupDrag { get { return tabDrag; } }
        internal ScrollViewer TabBarScroll { get { return tabScroll; } }
        sealed class EditingAction { public Control Control; public Func<bool> Available; }
        readonly List<EditingAction> editingActions = new List<EditingAction>();
        StackPanel titleHandle;
        readonly bool verify;
        DataGrid grid;
        ColumnSizing columnSizing;
        TextBox search;
        TextBlock empty, count, footer, hint;
        StackPanel tabs;
        Button pinButton;
        Button notesToggle;
        Forms.NotifyIcon tray;
        DispatcherTimer wallpaperTimer;
        int menusOpen, sampleGeneration;
        bool sampling, closing, wallpaperDark, nativeReady;
        string status;
        readonly Stopwatch inputClock = Stopwatch.StartNew();
        long lastSearchInput = -10000;

        public MainWindow(AppState state, StateStore store, bool verification)
        {
            State = state; Store = store; verify = verification; T = new Strings(state.Language);
            Title = "Keyside · " + T["Title"]; Width = State.PanelWidth; Height = State.PanelHeight;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
            AllowsTransparency = true;
            ShowActivated = false; Topmost = !verify; WindowStartupLocation = WindowStartupLocation.Manual;
            Background = Brushes.Transparent; FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"); FontSize = 13;
            UseLayoutRounding = true; SnapsToDevicePixels = true;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ShortcutDock.Ui.xaml"))
                Resources = (ResourceDictionary)XamlReader.Load(stream);
            FontSizing.Apply(this);
            desktopGlass = new DesktopGlass(this);
            SourceInitialized += delegate { nativeReady = true; Native.ToolWindow(this, false); ApplyTheme(); };
            SizeChanged += delegate { if (nativeReady) Native.RoundedRegion(this); };
            Loaded += delegate {
                if (Dock != null) return;
                Dock = new EdgeController(this, !verify); ApplyTheme();
                if (!verify)
                {
                    UpdateTray(); RequestWallpaper();
                    wallpaperTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
                    wallpaperTimer.Tick += delegate { RequestWallpaper(); }; wallpaperTimer.Start();
                    if (Store.Warning != null) Report(T[Store.Warning]);
                }
            };
            Closing += delegate {
                closing = true; sampleGeneration++;
                if (wallpaperTimer != null) wallpaperTimer.Stop();
                if (rowReorder != null) rowReorder.Dispose();
                if (tabDrag != null) tabDrag.Dispose();
                if (Dock != null) Dock.Dispose();
                if (columnSizing != null) columnSizing.Dispose();
                if (desktopGlass != null) desktopGlass.Dispose();
                if (tray != null) { tray.Visible = false; tray.Dispose(); }
                Save();
            };
            PreviewKeyDown += delegate(object sender, KeyEventArgs e) {
                if (e.Key == Key.Escape)
                {
                    if (rowReorder != null && rowReorder.IsInteracting) rowReorder.Cancel();
                    else if (tabDrag != null && tabDrag.IsInteracting) tabDrag.Cancel();
                    else { Keyboard.ClearFocus(); if (Dock != null && !State.Pinned) Dock.Collapse(); }
                    e.Handled = true;
                }
            };
            Rebuild();
        }
        static void BindBrush(DependencyObject control, DependencyProperty property, string key)
        {
            FrameworkElement element = control as FrameworkElement; if (element != null) element.SetResourceReference(property, key);
        }
        Button Button(string text, string tooltip, Action action)
        {
            Button button = new Button { Content = text, ToolTip = tooltip, VerticalAlignment = VerticalAlignment.Center };
            button.Click += delegate { action(); }; return button;
        }
        Button IconButton(string glyph, string tooltip, Action action)
        {
            Button button = Button(glyph, tooltip, action); button.FontFamily = new FontFamily("Segoe MDL2 Assets");
            button.Width = 32; button.Padding = new Thickness(0); FontSizing.Set(button, 15); return button;
        }
        internal void RegisterEditingControl(Control control, Func<bool> available = null)
        {
            Func<bool> allowed = available ?? delegate { return true; };
            editingActions.Add(new EditingAction { Control = control, Available = allowed });
            control.IsEnabled = !IsEditingLocked && allowed();
        }
        void UpdateEditingLock()
        {
            foreach (EditingAction action in editingActions)
            {
                action.Control.IsEnabled = !IsEditingLocked && action.Available();
                Thumb grip = action.Control as Thumb;
                if (grip != null) grip.Cursor = IsEditingLocked ? Cursors.Arrow : (Cursor)grip.Tag;
            }
            if (titleHandle != null) titleHandle.Cursor = IsEditingLocked ? Cursors.Arrow : Cursors.SizeAll;
            if (columnSizing != null) columnSizing.SetLocked(IsEditingLocked);
            if (rowReorder != null) rowReorder.SetLocked(IsEditingLocked);
            if (tabDrag != null) tabDrag.SetLocked(IsEditingLocked);
            if (hint != null) { hint.Text = T[IsEditingLocked ? "LockedHint" : "RowHint"]; hint.ToolTip = hint.Text; }
        }
        internal void TogglePanelPin()
        {
            State.Pinned = !State.Pinned; Save();
            if (State.Pinned && Dock != null) Dock.Expand();
        }
        public void Rebuild()
        {
            if (rowReorder != null) { rowReorder.Dispose(); rowReorder = null; }
            if (tabDrag != null) { tabDrag.Dispose(); tabDrag = null; }
            if (columnSizing != null) { columnSizing.Dispose(); columnSizing = null; }
            editingActions.Clear();
            T = new Strings(State.Language); Title = "Keyside · " + T["Title"];
            State.PanelHeight = Math.Max(State.PanelHeight, FontSizing.MinimumHeight(State.FontScale));
            FontSizing.Apply(this);
            Grid body = new Grid();
            double[] heights = { 84, 49, 61, -1, 56, 29, 24 };
            foreach (double height in heights)
            {
                RowDefinition row = new RowDefinition { Height = height < 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(height) };
                if (height > 0) row.SetResourceReference(RowDefinition.HeightProperty, "PanelRow" + height);
                body.RowDefinitions.Add(row);
            }
            Surface = new Border { CornerRadius = new CornerRadius(15), Padding = new Thickness(20, 13, 20, 15), BorderThickness = new Thickness(1), Child = body };
            BindBrush(Surface, Border.BackgroundProperty, "SurfaceBrush"); BindBrush(Surface, Border.BorderBrushProperty, "LineBrush");
            Grid frame = new Grid { ClipToBounds = true }; frame.Children.Add(Surface);
            desktopGlass.Attach(frame);
            new ResizeController(this, frame); Content = frame;

            Grid header = new Grid(); header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel title = new StackPanel { ToolTip = T["Drag"], Cursor = Cursors.SizeAll, Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center };
            titleHandle = title;
            TextBlock brand = new TextBlock { Text = "K E Y S I D E", FontSize = 10, FontWeight = FontWeights.SemiBold };
            FontSizing.Set(brand, 10);
            BindBrush(brand, TextBlock.ForegroundProperty, "AccentBrush"); title.Children.Add(brand);
            TextBlock titleText = new TextBlock { Name = "PanelTitle", Text = T["Title"], ToolTip = T["Title"], TextTrimming = TextTrimming.CharacterEllipsis,
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 6) }; FontSizing.Set(titleText, 23); title.Children.Add(titleText);
            StackPanel subtitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Image logo = new Image { Name = "XiaohongshuIcon", Source = BrandIcon.Source, Stretch = Stretch.Uniform, ToolTip = "小红书", Margin = new Thickness(0, 0, 6, 0) };
            logo.SetResourceReference(FrameworkElement.WidthProperty, "BrandIconSize"); logo.SetResourceReference(FrameworkElement.HeightProperty, "BrandIconSize");
            System.Windows.Automation.AutomationProperties.SetName(logo, "小红书"); subtitle.Children.Add(logo);
            TextBlock sub = new TextBlock { Text = T["Subtitle"], VerticalAlignment = VerticalAlignment.Center }; FontSizing.Set(sub, 11);
            BindBrush(sub, TextBlock.ForegroundProperty, "MutedBrush"); subtitle.Children.Add(sub); title.Children.Add(subtitle);
            title.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) {
                if (e.ClickCount != 1 || Dock == null || IsEditingLocked) return;
                Dock.Dragging = true;
                try { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); }
                finally { Dock.Dragging = false; Dock.DragFinished(); }
            };
            header.Children.Add(title);
            StackPanel controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 6, 0, 0) };
            pinButton = IconButton("\uE718", T["Pin"], TogglePanelPin); pinButton.Name = "PanelPin";
            controls.Children.Add(pinButton);
            Button settings = IconButton("\uE713", T["Settings"], delegate { Dialogs.Settings(this); }); settings.Name = "PanelSettings";
            RegisterEditingControl(settings); controls.Children.Add(settings);
            controls.Children.Add(IconButton("\uE70D", T["Collapse"], delegate { Dock.Collapse(); }));
            Grid.SetColumn(controls, 1); header.Children.Add(controls); body.Children.Add(header);

            Grid tabArea = new Grid { Margin = new Thickness(0, 8, 0, 3) };
            tabArea.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tabArea.ColumnDefinitions.Add(new ColumnDefinition()); tabArea.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tabBack = null; tabDrag = new TabGroupDrag(this);
            if (State.ActiveGroupId != null)
            {
                tabBack = Button("‹", T["BackToTabs"], LeaveGroup); tabBack.Name = "BackToTabs";
                tabBack.Width = 28; tabBack.FontSize = 20; tabBack.Padding = new Thickness(0); FontSizing.Set(tabBack, 20);
                tabArea.Children.Add(tabBack);
            }
            tabs = new StackPanel { Orientation = Orientation.Horizontal };
            tabScroll = new ScrollViewer { Content = tabs, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            tabDrag.BindBar(tabs, tabScroll);
            Grid.SetColumn(tabScroll, 1); tabArea.Children.Add(tabScroll);
            Button plus = Button("+", T[State.ActiveGroupId == null ? "AddTabOrGroup" : "AddTab"], delegate { }); plus.Width = 31; plus.Padding = new Thickness(0); plus.FontSize = 20;
            plus.Name = "AddSoftwareTab"; RegisterEditingControl(plus);
            FontSizing.Set(plus, 20);
            ContextMenu addMenu = Menu();
            MenuItem addTab = Item(T["AddTab"], AddTab), addGroup = Item(T["AddGroup"], AddGroup);
            RegisterEditingControl(addTab); RegisterEditingControl(addGroup, delegate { return State.ActiveGroupId == null; });
            addMenu.Items.Add(addTab); addMenu.Items.Add(addGroup); plus.ContextMenu = addMenu;
            plus.Click += delegate { if (IsEditingLocked) return; if (State.ActiveGroupId != null) AddTab(); else { addMenu.PlacementTarget = plus; addMenu.IsOpen = true; } };
            Grid.SetColumn(plus, 2); tabArea.Children.Add(plus); Grid.SetRow(tabArea, 1); body.Children.Add(tabArea); RenderTabs();

            Grid searchArea = new Grid { Margin = new Thickness(0, 11, 0, 12) };
            search = new TextBox { Name = "ShortcutSearch", Padding = new Thickness(32, 8, 12, 8), VerticalContentAlignment = VerticalAlignment.Center };
            BindBrush(search, Control.BackgroundProperty, "PanelControlBrush");
            searchArea.Children.Add(search);
            TextBlock magnifier = new TextBlock { Text = "\uE721", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 13, Margin = new Thickness(11, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            FontSizing.Set(magnifier, 13);
            BindBrush(magnifier, TextBlock.ForegroundProperty, "MutedBrush"); searchArea.Children.Add(magnifier);
            TextBlock placeholder = new TextBlock { Text = T["Search"], Margin = new Thickness(33, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false, FontSize = 12 };
            FontSizing.Set(placeholder, 12);
            BindBrush(placeholder, TextBlock.ForegroundProperty, "MutedBrush"); searchArea.Children.Add(placeholder);
            search.TextChanged += delegate { if (rowReorder != null) rowReorder.Cancel(); placeholder.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; RenderRows(); };
            search.PreviewKeyDown += delegate { lastSearchInput = inputClock.ElapsedMilliseconds; };
            search.PreviewMouseLeftButtonDown += delegate { Activate(); };
            Grid.SetRow(searchArea, 2); body.Children.Add(searchArea);

            Grid listArea = new Grid();
            grid = new DataGrid { Name = "ShortcutTable", EnableColumnVirtualization = false };
            grid.SetResourceReference(DataGrid.RowHeightProperty, "TableRowHeight");
            grid.Columns.Add(Column(T["Keys"], "Keys", true)); grid.Columns.Add(Column(T["Description"], "Description", false));
            grid.Columns.Add(Column(T["Notes"], "Notes", false));
            columnSizing = new ColumnSizing(this, grid);
            rowReorder = new RowReorder(this, grid);
            grid.MouseDoubleClick += delegate(object sender, MouseButtonEventArgs e) {
                e.Handled = EditCell(e.OriginalSource as DependencyObject);
            };
            grid.PreviewMouseRightButtonDown += delegate(object sender, MouseButtonEventArgs e) {
                if (rowReorder != null) rowReorder.Cancel();
                DataGridRow row = FindRow(e.OriginalSource as DependencyObject);
                if (row == null) grid.SelectedItems.Clear();
                else if (!row.IsSelected) { grid.SelectedItems.Clear(); grid.SelectedItem = row.Item; }
            };
            ContextMenu rowsMenu = Menu();
            MenuItem edit = Item(T["EditShortcut"], EditRow), delete = Item(T["DeleteShortcut"], DeleteRow);
            MenuItem pin = Item(T["PinRow"], ToggleRowPin), notes = Item(T["EditNotes"], EditNotes);
            rowsMenu.Items.Add(pin); rowsMenu.Items.Add(notes); rowsMenu.Items.Add(new Separator()); rowsMenu.Items.Add(edit); rowsMenu.Items.Add(delete);
            foreach (MenuItem item in new[] { pin, notes, edit, delete }) RegisterEditingControl(item, delegate { return grid.SelectedItems.Count == 1 && grid.SelectedItem is ShortcutEntry; });
            rowsMenu.Opened += delegate {
                ShortcutEntry row = grid.SelectedItem as ShortcutEntry;
                pin.IsEnabled = notes.IsEnabled = edit.IsEnabled = delete.IsEnabled = row != null && grid.SelectedItems.Count == 1 && !IsEditingLocked;
                pin.Header = T[row != null && row.Pinned ? "UnpinRow" : "PinRow"];
            };
            grid.ContextMenu = rowsMenu; listArea.Children.Add(grid);
            grid.SelectionChanged += delegate { UpdateEditingLock(); };
            empty = new TextBlock { Text = T["NoRows"], HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(18) };
            BindBrush(empty, TextBlock.ForegroundProperty, "MutedBrush"); listArea.Children.Add(empty);
            Grid.SetRow(listArea, 3); body.Children.Add(listArea);

            Grid toolbar = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            toolbar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); toolbar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition()); toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Button add = Button("+  " + T["AddShortcut"], T["AddShortcut"], AddRow); add.HorizontalAlignment = HorizontalAlignment.Left;
            add.Name = "AddShortcut"; RegisterEditingControl(add, delegate { return ActiveTab() != null; });
            BindBrush(add, Control.BackgroundProperty, "AccentFillBrush"); BindBrush(add, Control.ForegroundProperty, "AccentBrush"); toolbar.Children.Add(add);
            notesToggle = Button("", "", ToggleNotes); notesToggle.Name = "ToggleNotes"; FontSizing.Set(notesToggle, 11);
            Grid.SetColumn(notesToggle, 1); toolbar.Children.Add(notesToggle);
            count = new TextBlock { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 };
            FontSizing.Set(count, 11); Grid.SetRow(count, 1); Grid.SetColumnSpan(count, 2);
            BindBrush(count, TextBlock.ForegroundProperty, "MutedBrush"); toolbar.Children.Add(count);
            Grid.SetRow(toolbar, 4); body.Children.Add(toolbar);
            footer = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis };
            footer.Name = "PanelFooter";
            FontSizing.Set(footer, 10);
            BindBrush(footer, TextBlock.ForegroundProperty, "MutedBrush"); Grid.SetRow(footer, 5); body.Children.Add(footer);
            Grid bottom = new Grid();
            bottom.ColumnDefinitions.Add(new ColumnDefinition()); bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hint = new TextBlock { Text = T["RowHint"], FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
            FontSizing.Set(hint, 10); hint.TextTrimming = TextTrimming.CharacterEllipsis; hint.ToolTip = T["RowHint"];
            BindBrush(hint, TextBlock.ForegroundProperty, "MutedBrush"); bottom.Children.Add(hint);
            Button more = Button("···", T["Help"], delegate { }); more.FontSize = 18; more.Width = 30; more.Padding = new Thickness(0); more.MinHeight = 20; more.HorizontalAlignment = HorizontalAlignment.Right;
            more.Name = "MoreOptions";
            FontSizing.Set(more, 18); Grid.SetColumn(more, 1);
            ContextMenu options = Menu(); MenuItem txtImport = Item(T["ImportTxt"], ImportTextFile), jsonImport = Item(T["Import"], Import);
            RegisterEditingControl(txtImport); RegisterEditingControl(jsonImport);
            options.Items.Add(txtImport); options.Items.Add(Item(T["DownloadTemplate"], DownloadTemplate)); options.Items.Add(new Separator()); options.Items.Add(jsonImport); options.Items.Add(Item(T["Export"], Export)); options.Items.Add(new Separator()); options.Items.Add(Item(T["Exit"], Close));
            more.ContextMenu = options;
            more.Click += delegate { options.PlacementTarget = more; options.IsOpen = true; }; bottom.Children.Add(more);
            Grid.SetRow(bottom, 6); body.Children.Add(bottom);
            ApplyNotesVisibility(); RenderRows(); RefreshFooter(); ApplyTheme();
        }
        DataGridTemplateColumn Column(string title, string binding, bool keys)
        {
            FrameworkElementFactory text = new FrameworkElementFactory(keys ? typeof(TextBlock) : typeof(EmojiText));
            text.SetBinding(keys ? TextBlock.TextProperty : EmojiText.ValueProperty, new Binding(keys ? "DisplayKeys" : binding));
            if (keys) text.SetBinding(FrameworkElement.ToolTipProperty, new Binding(binding));
            text.SetResourceReference(TextBlock.ForegroundProperty, keys ? "AccentBrush" : "TextBrush");
            text.SetResourceReference(TextBlock.FontSizeProperty, FontSizing.Key(keys ? 11 : 12));
            text.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            text.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 6, 0));
            if (keys) text.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Cascadia Mono, Consolas"));
            return new DataGridTemplateColumn { Header = title, SortMemberPath = binding, CanUserSort = false, CanUserReorder = false,
                Width = new DataGridLength(binding == "Notes" ? 0.85 : 1.15, DataGridLengthUnitType.Star), CellTemplate = new DataTemplate { VisualTree = text } };
        }
        static DataGridCell FindCell(DependencyObject item)
        {
            while (item != null) { DataGridCell cell = item as DataGridCell; if (cell != null) return cell; item = ParentOf(item); }
            return null;
        }
        static DependencyObject ParentOf(DependencyObject item)
        {
            FrameworkContentElement content = item as FrameworkContentElement;
            return content != null ? content.Parent : VisualTreeHelper.GetParent(item);
        }
        internal bool EditCell(DependencyObject source)
        {
            if (IsEditingLocked || grid.SelectedItems.Count > 1) return false;
            DataGridCell cell = FindCell(source); DataGridRow row = cell == null ? null : FindRow(cell);
            if (row == null || !(row.Item is ShortcutEntry)) return false;
            grid.SelectedItem = row.Item;
            if (cell.Column.SortMemberPath == "Notes") EditNotes(); else EditRow();
            return true;
        }
        static DataGridRow FindRow(DependencyObject item)
        {
            while (item != null)
            {
                if (item is DataGridRow) return (DataGridRow)item;
                if (!(item is Visual) && !(item is System.Windows.Media.Media3D.Visual3D)) return null;
                item = ParentOf(item);
            }
            return null;
        }
        ContextMenu Menu()
        {
            ContextMenu menu = new ContextMenu(); menu.Resources.MergedDictionaries.Add(Resources);
            menu.SetResourceReference(Control.FontSizeProperty, FontSizing.Key(13));
            menu.Opened += delegate { menusOpen++; }; menu.Closed += delegate { menusOpen = Math.Max(0, menusOpen - 1); }; return menu;
        }
        static MenuItem Item(string title, Action action) { MenuItem item = new MenuItem { Header = title }; item.Click += delegate { action(); }; return item; }
        public SoftwareTab ActiveTab() { return State.Tabs.FirstOrDefault(t => t.Id == State.ActiveTabId); }
        public void RenderRows()
        {
            if (grid == null || empty == null) return;
            HashSet<string> selected = new HashSet<string>(grid.SelectedItems.OfType<ShortcutEntry>().Select(row => row.Id));
            SoftwareTab tab = ActiveTab(); string query = SearchQuery;
            List<ShortcutEntry> rows = tab == null ? new List<ShortcutEntry>() : tab.Shortcuts.Where(s => s.Keys.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || s.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || (s.Notes ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).OrderByDescending(s => s.Pinned).ToList();
            grid.ItemsSource = rows;
            empty.Text = T[tab != null ? "NoRows" : State.ActiveGroupId != null ? "EmptyGroup" : State.Groups.Count > 0 ? "RootEmpty" : "NoTabs"];
            foreach (ShortcutEntry row in rows) if (selected.Contains(row.Id)) grid.SelectedItems.Add(row);
            empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (count != null) count.Text = String.Format(T["Count"], rows.Count);
        }
        internal bool ReorderRows(IList<string> ids, int insertion)
        {
            if (IsEditingLocked || !RowOrdering.Move(ActiveTab(), grid.Items.OfType<ShortcutEntry>().ToList(), ids, insertion)) return false;
            RenderRows(); Save(); return true;
        }
        internal void AddTab()
        {
            if (IsEditingLocked) return;
            if (State.Tabs.Count >= 200) { Report(T["LimitTabs"]); return; }
            string name = Dialogs.TabName(this, null); if (name == null) return;
            SoftwareTab tab = new SoftwareTab { Name = name, GroupId = State.ActiveGroupId }; State.Tabs.Add(tab); State.ActiveTabId = tab.Id; Rebuild(); Save();
        }
        internal void AddRow()
        {
            if (IsEditingLocked) return;
            SoftwareTab tab = ActiveTab(); if (tab == null) return;
            ShortcutEntry row = Dialogs.Shortcut(this, null); if (row == null) return;
            tab.Shortcuts.Add(row); RenderRows(); Save();
        }
        internal void EditRow()
        {
            if (IsEditingLocked || grid.SelectedItems.Count != 1) return;
            ShortcutEntry old = grid.SelectedItem as ShortcutEntry; if (old == null) return;
            ShortcutEntry row = Dialogs.Shortcut(this, old); if (row == null) return;
            old.Keys = row.Keys; old.Description = row.Description; RenderRows(); Save();
        }
        internal void EditNotes()
        {
            if (IsEditingLocked || grid.SelectedItems.Count != 1) return;
            ShortcutEntry row = grid.SelectedItem as ShortcutEntry; if (row == null) return;
            string notes = Dialogs.Notes(this, row.Notes); if (notes == null) return;
            row.Notes = notes; RenderRows(); grid.SelectedItem = row; Save();
        }
        internal void ToggleRowPin()
        {
            if (IsEditingLocked || grid.SelectedItems.Count != 1) return;
            ShortcutEntry row = grid.SelectedItem as ShortcutEntry; if (row == null) return;
            row.Pinned = !row.Pinned; RenderRows(); grid.SelectedItem = row; Save();
        }
        internal void DeleteRow()
        {
            if (IsEditingLocked || grid.SelectedItems.Count != 1) return;
            ShortcutEntry row = grid.SelectedItem as ShortcutEntry;
            if (row == null || !Dialogs.Confirm(this, String.Format(T["DeleteQuestion"], row.Keys))) return;
            ActiveTab().Shortcuts.Remove(row); RenderRows(); Save();
        }
        internal void RenameTab(SoftwareTab tab)
        {
            if (IsEditingLocked) return;
            string name = Dialogs.TabName(this, tab.Name);
            if (name != null) { tab.Name = name; RenderTabs(); Save(); }
        }
        internal void DeleteTab(SoftwareTab tab)
        {
            if (IsEditingLocked || !Dialogs.Confirm(this, String.Format(T["DeleteQuestion"], tab.Name))) return;
            State.Tabs.Remove(tab); State.Validate(); Rebuild(); Save();
        }
        public void Save()
        {
            try { Store.Save(State); status = null; RefreshFooter(); }
            catch (Exception e) { Report(T["Error"] + ": " + e.Message); }
        }
        public void Report(string message)
        {
            status = message; RefreshFooter();
            try { File.AppendAllText(Path.Combine(Store.DirectoryPath, "diagnostics.log"), DateTime.Now.ToString("s") + " " + message + Environment.NewLine); } catch (Exception) { }
        }
        public void RefreshFooter()
        {
            if (footer == null) return;
            UpdateEditingLock();
            footer.Text = State.Pinned ? "● " + T["Pinned"] : status ?? (State.Edge == "Floating" ? T["Drag"] : String.Format(T["Docked"], T[State.Edge]));
            if (pinButton != null)
            {
                pinButton.SetResourceReference(Control.BackgroundProperty, State.Pinned ? "AccentFillBrush" : "TransparentBrush");
                pinButton.SetResourceReference(Control.ForegroundProperty, State.Pinned ? "AccentBrush" : "TextBrush");
            }
            if (status == null && State.Theme == "Glass" && !GlassAvailable) footer.Text += " · " + T["GlassFallback"];
            footer.ToolTip = State.Pinned && status != null ? footer.Text + "\n" + status : footer.Text;
        }
        public void ApplyTheme() { if (nativeReady) ThemeService.Apply(this, wallpaperDark); }
        internal bool EnableGlassLayer(bool enabled) { return desktopGlass.Enable(enabled); }
        internal void RefreshGlassLayer()
        {
            bool dark = desktopGlass.Dark, available = desktopGlass.Available;
            desktopGlass.Refresh(true);
            if (nativeReady && State.Theme == "Glass" && (dark != desktopGlass.Dark || available != desktopGlass.Available)) ApplyTheme();
        }
        public void PreviewFontScale(double scale)
        {
            if (IsEditingLocked) return;
            State.FontScale = Math.Max(0.8, Math.Min(1.5, scale)); FontSizing.Apply(this);
            if (columnSizing != null) columnSizing.Restore();
            double minimum = FontSizing.MinimumHeight(State.FontScale);
            if (State.PanelHeight < minimum)
            {
                State.PanelHeight = minimum;
                if (Dock != null) Dock.Place(); else Height = minimum;
            }
        }
        public void PreviewNotesVisibility(bool visible)
        {
            State.NotesCollapsed = !visible; ApplyNotesVisibility();
        }
        void ApplyNotesVisibility()
        {
            if (grid == null) return;
            foreach (DataGridColumn column in grid.Columns)
                if (column.SortMemberPath == "Notes") column.Visibility = State.NotesCollapsed ? Visibility.Collapsed : Visibility.Visible;
            if (columnSizing != null) columnSizing.Restore();
            if (notesToggle != null)
            {
                notesToggle.Content = T[State.NotesCollapsed ? "ShowNotes" : "HideNotes"];
                notesToggle.ToolTip = notesToggle.Content;
            }
        }
        internal void ToggleNotes() { PreviewNotesVisibility(State.NotesCollapsed); Save(); }
        public void PreviewOpacity(double opacity)
        {
            if (IsEditingLocked) return;
            State.PanelOpacity = Math.Max(0.35, Math.Min(1, opacity));
            if (nativeReady) ApplyTheme();
        }
        public void ReleaseSearchFocus() { if (search != null && search.IsKeyboardFocusWithin) Keyboard.ClearFocus(); }
        public async void RequestWallpaper()
        {
            if (sampling || closing || Dock == null) return;
            if (State.Theme != "Auto") { ApplyTheme(); return; }
            sampling = true; int generation = ++sampleGeneration; string monitor = State.Monitor;
            try
            {
                bool dark = await ThemeService.SampleAsync(Dock.Screen, wallpaperDark);
                if (!closing && generation == sampleGeneration && State.Theme == "Auto" && monitor == State.Monitor) { wallpaperDark = dark; ApplyTheme(); }
            }
            catch (Exception e) { if (!closing) Report(e.Message); }
            finally { sampling = false; }
        }
        public void UpdateTray()
        {
            if (verify) return;
            if (tray == null)
            {
                tray = new Forms.NotifyIcon { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location), Visible = true };
                tray.DoubleClick += delegate { Dispatcher.BeginInvoke(new Action(delegate { Dock.Expand(); })); };
            }
            tray.Text = "Keyside · " + T["Title"];
            Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
            menu.Items.Add(T["Show"], null, delegate { Dispatcher.BeginInvoke(new Action(Dock.Expand)); });
            Forms.ToolStripItem traySettings = menu.Items.Add(T["Settings"], null, delegate { Dispatcher.BeginInvoke(new Action(delegate { if (!IsEditingLocked) { Dock.Expand(); Dialogs.Settings(this); } })); });
            traySettings.Enabled = !IsEditingLocked; menu.Opening += delegate { traySettings.Enabled = !IsEditingLocked; };
            menu.Items.Add(T["Collapse"], null, delegate { Dispatcher.BeginInvoke(new Action(Dock.Collapse)); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(T["Exit"], null, delegate { Dispatcher.BeginInvoke(new Action(Close)); });
            if (tray.ContextMenuStrip != null) tray.ContextMenuStrip.Dispose(); tray.ContextMenuStrip = menu;
        }
        void Export()
        {
            Dock.InteractionDepth++;
            try
            {
                SaveFileDialog dialog = new SaveFileDialog { Filter = "Keyside JSON (*.json)|*.json", FileName = "Keyside-backup.json", AddExtension = true, DefaultExt = ".json" };
                if (dialog.ShowDialog(this) == true) { StateStore.Write(State, dialog.FileName); Report(T["Exported"]); }
            } catch (Exception e) { Report(T["Error"] + ": " + e.Message); }
            finally { Dock.InteractionDepth--; }
        }
        void Import()
        {
            if (IsEditingLocked) return;
            Dock.InteractionDepth++;
            try
            {
                OpenFileDialog dialog = new OpenFileDialog { Filter = "Keyside JSON (*.json)|*.json", CheckFileExists = true };
                if (dialog.ShowDialog(this) != true) return;
                AppState imported = StateStore.Read(dialog.FileName);
                if (!Dialogs.Confirm(this, T["ImportQuestion"])) return;
                Store.Save(imported); State = imported; Rebuild(); Dock.Place(); Dock.Expand(); UpdateTray(); RequestWallpaper();
            } catch (Exception e) { Report(T["Error"] + ": " + e.Message); }
            finally { Dock.InteractionDepth--; }
        }
        void ImportTextFile()
        {
            if (IsEditingLocked) return;
            Dock.InteractionDepth++;
            try
            {
                OpenFileDialog dialog = new OpenFileDialog { Filter = "Shortcut TXT (*.txt)|*.txt", CheckFileExists = true };
                if (dialog.ShowDialog(this) == true) ImportTextPath(dialog.FileName);
            }
            catch (Exception e) { Report(T["Error"] + ": " + e.Message); }
            finally { Dock.InteractionDepth--; }
        }
        internal void ImportTextPath(string path)
        {
            if (IsEditingLocked) return;
            List<ShortcutEntry> rows;
            try { rows = TextImport.Read(path); }
            catch (TxtFormatException e) { Report(String.Format(T["InvalidTxt"], e.LineNumber)); return; }
            if (rows.Count == 0) { Report(T["EmptyTxt"]); return; }
            TxtImportChoice choice = Dialogs.ImportTxt(this, Path.GetFileNameWithoutExtension(path), rows);
            if (choice != null) CommitTextImport(rows, choice);
        }
        internal void CommitTextImport(List<ShortcutEntry> rows, TxtImportChoice choice)
        {
            if (IsEditingLocked) return;
            SoftwareTab tab = State.Tabs.FirstOrDefault(t => t.Id == choice.TabId); bool creating = tab == null;
            if (creating)
            {
                if (State.Tabs.Count >= 200) { Report(T["LimitTabs"]); return; }
                tab = new SoftwareTab { Name = choice.Name, GroupId = State.ActiveGroupId };
            }
            int skipped, added;
            try { added = TextImport.Append(tab, rows, out skipped); }
            catch (InvalidDataException) { Report(T["ImportLimit"]); return; }
            if (creating) State.Tabs.Add(tab);
            State.ActiveGroupId = tab.GroupId; State.ActiveTabId = tab.Id; Rebuild(); Save(); Report(String.Format(T["ImportedTxt"], added, skipped));
        }
        void DownloadTemplate()
        {
            Dock.InteractionDepth++;
            try
            {
                SaveFileDialog dialog = new SaveFileDialog { Filter = "Shortcut TXT (*.txt)|*.txt", FileName = "Template.txt", AddExtension = true, DefaultExt = ".txt", OverwritePrompt = true };
                if (dialog.ShowDialog(this) == true) SaveTemplateTo(dialog.FileName);
            }
            catch (Exception e) { Report(T["Error"] + ": " + e.Message); }
            finally { Dock.InteractionDepth--; }
        }
        internal void SaveTemplateTo(string path) { TextImport.SaveTemplate(path); Report(T["TemplateSaved"]); }
    }
}
