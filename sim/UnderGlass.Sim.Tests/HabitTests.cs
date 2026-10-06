using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Acting normal: odd hours and curfews (design rules 1 and 17).</summary>
public class HabitTests
{
    private static readonly IReadOnlyList<ActKind> Kinds = new[]
    {
        new ActKind(Simulation.OutLate, 2.0, 0, 1, 1, 0, Array.Empty<string>()),
        new ActKind(Simulation.FamilyRow, 3.0, -1, 2, 15, 0, Array.Empty<string>()),
    };

    /// <summary>Obs spends every hour in the room and never sleeps. Sub, who lives at home, is in
    /// the room 10:00-20:00 each day, and on day 3 comes out at night (a one-off gathering).</summary>
    private static SimResult Night(int subAge, bool observerIsParent)
    {
        var places = new[]
        {
            new Location("Room", false, Enumerable.Repeat(new string('.', 20), 6).ToList()),
            new Location("Home:S", false, Enumerable.Repeat(new string('.', 10), 8).ToList()),
        };
        var sub = new Villager("Sub", "S", "villager", new Temperament(0.5, 0.5, 0.5, 0.5), new Body(100, -1), null,
            new[] { new Haunt("Room", new Tile(7, 2), Clock.At(10), Clock.At(20), 1) },
            new Dictionary<string, double>(), Array.Empty<string>(), subAge,
            observerIsParent ? new Dictionary<string, Kin> { ["Obs"] = Kin.Parent } : null);
        var obs = new Villager("Obs", observerIsParent ? "S" : "O", "villager", new Temperament(0.5, 0.5, 0.5, 0.5), new Body(100, -1), null,
            new[] { new Haunt("Room", new Tile(5, 2), 0, Clock.MinutesPerDay, 1000) },
            new Dictionary<string, double>(), Array.Empty<string>(), 45,
            observerIsParent ? new Dictionary<string, Kin> { ["Sub"] = Kin.Child } : null);
        var night = new Gathering("Night", "Room", new Tile(7, 2), 0, Clock.At(22), Clock.MinutesPerDay, Array.Empty<int>(), 1000, OnlyDay: 3);
        return new Simulation(1, new[] { sub, obs }, places, Kinds, wander: 0, gatherings: new[] { night },
            body: new BodyOptions { AwakeHoursAtRest = 100_000 }).Run(5);
    }

    [Fact]
    public void SomeoneSeenOutAtAnHourTheyNeverKeep_AtNight_IsWorthTalkingAbout()
    {
        SimResult r = Night(subAge: 30, observerIsParent: false);
        // (Sub, out at night for once, also finds Obs out at an hour Sub never saw: a story both ways.)
        Act late = Assert.Single(r.Acts, a => a.Kind == Simulation.OutLate && a.Actor == "Sub");
        Assert.Equal(3, Clock.Day(late.Tick));
        Assert.True(Clock.OfDay(late.Tick) >= Clock.At(22));
        Assert.Equal("Sub", r.Beliefs["Obs"][late.Id].Actor);
        Assert.DoesNotContain(r.Acts, a => a.Kind == Simulation.FamilyRow); // no curfew: not family
    }

    [Fact]
    public void AParentWhoSeesTheirTeenOutPastCurfew_HasItOutWithThem()
    {
        SimResult r = Night(subAge: 15, observerIsParent: true);
        Assert.Contains(r.Acts, a => a.Kind == Simulation.OutLate && a.Actor == "Sub");
        Assert.Contains(r.Acts, a => a.Kind == Simulation.FamilyRow && a.Actor == "Sub");
    }

    [Fact]
    public void AGrownChildOutAt22IsWithinCurfew()
    {
        SimResult r = Night(subAge: 25, observerIsParent: true); // grown children: curfew 1:00
        Assert.Contains(r.Acts, a => a.Kind == Simulation.OutLate);
        Assert.DoesNotContain(r.Acts, a => a.Kind == Simulation.FamilyRow);
    }
}
