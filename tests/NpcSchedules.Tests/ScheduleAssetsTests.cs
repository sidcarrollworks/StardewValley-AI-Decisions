using NpcSchedules;
using Xunit;

namespace NpcSchedules.Tests;

/// <summary>The schedule asset name (Characters/schedules/&lt;Name&gt;, not Data/Schedules) and
/// the ExtractAll loop's skip-one-and-continue behaviour (the fix for the seeding bug from
/// PR #16).</summary>
public class ScheduleAssetsTests
{
    [Fact]
    public void NameForIsTheVerifiedAssetPath()
    {
        // stardew-source-notes.md, "Motives verify pass", "Schedules": NPC.cs:5993 loads
        // Characters/schedules/<Name>; Data/Schedules/<Name> does not exist.
        Assert.Equal("Characters/schedules/Abigail", ScheduleAssets.NameFor("Abigail"));
        Assert.Equal("Characters/schedules/Krobus", ScheduleAssets.NameFor("Krobus"));
    }

    [Fact]
    public void ExtractAllSkipsOnlyTheMissingAsset()
    {
        var extractor = new RoutineExtractor(TestHelpers.Regions());
        var schedules = new Dictionary<string, string> { ["spring"] = TestHelpers.Base };
        var missing = new List<string>();

        Dictionary<string, NpcRoutine> routines = extractor.ExtractAll(
            new[] { "Clint", "Haley", "Penny" },
            name => name == ScheduleAssets.NameFor("Haley")
                ? throw new FileNotFoundException(name) // Haley has no schedule asset
                : schedules,
            missing);

        // The other two extracted fine; only Haley was skipped and reported, in name order.
        Assert.Equal(2, routines.Count);
        Assert.Contains("Clint", routines.Keys);
        Assert.Contains("Penny", routines.Keys);
        Assert.Equal(new[] { "Haley" }, missing);
    }

    [Fact]
    public void ExtractAllIsDeterministicInNameOrder()
    {
        var extractor = new RoutineExtractor(TestHelpers.Regions());
        var schedules = new Dictionary<string, string> { ["spring"] = TestHelpers.Base };

        Dictionary<string, NpcRoutine> routines = extractor.ExtractAll(
            new[] { "Penny", "Abigail", "Clint" }, // shuffled in, sorted out
            _ => schedules);

        Assert.Equal(new[] { "Abigail", "Clint", "Penny" }, routines.Keys);
    }
}
