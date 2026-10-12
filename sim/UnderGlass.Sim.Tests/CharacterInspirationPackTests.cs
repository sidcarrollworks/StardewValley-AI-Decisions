using System.Text.Json;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

public class CharacterInspirationPackTests
{
    private static CharacterInspirationPack Pack()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "sim", "character-inspiration.json")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return CharacterInspirationPack.Read(File.ReadAllText(Path.Combine(directory!.FullName, "sim", "character-inspiration.json")));
    }

    [Fact]
    public void ReviewedPackCoversBirthdayVillagersAndCurrentCastWithoutInventingANewcomerHistory()
    {
        CharacterInspirationPack pack = Pack();
        string[] expected = { "Abigail", "Alex", "Caroline", "Clint", "Demetrius", "Dwarf", "Elliott", "Emily",
            "Evelyn", "George", "Gus", "Haley", "Harvey", "Jas", "Jodi", "Kent", "Krobus", "Leah", "Leo",
            "Lewis", "Linus", "Marnie", "Maru", "Pam", "Penny", "Pierre", "Robin", "Sam", "Sandy",
            "Sebastian", "Shane", "Vincent", "Willy", "Wizard" };
        Assert.Equal(expected.Order(StringComparer.Ordinal), pack.Characters.Select(p => p.Name));
        Assert.All(DefaultTown.Cast().Where(p => p.Name != "Newcomer"),
            person => Assert.Contains(pack.Characters, p => p.Name == person.Name));
        Assert.Null(pack.ForCharacter("Newcomer", "Penny", 7, "scene"));
        Assert.All(pack.Characters, profile => Assert.InRange(profile.Background.Count, 2, 4));
        Assert.All(pack.Archetypes, archetype => Assert.Null(pack.ForArchetype(archetype.Id).Background));
    }

    [Fact]
    public void ReplayRequiresReflectionAndDoesNotImplicitlyAttachProfiles()
    {
        Assert.Throws<ArgumentException>(() => Replay.Record(new ReplayOptions { Days = 1, CharacterInspiration = Pack() }));
        Dictionary<string, object?> unprofiled = Replay.Record(new ReplayOptions { Days = 1 });
        Assert.False(unprofiled.ContainsKey("characterInspiration"));
    }

    [Fact]
    public async Task ProfileRecordingCarriesReviewedPackReceiptAndExactTapeReplays()
    {
        CharacterInspirationPack pack = Pack();
        var options = new ReplayOptions { Seed = 7, Days = 1, CharacterInspiration = pack,
            Reflection = new ReflectionOptions { Enabled = true, DailyChance = 1 } };
        using var run = JsonDocument.Parse(Replay.Json(options));
        JsonElement receipt = run.RootElement.GetProperty("characterInspiration");
        Assert.Equal(pack.ContentHash, receipt.GetProperty("hash").GetString());
        Assert.Equal(pack.Provenance, receipt.GetProperty("provenance").GetString());
        var records = run.RootElement.GetProperty("reflections").Deserialize<ReflectionRecord[]>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.NotEmpty(records);
        Assert.Contains(records, record => record.Request.Character is not null);
        using var replay = JsonDocument.Parse(await Replay.JsonAsync(options, new RecordedReflectionMind(records)));
        Assert.Equal(run.RootElement.GetProperty("hash").GetString(), replay.RootElement.GetProperty("hash").GetString());
        Assert.Equal(run.RootElement.GetProperty("reflections").GetRawText(), replay.RootElement.GetProperty("reflections").GetRawText());
    }
}
