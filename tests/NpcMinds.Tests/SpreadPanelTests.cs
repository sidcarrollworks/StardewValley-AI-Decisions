using NpcDecision;
using Xunit;

namespace NpcMinds.Tests;

/// <summary>The model spread panel: template grouping, per-day means and spread, the flat and
/// doesn't-follow marks, the 6:00 reset, and that recording never changes an answer
/// (docs/spec/debug-tools.md, "model spread panel" acceptance tests).</summary>
public sealed class SpreadPanelTests
{
    private static readonly SpreadOptions Options = new();

    private static SpreadTable Table(params (string Template, string Npc, double Value)[] calls)
    {
        var table = new SpreadTable();
        foreach ((string template, string npc, double value) in calls)
            table.Record(template, npc, value);
        return table;
    }

    private static TemperamentView View(string npc, double boldness) => new(
        new[] { new TraitValue("boldness", boldness) },
        Array.Empty<TraitValue>(), "", null, true);

    [Fact]
    public void TemplatesGroupAcrossNpcs()
    {
        // The same question with different NPC names becomes one template; the rest is kept.
        string a = SpreadTable.ReplaceNpc("Should Abigail try to get the player's attention with Emote now?", "Abigail");
        string s = SpreadTable.ReplaceNpc("Should Sam try to get the player's attention with Emote now?", "Sam");
        Assert.Equal(a, s);
        Assert.Contains("<npc>", a);
        Assert.Contains("Emote", a);
        // A name that is not in the question leaves it unchanged.
        Assert.Equal("Does anyone have news?", SpreadTable.ReplaceNpc("Does anyone have news?", null));
        Assert.Equal("Should <npc> go now?", SpreadTable.ReplaceNpc("Should Linus go now?", "Linus"));
    }

    [Fact]
    public void MeansAndSpreadAreRightOnFixedAnswers()
    {
        // Ten NPCs with means 0.0, 0.1, ..., 0.9: interpolated p90-p10 = 0.72, median = 0.45.
        var calls = new List<(string, string, double)>();
        for (int i = 0; i < 10; i++)
            calls.Add(("Should <npc> try to get the player's attention with Emote now?", $"N{i}", i / 10.0));
        IReadOnlyList<SpreadRow> rows = MindsSnapshotBuilder.SpreadRows(Table(calls.ToArray()).Copy(), null, null, Options);
        SpreadRow row = Assert.Single(rows);
        Assert.Equal(10, row.Npcs.Count);
        Assert.Equal(0.72, row.Spread, 3);
        Assert.Equal(0.45, row.Median, 3);
        Assert.Equal("N9", row.Npcs[0].Name); // highest mean first
        Assert.Equal("N0", row.Npcs[^1].Name);
        Assert.False(row.Flat); // ten NPCs, wide spread: not flat, and the mark is present
    }

    [Fact]
    public void FlatAndFollowsMarksAppearAtTheirThresholds()
    {
        const string template = "Should <npc> try to get the player's attention with Bubble now?";
        // Flat: six NPCs, all answers within a 0.04 band.
        var flat = new List<(string, string, double)>();
        for (int i = 0; i < 6; i++)
            flat.Add((template, $"N{i}", 0.20 + i * 0.004));
        SpreadRow flatRow = Assert.Single(MindsSnapshotBuilder.SpreadRows(Table(flat.ToArray()).Copy(), null, null, Options));
        Assert.True(flatRow.Flat);

        // Follows: means rising with boldness (correlation 1) — follows for a +1 trait.
        var rising = new List<(string, string, double)>();
        for (int i = 0; i < 6; i++)
            rising.Add((template, $"N{i}", 0.10 + i * 0.06));
        SpreadRow followsRow = Assert.Single(MindsSnapshotBuilder.SpreadRows(
            Table(rising.ToArray()).Copy(), null, npc => View(npc, (npc[1] - '0') / 5.0), Options));
        Assert.True(followsRow.Follows);
        Assert.Equal(1.0, followsRow.Correlation!.Value, 2);

        // Reversed means with the same traits: doesn't follow.
        var falling = new List<(string, string, double)>();
        for (int i = 0; i < 6; i++)
            falling.Add((template, $"N{i}", 0.40 - i * 0.06));
        SpreadRow noFollowRow = Assert.Single(MindsSnapshotBuilder.SpreadRows(
            Table(falling.ToArray()).Copy(), null, npc => View(npc, (npc[1] - '0') / 5.0), Options));
        Assert.False(noFollowRow.Follows);

        // Fewer than MinNpcsForSpread: both marks are null.
        SpreadRow fewRow = Assert.Single(MindsSnapshotBuilder.SpreadRows(
            Table(flat.Take(3).ToArray()).Copy(), null, null, Options));
        Assert.Null(fewRow.Flat);
        Assert.Null(fewRow.Follows);
    }

    [Fact]
    public void ResetClearsTheDay()
    {
        SpreadTable table = Table(("Does <npc> have news for the player?", "Haley", 0.4));
        Assert.NotEmpty(table.Copy());
        table.Reset();
        Assert.Empty(table.Copy());
        Assert.Empty(MindsSnapshotBuilder.SpreadRows(table.Copy(), null, null, Options));
    }

    [Fact]
    public void OvernightPlanAnswersSurviveUntilTheNextReset()
    {
        // The mod resets at DayEnding, just before the plan job starts, so the overnight plan's
        // answers count toward the day they plan for; nothing clears them at the 6:00 tick.
        // The table's only clearing path is an explicit Reset — recording and copying never
        // drop entries (ModEntry: OnDayEnding resets, OnTimeChanged does not).
        SpreadTable table = Table(("Does <npc> have news for the player?", "Haley", 0.4));
        table.Record("Does <npc> have news for the player?", "Sam", 0.6);

        // "6:00": the new day's tick does nothing to the table.
        IReadOnlyList<SpreadEntry> morning = table.Copy();
        Assert.Equal(2, morning.Count);
        // Copying for the snapshot is a read: the table is unchanged afterwards.
        Assert.Equal(2, table.Copy().Count);

        // The next DayEnding resets, and the fresh table starts collecting the new plan.
        table.Reset();
        Assert.Empty(table.Copy());
        table.Record("Does <npc> have news for the player?", "Haley", 0.5);
        Assert.Single(table.Copy());
    }

    [Fact]
    public void RecordingNeverChangesAnAnswer()
    {
        // The recorder wraps a resilient client and must return its answer unchanged while the
        // spread table observes it; a NaN answer or a missing NPC is dropped, not altered.
        var inner = new ResilientDecisionClient(new FakeDecisionClient());
        var table = new SpreadTable();
        var recorder = new RecordingDecisionClient(inner, new RingLog<DecisionCall>(4), "test", table);

        double before = inner.YesNo("npc: Haley\n", "Does Haley have news for the player?");
        double recorded = recorder.YesNo("npc: Haley\n", "Does Haley have news for the player?");
        Assert.Equal(before, recorded);

        IReadOnlyList<SpreadEntry> copy = table.Copy();
        SpreadEntry entry = Assert.Single(copy);
        Assert.Equal("Haley", entry.Npc);
        Assert.Equal("Does <npc> have news for the player?", entry.Template);
        Assert.Equal(1, entry.Count);
        Assert.Equal(recorded, entry.Mean, 6);

        // A missing NPC changes nothing.
        table.Record("whatever", null, 0.5);
        Assert.Single(table.Copy());
    }
}
