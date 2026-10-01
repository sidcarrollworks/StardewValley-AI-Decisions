using NpcTemperament;
using Xunit;

namespace NpcTemperament.Tests;

public class ScorerTests
{
    private static readonly GameTraits Plain = new("Neutral", "Neutral", "Neutral", "Adult");

    private static DialogueFeatures F(int pages = 100, double angry = 0.01, double dismiss = 0.01, double exclaim = 0.2)
        => new(pages, Happy: 0.1, Sad: 0.1, Angry: angry, Love: 0.01, Question: 0.2, Exclaim: exclaim, Trailing: 0.3,
            WordsPerPage: 11, Thanks: 0.05, Sorry: 0.01, Welcome: 0.01, Dismiss: dismiss, Gossip: 0.05);

    [Fact]
    public void SameFeaturesForEveryoneGiveTheGameTraitValuesOnly()
    {
        var scored = TemperamentScorer.Score(new[]
        {
            new CharacterInput("A", Plain, F()),
            new CharacterInput("B", new GameTraits("Polite", "Outgoing", "Positive", "Adult"), F()),
        });

        Assert.All(scored, s => Assert.Equal(s.FromTraits, s.Seed));
        Assert.Equal(Temperament.Neutral, scored[0].Seed);
        Assert.True(scored[1].Seed.Boldness > 0.5 && scored[1].Seed.Forgiveness > 0.5);
    }

    [Fact]
    public void ShyLowersBoldnessAndChattiness()
    {
        var shy = TemperamentScorer.TraitOffsets(new GameTraits("Neutral", "Shy", "Neutral", "Adult"));
        Assert.True(shy.Boldness < 0 && shy.Chattiness < 0);
    }

    [Fact]
    public void AngryDismissiveDialogueLowersForgiveness()
    {
        var scored = TemperamentScorer.Score(new[]
        {
            new CharacterInput("Calm", Plain, F()),
            new CharacterInput("Grump", Plain, F(angry: 0.2, dismiss: 0.1)),
            new CharacterInput("Mid", Plain, F(angry: 0.05, dismiss: 0.03)),
        });
        var byName = scored.ToDictionary(s => s.Name);

        Assert.True(byName["Grump"].Seed.Forgiveness < byName["Mid"].Seed.Forgiveness);
        Assert.True(byName["Mid"].Seed.Forgiveness < byName["Calm"].Seed.Forgiveness);
    }

    [Fact]
    public void PortraitAndWordsBothRaiseAnEmotion()
    {
        var scored = TemperamentScorer.Score(new[]
        {
            new CharacterInput("Calm", Plain, F()),
            new CharacterInput("Cross", Plain, F(angry: 0.2) with { AngerWords = 0.1 }),
            new CharacterInput("Mid", Plain, F(angry: 0.05) with { AngerWords = 0.02 }),
        }).ToDictionary(s => s.Name);

        Assert.True(scored["Cross"].Seed.Anger > scored["Mid"].Seed.Anger);
        Assert.True(scored["Mid"].Seed.Anger > scored["Calm"].Seed.Anger);
    }

    [Fact]
    public void WordsOnlyEmotionsMoveHalfAsFar()
    {
        // the same spread of signal: fear (words only) moves half as far as sadness (portrait + words)
        var scored = TemperamentScorer.Score(new[]
        {
            new CharacterInput("Low", Plain, F() with { Sad = 0, SadWords = 0, FearWords = 0 }),
            new CharacterInput("High", Plain, F() with { Sad = 0.2, SadWords = 0.2, FearWords = 0.2 }),
        }).ToDictionary(s => s.Name);

        double sadness = scored["High"].Seed.Sadness - 0.5;
        double fear = scored["High"].Seed.Fear - 0.5;
        double expected = sadness * TemperamentScorer.WordsOnlyDamping;
        Assert.InRange(fear, expected - 0.01, expected + 0.01); // seeds are rounded to 2 places
        Assert.Contains("fear", TemperamentScorer.WordsOnly);
        Assert.DoesNotContain("sadness", TemperamentScorer.WordsOnly);
    }

    [Fact]
    public void FewPagesMeansGameTraitsOnly()
    {
        var scored = TemperamentScorer.Score(new[]
        {
            new CharacterInput("A", Plain, F()),
            new CharacterInput("B", Plain, F(exclaim: 0.5)),
            new CharacterInput("Quiet", Plain, F(pages: TemperamentScorer.MinPages - 1, exclaim: 0.9)),
        });

        var quiet = scored.Single(s => s.Name == "Quiet");
        Assert.Equal(quiet.FromTraits, quiet.Seed);
    }

