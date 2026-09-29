using NpcSchedules;
using Xunit;

namespace NpcMemory.Tests;

/// <summary>
/// Tests for <see cref="RoutineBelief"/>: the region x time-block co-presence matrix,
/// prior seeding from NpcSchedules.RoutinePrior, decay, best-guess, unlock and JSON.
/// </summary>
public class RoutineBeliefTests
{
    // ---------------------------------------------------------------- construction

    [Fact]
    public void Defaults_TwoHourBlocksTenPerDay_AndLocked()
    {
        var belief = new RoutineBelief("Abby", "Testy");

        Assert.Equal("Abby", belief.Observer);
        Assert.Equal("Testy", belief.Subject);
        Assert.Equal(120, belief.BlockMinutes);
        Assert.Equal(10, belief.BlockCount);           // 120 ticks / 12 ticks per 2h block
        Assert.Equal(240, belief.UnlockThreshold);
        Assert.Equal(0.0, belief.CoPresenceTicks);
        Assert.False(belief.Unlocked);
        Assert.Empty(belief.Counts);
    }

    [Fact]
    public void BlockCount_FollowsBlockMinutes()
    {
        Assert.Equal(20, new RoutineBelief("o", "s", 60).BlockCount);
        Assert.Equal(10, new RoutineBelief("o", "s", 120).BlockCount);
        Assert.Equal(5, new RoutineBelief("o", "s", 240).BlockCount);
    }

    // ---------------------------------------------------------------- Observe

    [Fact]
    public void Observe_CreatesRegionAndAddsToTheRightBlock()
    {
        var belief = new RoutineBelief("Abby", "Testy");

        belief.Observe("Town", 3, 900);

        Assert.True(belief.Counts.ContainsKey("Town"));
        double[] column = belief.Counts["Town"];
        Assert.Equal(belief.BlockCount, column.Length);
        Assert.Equal(1.0, column[3]);
        for (int b = 0; b < column.Length; b++)
            if (b != 3)
                Assert.Equal(0.0, column[b]);
    }

    [Fact]
    public void Observe_AccumulatesIntoTheSameCell()
    {
        var belief = new RoutineBelief("Abby", "Testy");

        belief.Observe("Town", 2, 0);
        belief.Observe("Town", 2, 120);
        belief.Observe("Town", 2, 240);

        Assert.Equal(3.0, belief.Counts["Town"][2]);
    }

    [Fact]
    public void Observe_StrengthMultipliesTheAddedAmount()
    {
        var belief = new RoutineBelief("Abby", "Testy");

        belief.Observe("Town", 4, 0, 2.0);
        belief.Observe("Town", 4, 0, 0.5);
        belief.Observe("Mountain", 1, 0, 3.5);

        Assert.Equal(2.5, belief.Counts["Town"][4], 10);
        Assert.Equal(3.5, belief.Counts["Mountain"][1], 10);
    }

    [Fact]
    public void Observe_TickArgumentIsIgnored_ButRegionKeysAreCaseInsensitive()
    {
        var belief = new RoutineBelief("Abby", "Testy");

        belief.Observe("Town", 0, 1);
        belief.Observe("town", 0, 999_999, 2.0);   // different tick, same region by OrdinalIgnoreCase

        Assert.Single(belief.Counts);
        Assert.Equal(3.0, belief.Counts["Town"][0]);
        Assert.Equal(3.0, belief.Counts["TOWN"][0]);
    }

