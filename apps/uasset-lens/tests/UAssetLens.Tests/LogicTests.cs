using UAssetLens.Core;

namespace UAssetLens.Tests;

public class NamesTests
{
    [Theory]
    [InlineData("TriggerDelay", "Trigger Delay")]
    [InlineData("bReceiveDamageWhenInvisible", "Receive Damage When Invisible")]
    [InlineData("TimesCanTrigger", "Times Can Trigger")]
    [InlineData("__verse_0x92185E59_DefaultFallbackWeapon", "Default Fallback Weapon")]
    [InlineData("Features-Waves-wave_step_3", "Wave Step 3")]
    [InlineData("HUDController", "HUD Controller")]
    [InlineData("StaticMeshComponent0", "Static Mesh Component 0")]
    public void Humanize(string stored, string shown) => Assert.Equal(shown, Names.Humanize(stored));

    [Theory]
    [InlineData("Device_Trigger_V2_C", null, "Trigger Device")]
    [InlineData("Device_Trigger_V2_C", "/CreativeCoreDevices/SetupAssets/PID_Device_Trigger", "Trigger Device")]
    [InlineData("BP_ItemContainer_C", "/CP_Devices/PID_CP_Devices_ItemContainer_V2", "Item Container Device")]
    [InlineData("Device_Barrier_V2_Placed_C", null, "Barrier Device")]
    [InlineData("PBWA_W1_RoofS_C", null, "Wood Roof")]
    [InlineData("PBWA_S1_Solid_C", null, "Brick Wall")]
    [InlineData("PBWA_M1_Floor_C", null, "Metal Floor")]
    [InlineData("FortStaticMeshActor", null, "Static Mesh")]
    public void Type_from_class(string className, string? playset, string type) =>
        Assert.Equal(type, Names.TypeFromClass(className, playset));
}

public class ValuesTests
{
    [Theory]
    [InlineData("True", "On")]
    [InlineData("False", "Off")]
    [InlineData("0.000000", "0")]
    [InlineData("1.500000", "1.5")]
    [InlineData("", "(empty)")]
    [InlineData(null, "(empty)")]
    [InlineData("(TeamType=Any,TeamIndex=1)", "Team Type Any, Team Index 1")]
    [InlineData("/Game/Items/WID_Shotgun_Pump.WID_Shotgun_Pump", "WID_Shotgun_Pump")]
    [InlineData("NSLOCTEXT(\"\", \"5A1B\", \"Exit\")", "Exit")]
    [InlineData("Last", "Last")]
    public void Device_option_values(string? raw, string shown) => Assert.Equal(shown, Values.Option(raw));

    [Fact]
    public void Positions_read_like_the_mockup()
    {
        Assert.Equal("X 1200 · Y -340 · Z 64", Values.Vector(1200, -340, 64.0000001));
        Assert.Equal("0° · 90° · 0°", Values.Rotation(0, 90, 0));
    }
}

public class CompareTests
{
    static AssetSnapshot Snap(params (string Key, string Value)[] settings) => new()
    {
        Info = new AssetInfo("Sentry", "Sentry Device", "Device_Sentry_C", "Sentry", "", null, true),
        Settings = settings.Select(s => new Setting(s.Key, s.Key.StartsWith("pos/") ? SettingGroup.Position : SettingGroup.DeviceOptions,
            s.Key.Split('/').Last(), s.Value)).ToList(),
    };

    [Fact]
    public void Diff_marks_changes_and_defaults()
    {
        var diff = Compare.Diff(Snap(("opt/Item", "Rare"), ("opt/Time", "10")), Snap(("opt/Item", "Epic"), ("opt/New", "5"), ("opt/Time", "10")));
        Assert.Equal(Change.Changed, diff.Rows.Single(r => r.Key == "opt/Item").Change);
        Assert.Equal(Change.Same, diff.Rows.Single(r => r.Key == "opt/Time").Change);
        var added = diff.Rows.Single(r => r.Key == "opt/New");
        Assert.Equal((Compare.DefaultValue, "5"), (added.Before, added.After));
    }

