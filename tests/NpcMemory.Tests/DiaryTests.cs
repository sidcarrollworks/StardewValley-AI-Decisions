using System.Text.Json;
using NpcMemory;
using Xunit;

namespace NpcMemory.Tests;

/// <summary>
/// Tests for the per-NPC diary: insertion-order storage, tick/recency/subject queries, and
/// JSON round-tripping. All fixtures are literal and deterministic.
/// </summary>
public class DiaryTests
{
    private static DiaryEntry E(int tick, string subject, string kind, string? detail = null)
        => new(tick, subject, kind, detail);

    // ---------------------------------------------------------------- Append / ordering

    [Fact]
    public void Append_KeepsInsertionOrder_EvenWhenTicksArriveOutOfOrder()
    {
        var diary = new Diary();
        var late = E(GameClock.AbsoluteTick(new GameTime(0, 1, 100)), "Abigail", "Saw");
        var early = E(GameClock.AbsoluteTick(new GameTime(0, 1, 10)), "Pierre", "SpokeWith", "seed stock");
        var middle = E(GameClock.AbsoluteTick(new GameTime(0, 2, 0)), "Player", "ReceivedGift", "quartz");

        diary.Append(late);    // tick 100
        diary.Append(early);   // tick 10  (arrives late, must NOT be sorted to the front)
        diary.Append(middle);  // tick 120

        Assert.Equal(3, diary.Entries.Count);
        Assert.Equal(new[] { late, early, middle }, diary.Entries);
    }

    [Fact]
    public void Append_AllowsDuplicatesAndEmptySubjectsToBeStoredVerbatim()
    {
        var first = E(5, "Player", "Saw");
        var diary = new Diary();
        diary.Append(first);
        diary.Append(first); // same record value twice is legal

        Assert.Equal(2, diary.Entries.Count);
        Assert.Equal(new[] { first, first }, diary.Entries);
    }

    // ---------------------------------------------------------------- Since

    [Fact]
    public void Since_FiltersByTickInclusive_OldestFirstByInsertion()
    {
        var t10 = E(10, "Abigail", "Saw");
        var t20 = E(20, "Abigail", "SpokeWith");
        var t30 = E(30, "Player", "ReceivedGift", "quartz");
        var diary = new Diary();
        diary.Append(t20);
        diary.Append(t30);
        diary.Append(t10); // out of tick order

        // boundary tick is included; result keeps insertion order, not tick order
        Assert.Equal(new[] { t20, t30 }, diary.Since(20));
        Assert.Equal(new[] { t20, t30, t10 }, diary.Since(0));
        Assert.Equal(new[] { t30 }, diary.Since(21));
        Assert.Equal(new[] { t20, t30 }, diary.Since(11));   // t10 has tick 10, below the threshold
        Assert.Equal(new[] { t20, t30, t10 }, diary.Since(10)); // inclusive; still insertion order
    }

    [Fact]
    public void Since_TickBeyondEverything_IsEmpty_AndNegativeTickReturnsAll()
    {
        var diary = new Diary();
        diary.Append(E(10, "Abigail", "Saw"));
        diary.Append(E(20, "Pierre", "SpokeWith"));

        Assert.Empty(diary.Since(21));
        Assert.Empty(diary.Since(int.MaxValue));
        Assert.Equal(2, diary.Since(-1).Count());     // every tick is >= -1
        Assert.Equal(2, diary.Since(int.MinValue).Count());
    }

    [Fact]
    public void Since_ReflectsLaterAppends()
    {
        var diary = new Diary();
        diary.Append(E(10, "Abigail", "Saw"));
        Assert.Single(diary.Since(0));

        diary.Append(E(11, "Abigail", "Saw"));
        Assert.Equal(2, diary.Since(0).Count());
    }

    // ---------------------------------------------------------------- Recent

