using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// The act catalog's slice acts-2, Company (acts spec 3.2, 4.4, 4.5 and question 1): the per-head
/// draws, PlayedGame and TreatedToDrink, and Fond's gifts kept to occasions. Read on a stretch of the
/// shipped town with Returns and Company on: who draws, where and when, at what ages, what a treat
/// costs (money is conserved to the gram), and that Fond gives only on a birthday or a festival.
/// </summary>
public class CompanyTests
{
    private static (SimResult R, IReadOnlyList<Villager> Cast) Town(string slices, int days, long seed = 1)
    {
        FeelingOptions o = DefaultTown.Feelings();
        o.Acts.With(slices);
        IReadOnlyList<Villager> cast = ActCatalog.Cards(DefaultTown.Cast(), o.Acts);
        return (new Simulation(seed, cast: cast, kinds: ActCatalog.Kinds(o.Acts), feelings: o).Run(days), cast);
    }

    private static readonly Lazy<(SimResult R, IReadOnlyList<Villager> Cast)> Season = new(() => Town("returns,company", 56));

    [Fact]
    public void PerHeadDrawsAreMadeOnlyByThoseWhoCarryTheKind()
    {
        var (r, cast) = Season.Value;
        var byName = cast.ToDictionary(v => v.Name);
        string[] perHead = { "Complimented", "Joked", "PlayedGame", "TreatedToDrink" };
        var drawn = r.Acts.Where(a => perHead.Contains(a.Kind) && !r.Pursued.Contains(a.Id)).ToList();
        Assert.NotEmpty(drawn);
        Assert.All(drawn, a => Assert.True(byName[a.Actor].Acts.GetValueOrDefault(a.Kind) > 0, $"{a.Actor} drew {a.Kind} without its card"));
        foreach (string kind in perHead)
            Assert.Contains(drawn, a => a.Kind == kind);
        // Gate acts of the same kinds may be anyone's.
        Assert.Contains(r.Acts, a => perHead.Contains(a.Kind) && r.Pursued.Contains(a.Id));
    }

    [Fact]
    public void GamesKeepTheirPlacesHoursAndAges()
    {
        var (r, cast) = Season.Value;
        var age = cast.ToDictionary(v => v.Name, v => v.Age);
        var games = r.Acts.Where(a => a.Kind == "PlayedGame").ToList();
        Assert.NotEmpty(games);
        foreach (Act g in games)
        {
            int t = Clock.OfDay(g.Tick);
            Assert.True(g.Location == "Saloon" ? t >= Clock.At(17) : g.Location is "Square" or "Beach" && t >= Clock.At(9) && t < Clock.At(19), $"{g.Location} at {t}");
            int a = age[g.Actor], b = age[g.Target!];
            Assert.True(Math.Abs(a - b) <= 6 || a < 13 && b < 13, $"{g.Actor} {a} and {g.Target} {b}");
        }
        // Both are busy for the game's half hour: neither starts another act until it ends.
        foreach (Act g in games)
            Assert.DoesNotContain(r.Acts, a => a.Id != g.Id && (a.Actor == g.Actor || a.Actor == g.Target) && a.Tick > g.Tick && a.Tick < g.Tick + 30);
    }

    [Fact]
    public void ATreatIsAtTheBarOfAgeAndPaidFor()
    {
        var (r, cast) = Season.Value;
        var age = cast.ToDictionary(v => v.Name, v => v.Age);
        var treats = r.Acts.Where(a => a.Kind == "TreatedToDrink").ToList();
        Assert.NotEmpty(treats);
        foreach (Act a in treats)
        {
            int t = Clock.OfDay(a.Tick);
            Assert.Equal("Saloon", a.Location);
            Assert.True(t >= Clock.At(18) || t < Clock.At(1));
            Assert.True(age[a.Actor] >= 18 && age[a.Target!] >= 18);
        }
        // Money stays in town: what the town holds changed by exactly what came in and went out.
        Assert.Equal(r.TownCash[^1] - r.TownCash[0], r.OutsideIn - r.OutsideOut, 6);
        // A treat costs the town a restock, so the run's money differs from one without treats.
        var (plain, _) = Town("returns", 56);
        Assert.NotEqual(plain.OutsideOut, r.OutsideOut);
    }

    [Fact]
    public void WithCompanyOnFondGivesOnlyOnOccasions()
    {
        var (r, cast) = Season.Value;
        var byName = cast.ToDictionary(v => v.Name);
        var fondGifts = r.Pursuits.Where(p => p is { Motive: DesireKind.Fond, ActKind: "GaveGift", Acted: true }).ToList();
        foreach (Pursuit p in fondGifts)
        {
            int day = Clock.Day(p.Tick);
            Assert.True(Calendar.IsBirthday(byName[p.Subject].Birthday, day) || Calendar.FestivalOn(day) is not null, $"{p.Holder} to {p.Subject} on day {day}");
        }
        Assert.All(fondGifts.GroupBy(p => (p.Holder, Clock.Day(p.Tick))), g => Assert.Single(g));
        // Without Company, Fond gives on ordinary days too.
        var (plain, _) = Town("returns", 56);
        Assert.Contains(plain.Pursuits, p => p is { Motive: DesireKind.Fond, ActKind: "GaveGift", Acted: true }
                                             && Calendar.FestivalOn(Clock.Day(p.Tick)) is null && !Calendar.IsBirthday(byName[p.Subject].Birthday, Clock.Day(p.Tick)));
        // And with it, Fond reaches for the small acts instead.
        Assert.Contains(r.Pursuits, p => p is { Motive: DesireKind.Fond, Acted: true } && p.ActKind is "PlayedGame" or "TreatedToDrink");
    }

    [Fact]
    public void CompanyWithoutReturnsHasNoComplimentsOrJokes()
    {
        var (r, cast) = Town("company", 28);
        Assert.DoesNotContain(r.Acts, a => a.Kind is "Thanked" or "Complimented" or "Joked");
        Assert.Contains(r.Acts, a => a.Kind is "PlayedGame" or "TreatedToDrink");
        Assert.DoesNotContain(cast, v => v.Acts.ContainsKey("Complimented") || v.Acts.ContainsKey("Joked")); // their cards come with Returns
    }

    [Fact]
    public void PerHeadDrawsAreDeterministic()
    {
        var (a, _) = Town("returns,company", 14, seed: 5);
        var (b, _) = Town("returns,company", 14, seed: 5);
        Assert.Equal(Metrics.LogHash(a), Metrics.LogHash(b));
    }
}
