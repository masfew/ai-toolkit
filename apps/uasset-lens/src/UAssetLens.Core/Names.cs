using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace UAssetLens.Core;

// Turns Unreal's stored names into the words UEFN shows.
public static partial class Names
{
    // Classes whose names don't read well when split into words.
    static readonly Dictionary<string, string> TypeOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FortStaticMeshActor"] = "Static Mesh",
        ["FortPlaysetRoot"] = "Prefab Root",
        ["LandscapeStreamingProxy"] = "Landscape Section",
        ["VerseDevice"] = "Verse Device",
        ["ItemContainer"] = "Item Container",
        ["HUDController"] = "HUD Controller",
        ["AnalyticDevice"] = "Analytics",
        ["Powerup_VisualEffect"] = "Visual Effect Powerup",
        ["MutatorVolume"] = "Mutator Zone",
        ["Mutator"] = "Mutator Zone",
        ["CinematicSequence"] = "Cinematic Sequence",
        ["Cinematic"] = "Cinematic Sequence",
    };

    // Player-built pieces: PBWA_<material><tier>_<piece>.
    static readonly Dictionary<char, string> BuildMaterials = new() { ['W'] = "Wood", ['S'] = "Brick", ['M'] = "Metal" };
    static readonly (string Prefix, string Name)[] BuildPieces =
    [
        ("Solid", "Wall"), ("Floor", "Floor"), ("Roof", "Roof"), ("Stair", "Stairs"), ("Window", "Window Wall"),
        ("Door", "Door Wall"), ("Arch", "Arch"), ("Half", "Half Wall"), ("Quarter", "Quarter Wall"), ("Brace", "Brace Wall"),
    ];

    static readonly string[] ClassPrefixes =
        ["FNEC_BP_", "FNEC_", "Athena_Prop_", "BP_", "Prop_", "CP_", "SM_", "B_"];

    // "Device_Trigger_V2_C" or "PID_Device_Trigger" -> "Trigger Device"; "PBWA_W1_RoofS_C" -> "Wood Roof".
    public static string TypeFromClass(string className, string? playset = null)
    {
        var playsetName = playset?.Split('/').LastOrDefault();
        if (playsetName != null && playsetName.Contains("Device", StringComparison.OrdinalIgnoreCase))
            return DeviceType(StripPlaysetPrefix(playsetName));

        var name = StripClassSuffixes(className);
        if (TypeOverrides.TryGetValue(name, out var fixedName)) return fixedName;

        var build = BuildPieceRegex().Match(name);
        if (build.Success && BuildMaterials.TryGetValue(build.Groups[1].Value[0], out var material))
        {
            var piece = BuildPieces.FirstOrDefault(p => build.Groups[2].Value.StartsWith(p.Prefix, StringComparison.OrdinalIgnoreCase)).Name
                        ?? Humanize(build.Groups[2].Value);
            return $"{material} {piece}";
        }

        if (name.StartsWith("Device_", StringComparison.OrdinalIgnoreCase)) return DeviceType(name);
        foreach (var prefix in ClassPrefixes)
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { name = name[prefix.Length..]; break; }
        return Humanize(name);
    }

    static string DeviceType(string name)
    {
        foreach (var prefix in new[] { "CP_Devices_", "Devices_", "Device_" })
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { name = name[prefix.Length..]; break; }
        name = StripClassSuffixes(name);
        if (name.EndsWith("_Device", StringComparison.OrdinalIgnoreCase)) name = name[..^7];
        if (TypeOverrides.TryGetValue(name, out var fixedName))
            return fixedName.EndsWith("Device") || fixedName.EndsWith("Zone") || fixedName.EndsWith("Powerup") || fixedName.EndsWith("Sequence") ? fixedName : fixedName + " Device";
        return Humanize(name) + " Device";
    }

    static string StripPlaysetPrefix(string name)
    {
        foreach (var prefix in new[] { "PPID_", "PID_" })
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return name[prefix.Length..];
        return name;
    }

    // "Device_Barrier_V2_Placed_C" -> "Device_Barrier".
    static string StripClassSuffixes(string name)
    {
        string before;
        do
        {
            before = name;
            if (name.EndsWith("_C", StringComparison.Ordinal)) name = name[..^2];
            if (name.EndsWith("_Placed", StringComparison.OrdinalIgnoreCase)) name = name[..^7];
            name = VersionSuffixRegex().Replace(name, "");
        } while (name != before);
        return name;
    }

    // "__verse_0x92185E59_DefaultFallbackWeapon" -> "DefaultFallbackWeapon".
    public static string StripVerse(string name)
    {
        var m = VerseNameRegex().Match(name);
        return m.Success ? m.Groups[1].Value : name;
    }

    // "TriggerDelay" -> "Trigger Delay", "bReceiveDamageWhenInvisible" -> "Receive Damage When Invisible",
    // "ActivatingTeam" -> "Activating Team", "HUD_Controller" -> "HUD Controller".
    public static string Humanize(string name)
    {
        name = StripVerse(name);
        // Verse paths: "Features-Waves-wave_step_3" -> "wave_step_3".
        if (name.Contains('-') && !name.Contains(' ')) name = name[(name.LastIndexOf('-') + 1)..];
        if (name.Length > 1 && name[0] == 'b' && char.IsUpper(name[1])) name = name[1..];
        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c == '_' || c == ' ') { AppendSpace(sb); continue; }
            if (i > 0 && sb.Length > 0 && sb[^1] != ' ')
            {
                char prev = name[i - 1];
                bool next = i + 1 < name.Length && char.IsLower(name[i + 1]);
                if ((char.IsUpper(c) && (char.IsLower(prev) || char.IsDigit(prev))) ||
                    (char.IsUpper(c) && char.IsUpper(prev) && next) ||
                    (char.IsDigit(c) && char.IsLetter(prev) && !IsVersionDigit(name, i)))
                    AppendSpace(sb);
            }
            sb.Append(sb.Length == 0 || sb[^1] == ' ' ? char.ToUpper(c, CultureInfo.InvariantCulture) : c);
        }
        return sb.ToString().Trim();
    }

    // Keep "V2", "W1", "3D" and similar together.
    static bool IsVersionDigit(string name, int i) => i >= 1 && char.IsUpper(name[i - 1]) && (i < 2 || !char.IsLetter(name[i - 2]));

    static void AppendSpace(StringBuilder sb)
    {
        if (sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
    }

    [GeneratedRegex(@"_V\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionSuffixRegex();

    [GeneratedRegex(@"^PBWA_([WSM])\d_(\w+)$")]
    private static partial Regex BuildPieceRegex();

    [GeneratedRegex(@"^__verse_0x[0-9A-Fa-f]+_(.+)$")]
    private static partial Regex VerseNameRegex();
}
