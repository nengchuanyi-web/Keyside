using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ShortcutDock
{
    public static partial class Dialogs
    {
        static Window Create(MainWindow owner, string title, double width)
        {
            Window dialog = new Window { Owner = owner, Title = title, Width = width, SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false, Topmost = owner.Topmost, WindowStyle = WindowStyle.None,
                FontFamily = owner.FontFamily, FontSize = 13 };
            dialog.Resources.MergedDictionaries.Add(owner.Resources);
            dialog.SetResourceReference(Control.FontSizeProperty, FontSizing.Key(13));
            dialog.MaxHeight = Math.Max(300, SystemParameters.WorkArea.Height - 40);
            dialog.SetResourceReference(Control.ForegroundProperty, "TextBrush");
            dialog.SetResourceReference(Control.BackgroundProperty, "DialogSurfaceBrush");
            dialog.SourceInitialized += delegate { Native.Backdrop(dialog, false, owner.State.Theme == "Dark"); };
            return dialog;
        }
        static StackPanel Body(Window dialog, string title)
        {
            StackPanel stack = new StackPanel { Margin = new Thickness(24) };
            TextBlock header = new TextBlock { Text = title, FontSize = 21, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 18) };
            header.MouseLeftButtonDown += delegate { if (Mouse.LeftButton == MouseButtonState.Pressed) dialog.DragMove(); };
            stack.Children.Add(header); dialog.Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; return stack;
        }
        static TextBox Field(StackPanel body, string label, string value, int max)
        {
            body.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 7) });
            TextBox box = new TextBox { Text = value ?? "", MaxLength = max }; body.Children.Add(box); return box;
        }
        static void Actions(MainWindow owner, Window dialog, StackPanel body, Action save)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
            Button cancel = new Button { Content = owner.T["Cancel"], IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
            cancel.Click += delegate { dialog.DialogResult = false; };
            Button ok = new Button { Content = owner.T["Save"], IsDefault = true, MinWidth = 76 };
            ok.SetResourceReference(Control.BackgroundProperty, "AccentFillBrush");
            ok.SetResourceReference(Control.ForegroundProperty, "AccentBrush");
            ok.Click += delegate { save(); };
            row.Children.Add(cancel); row.Children.Add(ok); body.Children.Add(row);
        }
        static bool Show(MainWindow owner, Window dialog)
        {
            FontSizing.Adapt(dialog);
            if (owner.Dock != null) owner.Dock.InteractionDepth++;
            try { return dialog.ShowDialog() == true; }
            finally { if (owner.Dock != null) owner.Dock.InteractionDepth--; }
        }
        public static string TabName(MainWindow owner, string current)
        {
            if (owner.IsEditingLocked) return null;
            string title = owner.T[current == null ? "AddTab" : "Rename"];
            Window dialog = Create(owner, title, 380); StackPanel body = Body(dialog, title);
            TextBox name = Field(body, owner.T["Name"], current, 60);
            TextBlock error = new TextBlock { Margin = new Thickness(0, 8, 0, 0), Foreground = Brushes.IndianRed };
            body.Children.Add(error);
            Actions(owner, dialog, body, delegate {
                if (String.IsNullOrWhiteSpace(name.Text)) { error.Text = owner.T["Invalid"]; return; }
                dialog.DialogResult = true;
            });
            dialog.Loaded += delegate { name.Focus(); name.SelectAll(); };
            return Show(owner, dialog) ? name.Text.Trim() : null;
        }
        public static ShortcutEntry Shortcut(MainWindow owner, ShortcutEntry current)
        {
            if (owner.IsEditingLocked) return null;
            string title = owner.T[current == null ? "AddShortcut" : "EditShortcut"];
            Window dialog = Create(owner, title, 420); StackPanel body = Body(dialog, title);
            TextBox keys = Field(body, owner.T["Keys"], current == null ? "" : current.Keys, 160);
            Button capture = new Button { Content = owner.T["Capture"], HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 5, 0, 0) };
            capture.SetResourceReference(Control.ForegroundProperty, "AccentBrush");
            bool recording = false;
            capture.Click += delegate { recording = true; keys.Focus(); keys.SelectAll(); };
            keys.PreviewKeyDown += delegate(object sender, KeyEventArgs e) {
                if (!recording) return;
                Key key = e.Key == Key.System ? e.SystemKey : e.Key;
                e.Handled = true;
                if (key == Key.Escape) { recording = false; return; }
                if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt || key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin) return;
                string combo = ""; ModifierKeys mods = Keyboard.Modifiers;
                if ((mods & ModifierKeys.Control) != 0) combo += "Ctrl + ";
                if ((mods & ModifierKeys.Alt) != 0) combo += "Alt + ";
                if ((mods & ModifierKeys.Shift) != 0) combo += "Shift + ";
                if ((mods & ModifierKeys.Windows) != 0) combo += "Win + ";
                string keyName = new KeyConverter().ConvertToString(key);
                keys.Text = combo + keyName; recording = false;
            };
            body.Children.Add(capture);
            TextBox description = Field(body, owner.T["Description"], current == null ? "" : current.Description, 1000);
            description.AcceptsReturn = true; description.TextWrapping = TextWrapping.Wrap; description.Height = 88;
            TextBlock error = new TextBlock { Margin = new Thickness(0, 8, 0, 0), Foreground = Brushes.IndianRed }; body.Children.Add(error);
            Actions(owner, dialog, body, delegate {
                if (String.IsNullOrWhiteSpace(keys.Text) || String.IsNullOrWhiteSpace(description.Text)) { error.Text = owner.T["Invalid"]; return; }
                dialog.DialogResult = true;
            });
            dialog.Loaded += delegate { keys.Focus(); };
            return Show(owner, dialog) ? new ShortcutEntry(keys.Text.Trim(), description.Text.Trim()) {
                Id = current == null ? Guid.NewGuid().ToString("N") : current.Id,
                Notes = current == null ? "" : current.Notes, Pinned = current != null && current.Pinned } : null;
        }
        static ComboBox Options(MainWindow owner, StackPanel body, string label, string[] ids, string selected)
        {
            body.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, FontSize = 11, Margin = new Thickness(0, 15, 0, 8) });
            ComboBox box = new ComboBox { MinHeight = 34, IsEditable = false };
            foreach (string id in ids)
            {
                ComboBoxItem item = new ComboBoxItem { Content = owner.T[id], Tag = id };
                box.Items.Add(item); if (id == selected) box.SelectedItem = item;
            }
            body.Children.Add(box); return box;
        }
        public static void Settings(MainWindow owner)
        {
            if (owner.IsEditingLocked) return;
            Window dialog = Create(owner, owner.T["Settings"], 410); StackPanel body = Body(dialog, owner.T["Settings"]);
            ComboBox theme = Options(owner, body, owner.T["Appearance"], new[] { "Glass", "Light", "Dark", "Auto" }, owner.State.Theme);
            body.Children.Add(new TextBlock { Text = owner.T["AutoHint"], FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
            string previousColor = owner.State.PanelColor;
            double previousHeight = owner.State.PanelHeight;
            PaletteChoices(owner, body);
            double previousFontScale = owner.State.FontScale; bool previousNotesCollapsed = owner.State.NotesCollapsed;
            Grid fontHeading = new Grid { Margin = new Thickness(0, 16, 0, 6) };
            fontHeading.Children.Add(new TextBlock { Text = owner.T["FontScale"], FontWeight = FontWeights.SemiBold, FontSize = 11 });
            TextBlock fontPercentage = new TextBlock { HorizontalAlignment = HorizontalAlignment.Right, FontSize = 11 };
            fontPercentage.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush"); fontHeading.Children.Add(fontPercentage); body.Children.Add(fontHeading);
            Slider fontScale = new Slider { Name = "PanelFontScale", Minimum = 80, Maximum = 150, Value = Math.Round(previousFontScale * 100), TickFrequency = 5,
                IsSnapToTickEnabled = true, SmallChange = 5, LargeChange = 10, Margin = new Thickness(0, 6, 0, 2) };
            fontPercentage.Text = fontScale.Value.ToString("0") + "%";
            fontScale.ValueChanged += delegate { fontPercentage.Text = fontScale.Value.ToString("0") + "%"; owner.PreviewFontScale(fontScale.Value / 100); };
            body.Children.Add(fontScale);
            body.Children.Add(new TextBlock { Text = owner.T["FontHint"], FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
            CheckBox notes = new CheckBox { Name = "ShowNotesColumn", Content = owner.T["NotesVisible"], IsChecked = !owner.State.NotesCollapsed, Margin = new Thickness(0, 14, 0, 0) };
            notes.Checked += delegate { owner.PreviewNotesVisibility(true); }; notes.Unchecked += delegate { owner.PreviewNotesVisibility(false); }; body.Children.Add(notes);
            double previousOpacity = owner.State.PanelOpacity;
            Grid transparencyHeading = new Grid { Margin = new Thickness(0, 16, 0, 6) };
            transparencyHeading.Children.Add(new TextBlock { Text = owner.T["Transparency"], FontWeight = FontWeights.SemiBold, FontSize = 11 });
            TextBlock percentage = new TextBlock { HorizontalAlignment = HorizontalAlignment.Right, FontSize = 11 };
            percentage.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush"); transparencyHeading.Children.Add(percentage); body.Children.Add(transparencyHeading);
            Slider transparency = new Slider { Name = "PanelTransparency", Minimum = 0, Maximum = 65, Value = Math.Round((1 - previousOpacity) * 100), TickFrequency = 5,
                IsSnapToTickEnabled = false, Margin = new Thickness(0, 6, 0, 2), SmallChange = 1, LargeChange = 5 };
            percentage.Text = transparency.Value.ToString("0") + "%";
            transparency.ValueChanged += delegate { percentage.Text = transparency.Value.ToString("0") + "%"; owner.PreviewOpacity(1 - transparency.Value / 100); };
            body.Children.Add(transparency);
            body.Children.Add(new TextBlock { Text = owner.T["TransparencyHint"], FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
            ComboBox edge = Options(owner, body, owner.T["Dock"], new[] { "Left", "Right", "Top", "Bottom", "Floating" }, owner.State.Edge);
            body.Children.Add(new TextBlock { Text = owner.T["Language"], FontWeight = FontWeights.SemiBold, FontSize = 11, Margin = new Thickness(0, 18, 0, 8) });
            ComboBox lang = new ComboBox { MinHeight = 34 };
            lang.Items.Add(new ComboBoxItem { Content = "中文", Tag = "zh" }); lang.Items.Add(new ComboBoxItem { Content = "English", Tag = "en" });
            lang.SelectedIndex = owner.State.Language == "en" ? 1 : 0; body.Children.Add(lang);
            Actions(owner, dialog, body, delegate { dialog.DialogResult = true; });
            bool accepted = false;
            try { accepted = Show(owner, dialog); }
            finally
            {
                if (!accepted)
                {
                    owner.State.PanelColor = previousColor; owner.PreviewFontScale(previousFontScale); owner.State.PanelHeight = previousHeight;
                    if (owner.Dock != null) owner.Dock.Place(); owner.PreviewNotesVisibility(!previousNotesCollapsed); owner.PreviewOpacity(previousOpacity);
                }
            }
            if (accepted)
            {
                owner.State.Theme = (string)((ComboBoxItem)theme.SelectedItem).Tag;
                owner.State.Language = (string)((ComboBoxItem)lang.SelectedItem).Tag;
                owner.State.Edge = (string)((ComboBoxItem)edge.SelectedItem).Tag;
                owner.PreviewOpacity(1 - transparency.Value / 100);
                owner.Rebuild(); owner.Dock.ChangeEdge(owner.State.Edge); owner.ApplyTheme(); owner.RequestWallpaper(); owner.UpdateTray();
                owner.Save();
            }
        }
        static void PaletteChoices(MainWindow owner, StackPanel body)
        {
            body.Children.Add(new TextBlock { Text = owner.T["PanelColor"], FontWeight = FontWeights.SemiBold, FontSize = 11, Margin = new Thickness(0, 14, 0, 8) });
            WrapPanel choices = new WrapPanel(); Button[] buttons = new Button[3];
            Action refresh = delegate {
                foreach (Button button in buttons)
                {
                    if (button == null) continue; bool selected = (string)button.Tag == owner.State.PanelColor;
                    button.BorderThickness = new Thickness(selected ? 2 : 1);
                    button.SetResourceReference(Control.BorderBrushProperty, selected ? "AccentBrush" : "LineBrush");
                    System.Windows.Automation.AutomationProperties.SetHelpText(button, selected ? "Selected" : "");
                }
            };
            for (int i = 0; i < 3; i++)
            {
                string color = PanelPalette.Colors[i]; string label = PanelPalette.Labels[i];
                StackPanel content = new StackPanel();
                content.Children.Add(new Border { Height = 22, CornerRadius = new CornerRadius(5), Background = (SolidColorBrush)new BrushConverter().ConvertFromString(color), Margin = new Thickness(0, 0, 0, 5) });
                content.Children.Add(new TextBlock { Text = owner.T[label], FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center });
                Button choice = new Button { Name = label, Content = content, Tag = color, Width = 103, Padding = new Thickness(7), Margin = new Thickness(0, 0, 8, 0), ToolTip = owner.T[label] };
                System.Windows.Automation.AutomationProperties.SetName(choice, owner.T[label]);
                choice.Click += delegate { owner.State.PanelColor = color; owner.ApplyTheme(); refresh(); };
                buttons[i] = choice; choices.Children.Add(choice);
            }
            refresh(); body.Children.Add(choices);
            body.Children.Add(new TextBlock { Text = owner.T["PaletteHint"], FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
        }
        public static bool Confirm(MainWindow owner, string text)
        {
            if (owner.Dock != null) owner.Dock.InteractionDepth++;
            try { return MessageBox.Show(owner, text, owner.T["Title"], MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes; }
            finally { if (owner.Dock != null) owner.Dock.InteractionDepth--; }
        }
    }
}
