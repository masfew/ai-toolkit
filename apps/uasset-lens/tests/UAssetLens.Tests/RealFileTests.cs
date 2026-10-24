using UAssetLens.Core;

namespace UAssetLens.Tests;

// Runs over every file in the UASSETLENS_FIXTURES folder. A Fortnite update that changes the file format shows up
// here first, as a file that stops reading or a report that changes.
public class RealFileTests
{
    public static TheoryData<string> ActorFiles => new(Fixture.Files("*.uasset"));
    public static TheoryData<string> LevelFiles => new(Fixture.Files("*.umap"));
    public static TheoryData<string> AllFiles => new(Fixture.Files("*.uasset").Concat(Fixture.Files("*.umap")));
    public static TheoryData<string> CaseNames => new(Fixture.Cases().Select(c => c.Expected));

    [FixtureFact]
    public void Folder_has_files() =>
        Assert.True(Fixture.Files("*.uasset").Any() || Fixture.Files("*.umap").Any(),
            $"{Fixture.Dir} has no .uasset or .umap files.");

    [FixtureTheory]
    [MemberData(nameof(ActorFiles))]
    public void Actor_files_read_completely(string file)
    {
        var s = Fixture.Read(file);
        Assert.Null(s.Error);
        Assert.Empty(s.Problems);
        Assert.True(s.Info.IsActor);
        Assert.Contains(s.Settings, x => x.Group == SettingGroup.Position && x.Label == "Location");
    }

    [FixtureTheory]
    [MemberData(nameof(LevelFiles))]
    public void Level_files_read_their_world(string file)
    {
        var s = Fixture.Read(file);
        Assert.Null(s.Error);
        Assert.False(s.Info.IsActor);
        Assert.Equal("World", s.Info.ClassName);
        // Scene graph entities aren't readable yet; they're listed rather than failing the file.
        Assert.All(s.Problems, p => Assert.Contains("couldn't read", p));
    }

    [FixtureTheory]
    [MemberData(nameof(AllFiles))]
    public void Show_output_matches_expected(string file) =>
        Fixture.MatchesGolden(Path.GetFileNameWithoutExtension(file) + ".show", TextReport.Show(Fixture.Read(file)));

    [FixtureTheory]
    [MemberData(nameof(CaseNames))]
    public void Diff_and_merge_output_matches_expected(string expected)
    {
        var c = Fixture.Cases().Single(x => x.Expected == expected);
        if (c.Diff is [var beforeFile, var afterFile])
        {
            var before = Fixture.Read(beforeFile);
            var after = Fixture.Read(afterFile);
            Fixture.MatchesGolden(expected, TextReport.Diff(before, after, Compare.Diff(before, after)));
        }
        else if (c.Merge is [var baseFile, var theirsFile, var yoursFile])
        {
            var yours = Fixture.Read(yoursFile);
            var merge = Compare.Merge(Fixture.Read(baseFile), Fixture.Read(theirsFile), yours);
            Fixture.MatchesGolden(expected, TextReport.Merge(yours, merge));
        }
        else
        {
            Assert.Fail($"cases.json entry \"{expected}\" needs a diff of 2 files or a merge of 3.");
        }
    }
}
