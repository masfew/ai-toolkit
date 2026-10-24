using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetLens.Core.Reading;

namespace UAssetLens.Core;

// Reads one .uasset or .umap into a name, a type and a flat list of readable settings.
public static class SnapshotReader
{
    // Engine plumbing that every actor stores and nobody edits.
    static readonly HashSet<string> Plumbing = new(StringComparer.Ordinal)
    {
        "UCSSerializationIndex", "bNetAddressable", "CreationMethod", "AttachParent", "BlueprintCreatedComponents",
        "RootComponent", "ActorGuid", "SavedActorGuid", "MyGuid", "PlaysetPackagePathName", "LabelOverride",
        "ActorLabel", "DataVersion", "InstanceComponents", "UserConstructionScript",
        // Verse devices: the script object is listed through its own export, and the overrides blob
        // is a binary copy of the same @editable values.
        "Script", "ScriptClassPath", "ScriptPropertyOverrides", "ScriptPropertyOverridesVersion",
    };

    static readonly HashSet<string> DelegateTypes = new(StringComparer.Ordinal)
    {
        "DelegateProperty", "MulticastDelegateProperty", "MulticastInlineDelegateProperty", "MulticastSparseDelegateProperty",
    };

    static readonly string[] PositionProps = ["RelativeLocation", "RelativeRotation", "RelativeScale3D"];

    public static AssetSnapshot Read(string path)
    {
        UAsset asset;
        try { asset = AssetLoader.Load(path); }
        catch (Exception ex) { return AssetSnapshot.Failed(path, ex.Message); }
        return Read(asset, path);
    }

