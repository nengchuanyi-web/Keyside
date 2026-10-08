using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ShortcutDock
{
    public static partial class Dialogs
    {
        public static string Notes(MainWindow owner, string current)
        {
            if (owner.IsEditingLocked) return null;
            Window dialog = Create(owner, owner.T["EditNotes"], 470); StackPanel body = Body(dialog, owner.T["EditNotes"]);
            TextBox notes = Field(body, owner.T["Notes"], current, 4000);
            notes.Name = "NotesEditor"; notes.AcceptsReturn = true; notes.TextWrapping = TextWrapping.Wrap;
            notes.FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI, Microsoft YaHei UI");
            notes.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; notes.Height = 130;
            body.Children.Add(new TextBlock { Text = owner.T["NotesHint"], FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 12) });
            body.Children.Add(new TextBlock { Text = owner.T["Preview"], FontWeight = FontWeights.SemiBold, FontSize = 11, Margin = new Thickness(0, 0, 0, 8) });
            EmojiText preview = new EmojiText { Name = "EmojiPreview", Value = notes.Text, FontSize = 15, TextWrapping = TextWrapping.Wrap };
            preview.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            body.Children.Add(new ScrollViewer { Content = preview, MaxHeight = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            notes.TextChanged += delegate { preview.Value = notes.Text; };
            Actions(owner, dialog, body, delegate { dialog.DialogResult = true; });
            dialog.Loaded += delegate { notes.Focus(); notes.CaretIndex = notes.Text.Length; };
            return Show(owner, dialog) ? notes.Text : null;
        }
        public static TxtImportChoice ImportTxt(MainWindow owner, string suggested, List<ShortcutEntry> rows)
        {
            if (owner.IsEditingLocked) return null;
            Window dialog = Create(owner, owner.T["ImportTxt"], 520); StackPanel body = Body(dialog, owner.T["ImportTxt"]);
            body.Children.Add(new TextBlock { Text = owner.T["ImportTarget"], FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
            ComboBox target = new ComboBox { Name = "TxtTarget", MinHeight = 34 };
            target.Items.Add(new ComboBoxItem { Content = owner.T["NewImportTab"], Tag = "" });
            foreach (SoftwareTab tab in owner.State.Tabs)
                target.Items.Add(new ComboBoxItem { Content = tab == owner.ActiveTab() ? String.Format(owner.T["CurrentImportTab"], tab.Name) : tab.Name, Tag = tab.Id });
            target.SelectedIndex = 0; body.Children.Add(target);
            TextBox name = Field(body, owner.T["Name"], suggested.Length > 60 ? suggested.Substring(0, 60) : suggested, 60);
            target.SelectionChanged += delegate { name.IsEnabled = target.SelectedIndex == 0; };
            body.Children.Add(new TextBlock { Text = String.Format(owner.T["ImportPreview"], rows.Count), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 8) });
            DataGrid preview = new DataGrid { Height = 185, ItemsSource = rows, IsReadOnly = true, AutoGenerateColumns = false };
            preview.Columns.Add(new DataGridTextColumn { Header = owner.T["Keys"], Binding = new Binding("Keys"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            preview.Columns.Add(new DataGridTextColumn { Header = owner.T["Description"], Binding = new Binding("Description"), Width = new DataGridLength(1.6, DataGridLengthUnitType.Star) });
            body.Children.Add(preview);
            TextBlock error = new TextBlock { FontSize = 11, Foreground = Brushes.IndianRed, Margin = new Thickness(0, 8, 0, 0) }; body.Children.Add(error);
            Actions(owner, dialog, body, delegate {
                if (target.SelectedIndex == 0 && String.IsNullOrWhiteSpace(name.Text)) { error.Text = owner.T["Invalid"]; return; }
                if (target.SelectedIndex == 0 && owner.State.Tabs.Count >= 200) { error.Text = owner.T["LimitTabs"]; return; }
                dialog.DialogResult = true;
            });
            return Show(owner, dialog) ? new TxtImportChoice { TabId = (string)((ComboBoxItem)target.SelectedItem).Tag, Name = name.Text.Trim() } : null;
        }
    }
}
