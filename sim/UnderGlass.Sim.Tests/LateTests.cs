using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// The act catalog's slice acts-6, Late (acts spec 4.11): LateForWork, derived from the late check.
/// On the shipped town: each coming-in-late follows a late mark for that person and day, happens at
/// their job place, at most once a day; it is felt by the late one alone, and seen; it happens with
/// feelings off too; and with Late off nothing changes.
/// </summary>
public class LateTests
{
    private static (SimResult R, FeelingOptions O) Town(FeelingOptions o, int days = 56, long seed = 1)
    {
        o.Acts.Late = true;
        return (new Simulation(seed, kinds: ActCatalog.Kinds(o.Acts), feelings: o).Run(days), o);
    }

    [Fact]
    public void ComingInLateFollowsALateMarkAtTheJobPlace()
    {
        var (r, _) = Town(DefaultTown.Feelings());
        var jobs = DefaultTown.Cast().Where(v => v.Job is not null).ToDictionary(v => v.Name, v => v.Job!.Place);
        var late = r.Acts.Where(a => a.Kind == "LateForWork").ToList();
        Assert.NotEmpty(late);
        var marks = r.Late.ToHashSet();
        foreach (Act a in late)
        {
            Assert.Contains((a.Actor, Clock.Day(a.Tick)), marks);
            Assert.Equal(jobs[a.Actor], a.Location);
            Assert.Null(a.Target);
        }
        Assert.All(late.GroupBy(a => (a.Actor, Clock.Day(a.Tick))), g => Assert.Single(g));
        // The late one feels it; someone sees them come in.
        Assert.Contains(r.Feelings, f => f.Route == "Undergone" && late.Any(a => a.Id == f.ActId && a.Actor == f.Holder) && f.Mood < 0);
        Assert.Contains(late, a => r.Witnesses.GetValueOrDefault(a.Id) > 0);
    }

    [Fact]
    public void ItHappensWithFeelingsOffToo()
    {
        var (r, _) = Town(FeelingOptions.Off, 28);
        Assert.Contains(r.Acts, a => a.Kind == "LateForWork");
        Assert.DoesNotContain(r.Feelings, f => true);
    }

    [Fact]
    public void WithLateOffNothingComesIn()
    {
        FeelingOptions o = DefaultTown.Feelings();
        SimResult r = new Simulation(1, kinds: ActCatalog.Kinds(new ActOptions().With("late")), feelings: o).Run(28);
        Assert.DoesNotContain(r.Acts, a => a.Kind == "LateForWork");
        Assert.Equal(Metrics.LogHash(new Simulation(1, feelings: DefaultTown.Feelings()).Run(28)), Metrics.LogHash(r));
    }
}
