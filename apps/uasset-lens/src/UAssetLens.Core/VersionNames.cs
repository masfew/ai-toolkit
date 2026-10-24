namespace UAssetLens.Core;

// Plastic's names for the versions it compares, made short enough for the header.
public static class VersionNames
{
    // Diffs: "Plugins/x/__ExternalActors__/map/0/02/K3J8D2M5Q1W7E4R9T6Y0U2.uasset#cs:979" -> "cs:979".
    // Merges: "MyProject\...\P5N2X8C4V1B7Z3L9H6G0F2.uasset@cs:8@/main/feature" -> "cs:8 on /main/feature".
    public static string Short(string? name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name)) return fallback;
        var hash = name.LastIndexOf('#');
        if (hash >= 0 && hash + 1 < name.Length) return name[(hash + 1)..];
        var parts = name.Split('@');
        if (parts.Length >= 3) return $"{parts[1]} on {parts[2]}";
        if (parts.Length == 2) return parts[1];
        if (File.Exists(name) || Path.IsPathFullyQualified(name)) return "your workspace";
        return name;
    }

    // The branch's own name for a column header: "...@cs:8@/main/feature" -> "feature".
    public static string? Branch(string? name)
    {
        var parts = name?.Split('@');
        if (parts is not { Length: >= 3 }) return null;
        var branch = parts[2].TrimEnd('/');
        return branch[(branch.LastIndexOf('/') + 1)..];
    }

    // The repository path part of a Plastic name, when it has one.
    public static string? StoredPath(params string?[] names)
    {
        foreach (var n in names)
        {
            if (string.IsNullOrWhiteSpace(n)) continue;
            var p = n;
            var hash = p.LastIndexOf('#');
            if (hash >= 0) p = p[..hash];
            if (p.StartsWith("rev:", StringComparison.OrdinalIgnoreCase)) p = p[4..];
            var at = p.IndexOf('@');
            if (at > 0) p = p[..at];
            if (p.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".umap", StringComparison.OrdinalIgnoreCase))
            {
                var i = p.IndexOf("__ExternalActors__", StringComparison.Ordinal);
                return (i >= 0 ? p[i..] : p).Replace('\\', '/');
            }
        }
        return null;
    }
}