    [Fact]
    public void Diff_hides_noise_but_counts_it()
    {
        var diff = Compare.Diff(Snap(("x.StaticMeshComponent0/CachedMaxDrawDistance", "2800")), Snap(("x.StaticMeshComponent0/CachedMaxDrawDistance", "7014")));
        Assert.Empty(diff.Changed);
        Assert.Equal(1, diff.HiddenNoise);
    }

    [Fact]
    public void Added_file_lists_every_setting()
    {
        var diff = Compare.Diff(AssetSnapshot.Missing, Snap(("opt/Item", "Rare")));
        Assert.Equal(Change.Added, Assert.Single(diff.Rows).Change);
        Assert.Equal("Sentry: added", Compare.Summary(Snap(("opt/Item", "Rare")), diff, AssetSnapshot.Missing));
    }

    [Fact]
    public void Merge_reports_what_each_choice_drops()
    {
        var @base = Snap(("opt/Item", "Rare"), ("opt/Time", "10"), ("pos/Location", "X 500"));
        var theirs = Snap(("opt/Item", "Epic"), ("opt/Time", "10"), ("pos/Location", "X 520"));
        var yours = Snap(("opt/Item", "Rare"), ("opt/Time", "6"), ("pos/Location", "X 480"));
        var m = Compare.Merge(@base, theirs, yours);
        Assert.Equal(ChangedBy.Theirs, m.Rows.Single(r => r.Key == "opt/Item").By);
        Assert.Equal(ChangedBy.Yours, m.Rows.Single(r => r.Key == "opt/Time").By);
        Assert.True(m.Rows.Single(r => r.Key == "pos/Location").Conflicts);
        Assert.Equal(["Location", "Item"], m.LostByKeepingYours.Select(r => r.Label).Order().Reverse().ToArray());
        Assert.Equal(["Location", "Time"], m.LostByKeepingTheirs.Select(r => r.Label).Order().ToArray());
    }

    [Fact]
    public void Same_change_on_both_sides_is_not_a_conflict()
    {
        var m = Compare.Merge(Snap(("opt/Item", "Rare")), Snap(("opt/Item", "Epic")), Snap(("opt/Item", "Epic")));
        var row = Assert.Single(m.Changed);
        Assert.Equal(ChangedBy.Both, row.By);
        Assert.False(row.Conflicts);
        Assert.Empty(m.LostByKeepingYours);
        Assert.Empty(m.LostByKeepingTheirs);
    }
}

// Names in the formats Plastic 11.0.16 passes.
public class VersionNamesTests
{
    const string DiffName = "Plugins/mymap/Content/__ExternalActors__/mymap/0/02/K3J8D2M5Q1W7E4R9T6Y0U2.uasset#cs:979";
    const string MergeName = @"MyProject\Content\__ExternalActors__\MyProject\A\0W\P5N2X8C4V1B7Z3L9H6G0F2.uasset@cs:8@/main/feature-theirs";

    [Fact]
    public void Diff_names() => Assert.Equal("cs:979", VersionNames.Short(DiffName, ""));

    [Fact]
    public void Merge_names()
    {
        Assert.Equal("cs:8 on /main/feature-theirs", VersionNames.Short(MergeName, ""));
        Assert.Equal("feature-theirs", VersionNames.Branch(MergeName));
    }

    [Fact]
    public void Stored_path_from_either_form()
    {
        Assert.Equal("__ExternalActors__/mymap/0/02/K3J8D2M5Q1W7E4R9T6Y0U2.uasset", VersionNames.StoredPath(DiffName));
        Assert.Equal("__ExternalActors__/MyProject/A/0W/P5N2X8C4V1B7Z3L9H6G0F2.uasset", VersionNames.StoredPath(MergeName));
    }
}