    [Fact]
    public void Observe_OutOfRangeBlock_Throws()
    {
        var belief = new RoutineBelief("Abby", "Testy");

        Assert.Throws<ArgumentOutOfRangeException>(() => belief.Observe("Town", -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => belief.Observe("Town", belief.BlockCount, 0));
    }

    // ---------------------------------------------------------------- SeedPrior

    [Fact]
    public void SeedPrior_LoadsEveryPriorCellIntoTheRightBlock()
    {
        var routine = TestHelpers.Routine("Testy", "Town");
        var options = new PriorOptions { Kind = RelationshipKind.Friend };
        const string seed = "save-seed-1";

        var belief = new RoutineBelief("Abby", "Testy");
        belief.SeedPrior(routine, seed, options);

        List<RoutinePrior.PriorCell> expected = RoutinePrior.Build(routine, "Abby", seed, options);
        Assert.NotEmpty(expected);

        foreach (RoutinePrior.PriorCell cell in expected)
        {
            int block = -1;
            for (int b = 0; b < belief.BlockCount; b++)
                if (TimeUtils.BlockLabel(b, belief.BlockMinutes) == cell.Block)
                {
                    block = b;
                    break;
                }

            Assert.True(block >= 0, $"no block index for label {cell.Block}");
            Assert.True(belief.Counts.ContainsKey(cell.Region), $"missing region {cell.Region}");
            Assert.Equal(cell.Count, belief.Counts[cell.Region][block], 10);
        }

        foreach (double[] column in belief.Counts.Values)
            Assert.Equal(belief.BlockCount, column.Length);

        // The prior is scaled to the friend strength, so the total mass is about that.
        double total = belief.Counts.Values.SelectMany(c => c).Sum();
        Assert.InRange(total, options.FriendStrength * 0.75, options.FriendStrength * 1.25);
    }

    [Fact]
    public void SeedPrior_IsDeterministic_ForSameInputs()
    {
        var routine = TestHelpers.Routine("Testy", "Town");
        var options = new PriorOptions { Kind = RelationshipKind.Family };

        var a = new RoutineBelief("Abby", "Testy");
        var b = new RoutineBelief("Abby", "Testy");
        a.SeedPrior(routine, "seed", options);
        b.SeedPrior(routine, "seed", options);

        Assert.Equal(a.Counts.Keys.OrderBy(k => k), b.Counts.Keys.OrderBy(k => k));
        foreach (string region in a.Counts.Keys)
            for (int i = 0; i < a.BlockCount; i++)
                Assert.Equal(a.Counts[region][i], b.Counts[region][i]);
    }

    [Fact]
    public void SeedPrior_AccumulatesOnTopOfExistingCounts()
    {
        var routine = TestHelpers.Routine("Testy", "Town");
        var options = new PriorOptions { Kind = RelationshipKind.Family };

        var belief = new RoutineBelief("Abby", "Testy");
        belief.Observe("Town", 0, 0, 100.0);        // already-known presence at block 0
        belief.SeedPrior(routine, "seed", options);

        double seededBlock0 = belief.Counts["Town"][0];
        Assert.True(seededBlock0 > 100.0, "prior must add to the existing count, not overwrite it");

        // Seeding again doubles the prior mass but keeps our observation there exactly once.
        var second = new RoutineBelief("Abby", "Testy");
        second.Observe("Town", 0, 0, 100.0);
        second.SeedPrior(routine, "seed", options);
        second.SeedPrior(routine, "seed", options);

        var priorOnly = new RoutineBelief("Abby", "Testy");
        priorOnly.SeedPrior(routine, "seed", options);
        priorOnly.SeedPrior(routine, "seed", options);

        for (int b = 0; b < belief.BlockCount; b++)
            Assert.Equal(priorOnly.Counts["Town"][b] + (b == 0 ? 100.0 : 0.0), second.Counts["Town"][b], 8);
    }

    [Fact]
    public void SeedPrior_MismatchedBlockMinutes_Throws()
    {
        var routine = TestHelpers.Routine("Testy", "Town", blockMinutes: 60);
        var belief = new RoutineBelief("Abby", "Testy", blockMinutes: 120);

        var ex = Assert.Throws<ArgumentException>(() => belief.SeedPrior(routine, "seed", new PriorOptions()));
        Assert.Equal("routine", ex.ParamName);
        Assert.Empty(belief.Counts);
    }

    [Fact]
    public void SeedPrior_MatchingNonDefaultBlockSize_Works()
    {
        var routine = TestHelpers.Routine("Testy", "Forest", blockMinutes: 240);
        var belief = new RoutineBelief("Abby", "Testy", blockMinutes: 240);

        belief.SeedPrior(routine, "seed", new PriorOptions { Kind = RelationshipKind.Family });

        Assert.Equal(5, belief.BlockCount);
        Assert.True(belief.Counts.ContainsKey("Forest"));
        Assert.Equal(5, belief.Counts["Forest"].Length);
    }

    // ---------------------------------------------------------------- Co-presence / unlock

    [Fact]
    public void NoteCoPresence_AccumulatesAndUnlocksAtTheThreshold()
    {
        var belief = new RoutineBelief("Abby", "Testy");
        Assert.False(belief.Unlocked);

        belief.NoteCoPresence(100);
        Assert.Equal(100.0, belief.CoPresenceTicks);
        Assert.False(belief.Unlocked);

        belief.NoteCoPresence(139);
        Assert.Equal(239.0, belief.CoPresenceTicks);
        Assert.False(belief.Unlocked);

        belief.NoteCoPresence(1);                  // exactly the threshold
        Assert.Equal(240.0, belief.CoPresenceTicks);
        Assert.True(belief.Unlocked);
    }

    [Fact]
    public void NoteCoPresence_RespectsACustomThreshold()
    {
        var belief = new RoutineBelief("Abby", "Testy") { UnlockThreshold = 10 };

        belief.NoteCoPresence(9);
        Assert.False(belief.Unlocked);

        belief.NoteCoPresence(1);
        Assert.True(belief.Unlocked);
    }

    // ---------------------------------------------------------------- Decay

    [Fact]
    public void Decay_ScalesEveryCellInEveryRegion()
    {
        var belief = new RoutineBelief("Abby", "Testy");
        belief.Observe("Town", 1, 0, 4.0);
        belief.Observe("Town", 6, 0, 2.0);
        belief.Observe("Beach", 3, 0, 8.0);

        belief.Decay(0.5);

        Assert.Equal(2.0, belief.Counts["Town"][1], 10);
        Assert.Equal(1.0, belief.Counts["Town"][6], 10);
        Assert.Equal(4.0, belief.Counts["Beach"][3], 10);
        Assert.Equal(0.0, belief.Counts["Town"][0], 10);
    }

    [Fact]
    public void Decay_ClampsFactorToUnitRange()
    {
        var belief = new RoutineBelief("Abby", "Testy");
        belief.Observe("Town", 0, 0, 5.0);

        belief.Decay(3.0);                       // > 1 clamps to 1: unchanged
        Assert.Equal(5.0, belief.Counts["Town"][0], 10);

        belief.Decay(-2.0);                      // < 0 clamps to 0: cleared
        Assert.Equal(0.0, belief.Counts["Town"][0], 10);

        belief.Observe("Town", 0, 0, 5.0);
        belief.Decay(1.0);                       // exactly 1: unchanged
        Assert.Equal(5.0, belief.Counts["Town"][0], 10);
    }

    // ---------------------------------------------------------------- BestGuess

    [Fact]
    public void BestGuess_ReturnsTheHighestCell()
    {
        var belief = new RoutineBelief("Abby", "Testy");
        belief.Observe("Town", 2, 0, 3.0);
        belief.Observe("Town", 5, 0, 1.0);
        belief.Observe("Mountain", 7, 0, 2.0);

        var guess = belief.BestGuess();

        Assert.NotNull(guess);
        Assert.Equal("Town", guess!.Value.Region);
        Assert.Equal(2, guess.Value.Block);
    }

    [Fact]
    public void BestGuess_IsNullWhenNothingHasBeenObserved()
    {
        var belief = new RoutineBelief("Abby", "Testy");

        Assert.Null(belief.BestGuess());

        belief.Observe("Town", 4, 0, 0.0);       // a region exists but every cell is zero
        Assert.Null(belief.BestGuess());
    }

    [Fact]
    public void BestGuess_IsNullAfterEverythingDecaysToZero()
    {
        var belief = new RoutineBelief("Abby", "Testy");
        belief.Observe("Town", 4, 0, 2.0);

        belief.Decay(0.0);

        Assert.Null(belief.BestGuess());
    }

    [Fact]
    public void BestGuess_TieBreaksByRegionNameThenBlockIndex()
    {
        // Equal maxima in two regions: the region name (OrdinalIgnoreCase) decides.
        var byRegion = new RoutineBelief("Abby", "Testy");
        byRegion.Observe("Town", 1, 0, 5.0);
        byRegion.Observe("Beach", 8, 0, 5.0);

        var guess = byRegion.BestGuess();
        Assert.Equal("Beach", guess!.Value.Region);
        Assert.Equal(8, guess.Value.Block);

        // Equal maxima in one region: the lower block index wins.
        var byBlock = new RoutineBelief("Abby", "Testy");
        byBlock.Observe("Town", 9, 0, 5.0);
        byBlock.Observe("Town", 1, 0, 5.0);
        byBlock.Observe("Town", 4, 0, 5.0);

        var guess2 = byBlock.BestGuess();
        Assert.Equal(("Town", 1), guess2!.Value);

        // Order of insertion must not matter.
        var reversed = new RoutineBelief("Abby", "Testy");
        reversed.Observe("Town", 1, 0, 5.0);
        reversed.Observe("Beach", 8, 0, 5.0);
        Assert.Equal(("Beach", 8), reversed.BestGuess()!.Value);
    }

    [Fact]
    public void BestGuess_AfterSeedPrior_PicksAnObservedCellOverTheSpreadPrior()
    {
        var routine = TestHelpers.Routine("Testy", "Town");
        var belief = new RoutineBelief("Abby", "Testy");
        belief.SeedPrior(routine, "seed", new PriorOptions { Kind = RelationshipKind.Family });
        belief.Observe("Mountain", 9, 0, 500.0);   // strong first-hand evidence elsewhere

        var guess = belief.BestGuess();

        Assert.Equal("Mountain", guess!.Value.Region);
        Assert.Equal(9, guess.Value.Block);
    }

    // ---------------------------------------------------------------- JSON

    [Fact]
    public void Json_RoundTripsEverything()
    {
        var belief = new RoutineBelief("Abby", "Testy");
        belief.Observe("Town", 2, 1234, 2.5);
        belief.Observe("Town", 2, 1234, 0.5);
        belief.Observe("Beach", 7, 0, 1.25);
        belief.NoteCoPresence(100);
        belief.NoteCoPresence(30);
        belief.UnlockThreshold = 300;

        string json = belief.ToJson();
        RoutineBelief back = RoutineBelief.FromJson(json);

        Assert.Equal(belief.Observer, back.Observer);
        Assert.Equal(belief.Subject, back.Subject);
        Assert.Equal(belief.BlockMinutes, back.BlockMinutes);
        Assert.Equal(belief.BlockCount, back.BlockCount);
        Assert.Equal(300, back.UnlockThreshold);
        Assert.Equal(130.0, back.CoPresenceTicks, 10);
        Assert.False(back.Unlocked);

        Assert.Equal(belief.Counts.Count, back.Counts.Count);
        foreach (KeyValuePair<string, double[]> pair in belief.Counts)
        {
            Assert.True(back.Counts.ContainsKey(pair.Key), $"region {pair.Key} missing after round-trip");
            double[] restored = back.Counts[pair.Key];
            Assert.Equal(belief.BlockCount, restored.Length);
            for (int b = 0; b < restored.Length; b++)
                Assert.Equal(pair.Value[b], restored[b], 10);
        }

        Assert.Equal(3.0, back.Counts["Town"][2], 10);
        Assert.Equal(1.25, back.Counts["Beach"][7], 10);

        // Round-tripping twice is stable and the reloaded instance still behaves.
        Assert.Equal(json, RoutineBelief.FromJson(back.ToJson()).ToJson());
        Assert.Equal(("Town", 2), back.BestGuess()!.Value);
        back.Observe("Desert", 0, 0, 1.0);
        Assert.Equal(("Town", 2), back.BestGuess()!.Value);
    }

    [Fact]
    public void Json_RoundTripPreservesAnEmptyBelief()
    {
        var belief = new RoutineBelief("Abby", "Testy", blockMinutes: 240);

        RoutineBelief back = RoutineBelief.FromJson(belief.ToJson());

        Assert.Equal(240, back.BlockMinutes);
        Assert.Equal(5, back.BlockCount);
        Assert.Empty(back.Counts);
        Assert.Equal(0.0, back.CoPresenceTicks);
        Assert.Equal(240, back.UnlockThreshold);
        Assert.Null(back.BestGuess());
    }

    [Fact]
    public void Json_RegionLookupStaysCaseInsensitiveAfterRoundTrip()
    {
        var belief = new RoutineBelief("Abby", "Testy");
        belief.Observe("Town", 1, 0, 1.0);

        RoutineBelief back = RoutineBelief.FromJson(belief.ToJson());

        Assert.True(back.Counts.ContainsKey("town"));
    }

    [Fact]
    public void Json_FromJsonRejectsGarbage()
    {
        Assert.Throws<ArgumentNullException>(() => RoutineBelief.FromJson(null!));
        Assert.ThrowsAny<Exception>(() => RoutineBelief.FromJson("not json at all"));
        Assert.Throws<ArgumentException>(() => RoutineBelief.FromJson("{}")); // block size 0
    }
}
