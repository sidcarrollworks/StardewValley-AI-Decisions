using System.Globalization;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

public class ReflectionRunCancellationTests
{
    private static Simulation Scene(bool reflection)
    {
        Villager Person(string name, int x) => new(name, name, "villager", new Temperament(0.5, 0.5, 0.5, 0.5),
            new Body(100, -1), null, new[] { new Haunt("Room", new Tile(x, 2), 0, Clock.MinutesPerDay, 1) },
            new Dictionary<string, double>(), Array.Empty<string>());
        var town = new TownData
        {
            Cast = new[] { Person("Ann", 3), Person("Bob", 5) },
            Places = new[] { new Location("Room", false, Enumerable.Repeat(new string('.', 10), 6).ToArray()) },
            Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(), Acts = Array.Empty<ActKind>(),
            Feelings = new FeelingOptions { Desire = true }, Authority = new AuthorityOptions { ElectConstable = false },
            Body = new BodyOptions { AwakeHoursAtRest = 100_000 }, Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
        };
        var sim = new Simulation(1, town);
        sim.ConfigureReflection(new ReflectionOptions { Enabled = reflection });
        return sim;
    }

    private sealed class UnusedMind : IReflectionMind
    {
        public int Calls;
        public Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("This scene has no memory from which to request a reflection.");
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    public async Task PrecancellationDoesNotInitializeBodiesOrAdvanceTheWorld(int days, bool reflection)
    {
        Simulation sim = Scene(reflection);
        var ann = sim.Where("Ann");
        var bob = sim.Where("Bob");
        CultureInfo culture = CultureInfo.CurrentCulture;
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        var mind = new UnusedMind();
        int callbacks = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sim.RunAsync(days, mind,
            (_, _) => callbacks++, cancel.Token));

        Assert.Equal(ann, sim.Where("Ann"));
        Assert.Equal(bob, sim.Where("Bob"));
        Assert.Equal(0, callbacks);
        Assert.Equal(0, mind.Calls);
        Assert.Equal(culture, CultureInfo.CurrentCulture);
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(600, true)]
    [InlineData(1439, false)]
    [InlineData(1439, true)]
    public async Task CallbackCancellationStopsAtThatMinuteEvenWhenItIsTheLastAndNoModelRuns(int stopAt, bool reflection)
    {
        Simulation sim = Scene(reflection);
        var ann = sim.Where("Ann");
        var bob = sim.Where("Bob");
        CultureInfo culture = CultureInfo.CurrentCulture;
        using var cancel = new CancellationTokenSource();
        var mind = new UnusedMind();
        int callbacks = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sim.RunAsync(1, mind, (m, current) =>
        {
            callbacks++;
            if (m != stopAt) return;
            ann = current.Where("Ann");
            bob = current.Where("Bob");
            cancel.Cancel();
        }, cancel.Token));

        // Energy changes on every sleep/awake minute. Equality detects an extra Step even
        // when no callback or model request was delivered for that unwanted minute.
        Assert.Equal(ann, sim.Where("Ann"));
        Assert.Equal(bob, sim.Where("Bob"));
        Assert.Equal(stopAt + 1, callbacks);
        Assert.Equal(0, mind.Calls);
        Assert.Equal(culture, CultureInfo.CurrentCulture);
    }
}
