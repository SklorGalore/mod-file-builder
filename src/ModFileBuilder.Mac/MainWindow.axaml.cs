using System.Collections.ObjectModel;
using System.IO;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using ModFileBuilder.Core;

namespace ModFileBuilder.Mac;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<Modification> entries = [];
    private Modification draft = Modification.Create("bus");
    private int? editIndex;
    private bool ready;
    private bool loadingEditor;
    private bool unsaved;
    private bool draftChanged;
    private bool allowClose;
    private bool confirmingClose;
    private bool Python => FormatSelector.SelectedIndex == 0;
    private string Output => CommandWriter.Export(entries, Python,
        new ExportOptions(ImportPsspy.IsChecked == true, DefineDefaults.IsChecked == true));

    public MainWindow()
    {
        InitializeComponent();
        EntryList.ItemsSource = entries;
        EquipmentSelector.ItemsSource = Catalog.Types.Select(t => new ComboBoxItem { Content = t.Name, Tag = t.Key }).ToArray();
        ready = true;
        LoadEditor();
        RefreshOutput();
    }

    private void LoadEditor()
    {
        loadingEditor = true;
        EquipmentSelector.SelectedIndex = Array.FindIndex(Catalog.Types, t => t.Key == draft.TypeKey);
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
                Text = group.Key, FontWeight = FontWeight.SemiBold,
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
                    var select = new ComboBox();
                    select.Items.Add(new ComboBoxItem { Content = draft.Placeholder(field), Tag = "" });
                    select.Items.Add(new ComboBoxItem { Content = "On-Line", Tag = "1" });
                    select.Items.Add(new ComboBoxItem { Content = "Out-Of-Service", Tag = "0" });
                    select.SelectedIndex = draft.Get(field.Key) switch { "1" => 1, "0" => 2, _ => 0 };
                    select.SelectionChanged += (_, _) => SetValue(field.Key, (select.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "");
                    AutomationProperties.SetName(select, label);
                    panel.Children.Add(select);
                }
                else
                {
                    var input = new TextBox
                    {
                        Text = draft.Get(field.Key), PlaceholderText = draft.Placeholder(field),
                        MaxLength = field.Kind == "text" ? field.MaxLength : 40
                    };
                    AutomationProperties.SetName(input, label);
                    ToolTip.SetTip(input, $"Default: {draft.Placeholder(field)}");
                    input.PropertyChanged += (_, e) =>
                    {
                        if (e.Property == TextBox.TextProperty) SetValue(field.Key, input.Text ?? "");
                    };
                    panel.Children.Add(input);
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
        if (!ready || loadingEditor || EquipmentSelector.SelectedItem is not ComboBoxItem { Tag: string key }) return;
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
        PythonOptions.IsVisible = EntryPythonOptions.IsVisible = Python;
        PreviewText.Text = entries.Count == 0 ? "Your generated commands will appear here." : Output;
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

    private async void ExportFile(object? sender, RoutedEventArgs e)
    {
        // Capture the current output before opening the asynchronous native dialog.
        var output = Output;
        var python = Python;
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save case modifications", SuggestedFileName = "case_modifications" + (python ? ".py" : ".idv"),
                DefaultExtension = python ? "py" : "idv", ShowOverwritePrompt = true,
                FileTypeChoices = [new FilePickerFileType(python ? "Python" : "PSS/E batch")
                {
                    Patterns = [python ? "*.py" : "*.idv"]
                }]
            });
            if (file is null) return;
            using (file)
            {
                await using var stream = await file.OpenWriteAsync();
                stream.SetLength(0);
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(output);
                await writer.FlushAsync();
                unsaved = Output != output;
                StatusText.Text = $"Saved {file.Name}";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"Could not save file: {ex.Message}";
        }
    }

    private void ToggleTheme(object? sender, RoutedEventArgs e)
    {
        RequestedThemeVariant = ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (allowClose || (!unsaved && !draftChanged && editIndex is null)) return;
        e.Cancel = true;
        if (confirmingClose) return;
        confirmingClose = true;
        try
        {
            var dialog = new Window
            {
                Title = "Unsaved changes", Width = 420, SizeToContent = SizeToContent.Height,
                CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            var keep = new Button { Content = "Keep editing", IsCancel = true };
            var discard = new Button { Content = "Discard and close" };
            keep.Click += (_, _) => dialog.Close(false);
            discard.Click += (_, _) => dialog.Close(true);
            dialog.Content = new StackPanel
            {
                Margin = new Thickness(24), Spacing = 20,
                Children =
                {
                    new TextBlock { Text = "There are unsaved changes. Close and discard them?", TextWrapping = TextWrapping.Wrap },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { keep, discard } }
                }
            };
            if (await dialog.ShowDialog<bool>(this))
            {
                allowClose = true;
                Close();
            }
        }
        finally { confirmingClose = false; }
    }
}
