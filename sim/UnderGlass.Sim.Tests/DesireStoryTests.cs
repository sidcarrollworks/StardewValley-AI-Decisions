using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// Golden stories of the desire gate in the town as it ships (phase 0d; spec section 9), each the
/// first of its kind in seeds 1-3: a feud that grows from answering back (G1), and a friendship
/// that grows from kindness returned (G2). Each runs only as long as its story needs. The hermit
/// and the brawler (G3, G4) are measured, not pinned: neither appears as the spec expected
/// (`sim/README.md`, phase 0d.5).
/// </summary>
public class DesireStoryTests
{
    private static readonly Dictionary<string, Villager> Cast = DefaultTown.Cast().ToDictionary(v => v.Name);

    private static bool Close(string a, string b)
        => Cast[a].Household == Cast[b].Household || Cast[a].KinOf(b) is not null || Cast[b].KinOf(a) is not null;

    private static bool Between(Act a, string x, string y) => (a.Actor, a.Target) == (x, y) || (a.Actor, a.Target) == (y, x);

    /// <summary>G1, seed 1: Lewis argues with Pam at the saloon on the first evening. She answers
    /// within the half hour, citing it, and they answer each other every few days (each after the
    /// pair's cooldown) until both are under -0.3: a feud on day 10.</summary>
    [Fact]
    public void G1_AFeudGrowsFromAnsweringBack()
    {
        SimResult r = new Simulation(1, feelings: DefaultTown.Feelings()).Run(11);
        var tie = r.Ties.First(t => t.What == "feud" && !Close(t.A, t.B));
        Assert.Equal((10, "Lewis", "Pam"), (tie.Day, tie.A, tie.B));

        var hostile = r.Acts.Where(a => a.Kind == "Argued" && Between(a, tie.A, tie.B)).ToList();
        Act first = hostile[0];
        Assert.DoesNotContain(first.Id, r.Pursued); // a spark the town's rates drew
        // The one argued with wants to answer, citing it, and does.
        Stirring stir = r.Stirred.First(s => s.Holder == first.Target && s.Subject == first.Actor);
        Assert.Equal((DesireKind.Answer, first.Id), (stir.Motive, stir.Source));
        Act answer = hostile[1];
        Assert.Equal((first.Target, first.Actor, first.Id), (answer.Actor, answer.Target, answer.About));
        Assert.Contains(answer.Id, r.Pursued);
        int stirredAt = r.Log.ToList().FindIndex(l => l.StartsWith($"{stir.Tick} stirred {first.Target} Answer {first.Actor} act {first.Id} "));
        int desireAt = r.Log.ToList().FindIndex(l => l.StartsWith($"{answer.Tick} desire {first.Target} Answer {first.Actor} act {first.Id}: Argued "));
        Assert.True(stirredAt >= 0 && desireAt > stirredAt);
        // At least one answer each way from the gate, every one citing the other's last argument.
        foreach (string who in new[] { tie.A, tie.B })
            Assert.Contains(hostile, a => a.Actor == who && r.Pursued.Contains(a.Id));
        for (int k = 1; k < hostile.Count; k++)
            if (r.Pursued.Contains(hostile[k].Id))
                Assert.Equal(hostile.Last(a => a.Tick < hostile[k].Tick && a.Actor == hostile[k].Target).Id, hostile[k].About);
        Assert.True(r.Regard[(tie.A, tie.B)] <= -0.3 && r.Regard[(tie.B, tie.A)] <= -0.3);
    }

    /// <summary>G2, seed 2: Vincent gives Penny, his teacher, a gift on day 10 at a rate, and she
    /// gives one back. From then each kindness of hers is returned by him through the gate, and on
    /// day 80 they are friends (0.4 or more both ways), across households.</summary>
    [Fact]
    public void G2_AFriendshipGrowsFromKindnessReturned()
    {
        SimResult r = new Simulation(2, feelings: DefaultTown.Feelings()).Run(81);
        var tie = r.Ties.First(t => t.What == "friendship");
        Assert.Equal((80, "Penny", "Vincent"), (tie.Day, tie.A, tie.B));
        Assert.False(Close(tie.A, tie.B));

        var kind = r.Acts.Where(a => a.Kind is "GaveGift" or "HelpedSomeone" && Between(a, tie.A, tie.B)).ToList();
        Act first = kind[0];
        bool fond = r.Pursuits.Any(p => p.ActId == first.Id && p.Motive == DesireKind.Fond);
        Assert.True(!r.Pursued.Contains(first.Id) || fond, "the first kindness is drawn at a rate or comes from missing someone");
        // Returns follow, each way.
        var returns = r.Pursuits.Where(p => p.Acted && p.Motive == DesireKind.Return && Between(r.Acts[p.ActId], tie.A, tie.B)).ToList();
        foreach (string who in new[] { tie.A, tie.B })
            Assert.Contains(returns, p => p.Holder == who);
        foreach (Pursuit p in returns)
        {
            Act cited = r.Acts[p.Source];
            Assert.Equal((p.Subject, p.Holder), (cited.Actor, cited.Target)); // each return cites the other's kindness
        }
        Assert.True(r.Regard[(tie.A, tie.B)] >= 0.4 && r.Regard[(tie.B, tie.A)] >= 0.4);
    }
}
