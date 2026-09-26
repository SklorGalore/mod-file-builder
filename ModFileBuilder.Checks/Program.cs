using System.Globalization;
using ModFileBuilder.Core;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
var shunt = Modification.Create("shunt");
shunt.Values["bus"] = "101";
shunt.Values["status"] = "1";
shunt.Values["Real data0"] = "0";
shunt.Values["Real data1"] = "25.5";
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
Check(CommandWriter.Command(shunt, false) == "BAT_SHUNT_DATA, 101, '1', 1, 0, 25.5;", "IDV parameter order, quoting, invariant decimals and termination");
Check(CommandWriter.Command(shunt, true).StartsWith("ierr = psspy.shunt_data(101, \"1\", [1], [0, 25.5])"), "Python scalar identifiers and arrays");
var bus = Modification.Create("bus");
bus.Values["bus"] = "102";
Check(CommandWriter.Command(bus, true).Contains("(102, 0, [_i, _i, _i, _i], [_f, _f, _f, _f, _f, _f, _f], _s)"), "Bus default slots preserved");
Check(CommandWriter.Command(bus, false).Split(',').Length == 15, "Bus batch slot count");
var expectedSlots = new Dictionary<string, int> { ["bus"] = 14, ["load"] = 18, ["branch"] = 34, ["machine"] = 27, ["shunt"] = 5 };
foreach (var type in Catalog.Types)
{
    var entry = Modification.Create(type.Key);
    entry.Values["bus"] = "101";
    if(type.Key == "branch") entry.Values["to"] = "102";
    Check(entry.Validate().Count == 0, type.Name + " minimum identifiers");
    Check(CommandWriter.Command(entry, false).Split(',').Length - 1 == expectedSlots[type.Key], type.Name + " batch slots");
}
bus.Values["Real data0"] = "NaN";
Check(bus.Validate().Count > 0, "Reject non-finite values");
bus.Values.Remove("Real data0");
bus.Values["name"] = "bad\ncommand";
Check(bus.Validate().Count > 0, "Reject newline injection");
bus.Values.Remove("name");
var copy = bus.Copy(); copy.Values["bus"] = "999";
Check(bus.Get("bus") == "102", "Editing copy does not mutate queue");
var output = CommandWriter.Export([bus, shunt], true);
Check(output.IndexOf("psspy.bus_data_4", StringComparison.Ordinal) < output.IndexOf("psspy.shunt_data", StringComparison.Ordinal), "Export keeps queue order");
Check(output.Contains("getdefaultchar()") && output.Contains("raise RuntimeError"), "Defaults and runtime errors included");
Console.WriteLine($"Passed {checks} command generation checks.");
