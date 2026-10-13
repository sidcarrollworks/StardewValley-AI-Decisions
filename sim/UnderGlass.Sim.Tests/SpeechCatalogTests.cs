using System.Text.Json;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>The speech library: validation, deterministic weighted choice, and coverage of every
/// kind an actor can do to a chosen person.</summary>
public class SpeechCatalogTests
{
    private static string Pack(params string[] lines) => "{\"schema\":1,\"provenance\":\"test\",\"lines\":[" + string.Join(",", lines) + "]}";
    private static string Line(string id, string kind, string text, string archetypes = "", string relationship = "", string mood = "")
        => $"{{\"id\":\"{id}\",\"kind\":\"{kind}\",\"archetypes\":[{archetypes}],\"relationship\":[{relationship}],\"mood\":[{mood}],\"text\":\"{text}\",\"tags\":[\"t\"]}}";

    [Fact]
    public void ReadsValidLinesAndRejectsBadOnes()
    {
        SpeechPack pack = SpeechCatalog.Read(Pack(Line("b", "GaveGift", "Thought you might like this."), Line("a", "GaveGift", "For you, {subject}.")));
        Assert.Equal(new[] { "a", "b" }, pack.Lines.Select(l => l.Id)); // ordinal order, independent of file order
        foreach (string bad in new[]
        {
            Line("x", "GaveGift", "Too short"), Line("x", "GaveGift", new string('a', 111)), Line("x", "GaveGift", "Hello {name}, take this."),
            Line("x", "GaveGift", "Costs $5 but take it."), Line("x", "GaveGift", " leading space is not allowed"),
            Line("x", "GaveGift", "Fine line here.", relationship: "\"enemy\""), Line("x", "GaveGift", "Fine line here.", mood: "\"grim\""),
            Line("x", "", "A line without a kind here."),
        })
            Assert.Throws<InvalidDataException>(() => SpeechCatalog.Read(Pack(bad)));
        Assert.Throws<InvalidDataException>(() => SpeechCatalog.Read(Pack(Line("dup", "GaveGift", "One line here now."), Line("dup", "GaveGift", "Two lines here now."))));
        Assert.Throws<InvalidDataException>(() => SpeechCatalog.Read("{\"schema\":2,\"provenance\":\"p\",\"lines\":[]}"));
    }

    [Fact]
    public void ChoiceIsDeterministicFitsTheBandsAndPrefersSpecificLines()
    {
        SpeechPack pack = SpeechCatalog.Read(Pack(
            Line("generic", "GaveGift", "Thought you might like this."),
            Line("gruff", "GaveGift", "Take it before I change my mind.", archetypes: "\"weary-defender\""),
            Line("stranger-low", "GaveGift", "Here. No need to make anything of it.", relationship: "\"stranger\"", mood: "\"low\""),
            Line("other-kind", "Thanked", "Thanks for the other day.")));
        // Same inputs, same line; different act ids vary the draw on speech's own stream.
        SpeechLine first = SpeechCatalog.Choose(pack, "GaveGift", "weary-defender", "stranger", "low", 7, 1)!;
        Assert.Equal(first.Id, SpeechCatalog.Choose(pack, "GaveGift", "weary-defender", "stranger", "low", 7, 1)!.Id);
        var ids = Enumerable.Range(0, 60).Select(i => SpeechCatalog.Choose(pack, "GaveGift", "weary-defender", "stranger", "low", 7, i)!.Id).ToHashSet();
        Assert.Equal(new[] { "generic", "gruff", "stranger-low" }.ToHashSet(), ids);
        // A line tagged for another archetype, or for a relationship or mood band that is not the
        // speaker's or its neighbour, never fits.
        Assert.All(Enumerable.Range(0, 60), i => Assert.NotEqual("gruff", SpeechCatalog.Choose(pack, "GaveGift", "young-observer", "stranger", "low", 7, i)!.Id));
        Assert.All(Enumerable.Range(0, 60), i => Assert.NotEqual("gruff", SpeechCatalog.Choose(pack, "GaveGift", null, "stranger", "low", 7, i)!.Id));
        Assert.All(Enumerable.Range(0, 60), i => Assert.NotEqual("stranger-low", SpeechCatalog.Choose(pack, "GaveGift", null, "close", "low", 7, i)!.Id));
        // Weighting: the archetype line (3) and the stranger-low line (4) together outdraw the generic (1).
        int generic = Enumerable.Range(0, 400).Count(i => SpeechCatalog.Choose(pack, "GaveGift", "weary-defender", "stranger", "low", 7, i)!.Id == "generic");
        Assert.InRange(generic, 20, 100);
        // Recent lines are avoided when another fits (generic and gruff fit here, so gruff every
        // time); when only one fits it is still used; an unknown kind has no line.
        Assert.All(Enumerable.Range(0, 60), i => Assert.Equal("gruff", SpeechCatalog.Choose(pack, "GaveGift", "weary-defender", "close", "even", 7, i, avoid: new[] { "generic" })!.Id));
        Assert.Equal("generic", SpeechCatalog.Choose(pack, "GaveGift", null, "close", "even", 7, 1, avoid: new[] { "generic" })!.Id);
        Assert.Null(SpeechCatalog.Choose(pack, "Welcomed", null, "close", "even", 7, 1));
        Assert.Equal("Take it before I change my mind.", SpeechCatalog.Render(pack.Lines.Single(l => l.Id == "gruff"), "Ann"));
        Assert.Equal("low", SpeechCatalog.MoodBand(-0.5));
        Assert.Equal("even", SpeechCatalog.MoodBand(0.1));
        Assert.Equal("high", SpeechCatalog.MoodBand(0.4));
    }