    [Fact]
    public void OrderOfInputDoesNotMatterAndValuesStayInRange()
    {
        var inputs = new[]
        {
            new CharacterInput("Zed", new GameTraits("Rude", "Shy", "Negative", "Adult"), F(angry: 0.9, dismiss: 0.9, exclaim: 0)),
            new CharacterInput("Amy", new GameTraits("Polite", "Outgoing", "Positive", "Child"), F(exclaim: 0.9)),
            new CharacterInput("Mo", Plain, F()),
        };

        var a = TemperamentScorer.Score(inputs);
        var b = TemperamentScorer.Score(inputs.Reverse());

        Assert.Equal(a, b);
        Assert.Equal(new[] { "Amy", "Mo", "Zed" }, a.Select(s => s.Name));
        foreach (var s in a)
            foreach (string trait in Temperament.TraitNames)
                Assert.InRange(s.Seed.Get(trait), 0, 1);
    }
}

public class TableTests
{
    [Fact]
    public void UnknownCharacterIsNeutral()
        => Assert.Equal(Temperament.Neutral, new TemperamentTable(new Dictionary<string, Temperament>()).Of("ModdedNpc"));

    [Fact]
    public void OverridesApplyLastAndClamp()
    {
        var table = new TemperamentTable(new Dictionary<string, Temperament> { ["Shane"] = Temperament.Neutral });
        var overrides = TemperamentTable.OverridesFromJson(
            "{ \"note\": \"ignored\", \"Shane\": { \"forgiveness\": 0.2 }, \"Newbie\": { \"warmth\": 1.5 } }");

        var edited = table.WithOverrides(overrides);

        Assert.Equal(0.2, edited.Of("Shane").Forgiveness);
        Assert.Equal(0.5, edited.Of("Shane").Warmth);
        Assert.Equal(1.0, edited.Of("Newbie").Warmth);
    }

    [Fact]
    public void UnknownTraitInOverridesThrows()
    {
        var table = new TemperamentTable(new Dictionary<string, Temperament>());
        var overrides = TemperamentTable.OverridesFromJson("{ \"Sam\": { \"grumpiness\": 0.9 } }");
        Assert.Throws<ArgumentException>(() => table.WithOverrides(overrides));
    }

    [Fact]
    public void JsonRoundTrips()
    {
        var table = new TemperamentTable(new Dictionary<string, Temperament>
        {
            ["Robin"] = new(0.6, 0.26, 0.63, 0.8, 0.79, 0.79),
            ["Abigail"] = Temperament.Neutral,
        });

        var back = TemperamentTable.FromJson(table.ToJson());

        Assert.Equal(table.Rows, back.Rows);
        Assert.Equal(new[] { "Abigail", "Robin" }, back.Rows.Keys);
    }
}

/// <summary>Pins the committed draft table (fixtures/game/temperament), so a regeneration that
/// changes the numbers shows up in review.</summary>
public class CommittedTableTests
{
    private static string Dir => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "..", "..", "..", "..", "..", "fixtures", "game", "temperament"));

    [Fact]
    public void EveryCharacterWithGameTraitsHasASeed()
    {
        var table = TemperamentTable.FromJson(File.ReadAllText(Path.Combine(Dir, "temperament.json")));
        using var traits = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "characters.json")));
        var names = traits.RootElement.GetProperty("characters").EnumerateObject().Select(c => c.Name).ToList();

        Assert.Equal(34, names.Count);
        Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal), table.Rows.Keys);
        foreach (var row in table.Rows.Values)
            foreach (string trait in Temperament.TraitNames)
                Assert.InRange(row.Get(trait), 0, 1);
    }

    [Fact]
    public void KnownCharactersLandWhereTheGameWritesThem()
    {
        var table = TemperamentTable.FromJson(File.ReadAllText(Path.Combine(Dir, "temperament.json")));

        // Shane (rude, shy, negative; "Why are you talking to me?") forgives less and is shyer than Emily.
        Assert.True(table.Of("Shane").Forgiveness < table.Of("Emily").Forgiveness);
        Assert.True(table.Of("Shane").Boldness < table.Of("Emily").Boldness);
        // Penny (shy) is less bold than Robin (outgoing); Robin talks more.
        Assert.True(table.Of("Penny").Boldness < table.Of("Robin").Boldness);
        Assert.True(table.Of("Robin").Chattiness > 0.6);
        // Emotions: Shane leans sad, Robin happy, Haley and Sebastian quick to anger.
        Assert.True(table.Of("Shane").Sadness > table.Of("Robin").Sadness);
        Assert.True(table.Of("Robin").Happiness > table.Of("Shane").Happiness);
        Assert.True(table.Of("Haley").Anger > table.Of("Emily").Anger);
        Assert.True(table.Of("Sebastian").Anger > table.Of("Emily").Anger);
    }
}
