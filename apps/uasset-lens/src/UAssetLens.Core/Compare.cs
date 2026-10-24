namespace UAssetLens.Core;

public enum Change { Same, Changed, Added, Removed }

public sealed record DiffRow(string Key, SettingGroup Group, string Label, string? Before, string? After, Change Change, bool Noise);

public sealed record DiffResult(IReadOnlyList<DiffRow> Rows)
{
    public IEnumerable<DiffRow> Changed => Rows.Where(r => r.Change != Change.Same && !r.Noise);
    public int HiddenNoise => Rows.Count(r => r.Change != Change.Same && r.Noise);
}

[Flags]
public enum ChangedBy { None = 0, Theirs = 1, Yours = 2, Both = Theirs | Yours }

public sealed record MergeRow(string Key, SettingGroup Group, string Label, string? Base, string? Theirs, string? Yours, ChangedBy By, bool Noise)
{
    // Both changed it, to different values.
    public bool Conflicts => By == ChangedBy.Both && Theirs != Yours;
}

public sealed record MergeResult(IReadOnlyList<MergeRow> Rows)
{
    public IEnumerable<MergeRow> Changed => Rows.Where(r => r.By != ChangedBy.None && !r.Noise);
    public int HiddenNoise => Rows.Count(r => r.By != ChangedBy.None && r.Noise);

    // What keeping one whole side throws away from the other side.
    public IEnumerable<MergeRow> LostByKeepingYours => Changed.Where(r => r.By.HasFlag(ChangedBy.Theirs) && r.Theirs != r.Yours);
    public IEnumerable<MergeRow> LostByKeepingTheirs => Changed.Where(r => r.By.HasFlag(ChangedBy.Yours) && r.Theirs != r.Yours);
}

public static class Compare
{
    // Fields UEFN rewrites without the user changing them, found by comparing 152 changed actor files
    // from a real project's Plastic history (see the README). Hidden from the change list and counted in the footer.
    static readonly string[] NoiseProperties =
    [
        "CachedMaxDrawDistance",      // recomputed from the bounds whenever the actor moves or scales
        "CullDistance",               // the actor's copy of the same distance
        "bCanEverAffectNavigation",   // navigation flags UEFN updates rewrite
        "bUseAutoNavmeshFlags",
        "PIEDuplicateHelper",         // a level's play-in-editor helper, renumbered on save
    ];

    static readonly string[] NoiseFragments =
    [
        "/BodyInstance.CollisionResponses",        // collision channel lists, reordered by UEFN updates
        ".CreativeEditOnly",                       // editor-only helper meshes (patrol path beams, range spheres)
        ".EditorOnly",
        "/EditorViews",                            // the level's saved editor camera positions
        "/LastSavedReleaseVersion.CachedVersion",  // the Fortnite version that last saved the file
    ];

    public static bool IsNoise(string key)
    {
        var slash = key.IndexOf('/');
        var prop = slash >= 0 ? key[(slash + 1)..] : key;
        var dot = prop.LastIndexOfAny(['.', '/']);
        var last = dot >= 0 ? prop[(dot + 1)..] : prop;
        var bracket = last.IndexOf('[');
        if (bracket >= 0) last = last[..bracket];
        return NoiseProperties.Contains(last) || NoiseFragments.Any(f => key.Contains(f, StringComparison.Ordinal));
    }

    // A setting one version stores and the other doesn't was left at its default in the other.
    public const string DefaultValue = "Default";

    public static DiffResult Diff(AssetSnapshot before, AssetSnapshot after)
    {
        var rows = new List<DiffRow>();
        var b = before.Settings.ToDictionary(s => s.Key);
        var a = after.Settings.ToDictionary(s => s.Key);
        foreach (var s in Union(before.Settings, after.Settings))
        {
            b.TryGetValue(s.Key, out var old);
            a.TryGetValue(s.Key, out var @new);
            Change change;
            string? oldV = old?.Value, newV = @new?.Value;
            if (before.IsMissing) change = Change.Added;
            else if (after.IsMissing) change = Change.Removed;
            else
            {
                oldV ??= DefaultValue;
                newV ??= DefaultValue;
                change = oldV == newV ? Change.Same : Change.Changed;
            }
            rows.Add(new DiffRow(s.Key, s.Group, s.Label, oldV, newV, change, IsNoise(s.Key)));
        }
        return new DiffResult(Order(rows, r => r.Group));
    }

    public static MergeResult Merge(AssetSnapshot @base, AssetSnapshot theirs, AssetSnapshot yours)
    {
        var rows = new List<MergeRow>();
        var bd = @base.Settings.ToDictionary(s => s.Key);
        var td = theirs.Settings.ToDictionary(s => s.Key);
        var yd = yours.Settings.ToDictionary(s => s.Key);
        foreach (var s in Union(@base.Settings, theirs.Settings, yours.Settings))
        {
            string V(Dictionary<string, Setting> d, AssetSnapshot snap) =>
                d.TryGetValue(s.Key, out var x) ? x.Value : snap.IsMissing ? "" : DefaultValue;
            var bv = V(bd, @base);
            var tv = V(td, theirs);
            var yv = V(yd, yours);
            var by = (tv != bv ? ChangedBy.Theirs : ChangedBy.None) | (yv != bv ? ChangedBy.Yours : ChangedBy.None);
            rows.Add(new MergeRow(s.Key, s.Group, s.Label, bv, tv, yv, by, IsNoise(s.Key)));
        }
        return new MergeResult(Order(rows, r => r.Group));
    }

    // Every setting once, in the order the versions list them, so new ones appear next to their neighbours.
    static List<Setting> Union(params AssetSnapshot[] snaps) => Union(snaps.Select(s => s.Settings).ToArray());

    static List<Setting> Union(params IReadOnlyList<Setting>[] lists)
    {
        var seen = new HashSet<string>();
        var result = new List<Setting>();
        foreach (var list in lists.Reverse()) // newest version's labels and order first
            foreach (var s in list)
                if (seen.Add(s.Key)) result.Add(s);
        return result;
    }

    static List<T> Order<T>(List<T> rows, Func<T, SettingGroup> group) =>
        rows.Select((r, i) => (r, i)).OrderBy(x => group(x.r)).ThenBy(x => x.i).Select(x => x.r).ToList();

    // "Crosshair Station 1: Trigger Delay 0 to 1, Visible in Game Off to On, moved"
    public static string Summary(AssetSnapshot after, DiffResult diff, AssetSnapshot? before = null)
    {
        var name = (after.IsMissing ? before?.Info.Name : after.Info.Name) ?? "";
        if (before?.IsMissing == true) return $"{name}: added";
        if (after.IsMissing) return $"{name}: deleted";
        var parts = new List<string>();
        bool moved = false;
        foreach (var r in diff.Changed)
        {
            if (r.Group == SettingGroup.Position) { moved = true; continue; }
            parts.Add(r.Key == "actor/Label" ? $"renamed from {r.Before}" : $"{r.Label} {r.Before} to {r.After}");
        }
        if (moved) parts.Add("moved");
        return parts.Count == 0 ? $"{name}: no setting changes" : $"{name}: {string.Join(", ", parts)}";
    }
}
