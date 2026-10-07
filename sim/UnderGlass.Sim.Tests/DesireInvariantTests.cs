using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// D35: the desire gate's run-wide invariants (phase 0d; rule 10) over the default town, seeds 1-3,
/// 56 days. Each seed is run with the town's shipped feelings, and with every added rule on
/// (light acts, stance, Fond, Pity, the first greeting, and the power of acting at weight 1).
/// Everything is checked from the result, except reach (invariant 6), which needs where people
/// stood: the run records everyone's place and tile at the end of each tick, and nobody moves
/// between the gate and the end of the minute (only Live walks, and it runs first). Whether the
/// target was free can't be read afterwards (the act itself makes both busy), so invariant 6
/// checks that they were awake, in the same place, within reach and in sight.
/// </summary>
public class DesireInvariantTests
{
    private const int Days = 56;
    private const int D = Clock.MinutesPerDay;

    /// <summary>The town's feelings, as shipped or with every added switch on.</summary>
    private static FeelingOptions Options(bool allOn)
    {
        FeelingOptions o = DefaultTown.Feelings();
        if (allOn)
        {
            o.LightActsOn = true;
            o.StanceOn = true;
            o.FondOn = true;
            o.PityOn = true;
            o.ToneOn = true;
            o.PowerWeight = 1;
        }
        return o;
    }

    public static IEnumerable<object[]> Runs()
    {
        foreach (bool allOn in new[] { false, true })
            for (long seed = 1; seed <= 3; seed++)
                yield return new object[] { seed, allOn };
    }

    private sealed record Spot(string Place, Tile At, bool Asleep);

    /// <summary>A run of the default town, with everyone's position at the end of each tick.</summary>
    private static (SimResult R, Dictionary<int, Dictionary<string, Spot>> Where) Run(long seed, bool allOn)
    {
        var where = new Dictionary<int, Dictionary<string, Spot>>();
        string[] names = DefaultTown.Cast().Select(v => v.Name).ToArray();
        var sim = new Simulation(seed, feelings: Options(allOn));
        SimResult r = sim.Run(Days, (m, s) =>
        {
            if (m % Clock.TickMinutes != 0)
                return;
            var now = new Dictionary<string, Spot>(names.Length);
            foreach (string n in names)
            {
                var (place, at, asleep, _) = s.Where(n);
                now[n] = new Spot(place, at, asleep);
            }
            where[m] = now;
        });
        return (r, where);
    }

    private static readonly Dictionary<string, Villager> Cast = DefaultTown.Cast().ToDictionary(v => v.Name);

    /// <summary>Kin or housemates (families cover).</summary>
    private static bool Close(string a, string b)
        => Cast[a].Household == Cast[b].Household || Cast[a].KinOf(b) is not null || Cast[b].KinOf(a) is not null;

    private static double Min(string kind, FeelingOptions o) => kind switch
    {
        "GaveGift" => o.GiftMin,
        "HelpedSomeone" => o.HelpMin,
        Simulation.Snubbed => o.SnubMin,
        Simulation.TurnedAway => o.SnubMin,
        _ => o.ArgueMin,
    };

    private static bool Friendly(string kind) => kind is "GaveGift" or "HelpedSomeone";

    /// <summary>The heavy hostile acts of a run: arguments with a target, and confrontations.</summary>
    private static IEnumerable<(string Actor, string To, int Tick, string Kind)> Heavy(SimResult r)
        => r.Acts.Where(a => a.Kind == "Argued" && a.Target is not null).Select(a => (a.Actor, To: a.Target!, a.Tick, a.Kind))
            .Concat(r.Confrontations.Select(c => (Actor: c.By, To: c.Target, c.Tick, Kind: "Confront")));