    public static AssetSnapshot Read(UAsset asset, string path = "")
    {
        var exports = asset.Exports;
        var settings = new List<Setting>();
        var problems = new List<string>();

        // One-actor files (__ExternalActors__) hold one actor; levels hold a World with actors inside it.
        int worldIndex = exports.FindIndex(e => e.GetExportClassType()?.Value?.Value == "World");
        int actorIndex = worldIndex >= 0 ? -1 : exports.FindIndex(e => e is NormalExport ne && ne.Data.Any(p => p.Name.Value.Value == "ActorGuid"));
        bool isActor = actorIndex >= 0;
        int mainIndex = isActor ? actorIndex : worldIndex >= 0 ? worldIndex : exports.FindIndex(e => e.bIsAsset);
        if (mainIndex < 0) mainIndex = exports.FindIndex(e => e.OuterIndex.IsNull());
        var main = mainIndex >= 0 ? exports[mainIndex] : null;
        var mainData = (main as NormalExport)?.Data ?? [];

        string? Str(string name) => (mainData.FirstOrDefault(p => p.Name.Value.Value == name) as StrPropertyData)?.Value?.Value;
        string? Name(string name) => (mainData.FirstOrDefault(p => p.Name.Value.Value == name) as NamePropertyData)?.Value?.Value?.Value;

        var className = main?.GetExportClassType()?.Value?.Value ?? "";
        var label = Str("ActorLabel") ?? Str("LabelOverride");
        if (label != null && IsAutomaticLabel(label, className)) label = null;
        var type = Names.TypeFromClass(className, Name("PlaysetPackagePathName"));
        var verseClass = isActor ? VerseClass(asset, actorIndex) : null;
        if (verseClass != null) type = Names.Humanize(verseClass);

        var package = asset.FolderName?.Value ?? "";
        var assetName = package.Split('/').LastOrDefault() ?? Path.GetFileNameWithoutExtension(path);
        string? map = null;
        var parts = package.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var ea = Array.IndexOf(parts, "__ExternalActors__");
        if (ea >= 0 && ea + 1 < parts.Length) map = parts[ea + 1];
        else if (path.EndsWith(".umap", StringComparison.OrdinalIgnoreCase)) map = assetName;

        var info = new AssetInfo(
            Name: label ?? (isActor ? type : assetName),
            Type: isActor ? type : Names.TypeFromClass(className),
            ClassName: className,
            Label: label,
            PackagePath: package,
            Map: map,
            IsActor: isActor);

        if (isActor)
        {
            if (label != null) settings.Add(new Setting("actor/Label", SettingGroup.Other, "Name", label));
            AddPosition(asset, actorIndex, settings);
        }

        // Devices keep each option twice: in the options list and as a property of the actor.
        // Show them once, under Device options.
        var optionNames = exports.OfType<NormalExport>().SelectMany(e => e.Data).OfType<StructPropertyData>()
            .Where(p => p.Name.Value.Value == "PlayerOptionData").SelectMany(OptionEntries).Select(o => o.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var scriptIndex = isActor ? ScriptObject(asset, actorIndex) : -1;
        var used = new Dictionary<string, int>();
        for (int i = 0; i < exports.Count; i++)
        {
            var export = exports[i];
            var exportName = export.ObjectName.Value.Value;
            var key = ExportKey(asset, i, used);
            bool isMain = i == mainIndex;
            var ownerName = OwnerLabel(asset, i, mainIndex, scriptIndex);
            var owner = ownerName.Length == 0 ? "" : ownerName + " › ";

            if (export is RawExport)
            {
                problems.Add($"{Names.Humanize(exportName)}: couldn't read its settings");
                continue;
            }
            if (export is not NormalExport ne) continue;

            foreach (var p in ne.Data)
            {
                var name = p.Name.Value.Value;
                if (Plumbing.Contains(name) || DelegateTypes.Contains(p.PropertyType.Value)) continue;
                if (isActor && p is ObjectPropertyData op && op.Value.IsExport()) continue; // wiring to its own components
                if (isMain && optionNames.Contains(name)) continue;
                if (isActor && !isMain && IsRootComponent(asset, actorIndex, i) && PositionProps.Contains(name)) continue;
                if (name == "PlayerOptionData" && p is StructPropertyData options)
                {
                    AddDeviceOptions(options, key, settings);
                    continue;
                }
                var verse = name.StartsWith("__verse_", StringComparison.Ordinal) || IsVerseObject(export);
                var group = verse ? SettingGroup.DeviceOptions : SettingGroup.Other;
                // A Verse field holding a device: "Hold Barrier 1 › Saved Actor" reads as "Hold Barrier 1".
                var rowLabel = verse && name == "SavedActor" && ownerName.Length > 0 ? ownerName : owner + Names.Humanize(name);
                Flatten(p, asset, $"{key}/{name}", rowLabel, group, settings, problems);
            }
        }

        return new AssetSnapshot { Info = info, Settings = UniqueKeys(settings), Problems = problems };
    }

    // UEFN labels unnamed actors after their class: "PBWA_W1_RoofS_C2147482698". Those read better as their type.
    static bool IsAutomaticLabel(string label, string className)
    {
        if (className.Length == 0 || !label.StartsWith(className, StringComparison.Ordinal)) return false;
        var rest = label[className.Length..].TrimStart('_');
        return rest.Length == 0 || rest.All(char.IsDigit);
    }

    // Verse devices name their Verse class in ScriptClassPath: "Features-Waves-wave_manager".
    static string? VerseClass(UAsset asset, int actorIndex)
    {
        var path = (asset.Exports[actorIndex] as NormalExport)?.Data
            .FirstOrDefault(p => p.Name.Value.Value == "ScriptClassPath") as StructPropertyData;
        return (path?.Value?.FirstOrDefault(p => p.Name.Value.Value == "AssetName") as NamePropertyData)?.Value?.Value?.Value;
    }

    // The object holding a Verse device's @editable fields.
    static int ScriptObject(UAsset asset, int actorIndex)
    {
        var script = (asset.Exports[actorIndex] as NormalExport)?.Data
            .FirstOrDefault(p => p.Name.Value.Value == "Script") as ObjectPropertyData;
        return script != null && script.Value.IsExport() ? script.Value.Index - 1 : -1;
    }

    static bool IsVerseObject(Export e) =>
        e.ObjectName.Value.Value.StartsWith("__verse_", StringComparison.Ordinal) ||
        (e is NormalExport ne && ne.Data.Any(p => p.Name.Value.Value.StartsWith("__verse_", StringComparison.Ordinal)));

    static bool IsRootComponent(UAsset asset, int actorIndex, int exportIndex)
    {
        var root = (asset.Exports[actorIndex] as NormalExport)?.Data
            .FirstOrDefault(p => p.Name.Value.Value == "RootComponent") as ObjectPropertyData;
        return root != null && root.Value.IsExport() && root.Value.Index - 1 == exportIndex;
    }

    static void AddPosition(UAsset asset, int actorIndex, List<Setting> settings)
    {
        var root = (asset.Exports[actorIndex] as NormalExport)?.Data
            .FirstOrDefault(p => p.Name.Value.Value == "RootComponent") as ObjectPropertyData;
        var data = root != null && root.Value.IsExport() ? (root.Value.ToExport(asset) as NormalExport)?.Data : null;
        FVector? Vec(string n) => (data?.FirstOrDefault(p => p.Name.Value.Value == n) as StructPropertyData)?.Value
            .OfType<VectorPropertyData>().FirstOrDefault()?.Value;
        FRotator? Rot(string n) => (data?.FirstOrDefault(p => p.Name.Value.Value == n) as StructPropertyData)?.Value
            .OfType<RotatorPropertyData>().FirstOrDefault()?.Value;

        // Unreal doesn't store values left at their defaults, so a missing value is 0 (or 1 for scale).
        var loc = Vec("RelativeLocation");
        var rot = Rot("RelativeRotation");
        var scale = Vec("RelativeScale3D");
        settings.Add(new Setting("pos/Location", SettingGroup.Position, "Location",
            loc is { } l ? Values.Vector(l.X, l.Y, l.Z) : Values.Vector(0, 0, 0)));
        settings.Add(new Setting("pos/Rotation", SettingGroup.Position, "Rotation",
            rot is { } r ? Values.Rotation(r.Pitch, r.Yaw, r.Roll) : Values.Rotation(0, 0, 0)));
        settings.Add(new Setting("pos/Scale", SettingGroup.Position, "Scale",
            scale is { } s ? Values.Vector(s.X, s.Y, s.Z) : Values.Vector(1, 1, 1)));
    }

    // UEFN stores a device's options as name/value text pairs, the same ones its details panel shows.
    static void AddDeviceOptions(StructPropertyData options, string key, List<Setting> settings)
    {
        foreach (var (scope, name, data) in OptionEntries(options))
        {
            var label = Names.Humanize(name);
            if (!string.IsNullOrEmpty(scope)) label = $"{Names.Humanize(scope)} › {label}";
            settings.Add(new Setting($"opt/{scope}/{name}", SettingGroup.DeviceOptions, label, Values.Option(data)));
        }
    }

    static IEnumerable<(string? Scope, string Name, string? Data)> OptionEntries(StructPropertyData options)
    {
        var overrides = options.Value?.OfType<ArrayPropertyData>().FirstOrDefault(a => a.Name.Value.Value == "PropertyOverrides");
        if (overrides?.Value == null) yield break;
        foreach (var entry in overrides.Value.OfType<StructPropertyData>())
        {
            string? Field(string n) => (entry.Value?.FirstOrDefault(p => p.Name.Value.Value == n) as StrPropertyData)?.Value?.Value;
            var name = Field("PropertyName");
            if (!string.IsNullOrEmpty(name)) yield return (Field("PropertyScope"), name, Field("PropertyData"));
        }
    }

    static void Flatten(PropertyData p, UAsset asset, string key, string label, SettingGroup group, List<Setting> settings, List<string> problems)
    {
        if (p.ArrayIndex > 0) { key += $"[{p.ArrayIndex}]"; label += $" {p.ArrayIndex + 1}"; }
        switch (p)
        {
            case LensArrayPropertyData { Unreadable: true }:
            case LensSetPropertyData { Unreadable: true }:
            case LensMapPropertyData { Unreadable: true }:
                settings.Add(new Setting(key, group, label, Values.Unreadable));
                problems.Add($"{label}: couldn't read this list");
                return;
            case StructPropertyData s when s.Value != null && !IsLeafStruct(s):
                foreach (var c in s.Value)
                {
                    var childName = c.Name.Value.Value;
                    if (DelegateTypes.Contains(c.PropertyType.Value)) continue;
                    // Single-value structs repeat their own name inside.
                    if (s.Value.Count == 1 && childName == p.Name.Value.Value) { Flatten(c, asset, key, label, group, settings, problems); continue; }
                    Flatten(c, asset, $"{key}.{childName}", $"{label} › {Names.Humanize(childName)}", group, settings, problems);
                }
                return;
            case StructPropertyData s when IsLeafStruct(s):
                Flatten(s.Value[0], asset, key, label, group, settings, problems);
                return;
            case ArrayPropertyData a when a.Value != null: // sets too
                if (a.Value.Length == 0) { settings.Add(new Setting(key, group, label, "(empty)")); return; }
                for (int i = 0; i < a.Value.Length; i++)
                    Flatten(a.Value[i], asset, $"{key}[{i}]", $"{label} {i + 1}", group, settings, problems);
                return;
            case MapPropertyData m when m.Value != null:
                if (m.Value.Count == 0) { settings.Add(new Setting(key, group, label, "(empty)")); return; }
                foreach (var kv in m.Value)
                {
                    var k = Values.Format(kv.Key, asset);
                    Flatten(kv.Value, asset, $"{key}[{k}]", $"{label} › {k}", group, settings, problems);
                }
                return;
        }
        if (p is RawStructPropertyData) problems.Add($"{label}: couldn't read this value");
        settings.Add(new Setting(key, group, label, Values.Format(p, asset)));
    }

    // Structs like Vector and Rotator are stored as a struct holding one typed value.
    static bool IsLeafStruct(StructPropertyData s) =>
        s.Value is { Count: 1 } && s.Value[0] is VectorPropertyData or RotatorPropertyData or GuidPropertyData
            or LinearColorPropertyData or ColorPropertyData or QuatPropertyData or Vector2DPropertyData;

    // "Wave 0 › Wave Step 3 › Spawn Count": the export's outers below the actor
    // (or the Verse script object), so repeated names stay apart.
    static string OwnerLabel(UAsset asset, int index, int mainIndex, int scriptIndex)
    {
        var parts = new List<string>();
        int i = index, guard = 0;
        while (i >= 0 && i != mainIndex && i != scriptIndex && ++guard < 32)
        {
            var e = asset.Exports[i];
            parts.Add(Names.Humanize(e.ObjectName.ToString()));
            i = e.OuterIndex.IsExport() ? e.OuterIndex.Index - 1 : -1;
        }
        parts.Reverse();
        return string.Join(" › ", parts);
    }

    // Map keys that format alike (two widgets of one class, say) would collide; number the repeats.
    static List<Setting> UniqueKeys(List<Setting> settings)
    {
        var seen = new Dictionary<string, int>();
        for (int i = 0; i < settings.Count; i++)
        {
            var key = settings[i].Key;
            var n = seen.GetValueOrDefault(key);
            seen[key] = n + 1;
            if (n > 0) settings[i] = settings[i] with { Key = $"{key}#{n + 1}" };
        }
        return settings;
    }

    // A stable key for an export: its name inside its outers, with a counter for repeats.
    static string ExportKey(UAsset asset, int index, Dictionary<string, int> used)
    {
        var parts = new List<string>();
        var e = asset.Exports[index];
        int guard = 0;
        while (true)
        {
            parts.Add(e.ObjectName.ToString());
            if (!e.OuterIndex.IsExport() || ++guard > 32) break;
            e = e.OuterIndex.ToExport(asset);
        }
        parts.Reverse();
        var key = string.Join(".", parts);
        var n = used.GetValueOrDefault(key);
        used[key] = n + 1;
        return n == 0 ? key : $"{key}#{n + 1}";
    }
}
