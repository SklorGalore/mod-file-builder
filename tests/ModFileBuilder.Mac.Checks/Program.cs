using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using ModFileBuilder.Mac;

AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
var window = new MainWindow();
window.Show();
var checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks++;
}
T Control<T>(string name) where T : Control => window.FindControl<T>(name) ?? throw new Exception(name);
void Click(string name)
{
    Dispatcher.UIThread.RunJobs();
    Control<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Dispatcher.UIThread.RunJobs();
}
TextBox Input(string label) => Control<StackPanel>("FieldsPanel").GetLogicalDescendants().OfType<TextBox>()
    .Single(t => AutomationProperties.GetName(t) == label);
var list = Control<ListBox>("EntryList");
var preview = Control<TextBox>("PreviewText");
Check(!Control<Button>("ExportButton").IsEnabled, "Empty queue cannot export");
Click("SaveEntryButton");
Check(Control<TextBlock>("ValidationText").Text!.Contains("Bus number is required"), "Invalid entry shows validation");
Input("Bus number *").Text = "101";
Check(Input("Node number").Text == "" && Input("Node number").PlaceholderText == "0", "Node default is placeholder only");
Click("SaveEntryButton");
Check(list.ItemCount == 1 && preview.Text!.Contains("bus_data_4(101, 0"), "Bus saved with default identifier");
Control<ComboBox>("EquipmentSelector").SelectedIndex = 4;
Input("Bus number *").Text = "101";
var status = Control<StackPanel>("FieldsPanel").GetLogicalDescendants().OfType<ComboBox>().Single();
Check(((ComboBoxItem)status.Items[1]!).Content?.ToString() == "On-Line", "Readable status choices");
status.SelectedIndex = 2;
Control<ComboBox>("ErrorSelector").SelectedIndex = 1;
Click("SaveEntryButton");
Check(preview.Text!.Contains("shunt_data(101, \"1\", [0]"), "Out-Of-Service exports zero");
Check(preview.Text.Contains("print(\"WARNING:") && preview.Text.Contains("raise RuntimeError"), "Queue preserves mixed error policies");
Click("EditButton");
Check(Control<ComboBox>("ErrorSelector").SelectedIndex == 1, "Editing restores warning policy");
Check(!Control<Button>("ExportButton").IsEnabled && !Control<Button>("RemoveButton").IsEnabled, "Active edit protects queue and export");
Input("Bus number *").Text = "202";
Click("ResetButton");
Check(preview.Text.Contains("shunt_data(101"), "Cancel discards edit");
Click("UpButton");
Check(preview.Text!.IndexOf("shunt_data", StringComparison.Ordinal) < preview.Text.IndexOf("bus_data_4", StringComparison.Ordinal), "Reordering updates preview");
Control<ComboBox>("FormatSelector").SelectedIndex = 1;
Check(preview.Text!.StartsWith("BAT_SHUNT_DATA,101,'1',0,"), "Batch output uses numeric status");
Check(!Control<StackPanel>("EntryPythonOptions").IsVisible, "Python settings hidden in batch mode");
Control<ComboBox>("FormatSelector").SelectedIndex = 0;
Click("EditButton");
Check(Control<ComboBox>("ErrorSelector").SelectedIndex == 1, "Format switching retains error policy");
Click("ResetButton");
Click("RemoveButton");
Check(list.ItemCount == 1 && !preview.Text!.Contains("shunt_data"), "Remove updates export");
Console.WriteLine($"Passed {checks} Avalonia UI checks.");
