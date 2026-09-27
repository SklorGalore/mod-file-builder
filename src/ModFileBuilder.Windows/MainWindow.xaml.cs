using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using ModFileBuilder.Core;

namespace ModFileBuilder.Windows;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<Modification> entries = [];
    private Modification draft = Modification.Create("bus");
    private int? editIndex;
    private bool ready;
    private bool loadingEditor;
    private bool unsaved;
    private bool draftChanged;
    private bool darkMode;
    private bool acrylicAvailable;
    private bool Python => FormatSelector.SelectedIndex == 0;
    private string Output => CommandWriter.Export(entries, Python,
        new ExportOptions(ImportPsspy.IsChecked == true, DefineDefaults.IsChecked == true));

    public MainWindow()
    {
        InitializeComponent();
        EntryList.ItemsSource = entries;
        EquipmentSelector.ItemsSource = Catalog.Types;
        ready = true;
        LoadEditor();
        RefreshOutput();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var backdrop = 3; // DWMSBT_TRANSIENTWINDOW: Desktop Acrylic on Windows 11 22H2+.
        acrylicAvailable = DwmSetWindowAttribute(handle, 38, ref backdrop, sizeof(int)) == 0;
        UpdateWindowAppearance(handle);
    }

    private void UpdateWindowAppearance(IntPtr handle)
    {
        var darkTitleBar = darkMode ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, 20, ref darkTitleBar, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
        Resources["AcrylicBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
            acrylicAvailable ? (darkMode ? "#D9181D25" : "#D9F3F6FA") : (darkMode ? "#FF171A20" : "#FFF3F6FA")));
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    private void LoadEditor()
    {
        loadingEditor = true;
        EquipmentSelector.SelectedValue = draft.TypeKey;
        DescriptionText.Text = draft.Type.Description;
        EditorTitle.Text = editIndex is null ? "Add equipment" : "Edit equipment";
        SaveEntryButton.Content = editIndex is null ? "Add to file" : "Save changes";
        ResetButton.Content = editIndex is null ? "Reset fields" : "Cancel edit";
        ValidationText.Text = "";
        FieldsPanel.Children.Clear();
        foreach (var group in draft.Type.Fields.GroupBy(f => f.Group))
        {
            FieldsPanel.Children.Add(new TextBlock
            {
                Text = group.Key, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 20, 0, 10)
            });
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var index = 0;
            foreach (var field in group)
            {
                if (index % 2 == 0) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var panel = new StackPanel { Margin = new Thickness(0, 0, 12, 12) };
                var label = field.Label + (field.Required && field.Key is not ("node" or "id") ? " *" : "");
                panel.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 5), FontSize = 12 });
                if (field.Key is "status" or "dg")
                {
                    var select = new ComboBox { SelectedValuePath = "Tag" };
                    select.Items.Add(new ComboBoxItem { Content = draft.Placeholder(field), Tag = "" });
                    select.Items.Add(new ComboBoxItem { Content = "On-Line", Tag = "1" });
                    select.Items.Add(new ComboBoxItem { Content = "Out-Of-Service", Tag = "0" });
                    select.SelectedValue = draft.Get(field.Key);
                    select.SelectionChanged += (_, _) => SetValue(field.Key, select.SelectedValue?.ToString() ?? "");
                    AutomationProperties.SetName(select, label);
                    panel.Children.Add(select);
                }
                else
                {
                    var input = new TextBox { Text = draft.Get(field.Key), MaxLength = field.Kind == "text" ? field.MaxLength : 40 };
                    AutomationProperties.SetName(input, label);
                    var placeholder = new TextBlock
                    {
                        Text = draft.Placeholder(field), Foreground = Brushes.Gray,
                        Margin = new Thickness(9, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center,
                        IsHitTestVisible = false, TextTrimming = TextTrimming.CharacterEllipsis,
                        Visibility = input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed
                    };
                    input.ToolTip = $"Default: {draft.Placeholder(field)}";
                    input.TextChanged += (_, _) =>
                    {
                        placeholder.Visibility = input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                        SetValue(field.Key, input.Text);
                    };
                    var overlay = new Grid();
                    overlay.Children.Add(input);
                    overlay.Children.Add(placeholder);
                    panel.Children.Add(overlay);
                }
                Grid.SetRow(panel, index / 2);
                Grid.SetColumn(panel, index % 2);
                grid.Children.Add(panel);
                index++;
            }
            FieldsPanel.Children.Add(grid);
        }
        ErrorSelector.SelectedIndex = (int)draft.ErrorHandling;
        loadingEditor = false;
        draftChanged = false;
        RefreshActions();
    }

    private void SetValue(string key, string value)
    {
        draft.Values[key] = value;
        draftChanged = true;
    }

    private void ChangeEquipment(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || loadingEditor || EquipmentSelector.SelectedValue is not string key) return;
        draft = Modification.Create(key);
        LoadEditor();
    }

    private void ErrorChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || loadingEditor) return;
        draft.ErrorHandling = (PythonErrorHandling)ErrorSelector.SelectedIndex;
        draftChanged = true;
    }

    private void SaveEntry(object sender, RoutedEventArgs e)
    {
        var errors = draft.Validate();
        ValidationText.Text = string.Join("\n", errors);
        if (errors.Count > 0) return;
        var index = editIndex ?? entries.Count;
        if (editIndex is int existing) entries[existing] = draft.Copy();
        else entries.Add(draft.Copy());
        unsaved = true;
        ResetEditor(sender, e);
        EntryList.SelectedIndex = index;
        StatusText.Text = "Modification saved to the list. Save the file when finished.";
        RefreshOutput();
    }

    private void ResetEditor(object sender, RoutedEventArgs e)
    {
        draft = Modification.Create(draft.TypeKey);
        editIndex = null;
        LoadEditor();
    }

    private void EditEntry(object sender, RoutedEventArgs e)
    {
        if (editIndex is not null || EntryList.SelectedIndex < 0) return;
        editIndex = EntryList.SelectedIndex;
        draft = entries[editIndex.Value].Copy();
        LoadEditor();
    }

    private void RemoveEntry(object sender, RoutedEventArgs e)
    {
        if (editIndex is not null || EntryList.SelectedIndex < 0) return;
        var index = EntryList.SelectedIndex;
        entries.RemoveAt(index);
        EntryList.SelectedIndex = Math.Min(index, entries.Count - 1);
        unsaved = true;
        RefreshOutput();
    }

    private void MoveUp(object sender, RoutedEventArgs e) => Move(-1);
    private void MoveDown(object sender, RoutedEventArgs e) => Move(1);
    private void Move(int direction)
    {
        var index = EntryList.SelectedIndex;
        var other = index + direction;
        if (editIndex is not null || index < 0 || other < 0 || other >= entries.Count) return;
        entries.Move(index, other);
        EntryList.SelectedIndex = other;
        unsaved = true;
        RefreshOutput();
    }

    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ready) RefreshActions();
    }

    private void OutputChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        if (entries.Count > 0) unsaved = true;
        RefreshOutput();
    }

    private void RefreshOutput()
    {
        PythonOptions.Visibility = EntryPythonOptions.Visibility = Python ? Visibility.Visible : Visibility.Collapsed;
        PreviewText.Text = entries.Count == 0 ? "" : Output;
        EmptyListState.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyPreviewState.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        QueueTitle.Text = $"Modification list ({entries.Count})";
        RefreshActions();
    }

    private void RefreshActions()
    {
        var index = EntryList.SelectedIndex;
        var canAct = editIndex is null && index >= 0;
        EditButton.IsEnabled = RemoveButton.IsEnabled = canAct;
        UpButton.IsEnabled = canAct && index > 0;
        DownButton.IsEnabled = canAct && index < entries.Count - 1;
        ExportButton.IsEnabled = entries.Count > 0 && editIndex is null;
    }

    private void ExportFile(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save case modifications", FileName = "case_modifications",
            DefaultExt = Python ? ".py" : ".idv",
            Filter = Python ? "Python files (*.py)|*.py" : "PSS/E batch files (*.idv)|*.idv",
            AddExtension = true, OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, Output);
            unsaved = false;
            StatusText.Text = $"Saved {dialog.FileName}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Could not save file", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ToggleTheme(object sender, RoutedEventArgs e)
    {
        darkMode = !darkMode;
        Resources["PageBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(darkMode ? "#FF171A20" : "#FFF3F6FA"));
        Resources["CardBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(darkMode ? "#D9232932" : "#D9FFFFFF"));
        Resources["ControlBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(darkMode ? "#CC20252D" : "#CCFFFFFF"));
        Resources["InkBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(darkMode ? "#FFE6EAF0" : "#FF202631"));
        Resources["HintBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(darkMode ? "#FFADB7C4" : "#FF5D6878"));
        Resources["LineBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(darkMode ? "#607D8998" : "#507D8998"));
        Resources["AccentBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(darkMode ? "#FF60A9F5" : "#FF176BCE"));
        UpdateWindowAppearance(new WindowInteropHelper(this).Handle);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!unsaved && !draftChanged && editIndex is null) return;
        e.Cancel = MessageBox.Show(this, "There are unsaved changes. Close and discard them?",
            "Unsaved changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes;
    }
}