    [Fact]
    public void ANeighbouringBandStillFitsLessWellAndTheViewerAgrees()
    {
        // The same fixture pins the viewer's speech page (sim/tools/test-speech-page.cjs).
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "sim", "tools", "fixtures", "speech-weights.json")));
        SpeechPack pack = SpeechCatalog.Read(fixture.RootElement.GetProperty("pack").GetRawText());
        foreach (JsonElement c in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            string kind = c.GetProperty("kind").GetString()!, relationship = c.GetProperty("relationship").GetString()!, mood = c.GetProperty("mood").GetString()!;
            string? archetype = c.GetProperty("archetype").ValueKind == JsonValueKind.Null ? null : c.GetProperty("archetype").GetString();
            var expected = c.GetProperty("weights").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetDouble());
            Assert.Equal(pack.Lines.Where(l => l.Kind == kind).Select(l => l.Id).OrderBy(i => i, StringComparer.Ordinal), expected.Keys.OrderBy(i => i, StringComparer.Ordinal));
            foreach (SpeechLine line in pack.Lines.Where(l => l.Kind == kind))
                Assert.True(expected[line.Id] == SpeechCatalog.Weight(line, archetype, relationship, mood),
                    $"{line.Id} for {archetype ?? "no archetype"}, {relationship}, {mood}: expected {expected[line.Id]}, got {SpeechCatalog.Weight(line, archetype, relationship, mood)}");
        }
        // A neighbour line is drawn, a household line never leaves the household, a low line is
        // never said in high spirits.
        var drawn = Enumerable.Range(0, 200).Select(i => SpeechCatalog.Choose(pack, "GaveGift", null, "acquaintance", "even", 7, i)!.Id).ToHashSet();
        Assert.Contains("stranger-low", drawn);
        Assert.DoesNotContain("house", Enumerable.Range(0, 200).Select(i => SpeechCatalog.Choose(pack, "GaveGift", null, "close", "even", 7, i)!.Id));
        Assert.DoesNotContain("stranger-low", Enumerable.Range(0, 200).Select(i => SpeechCatalog.Choose(pack, "GaveGift", null, "stranger", "high", 7, i)!.Id));
    }

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "sim", "character-inspiration.json")))
            directory = directory.Parent;
        return directory!.FullName;
    }

    [Fact]
    public void EmbeddedLibraryCoversEveryKindDoneToAChosenPerson()
    {
        SpeechPack pack = SpeechCatalog.Default;
        IEnumerable<ActKind> kinds = ActCatalog.Returns.Concat(ActCatalog.Company).Concat(ActCatalog.Welcome)
            .Concat(ActCatalog.Repair).Concat(ActCatalog.Sides).Concat(DefaultTown.Acts());
        foreach (ActKind kind in kinds.Where(k => k.Affect is { Target: TargetIs.Chosen, Patient: Patient.Target } && !SpeechCatalog.Silent.Contains(k.Name)))
            Assert.True(pack.Lines.Any(l => l.Kind == kind.Name && l.Archetypes.Count == 0), $"{kind.Name} has no archetype-free line");
        Assert.Null(SpeechCatalog.Choose(pack, "Snubbed", null, "close", "even", 7, 1)); // turning away is silent
        Assert.Contains(pack.Lines, l => l.Kind == "Squabbled" && l.Archetypes.Count == 0);
        // Every archetype named in the library exists in the character pack.
        var known = CharacterInspirationPack.Read(File.ReadAllText(Path.Combine(RepoRoot(), "sim", "character-inspiration.json")))
            .Archetypes.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        foreach (string archetype in pack.Lines.SelectMany(l => l.Archetypes).Distinct())
            Assert.Contains(archetype, known);
    }
}
