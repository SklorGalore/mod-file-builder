using System.Globalization;
using System.Text.Json;

namespace ModFileBuilder.Core;

public record Field(string Key, string Label, string Group, string Kind = "real", bool Required = false, int MaxLength = 0);
public record EquipmentType(string Key, string Name, string Api, string Description, Field[] Fields);
public record ExportOptions(bool ImportPsspy = true, bool CheckIerr = true, bool DefineDefaults = true);

public static class Catalog
{
    private static Field I(string key, string label, string group = "Integer data", bool required = false) => new(key, label, group, "int", required);
    private static Field S(string key, string label, string group, int max, bool required = false) => new(key, label, group, "text", required, max);
    private static Field[] Reals(string group, params string[] labels) => labels.Select((label, i) => new Field($"{group}{i}", label, group)).ToArray();
    private static Field Bus() => I("bus", "Bus number", "Identifiers", true);
    private static Field Id() => S("id", "Equipment ID", "Identifiers", 2, true);
    public static readonly EquipmentType[] Types =
    [
        new("bus", "Bus", "bus_data_4", "Add or modify a bus and its voltage limits.",
        [Bus(), I("node", "Node number", "Identifiers", true), I("type", "Bus type (1–4)"), I("area", "Area"), I("zone", "Zone"), I("owner", "Owner"),
         ..Reals("Real data", "Base voltage (kV)", "Voltage magnitude (pu)", "Voltage angle (°)", "Normal upper voltage (pu)", "Normal lower voltage (pu)", "Emergency upper voltage (pu)", "Emergency lower voltage (pu)"), S("name", "Bus name", "Text", 12)]),
        new("load", "Load", "load_data_6", "Set load power, status, and distributed generation.",
        [Bus(), Id(), I("status", "Status (0 or 1)"), I("area", "Area"), I("zone", "Zone"), I("owner", "Owner"), I("scale", "Scalable (0 or 1)"), I("interrupt", "Interruptible (0 or 1)"), I("dg", "Distributed generation status (0 or 1)"),
         ..Reals("Real data", "Active power (MW)", "Reactive power (Mvar)", "Current active load (MW)", "Current reactive load (Mvar)", "Admittance active load (MW)", "Admittance reactive load (Mvar)", "Generation active power (MW)", "Generation reactive power (Mvar)"), S("name", "Load type", "Text", 12)]),
        new("branch", "Branch", "branch_data_3", "Add or modify a non-transformer transmission branch.",
        [Bus(), I("to", "To bus number", "Identifiers", true), Id(), I("status", "Status (0 or 1)"), I("meter", "Metered end bus"), I("owner1", "Owner 1"), I("owner2", "Owner 2"), I("owner3", "Owner 3"), I("owner4", "Owner 4"),
         ..Reals("Real data", "Resistance (pu)", "Reactance (pu)", "Line charging (pu)", "From-end conductance (pu)", "From-end susceptance (pu)", "To-end conductance (pu)", "To-end susceptance (pu)", "Length", "Owner 1 fraction", "Owner 2 fraction", "Owner 3 fraction", "Owner 4 fraction"),
         ..Reals("Ratings", Enumerable.Range(1,12).Select(i => $"Rating {i} (MVA)").ToArray()), S("name", "Branch name", "Text", 40)]),
        new("machine", "Machine", "machine_data_4", "Set generator output, operating limits, and impedance.",
        [Bus(), Id(), I("status", "Status (0 or 1)"), I("owner1", "Owner 1"), I("owner2", "Owner 2"), I("owner3", "Owner 3"), I("owner4", "Owner 4"), I("mode", "Reactive limits mode (0–4)"), I("base", "Baseload flag (0–2)"),
         ..Reals("Real data", "Active power (MW)", "Reactive power (Mvar)", "Maximum reactive power (Mvar)", "Minimum reactive power (Mvar)", "Maximum active power (MW)", "Minimum active power (MW)", "Machine base (MVA)", "Resistance (pu)", "Reactance (pu)", "Transformer resistance (pu)", "Transformer reactance (pu)", "Transformer tap (pu)", "Owner 1 fraction", "Owner 2 fraction", "Owner 3 fraction", "Owner 4 fraction", "Power factor"), S("name", "Machine name", "Text", 40)]),
        new("shunt", "Fixed shunt", "shunt_data", "Set fixed shunt status and admittance at a bus.",
        [Bus(), Id(), I("status", "Status (0 or 1)"), ..Reals("Real data", "Conductance (MW at 1 pu)", "Susceptance (Mvar at 1 pu)")])
    ];
}