    /// <summary>Invariant 3 in full: a confrontation, too, comes at least 3 days after the pair's
    /// last heavy hostile act. Seed 3 of the shipped town: Lewis argues with Pam over her
    /// rummaging in his bin (a Retaliate motive, minute 77330), and 17 hours later confronts her
    /// over the same act (78350). Rule 9's confrontation stamps the cooldown but never reads it.</summary>
    [Fact]
    public void ConfrontationsKeepTheCooldown()
    {
        SimResult r = new Simulation(3, feelings: Options(false)).Run(Days);
        foreach (var pair in Heavy(r).GroupBy(x => (x.Actor, x.To)))
        {
            var ticks = pair.Select(x => x.Tick).OrderBy(t => t).ToList();
            for (int i = 1; i < ticks.Count; i++)
                Assert.True(ticks[i] - ticks[i - 1] >= 3 * D, $"{pair.Key.Actor} -> {pair.Key.To} at {ticks[i - 1]} and {ticks[i]}");
        }
    }

    [Theory]
    [MemberData(nameof(Runs))]
    public void TheGateKeepsItsInvariants(long seed, bool allOn)
    {
        FeelingOptions o = Options(allOn);
        // The second run for invariant 10 goes alongside the first (runs share no state). With every
        // rule on a run takes about five times as long (it has about five times the acts), so only
        // seed 1 is run twice there.
        Task<string>? again = allOn && seed != 1 ? null
            : Task.Run(() => Metrics.LogHash(new Simulation(seed, feelings: Options(allOn)).Run(Days)));
        var (r, where) = Run(seed, allOn);
        var locations = DefaultTown.Locations().ToDictionary(l => l.Name);
        var po = new PerceptionOptions();

        var gateActs = r.Pursued.OrderBy(id => id).Select(id => r.Acts[id]).ToList();
        Assert.NotEmpty(gateActs); // the gate acts in every run, so the checks below check something
        Assert.NotEmpty(r.Pursuits);

        // 1. Every gate act has a target, and (apart from turning away, which asks no question) a
        //    weighing that acted on it, at an intensity at least its kind's minimum.
        foreach (Act a in gateActs)
        {
            Assert.NotNull(a.Target);
            if (a.Kind == Simulation.TurnedAway)
            {
                Assert.DoesNotContain(r.Pursuits, p => p.ActId == a.Id);
                continue;
            }
            Pursuit p = Assert.Single(r.Pursuits, p => p.Acted && p.ActId == a.Id);
            Assert.Equal((a.Actor, a.Target, a.Kind, a.Tick), (p.Holder, p.Subject, p.ActKind, p.Tick));
            Assert.True(p.Intensity >= Min(a.Kind, o), $"act {a.Id} {a.Kind} at intensity {p.Intensity}");
        }
        Assert.All(r.Pursuits.Where(p => p.Acted), p => Assert.Contains(p.ActId, r.Pursued));

        // 2. No gate act between kin or housemates; no gate hostility toward someone loved (0.4).
        foreach (Act a in gateActs)
        {
            Assert.False(Close(a.Actor, a.Target!), $"gate act {a.Id} {a.Kind} {a.Actor} -> {a.Target}");
            if (a.Kind is "Argued" or Simulation.Snubbed)
                Assert.True(r.AimedAt[a.Id] < o.CoverAt, $"act {a.Id} at regard {r.AimedAt[a.Id]}");
        }

        // 3. Heavy hostile acts per ordered pair at least 3 days apart: every argument, gated or drawn
        //    at a rate, at least 3 days after the pair's last argument or confrontation. (A
        //    confrontation soon after an argument is the deviation in ConfrontationsKeepTheCooldown.)
        foreach (var pair in Heavy(r).GroupBy(x => (x.Actor, x.To)))
        {
            var list = pair.OrderBy(x => x.Tick).ToList();
            for (int i = 1; i < list.Count; i++)
                if (list[i].Kind == "Argued")
                    Assert.True(list[i].Tick - list[i - 1].Tick >= o.HostileCooldownDays * D,
                        $"{pair.Key.Actor} -> {pair.Key.To}: {list[i - 1].Kind} at {list[i - 1].Tick}, Argued at {list[i].Tick}");
        }

        // 4. At most 2 slots per (holder, subject, day): a weighing uses one when it acted or drew a
        //    close call. At most 2 light acts per holder per day, and 1 turning away per pair per day.
        var slots = r.Pursuits.GroupBy(p => (p.Tick, p.Holder, p.Subject, p.Motive))
            .Where(g => g.Any(p => p.Acted || p.Call is "close-yes" or "close-no"))
            .GroupBy(g => (g.Key.Holder, g.Key.Subject, Day: Clock.Day(g.Key.Tick)));
        Assert.All(slots, g => Assert.True(g.Count() <= o.SlotsPerDay, $"{g.Key} used {g.Count()} slots"));
        var light = r.Acts.Where(a => a.Kind is Simulation.Snubbed or Simulation.TurnedAway).ToList();
        Assert.All(light.GroupBy(a => (a.Actor, Clock.Day(a.Tick))), g => Assert.True(g.Count() <= o.LightPerDay));
        Assert.All(light.Where(a => a.Kind == Simulation.TurnedAway).GroupBy(a => (a.Actor, a.Target, Clock.Day(a.Tick))),
            g => Assert.Single(g));

        // 5. No friendly act, gated or drawn at a rate, goes to someone the actor is avoiding.
        foreach (Act a in r.Acts.Where(a => Friendly(a.Kind) && a.Target is not null))
            Assert.DoesNotContain(r.Avoids, v => v.Holder == a.Actor && v.Subject == a.Target && v.Tick < a.Tick && a.Tick < v.Until);

        // 6. Every gate act's target was awake, in the same place, within NearTiles and in sight.
        foreach (Act a in gateActs)
        {
            Spot actor = where[a.Tick][a.Actor], target = where[a.Tick][a.Target!];
            Assert.Equal((a.Location, a.At), (actor.Place, actor.At));
            Assert.False(target.Asleep);
            Assert.Equal(a.Location, target.Place);
            Assert.True(a.At.Chebyshev(target.At) <= po.NearTiles, $"act {a.Id}: {a.At} to {target.At}");
            Assert.True(Perception.LineOfSight(locations[a.Location], a.At, target.At, po) > 0, $"act {a.Id} out of sight");
        }

        // 7. Every Did or Undergone older than OutcomeDays at the end has an outcome; light ones are
        //    never Ignored.
        int end = Days * D;
        var record = r.LifeEvents.Where(e => e.Role is LifeRole.Did or LifeRole.Undergone).ToList();
        Assert.NotEmpty(record);
        Assert.All(record.Where(e => end - e.Tick > o.OutcomeDays * D), e => Assert.NotEqual(Outcome.Open, e.Outcome));
        Assert.All(record.Where(e => e.Light), e => Assert.NotEqual(Outcome.Ignored, e.Outcome));

        // 8. Stance stays in [-1, 1].
        Assert.Equal(Cast.Count, r.Stances.Count);
        Assert.All(r.Stances.Values.SelectMany(s => s), s => Assert.InRange(s, -1, 1));

        // 9. Cash is conserved.
        Assert.Equal(r.TownCash[^1] - r.TownCash[0], r.OutsideIn - r.OutsideOut, 3);

        // 10. The same seed gives the same log; the motive log is kept out of it (empty while acting).
        Assert.Empty(r.MotiveLog);
        if (again is not null)
            Assert.Equal(Metrics.LogHash(r), again.Result);

        if (allOn)
        {
            // With every rule on, the added rules are exercised: light acts, stance and Fond happen.
            Assert.Contains(r.Acts, a => a.Kind == Simulation.Snubbed || a.Kind == Simulation.TurnedAway);
            Assert.Contains(r.Stances.Values.SelectMany(s => s), s => s != 0);
            Assert.Contains(r.Pursuits, p => p.Motive == DesireKind.Fond && p.Acted);
        }
        else
        {
            // As shipped: missing loved ones, stance and power of acting on; light acts and pity off.
            Assert.DoesNotContain(r.Acts, a => a.Kind == Simulation.Snubbed || a.Kind == Simulation.TurnedAway);
            Assert.Contains(r.Stances.Values.SelectMany(s => s), s => s != 0);
            Assert.Contains(r.Pursuits, p => p.Motive == DesireKind.Fond);
            Assert.DoesNotContain(r.Pursuits, p => p.Motive == DesireKind.Pity);
        }
    }
}
