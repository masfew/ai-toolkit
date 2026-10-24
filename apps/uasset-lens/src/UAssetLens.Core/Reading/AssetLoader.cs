using System.Reflection;
using UAssetAPI;
using UAssetAPI.UnrealTypes;

namespace UAssetLens.Core.Reading;

public static class AssetLoader
{
    // Fortnite 42.30 writes package version IMPORT_TYPE_HIERARCHIES, the newest UAssetAPI 1.1.0 knows.
    // Editor saves are versioned, so this only matters for parts the file doesn't describe itself.
    public const EngineVersion Engine = EngineVersion.VER_UE5_7;

    public static UAsset Load(string path) => Load(ReadShared(path), path);

    public static UAsset Load(byte[] bytes, string? path = null)
    {
        var asset = new UAsset(Engine);
        if (path != null) asset.FilePath = path;
        asset.Read(new AssetBinaryReader(new MemoryStream(SkipUnneededSections(bytes)), asset));
        return asset;
    }

    // UEFN keeps the level's files open, so read without asking for exclusive access.
    public static byte[] ReadShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var ms = new MemoryStream();
        fs.CopyTo(ms);
        return ms.ToArray();
    }

    static readonly BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // Three header sections hold nothing the viewer shows, and UAssetAPI 1.1.0 can't read them in
    // UEFN files: gatherable text (its localisation metadata isn't implemented), editor metadata (read
    // from the wrong place once gatherable text is skipped; fixed upstream after 1.1.0) and import type
    // hierarchies (a newer layout). Blank their entries in the package summary so UAssetAPI skips them.
    // Each entry is found by its values plus a known neighbour, and only blanked on a single match.
    static readonly (string[] Blank, string[] Anchor)[] Sections =
    [
        (["GatherableTextDataCount", "GatherableTextDataOffset"], []),
        (["MetaDataOffset"], ["DependsOffset"]),
        (["ImportTypeHierarchiesCount", "ImportTypeHierarchiesOffset"], []),
    ];

    internal static byte[] SkipUnneededSections(byte[] bytes)
    {
        var probe = new UAsset(Engine);
        var reader = new AssetBinaryReader(new MemoryStream(bytes), probe);
        typeof(UAsset).GetMethod("ReadHeader", AnyInstance)!.Invoke(probe, [reader]);
        var headerEnd = (int)reader.BaseStream.Position;
        int Field(string name) => (int)typeof(UAsset).GetField(name, AnyInstance)!.GetValue(probe)!;

        var copy = (byte[])bytes.Clone();
        foreach (var (blank, anchor) in Sections)
        {
            if (blank.All(f => Field(f) == 0)) continue;
            var pattern = blank.Concat(anchor).Select(Field).SelectMany(BitConverter.GetBytes).ToArray();
            int hit = -1, hits = 0;
            for (int i = 0; i + pattern.Length <= headerEnd; i++)
                if (copy.AsSpan(i, pattern.Length).SequenceEqual(pattern)) { hit = i; hits++; }
            if (hits == 1) Array.Clear(copy, hit, blank.Length * sizeof(int));
        }
        return copy;
    }
}
