using NpcMemory;
using NpcTemperament;
using Xunit;

namespace NpcMinds.Tests;

/// <summary>The viewer's temperament section (seed table from docs/spec/temperament.md).</summary>
public sealed class TemperamentViewTests
{
    [Fact]
    public void AllTwelveTraitsInTheTablesOrder()
    {
        TemperamentView view = MindsSnapshotBuilder.TemperamentOf(Temperament.Neutral with { Boldness = 0.8 }, seeded: true, "manners rude, outgoing, neutral");

        Assert.Equal(Temperament.BehaviourTraits, view.Traits.Select(t => t.Name));
        Assert.Equal(Temperament.EmotionTraits, view.Emotions.Select(t => t.Name));
        Assert.Equal(0.8, view.Traits.Single(t => t.Name == "boldness").Value);
        Assert.Equal("manners rude, outgoing, neutral", view.GameTraits);
        Assert.True(view.Seeded);
    }

    [Fact]
    public void TheSummaryNamesTheStrongestLeaningsFirst()
    {
        var t = new Temperament(0.5, 0.5, 0.25, 0.62, 0.5, 0.5, Anger: 0.75, Happiness: 0.45);

        TemperamentView view = MindsSnapshotBuilder.TemperamentOf(t, true, null);

        // forgiveness 0.25 and anger 0.75 tie at 0.25 off: trait order breaks the tie.
        Assert.Equal("holds grudges, quick to anger, chatty", view.Summary);
    }

    [Fact]
    public void SmallLeaningsAreLeftOut()
    {
        Assert.Equal("even-tempered", MindsSnapshotBuilder.TemperamentOf(Temperament.Neutral with { Warmth = 0.55 }, true, null).Summary);
        Assert.Equal("no seed (town average)", MindsSnapshotBuilder.TemperamentOf(Temperament.Neutral, false, null).Summary);
    }

    [Fact]
    public void TheSnapshotCarriesItOnlyWhenGiven()
    {
        var memory = new MemoryStore();
        memory.Note("Shane", new DiaryEntry(10, "Player", "Saw", "Saloon"));
        var inputs = new MindsInputs(1, 20, "Fake", true, MindsStats.None, "none", null,
            Array.Empty<NpcInitiation.InitiationInput>(), Array.Empty<string>(),
            Array.Empty<NpcIntents.IntentCandidate>(), Array.Empty<FeedItem>());

        Assert.Null(new MindsSnapshotBuilder().Build(memory, inputs).Npcs.Single().Temperament);

        TemperamentView shane = MindsSnapshotBuilder.TemperamentOf(Temperament.Neutral with { Forgiveness = 0.2 }, true, null);
        NpcMind mind = new MindsSnapshotBuilder().Build(memory, inputs with { TemperamentFor = n => n == "Shane" ? shane : null }).Npcs.Single();
        Assert.Same(shane, mind.Temperament);
    }

    [Fact]
    public void TheShippedTableLoadsAndKeepsKnownOrderings()
    {
        // The mod copies this file into its folder (StardewNpcMod.csproj) and reads it at start.
        string dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "NpcSchedules.sln")))
            dir = Path.GetDirectoryName(dir)!;
        string path = Path.Combine(dir!, "fixtures", "game", "temperament", "temperament.json");

        TemperamentTable table = TemperamentTable.FromJson(File.ReadAllText(path));

        Assert.True(table.Of("Shane").Forgiveness < table.Of("Emily").Forgiveness);
        Assert.Contains(table.Rows.Keys, k => k == "Abigail");
    }
}
