using System.Text.Json;
using NpcMotives;
using Xunit;

namespace NpcLive.Tests;

/// <summary>Each villager's own bubble lines (BubbleVoices): every vanilla villager has them,
/// they are short and safe for the game, and a villager speaks in its own voice.</summary>
public sealed class BubbleVoiceTests
{
    private static readonly Motive[] Friendly = { Motive.Greeting, Motive.MissingYou, Motive.News, Motive.Grateful, Motive.Worried };
    private static readonly Motive[] Hostile = { Motive.Hurt, Motive.Jealous };

    /// <summary>The 34 villagers of the temperament table (fixtures/game/temperament).</summary>
    private static IReadOnlyList<string> Villagers()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "NpcSchedules.sln")))
            dir = Path.GetDirectoryName(dir)!;
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "fixtures", "game", "temperament", "temperament.json")));
        return doc.RootElement.GetProperty("characters").EnumerateObject().Select(p => p.Name).ToList();
    }

    [Fact]
    public void EveryVillagerHasEveryFeeling_ShortAndSafe()
    {
        IReadOnlyList<string> villagers = Villagers();
        Assert.Equal(34, villagers.Count);
        foreach (string npc in villagers)
        {
            Assert.True(BubbleVoices.All.ContainsKey(npc), npc + " has no bubble voice");
            foreach ((Motive m, bool hostile) in Friendly.Select(m => (m, false)).Concat(Hostile.Select(m => (m, true))))
            {
                string[] lines = BubbleVoices.For(npc, m, hostile);
                Assert.True(lines.Length >= 2 || (m == Motive.Worried && lines.Length >= 1), $"{npc} {m}: {lines.Length} lines");
                Assert.Equal(lines.Length, lines.Distinct().Count());
                foreach (string line in lines)
                {
                    string shown = line.Replace("{player}", "Sidney");
                    Assert.True(shown.Length <= 40, $"{npc} {m}: \"{shown}\" is {shown.Length} characters"); // docs/spec/text.md: bubbles 40 chars
                    Assert.DoesNotContain(shown, c => "#$%{[}]".Contains(c));
                }
            }
        }
        Assert.Empty(BubbleVoices.All.Keys.Except(villagers, StringComparer.OrdinalIgnoreCase)); // no stray names
    }

    [Fact]
    public void AVillagerSpeaksInItsOwnVoice_AnUnknownOneInThePlainLines()
    {
        var shane = Enumerable.Range(0, 200).Select(t => LivePlanner.LineFor("Shane", Motive.Greeting, false, t, "Sid")).ToHashSet();
        Assert.Equal(new HashSet<string> { "...Hey.", "Oh. It's you.", "Hey." }, shane);

        var gunther = Enumerable.Range(0, 200).Select(t => LivePlanner.LineFor("Gunther", Motive.Greeting, false, t, "Sid")).ToHashSet();
        Assert.Contains("Hey, Sid!", gunther); // the plain lines

        // Curious has no voiced set: the plain lines, even for a voiced villager.
        Assert.Contains(LivePlanner.LineFor("Shane", Motive.Curious, false, 1), new[] { "What are you up to?", "Ooh, what's that?" });
    }

    [Fact]
    public void WithoutAPlayerNameEveryVoicedLineStillReads()
    {
        foreach (BubbleVoice v in BubbleVoices.All.Values)
            foreach (string line in new[] { v.Greeting, v.MissingYou, v.News, v.Grateful, v.Worried, v.Hurt, v.Jealous }.SelectMany(x => x))
            {
                if (!line.Contains("{player}"))
                    continue;
                string npc = BubbleVoices.All.First(kv => kv.Value == v).Key;
                // Find the tick that picks this line, then render it without a name.
                foreach ((Motive m, bool hostile, string[] set) in new[]
                         {
                             (Motive.Greeting, false, v.Greeting), (Motive.MissingYou, false, v.MissingYou), (Motive.News, false, v.News),
                             (Motive.Grateful, false, v.Grateful), (Motive.Worried, false, v.Worried), (Motive.Hurt, true, v.Hurt), (Motive.Jealous, true, v.Jealous),
                         })
                {
                    if (!set.Contains(line))
                        continue;
                    for (int t = 0; t < 400; t++)
                    {
                        string named = LivePlanner.LineFor(npc, m, hostile, t, "Sid");
                        if (named != line.Replace("{player}", "Sid"))
                            continue;
                        string bare = LivePlanner.LineFor(npc, m, hostile, t, "");
                        Assert.False(bare.Contains(" .") || bare.Contains(" !") || bare.Contains(",.") || bare.Contains(",!") || bare.Contains(" ?"), $"\"{line}\" -> \"{bare}\"");
                        Assert.False(bare.StartsWith("!") || bare.StartsWith(".") || bare.StartsWith(","), $"\"{line}\" -> \"{bare}\"");
                        break;
                    }
                }
            }
    }
}
