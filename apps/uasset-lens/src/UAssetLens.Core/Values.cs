using System.Globalization;
using System.Text.RegularExpressions;
using UAssetAPI;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetLens.Core.Reading;

namespace UAssetLens.Core;

// Formats stored values the way a person reads them in UEFN.
public static partial class Values
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public const string Unreadable = "(couldn't read)";

    public static string Number(double v)
    {
        if (Math.Abs(v) < 1e-9) v = 0;
        var r = Math.Round(v, 3);
        return r.ToString(Math.Abs(r) >= 1e15 ? "E3" : "0.###", Inv);
    }

    public static string Vector(double x, double y, double z) => $"X {Number(x)} · Y {Number(y)} · Z {Number(z)}";

    public static string Rotation(double pitch, double yaw, double roll) => $"{Number(pitch)}° · {Number(yaw)}° · {Number(roll)}°";

    // A single stored value. Structs, arrays and maps are flattened by SnapshotReader instead.
    public static string Format(PropertyData p, UAsset asset)
    {
        switch (p)
        {
            case BoolPropertyData b: return b.Value ? "On" : "Off";
            case LensTextPropertyData { Unreadable: true }: return Unreadable;
            case TextPropertyData t: return t.CultureInvariantString?.Value ?? t.Value?.Value ?? "";
            case StrPropertyData s: return s.Value?.Value ?? "";
            case NamePropertyData n: return n.Value?.Value?.Value ?? "None";
            case EnumPropertyData e: return EnumValue(e.Value?.Value?.Value);
            case BytePropertyData by:
                return by.ByteType == BytePropertyType.FName ? EnumValue(by.EnumValue?.Value?.Value) : by.Value.ToString(Inv);
            case FloatPropertyData f: return Number(f.Value);
            case DoublePropertyData d: return Number(d.Value);
            case ObjectPropertyData o: return ObjectName(o.Value, asset);
            case SoftObjectPropertyData so: return AssetPath(so.Value.AssetPath.PackageName?.Value?.Value, so.Value.AssetPath.AssetName?.Value?.Value);
            case VectorPropertyData v: return Vector(v.Value.X, v.Value.Y, v.Value.Z);
            case RotatorPropertyData r: return Rotation(r.Value.Pitch, r.Value.Yaw, r.Value.Roll);
            case GuidPropertyData g: return g.Value.ToString();
            case UnknownPropertyData: return Unreadable;
        }
        object? raw;
        try { raw = p.RawValue; } catch { return Unreadable; }
        return raw switch
        {
            null => "",
            IFormattable num => num.ToString(null, Inv),
            _ => raw.ToString() ?? "",
        };
    }

    // "EFortTeam::TeamIndex_1" -> "Team Index 1".
    public static string EnumValue(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "None";
        var i = v.LastIndexOf("::", StringComparison.Ordinal);
        return Names.Humanize(i >= 0 ? v[(i + 2)..] : v);
    }

    public static string ObjectName(FPackageIndex? index, UAsset asset)
    {
        if (index == null || index.IsNull()) return "None";
        if (index.IsExport()) return Names.Humanize(index.ToExport(asset).ObjectName.ToString());
        var import = index.ToImport(asset);
        var name = import.ObjectName.ToString();
        // References to other actors in the level: "Device_Trigger_V2_C_UAID_047C16C931E2070603_1571026438".
        var actor = ActorRefRegex().Match(name);
        if (actor.Success) return $"{Names.TypeFromClass(actor.Groups[1].Value)} ({actor.Groups[2].Value})";
        return ShortAssetName(name);
    }

    static string AssetPath(string? package, string? asset) =>
        asset != null ? ShortAssetName(asset) : package != null ? ShortAssetName(package) : "None";

    static string ShortAssetName(string name)
    {
        name = name.TrimEnd('\'', '"');
        var slash = name.LastIndexOf('/');
        if (slash >= 0) name = name[(slash + 1)..];
        var dot = name.LastIndexOf('.');
        if (dot >= 0) name = name[(dot + 1)..];
        if (name.EndsWith("_C", StringComparison.Ordinal)) name = name[..^2];
        return name;
    }

    // Device option values are stored as the text UEFN's details panel round-trips:
    // "True", "0.000000", "(TeamType=Any,TeamIndex=1)", "/Game/Items/WID_Shotgun.WID_Shotgun", NSLOCTEXT(...).
    public static string Option(string? raw)
    {
        raw = raw?.Trim();
        if (string.IsNullOrEmpty(raw)) return "(empty)";
        if (raw == "True") return "On";
        if (raw == "False") return "Off";
        if (double.TryParse(raw, NumberStyles.Float, Inv, out var num)) return Number(num);
        var text = LocTextRegex().Match(raw);
        if (text.Success) return Regex.Unescape(text.Groups[1].Value);
        if (raw.StartsWith('(') && raw.EndsWith(')') && raw.Contains('='))
            return string.Join(", ", SplitTopLevel(raw[1..^1]).Select(part =>
            {
                var eq = part.IndexOf('=');
                return eq < 0 ? Option(part) : $"{Names.Humanize(part[..eq])} {Option(part[(eq + 1)..])}";
            }));
        if (raw.StartsWith('/') || raw.Contains("'/")) return ShortAssetName(raw);
        return raw;
    }

    static IEnumerable<string> SplitTopLevel(string s)
    {
        int depth = 0, start = 0;
        bool quoted = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '"') quoted = !quoted;
            else if (!quoted && c == '(') depth++;
            else if (!quoted && c == ')') depth--;
            else if (!quoted && depth == 0 && c == ',') { yield return s[start..i]; start = i + 1; }
        }
        yield return s[start..];
    }

    [GeneratedRegex(@"^(\w+?)_UAID_[0-9A-F]+_(\d+)$")]
    private static partial Regex ActorRefRegex();

    [GeneratedRegex(@"^(?:NSLOCTEXT|LOCTEXT|INVTEXT|LOCTABLE)\((?:""[^""]*"",\s*)*""((?:[^""\\]|\\.)*)""\)$")]
    private static partial Regex LocTextRegex();
}
