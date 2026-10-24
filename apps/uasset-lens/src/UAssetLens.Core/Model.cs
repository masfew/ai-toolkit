namespace UAssetLens.Core;

public enum SettingGroup { DeviceOptions, Position, Other }

// One readable setting of one version. Key matches the same setting across versions.
public sealed record Setting(string Key, SettingGroup Group, string Label, string Value);

public sealed record AssetInfo(
    string Name,          // label from UEFN, or a name made from the type
    string Type,          // plain words: "Trigger Device", "Wood Roof"
    string ClassName,     // stored class: "Device_Trigger_V2_C"
    string? Label,        // the actor label, when the file has one
    string PackagePath,   // "/mymap/__ExternalActors__/mymap/0/02/K3J8D2M5Q1W7E4R9T6Y0U2"
    string? Map,          // "mymap" for actor files
    bool IsActor);

public sealed class AssetSnapshot
{
    public required AssetInfo Info { get; init; }
    public required IReadOnlyList<Setting> Settings { get; init; }
    // Parts of the file that couldn't be read; the rest is still shown.
    public IReadOnlyList<string> Problems { get; init; } = [];
    // Set when the file couldn't be read at all.
    public string? Error { get; init; }

    public static AssetSnapshot Failed(string path, string error) => new()
    {
        Info = new AssetInfo(Path.GetFileNameWithoutExtension(path), "Unreadable file", "", null, "", null, false),
        Settings = [],
        Error = error,
    };

    // A side that doesn't exist, such as the old version of an added file.
    public static AssetSnapshot Missing { get; } = new()
    {
        Info = new AssetInfo("", "", "", null, "", null, false),
        Settings = [],
    };

    public bool IsMissing => ReferenceEquals(this, Missing);
}
