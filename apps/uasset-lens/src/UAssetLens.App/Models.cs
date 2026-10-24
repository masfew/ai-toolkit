using System.Windows;
using System.Windows.Media;
using UAssetLens.Core;

namespace UAssetLens.App;

// Colours from the mockups, chosen to sit next to Plastic's dark theme.
public static class Palette
{
    static SolidColorBrush B(string hex, byte alpha = 255)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        c.A = alpha;
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
    public static readonly SolidColorBrush Fg = B("#d9dbde"), Muted = B("#8d9298"), White = B("#ffffff");
    public static readonly SolidColorBrush Red = B("#e2706a"), Green = B("#5fbf5f"), Orange = B("#e8933c");
    public static readonly SolidColorBrush ChangedBg = B("#4b8ef0", 36), ConflictRowBg = B("#e8933c", 31), ConflictCellBg = B("#e8933c", 56);
    public static readonly SolidColorBrush TheirsTagBg = B("#4b8ef0", 51), TheirsTagFg = B("#9fc2ff");
    public static readonly SolidColorBrush YoursTagBg = B("#5fbf5f", 46), YoursTagFg = B("#9ee39e");
    public static readonly SolidColorBrush BothTagBg = B("#e8933c", 64), BothTagFg = B("#ffc58a");
    public static readonly SolidColorBrush None = Brushes.Transparent;
}

public abstract class RowItem;

public sealed class GroupItem(string title) : RowItem
{
    public string Title { get; } = title.ToUpperInvariant();
}

public sealed class Cell(string text, Brush foreground, Brush background)
{
    public string Text { get; } = text;
    public Brush Foreground { get; } = foreground;
    public Brush Background { get; } = background;
}

public sealed class DiffItem(string label, Cell before, Cell after) : RowItem
{
    public string Label { get; } = label;
    public Cell Before { get; } = before;
    public Cell After { get; } = after;
}

public sealed class MergeItem(string label, string tag, Brush tagFg, Brush tagBg, Brush rowBg, Cell @base, Cell theirs, Cell yours) : RowItem
{
    public string Label { get; } = label;
    public string Tag { get; } = tag;
    public Visibility TagVisibility => Tag.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Brush TagForeground { get; } = tagFg;
    public Brush TagBackground { get; } = tagBg;
    public Brush RowBackground { get; } = rowBg;
    public Cell Base { get; } = @base;
    public Cell Theirs { get; } = theirs;
    public Cell Yours { get; } = yours;
}

public sealed class DiffModel
{
    public AssetSnapshot Before { get; }
    public AssetSnapshot After { get; }
    public DiffResult Result { get; }
    public AssetSnapshot Shown => After.IsMissing ? Before : After;
    public string Title { get; }
    public string StoredPath { get; }
    public string Versions { get; }

    public DiffModel(AssetSnapshot before, AssetSnapshot after, string? beforeName, string? afterName)
    {
        Before = before;
        After = after;
        Result = Compare.Diff(before, after);
        StoredPath = VersionNames.StoredPath(afterName, beforeName) ?? Shown.Info.PackagePath;
        if (before.IsMissing && beforeName == null)
        {
            Title = "Show";
            Versions = VersionNames.Short(afterName, "");
        }
        else
        {
            Title = "Diff";
            Versions = before.IsMissing ? $"Added in {VersionNames.Short(afterName, "the new version")}"
                : after.IsMissing ? $"Deleted in {VersionNames.Short(afterName, "the new version")}"
                : $"{VersionNames.Short(beforeName, "before")}  →  {VersionNames.Short(afterName, "after")}";
        }
    }

    public string ChangedText => Before.IsMissing ? $"{Result.Rows.Count} settings" : $"{Result.Changed.Count()} settings changed";

    public string FooterText => Result.HiddenNoise switch
    {
        0 => "",
        1 => "1 field UEFN rewrites on every save is hidden",
        var n => $"{n} fields UEFN rewrites on every save are hidden",
    };

    public string Summary => Compare.Summary(After, Result, Before);

    public List<RowItem> Rows(bool showUnchanged)
    {
        var list = new List<RowItem>();
        SettingGroup? group = null;
        bool oneSided = Before.IsMissing || After.IsMissing;
        foreach (var r in Result.Rows)
        {
            if (!oneSided && !showUnchanged && (r.Change == Change.Same || r.Noise)) continue;
            if (r.Group != group) { group = r.Group; list.Add(new GroupItem(TextReport.GroupName(r.Group))); }
            Cell before, after;
            if (r.Change == Change.Changed && !r.Noise)
            {
                before = new Cell(r.Before ?? "", Palette.Red, Palette.None);
                after = new Cell(r.After ?? "", Palette.Green, Palette.None);
            }
            else
            {
                before = new Cell(r.Before ?? "", oneSided ? Palette.Fg : Palette.Muted, Palette.None);
                after = new Cell(r.After ?? "", oneSided ? Palette.Fg : Palette.Muted, Palette.None);
            }
            list.Add(new DiffItem(r.Label, before, after));
        }
        return list;
    }
}

