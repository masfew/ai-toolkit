using System.Text;

namespace UAssetLens.Core;

// Plain-text versions of the two windows, for the command line and the golden-file tests.
public static class TextReport
{
    public static string GroupName(SettingGroup g) => g switch
    {
        SettingGroup.DeviceOptions => "Device options",
        SettingGroup.Position => "Position",
        _ => "Other settings",
    };

    public static string Header(AssetSnapshot s)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{s.Info.Name}  [{s.Info.Type}]");
        if (s.Info.PackagePath.Length > 0) sb.AppendLine(s.Info.PackagePath);
        if (s.Error != null) sb.AppendLine($"Couldn't read this file: {s.Error}");
        foreach (var p in s.Problems) sb.AppendLine($"Not read: {p}");
        return sb.ToString();
    }

    public static string Show(AssetSnapshot s)
    {
        var sb = new StringBuilder(Header(s));
        SettingGroup? group = null;
        foreach (var row in s.Settings.OrderBy(r => r.Group))
        {
            if (row.Group != group) { group = row.Group; sb.AppendLine($"-- {GroupName(row.Group)}"); }
            sb.AppendLine($"{row.Label} = {row.Value}{(Compare.IsNoise(row.Key) ? "   (rewritten on save)" : "")}");
        }
        return sb.ToString();
    }

    public static string Diff(AssetSnapshot before, AssetSnapshot after, DiffResult diff, bool all = false)
    {
        var sb = new StringBuilder(Header(after.IsMissing ? before : after));
        var rows = diff.Rows.Where(r => all || (r.Change != Change.Same && !r.Noise)).ToList();
        sb.AppendLine($"{diff.Changed.Count()} settings changed");
        SettingGroup? group = null;
        foreach (var r in rows)
        {
            if (r.Group != group) { group = r.Group; sb.AppendLine($"-- {GroupName(r.Group)}"); }
            var mark = r.Change switch { Change.Same => " ", Change.Added => "+", Change.Removed => "-", _ => "*" };
            sb.AppendLine($"{mark} {r.Label}: {r.Before ?? ""} -> {r.After ?? ""}{(r.Noise ? "   (rewritten on save)" : "")}");
        }
        if (diff.HiddenNoise > 0) sb.AppendLine($"{diff.HiddenNoise} fields UEFN rewrites on every save are hidden");
        sb.AppendLine($"Summary: {Compare.Summary(after, diff, before)}");
        return sb.ToString();
    }

    public static string Merge(AssetSnapshot yours, MergeResult merge)
    {
        var sb = new StringBuilder(Header(yours));
        SettingGroup? group = null;
        foreach (var r in merge.Changed)
        {
            if (r.Group != group) { group = r.Group; sb.AppendLine($"-- {GroupName(r.Group)}"); }
            var who = r.By switch { ChangedBy.Theirs => "theirs", ChangedBy.Yours => "yours", _ => r.Conflicts ? "both" : "both, same" };
            sb.AppendLine($"[{who}] {r.Label}: base {r.Base} | theirs {r.Theirs} | yours {r.Yours}");
        }
        sb.AppendLine($"Keep yours drops: {List(merge.LostByKeepingYours)}");
        sb.AppendLine($"Keep theirs drops: {List(merge.LostByKeepingTheirs)}");
        if (merge.HiddenNoise > 0) sb.AppendLine($"{merge.HiddenNoise} fields UEFN rewrites on every save are hidden");
        return sb.ToString();
    }

    static string List(IEnumerable<MergeRow> rows)
    {
        var labels = rows.Select(r => r.Group == SettingGroup.Position ? "position" : r.Label).Distinct().ToList();
        return labels.Count == 0 ? "nothing" : string.Join(", ", labels);
    }
}
