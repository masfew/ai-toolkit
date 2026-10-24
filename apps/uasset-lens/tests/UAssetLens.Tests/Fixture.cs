using System.Text.Json;
using UAssetLens.Core;

namespace UAssetLens.Tests;

// Real UEFN files to test against, kept outside the repo: the folder UASSETLENS_FIXTURES names (see the README,
// "Testing with your own files"). Tests marked [FixtureFact] or [FixtureTheory] are skipped when it isn't set.
static class Fixture
{
    public const string Variable = "UASSETLENS_FIXTURES";
    public const string SkipReason = "Set UASSETLENS_FIXTURES to a folder of real UEFN files to run this test.";

    public static string? Dir
    {
        get
        {
            var dir = Environment.GetEnvironmentVariable(Variable);
            return string.IsNullOrWhiteSpace(dir) ? null : dir;
        }
    }

    public static string Path_(string name) => Path.Combine(Dir!, name);

    public static AssetSnapshot Read(string name) => SnapshotReader.Read(Path_(name));

    public static IEnumerable<string> Files(string pattern) =>
        Dir is null ? [] : Directory.GetFiles(Dir, pattern).Select(f => Path.GetFileName(f)!).Order();

    // cases.json in the folder: [{"diff": ["before.uasset", "after.uasset"], "expected": "name"},
    // {"merge": ["base.uasset", "theirs.uasset", "yours.uasset"], "expected": "name"}].
    public static IEnumerable<Case> Cases()
    {
        if (Dir is null || !File.Exists(Path_("cases.json"))) return [];
        return JsonSerializer.Deserialize<Case[]>(File.ReadAllText(Path_("cases.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    public record Case(string[]? Diff, string[]? Merge, string Expected);

    // Compares against expected/<name>.txt in the folder. Set UPDATE_GOLDEN=1 to rewrite the expected files
    // after a deliberate change, then review the diff.
    public static void MatchesGolden(string name, string actual)
    {
        var path = Path.Combine(Dir!, "expected", name + ".txt");
        actual = actual.Replace("\r\n", "\n");
        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
            return;
        }
        Assert.True(File.Exists(path), $"Missing {path}. Run the tests with UPDATE_GOLDEN=1 to create it.");
        var expected = File.ReadAllText(path).Replace("\r\n", "\n");
        Assert.Equal(expected, actual);
    }
}

public sealed class FixtureFactAttribute : FactAttribute
{
    public FixtureFactAttribute()
    {
        if (Fixture.Dir is null) Skip = Fixture.SkipReason;
    }
}

public sealed class FixtureTheoryAttribute : TheoryAttribute
{
    public FixtureTheoryAttribute()
    {
        if (Fixture.Dir is null) Skip = Fixture.SkipReason;
    }
}