public sealed class Modification
{
    public string TypeKey { get; set; } = "bus";
    public Dictionary<string, string> Values { get; set; } = [];
    public EquipmentType Type => Catalog.Types.Single(t => t.Key == TypeKey);
    public string Get(string key) => Values.GetValueOrDefault(key, "");
    public string Summary => $"{Type.Name} · {Get("bus")}" + (Get("to") is { Length: > 0 } to ? $" → {to}" : "") + (Get("id") is { Length: > 0 } id ? $" · ID {id}" : "");
    public Modification Copy() => new() { TypeKey = TypeKey, Values = new(Values) };
    public static Modification Create(string key) => new() { TypeKey = key, Values = key == "bus" ? new() { ["node"] = "0" } : new() { ["id"] = "1" } };

    public List<string> Validate()
    {
        List<string> errors = [];
        foreach (var f in Type.Fields)
        {
            var value = Get(f.Key).Trim();
            if (value.Length == 0) { if (f.Required) errors.Add($"{f.Label} is required."); continue; }
            if (f.Kind == "text")
            {
                if (value.Length > f.MaxLength) errors.Add($"{f.Label} must be at most {f.MaxLength} characters.");
                if (value.Any(c => c < 32 || c > 126 || c == '\'')) errors.Add($"{f.Label} must use printable ASCII without single quotes.");
            }
            else if (f.Kind == "int")
            {
                if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)) errors.Add($"{f.Label} must be a whole number.");
                else if ((f.Key is "bus" or "to") && (number < 1 || number > 999997)) errors.Add($"{f.Label} must be between 1 and 999997.");
                else if (f.Key == "node" && number < 0) errors.Add("Node number cannot be negative.");
                else if ((f.Key is "status" or "scale" or "interrupt" or "dg") && number is not (0 or 1)) errors.Add($"{f.Label} must be 0 or 1.");
                else if (f.Key == "type" && (number < 1 || number > 4)) errors.Add("Bus type must be 1–4.");
            }
            else if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) errors.Add($"{f.Label} must be a finite number (use a decimal point).");
        }
        if (TypeKey == "branch" && Get("bus").Trim() == Get("to").Trim()) errors.Add("Branch endpoints must be different buses.");
        return errors;
    }
}

public static class CommandWriter
{
    private static string Value(Modification m, Field f, bool python)
    {
        var raw = m.Get(f.Key).Trim();
        if (raw.Length == 0) return python ? f.Kind switch { "int" => "_i", "text" => "_s", _ => "_f" } : "";
        return f.Kind switch
        {
            "text" => python ? JsonSerializer.Serialize(raw) : $"'{raw}'",
            "int" => int.Parse(raw, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            _ => double.Parse(raw, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture)
        };
    }
    public static string Command(Modification m, bool python)
    {
        var errors = m.Validate();
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));
        if (!python) return "BAT_" + m.Type.Api.ToUpperInvariant() + "," + string.Join(",", m.Type.Fields.Select(f => Value(m, f, false))) + ";";
        List<string> args = [];
        foreach (var group in m.Type.Fields.GroupBy(f => f.Group))
        {
            var values = group.Select(f => Value(m, f, true));
            if (group.Key is "Identifiers" or "Text") args.AddRange(values);
            else args.Add("[" + string.Join(", ", values) + "]");
        }
        return $"ierr = psspy.{m.Type.Api}({string.Join(", ", args)})";
    }
    public static string Export(IEnumerable<Modification> entries, bool python, ExportOptions? options = null)
    {
        options ??= new ExportOptions();
        if (!python) return string.Join("\n", entries.Select(m => Command(m, false))) + "\n";

        var header = "# PSS/E case modifications — run with a case already loaded.\n";
        if (options.ImportPsspy) header += "import psspy\n";
        if (options.DefineDefaults) header += "\n_i = psspy.getdefaultint()\n_f = psspy.getdefaultreal()\n_s = psspy.getdefaultchar()\n";
        if (options.ImportPsspy || options.DefineDefaults) header += "\n";
        var commands = entries.Select(m =>
        {
            var command = Command(m, true);
            if (!options.CheckIerr) return command.Replace("ierr = ", "", StringComparison.Ordinal);
            return command + $"\nif ierr:\n    raise RuntimeError(\"{m.Type.Api} failed: {{}}\".format(ierr))";
        });
        return header + string.Join("\n\n", commands) + "\n";
    }
}
