namespace UAssetLens.App;

// UAssetLens.exe diff  --before FILE --after FILE [--before-name NAME] [--after-name NAME]
// UAssetLens.exe merge --base FILE --theirs FILE --yours FILE --out FILE [--base-name ...] [--theirs-name ...] [--yours-name ...]
// UAssetLens.exe show FILE
// --text prints the report instead of opening a window; --all includes unchanged settings.
public sealed class CommandLine
{
    public string Mode { get; private init; } = "";
    public Dictionary<string, string> Options { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Positional { get; } = [];
    public bool Text => Options.ContainsKey("text");
    public bool All => Options.ContainsKey("all");

    static readonly HashSet<string> Flags = new(StringComparer.OrdinalIgnoreCase) { "text", "all" };

    public string? this[string name] => Options.TryGetValue(name, out var v) ? v : null;

    public static CommandLine Parse(string[] args)
    {
        var cl = new CommandLine { Mode = args.Length > 0 ? args[0].ToLowerInvariant() : "" };
        for (int i = 1; i < args.Length; i++)
        {
            var a = args[i];
            if (a.StartsWith("--", StringComparison.Ordinal))
            {
                var name = a[2..];
                if (Flags.Contains(name)) cl.Options[name] = "";
                else if (i + 1 < args.Length) cl.Options[name] = args[++i];
                else throw new UsageException($"{a} needs a value");
            }
            else cl.Positional.Add(a);
        }
        // Positional forms: diff BEFORE AFTER, merge BASE THEIRS YOURS OUT, show FILE.
        string[] order = cl.Mode switch
        {
            "diff" => ["before", "after"],
            "merge" => ["base", "theirs", "yours", "out"],
            "show" => ["file"],
            _ => throw new UsageException(cl.Mode.Length == 0 ? "No command given" : $"Unknown command: {cl.Mode}"),
        };
        for (int i = 0; i < cl.Positional.Count && i < order.Length; i++) cl.Options.TryAdd(order[i], cl.Positional[i]);
        foreach (var required in order)
            if (cl[required] is not { Length: > 0 }) throw new UsageException($"{cl.Mode} needs --{required}");
        return cl;
    }

    public const string Usage =
        "UAsset Lens: readable diffs and conflict choices for UEFN .uasset and .umap files.\n\n" +
        "  UAssetLens diff  --before FILE --after FILE [--before-name NAME] [--after-name NAME]\n" +
        "  UAssetLens merge --base FILE --theirs FILE --yours FILE --out FILE\n" +
        "                   [--base-name NAME] [--theirs-name NAME] [--yours-name NAME]\n" +
        "  UAssetLens show  FILE\n\n" +
        "  --text  print the report instead of opening a window\n" +
        "  --all   include settings that didn't change\n\n" +
        "merge copies the side you keep to --out and exits 0; Cancel exits 1 so Plastic leaves the conflict open.";
}

public sealed class UsageException(string message) : Exception(message);