    [Fact]
    public void Recent_ReturnsNewestFirst()
    {
        var e1 = E(10, "Abigail", "Saw");
        var e2 = E(20, "Pierre", "SpokeWith");
        var e3 = E(30, "Player", "ReceivedGift", "quartz");
        var diary = new Diary();
        diary.Append(e1);
        diary.Append(e2);
        diary.Append(e3);

        Assert.Equal(new[] { e3, e2 }, diary.Recent(2));
        Assert.Equal(new[] { e3, e2, e1 }, diary.Recent(3));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 3)]   // more than length -> all
    [InlineData(100, 3)] // far more than length -> all
    public void Recent_ClampsCountAboveLength(int count, int expected)
    {
        var diary = new Diary();
        diary.Append(E(10, "Abigail", "Saw"));
        diary.Append(E(20, "Pierre", "SpokeWith"));
        diary.Append(E(30, "Player", "ReceivedGift"));

        var result = diary.Recent(count).ToList();
        Assert.Equal(expected, result.Count);
        // newest-first: the tail of insertion order, reversed
        Assert.Equal(diary.Entries.Reverse().Take(expected), result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void Recent_NonPositiveCount_IsEmpty(int count)
    {
        var diary = new Diary();
        diary.Append(E(10, "Abigail", "Saw"));

        Assert.Empty(diary.Recent(count));
    }

    // ---------------------------------------------------------------- About

    [Fact]
    public void About_MatchesSubjectCaseInsensitively_OldestFirst()
    {
        var lower = E(10, "abigail", "Saw");
        var upper = E(20, "ABIGAIL", "SpokeWith", "the farm");
        var other = E(30, "Pierre", "SpokeWith");
        var diary = new Diary();
        diary.Append(lower);
        diary.Append(other);
        diary.Append(upper);

        Assert.Equal(new[] { lower, upper }, diary.About("Abigail"));
        Assert.Equal(new[] { lower, upper }, diary.About("abigail"));
        Assert.Equal(new[] { lower, upper }, diary.About("ABIGAIL"));
        Assert.Equal(new[] { lower, upper }, diary.About("aBIGail"));
    }

    [Fact]
    public void About_UnknownOrMismatchedSubject_IsEmpty()
    {
        var diary = new Diary();
        diary.Append(E(10, "Abigail", "Saw"));
        diary.Append(E(20, "Pierre", "SpokeWith"));

        Assert.Empty(diary.About("Player"));
        Assert.Empty(diary.About("Abigai"));   // no partial matching
        Assert.Empty(diary.About("Abigail "));  // no trimming
    }

    [Fact]
    public void About_MatchesAreExactSubjectOnly_NotDetailOrKind()
    {
        var diary = new Diary();
        diary.Append(E(10, "Player", "Saw", "Abigail"));
        diary.Append(E(20, "Abigail", "Saw", "Player"));

        var about = diary.About("Abigail").ToList();
        Assert.Single(about);
        Assert.Equal(20, about[0].AbsoluteTick);
    }

    // ---------------------------------------------------------------- JSON

    [Fact]
    public void Json_RoundTripsAllEntriesExactly_IncludingNullDetail()
    {
        var nullDetail = E(30, "Abigail", "Saw");
        var richDetail = E(10, "Pierre", "\"quoted\"", "seed\nstock, 50g");
        var diary = new Diary();
        diary.Append(richDetail);
        diary.Append(nullDetail);

        string json = diary.ToJson();
        var restored = Diary.FromJson(json);

        Assert.Equal(diary.Entries, restored.Entries);
        Assert.Null(restored.Entries[1].Detail);
        Assert.Equal("seed\nstock, 50g", restored.Entries[0].Detail);
        Assert.Equal("Abigail", restored.Entries[1].Subject);
        Assert.Equal("\"quoted\"", restored.Entries[0].Kind);

        // serializing the restored diary reproduces the same bytes
        Assert.Equal(json, restored.ToJson());
        Assert.Equal(json, Diary.FromJson(json).ToJson());
    }

    [Fact]
    public void Json_IsAFlatArrayOfEntries_WithTheExpectedFields()
    {
        var diary = new Diary();
        diary.Append(E(30, "Abigail", "Saw"));
        diary.Append(E(10, "Pierre", "SpokeWith", "seed stock"));

        using var doc = JsonDocument.Parse(diary.ToJson());

        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());

        var first = doc.RootElement[0];
        Assert.Equal(30, first.GetProperty("AbsoluteTick").GetInt32());
        Assert.Equal("Abigail", first.GetProperty("Subject").GetString());
        Assert.Equal("Saw", first.GetProperty("Kind").GetString());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("Detail").ValueKind);

        var second = doc.RootElement[1];
        Assert.Equal("seed stock", second.GetProperty("Detail").GetString());
    }

    [Fact]
    public void Json_RoundTripPreservesInsertionOrder()
    {
        var a = E(50, "Abigail", "Saw");
        var b = E(5, "Abigail", "Saw");
        var diary = new Diary();
        diary.Append(a);
        diary.Append(b);

        var restored = Diary.FromJson(diary.ToJson());
        Assert.Equal(new[] { a, b }, restored.Entries); // not sorted by tick
    }

    [Fact]
    public void Json_EmptyDiary_RoundTripsToEmptyArray()
    {
        var diary = new Diary();

        Assert.Equal("[]", diary.ToJson());

        var restored = Diary.FromJson("[]");
        Assert.Empty(restored.Entries);
        Assert.Equal("[]", restored.ToJson());
    }

    [Fact]
    public void FromJson_ProducesAnIndependentDiaryThatCanBeAppendedTo()
    {
        var diary = new Diary();
        diary.Append(E(10, "Abigail", "Saw"));
        string json = diary.ToJson();

        var restored = Diary.FromJson(json);
        restored.Append(E(20, "Pierre", "SpokeWith"));

        Assert.Single(diary.Entries);          // original untouched
        Assert.Equal(2, restored.Entries.Count);
        Assert.Equal(2, Diary.FromJson(restored.ToJson()).Entries.Count);
    }

    // ---------------------------------------------------------------- Empty diary

    [Fact]
    public void EmptyDiary_AllQueriesBehave()
    {
        var diary = new Diary();

        Assert.Empty(diary.Entries);
        Assert.Empty(diary.Since(0));
        Assert.Empty(diary.Since(-5));
        Assert.Empty(diary.Since(int.MaxValue));
        Assert.Empty(diary.Recent(0));
        Assert.Empty(diary.Recent(10));
        Assert.Empty(diary.Recent(-3));
        Assert.Empty(diary.About("Abigail"));
        Assert.Empty(diary.About(""));
    }
}
