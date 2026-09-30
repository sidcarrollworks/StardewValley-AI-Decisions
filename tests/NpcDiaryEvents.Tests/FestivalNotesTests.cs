using NpcMemory;
using Xunit;

namespace NpcDiaryEvents.Tests;

public class FestivalNotesTests
{
    private static IReadOnlySet<string> TalkedWith(params string[] names)
        => new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void AtDayEnd_Attended_WritesOneFestivalEntryPerDistinctActor()
    {
        var actors = new string[] { "Alex", "alex", "", null!, "Player", "Willy" };

        IReadOnlyList<(string Npc, DiaryEntry Entry)> result = FestivalNotes.AtDayEnd(
            "Summer12",
            attended: true,
            actors: actors,
            talkedWith: TalkedWith("Alex"),
            villagers: Array.Empty<string>(),
            heartsFor: _ => 0,
            options: new DiaryOptions(),
            absoluteTick: 10_000);

        Assert.Equal(new[] { "Alex", "Willy" }, result.Select(r => r.Npc));
        Assert.All(result, r =>
        {
            Assert.Equal(10_000, r.Entry.AbsoluteTick);
            Assert.Equal("Player", r.Entry.Subject);
            Assert.Equal("Festival", r.Entry.Kind);
            Assert.Equal("Summer12", DiaryDetail.Parse(r.Entry.Detail)["festival"]);
        });

        // "Alex" was talked with, "Willy" was not; the first casing of a duplicate wins.
        Assert.Equal("festival=Summer12;with=1", result[0].Entry.Detail);
        Assert.Equal("1", DiaryDetail.Parse(result[0].Entry.Detail)["with"]);
        Assert.Equal("0", DiaryDetail.Parse(result[1].Entry.Detail)["with"]);
    }

    [Fact]
    public void AtDayEnd_Attended_SortsNamesCaseInsensitively()
    {
        var actors = new string[] { "willy", "Bruce", "Alex" };

        IReadOnlyList<(string Npc, DiaryEntry Entry)> result = FestivalNotes.AtDayEnd(
            "Spring13",
            attended: true,
            actors: actors,
            talkedWith: TalkedWith(),
            villagers: Array.Empty<string>(),
            heartsFor: _ => 0,
            options: new DiaryOptions(),
            absoluteTick: 1);

        Assert.Equal(new[] { "Alex", "Bruce", "willy" }, result.Select(r => r.Npc));
    }

    [Fact]
    public void AtDayEnd_NotAttended_WritesOnlyVillagersAtOrAboveHeartsFloor()
    {
        var villagers = new string[] { "Alex", "Abigail", "Willy", "Sebastian", "Pam" };

        int Hearts(string npc) => npc switch
        {
            "Alex" => 2,
            "Abigail" => 3, // exactly at the floor: included
            "Willy" => 4,
            "Sebastian" => 10,
            _ => 0,         // Pam
        };

        IReadOnlyList<(string Npc, DiaryEntry Entry)> result = FestivalNotes.AtDayEnd(
            "Fall16",
            attended: false,
            actors: new string[] { "Alex", "Willy" },
            talkedWith: TalkedWith("Alex"),
            villagers: villagers,
            heartsFor: Hearts,
            options: new DiaryOptions { MissedFestivalHeartsFloor = 3 },
            absoluteTick: 12_345);

        Assert.Equal(new[] { "Abigail", "Sebastian", "Willy" }, result.Select(r => r.Npc));
        Assert.All(result, r =>
        {
            Assert.Equal("Player", r.Entry.Subject);
            Assert.Equal("MissedFestival", r.Entry.Kind);
            Assert.Equal(12_345, r.Entry.AbsoluteTick);
            Assert.Equal("festival=Fall16", r.Entry.Detail);
            Assert.Equal("Fall16", DiaryDetail.Parse(r.Entry.Detail)["festival"]);
        });
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(null, false)]
    [InlineData("", true)]
    [InlineData("", false)]
    public void AtDayEnd_EmptyFestivalId_ReturnsEmptyList(string? festivalId, bool attended)
    {
        IReadOnlyList<(string Npc, DiaryEntry Entry)> result = FestivalNotes.AtDayEnd(
            festivalId,
            attended: attended,
            actors: new string[] { "Alex" },
            talkedWith: TalkedWith("Alex"),
            villagers: new string[] { "Willy" },
            heartsFor: _ => 10,
            options: new DiaryOptions(),
            absoluteTick: 1);

        Assert.Empty(result);
    }

    [Fact]
    public void AtDayEnd_Attended_DoesNotWriteVillagersOrConsultHearts()
    {
        int heartsCalls = 0;

        IReadOnlyList<(string Npc, DiaryEntry Entry)> result = FestivalNotes.AtDayEnd(
            "Summer12",
            attended: true,
            actors: Array.Empty<string>(),
            talkedWith: TalkedWith(),
            villagers: new string[] { "Willy" },
            heartsFor: _ => { heartsCalls++; return 10; },
            options: new DiaryOptions(),
            absoluteTick: 1);

        Assert.Empty(result);
        Assert.Equal(0, heartsCalls);
    }

    [Fact]
    public void AtDayEnd_IsDeterministic()
    {
        var actors = new string[] { "Willy", "Alex", "alex", "", "Player" };
        var villagers = new string[] { "Pam", "Willy" };
        var options = new DiaryOptions { MissedFestivalHeartsFloor = 3 };
        int Hearts(string npc) => npc == "Pam" ? 5 : 1;

        var attendedFirst = FestivalNotes.AtDayEnd(
            "Winter25", true, actors, TalkedWith("willy"), villagers, Hearts, options, 777);
        var attendedSecond = FestivalNotes.AtDayEnd(
            "Winter25", true, actors, TalkedWith("willy"), villagers, Hearts, options, 777);
        var missedFirst = FestivalNotes.AtDayEnd(
            "Winter25", false, actors, TalkedWith("willy"), villagers, Hearts, options, 777);
        var missedSecond = FestivalNotes.AtDayEnd(
            "Winter25", false, actors, TalkedWith("willy"), villagers, Hearts, options, 777);

        Assert.Equal(2, attendedFirst.Count);
        Assert.Equal(new[] { "Alex", "Willy" }, attendedFirst.Select(r => r.Npc));
        (string Npc, DiaryEntry Entry) missedOnly = Assert.Single(missedFirst);
        Assert.Equal("Pam", missedOnly.Npc);

        Assert.Equal(
            attendedFirst.Select(r => (r.Npc, r.Entry)),
            attendedSecond.Select(r => (r.Npc, r.Entry)));
        Assert.Equal(
            missedFirst.Select(r => (r.Npc, r.Entry)),
            missedSecond.Select(r => (r.Npc, r.Entry)));
    }
}