public sealed class MergeModel
{
    public AssetSnapshot Base { get; }
    public AssetSnapshot Theirs { get; }
    public AssetSnapshot Yours { get; }
    public MergeResult Result { get; }
    public string TheirsFile { get; }
    public string YoursFile { get; }
    public string OutputFile { get; }
    public string StoredPath { get; }
    public string Versions { get; }
    public string TheirsLabel { get; }
    public AssetSnapshot Shown => Yours.IsMissing ? Theirs : Yours;

    public MergeModel(AssetSnapshot @base, AssetSnapshot theirs, AssetSnapshot yours, string theirsFile, string yoursFile, string outputFile,
        string? baseName, string? theirsName, string? yoursName)
    {
        Base = @base;
        Theirs = theirs;
        Yours = yours;
        Result = Compare.Merge(@base, theirs, yours);
        TheirsFile = theirsFile;
        YoursFile = yoursFile;
        OutputFile = outputFile;
        StoredPath = VersionNames.StoredPath(yoursName, theirsName, baseName) ?? Shown.Info.PackagePath;
        var theirsShort = VersionNames.Short(theirsName, "theirs");
        TheirsLabel = VersionNames.Branch(theirsName) is { } branch ? $"Theirs ({branch})" : "Theirs";
        Versions = $"Base {VersionNames.Short(baseName, "")}  ·  Theirs {theirsShort}  ·  Yours {VersionNames.Short(yoursName, "your workspace")}";
    }

    public int ConflictCount => Result.Changed.Count(r => r.Conflicts);

    public string FooterText => ConflictCount switch
    {
        0 => "No setting changed on both sides",
        1 => "1 setting changed on both sides",
        var n => $"{n} settings changed on both sides",
    };

    // "Keep yours drops their change to Item. Keep theirs drops your change to Respawn Time."
    public string Warning
    {
        get
        {
            if (Theirs.IsMissing || Yours.IsMissing)
                return Theirs.IsMissing ? "Theirs deleted this file. Keep theirs deletes it here too." : "You deleted this file. Keep yours keeps it deleted.";
            string Names(IEnumerable<MergeRow> rows)
            {
                var names = rows.Select(r => r.Group == SettingGroup.Position ? "position" : r.Label).Distinct().ToList();
                const int shown = 5;
                return names.Count switch
                {
                    0 => "",
                    1 => names[0],
                    <= shown + 1 => string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1],
                    _ => string.Join(", ", names.Take(shown)) + $" and {names.Count - shown} more",
                };
            }
            var lostYours = Names(Result.LostByKeepingYours);
            var lostTheirs = Names(Result.LostByKeepingTheirs);
            var parts = new List<string>
            {
                lostYours.Length > 0 ? $"Keep yours drops their change to {lostYours}." : "Keep yours drops nothing of theirs.",
                lostTheirs.Length > 0 ? $"Keep theirs drops your change to {lostTheirs}." : "Keep theirs drops nothing of yours.",
            };
            return string.Join(" ", parts);
        }
    }

    public List<RowItem> Rows(bool showUnchanged)
    {
        var list = new List<RowItem>();
        SettingGroup? group = null;
        foreach (var r in Result.Rows)
        {
            if (!showUnchanged && (r.By == ChangedBy.None || r.Noise)) continue;
            if (r.Group != group) { group = r.Group; list.Add(new GroupItem(TextReport.GroupName(r.Group))); }
            var conflict = r.Conflicts && !r.Noise;
            Cell C(string? v, bool changed) => new(v ?? "",
                changed ? (conflict ? Palette.White : Palette.Fg) : Palette.Muted,
                changed ? (conflict ? Palette.ConflictCellBg : Palette.ChangedBg) : Palette.None);
            var (tag, fg, bg) = r.Noise ? ("", Palette.Fg, Palette.None) : r.By switch
            {
                ChangedBy.Theirs => ("theirs", Palette.TheirsTagFg, Palette.TheirsTagBg),
                ChangedBy.Yours => ("yours", Palette.YoursTagFg, Palette.YoursTagBg),
                ChangedBy.Both => (r.Conflicts ? "both" : "both, same", Palette.BothTagFg, Palette.BothTagBg),
                _ => ("", Palette.Fg, Palette.None),
            };
            list.Add(new MergeItem(r.Label, tag, fg, bg, conflict ? Palette.ConflictRowBg : Palette.None,
                C(r.Base, false),
                C(r.Theirs, r.By.HasFlag(ChangedBy.Theirs) && !r.Noise),
                C(r.Yours, r.By.HasFlag(ChangedBy.Yours) && !r.Noise)));
        }
        return list;
    }
}
