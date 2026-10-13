using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Invitations (invitation spec): a game or a drink is asked for first, through the general
/// Ask; a yes begins it as before, a no begins Refused instead, which the asker feels and others can
/// see; the asker waits before asking that person again; an injected act is not asked; speech records
/// the offer and the answer; with the switch off nothing is asked.</summary>
public class InvitationSceneTests
{
    private const string Slices = "returns,company,welcome,repair,sides";
    private static readonly Lazy<SimResult> Month = new(() => Run(7, invitations: true, speech: true, days: 28));

    private static FeelingOptions Options(bool invitations)
    {
        FeelingOptions f = DefaultTown.Feelings();
        (f.Steer, f.Desire, f.DesireActs) = (true, true, true);
        f.Acts.With(invitations ? Slices + ",invitations" : Slices);
        return f;
    }

    private static SimResult Run(long seed, bool invitations, bool speech, int days)
    {
        FeelingOptions f = Options(invitations);
        var sim = new Simulation(seed, cast: ActCatalog.Cards(DefaultTown.Cast(), f.Acts), kinds: ActCatalog.Kinds(f.Acts), feelings: f);
        sim.ConfigureSpeech(new SpeechOptions { Enabled = speech });
        return sim.Run(days);
    }

    [Fact]
    public void InvitationsNeedCompanyAndAddOnlyTheRefusal()
    {
        FeelingOptions f = DefaultTown.Feelings();
        f.Acts.Invitations = true;
        Assert.Throws<ArgumentException>(() => new Simulation(1, feelings: f));
        Assert.Equal(new[] { "Refused" },
            ActCatalog.Kinds(Options(true).Acts).Select(k => k.Name).Except(ActCatalog.Kinds(Options(false).Acts).Select(k => k.Name)));
        Assert.Equal("Invitations", ActCatalog.SliceOf("Refused"));
        Assert.Contains("Invitations", ActOptions.Switches);
    }

    [Fact]
    public void WithTheSwitchOffNothingIsAsked()
    {
        SimResult r = Run(7, invitations: false, speech: false, days: 7);
        Assert.Empty(r.Invitations);
        Assert.DoesNotContain(r.Acts, a => a.Kind == "Refused");
        Assert.DoesNotContain(r.Log, l => l.Contains(" Invitation ", StringComparison.Ordinal));
        Assert.Contains(r.Acts, a => a.Kind is "PlayedGame" or "TreatedToDrink"); // company still happens, unasked
    }

    [Fact]
    public void AYesBeginsTheCompanyAndANoBeginsTheRefusalInstead()
    {
        SimResult r = Month.Value;
        Assert.Contains(r.Invitations, i => i.Accepted);
        Assert.Contains(r.Invitations, i => !i.Accepted);
        foreach (Invitation i in r.Invitations)
        {
            Act act = r.Acts[i.ActId];
            Assert.Equal(i.Tick, act.Tick);
            if (i.Accepted)
                Assert.Equal((i.Kind, i.Asker, i.Asked), (act.Kind, act.Actor, act.Target));
            else
            {
                Assert.Equal(("Refused", i.Asked, i.Asker), (act.Kind, act.Actor, act.Target!));
                Assert.DoesNotContain(r.Acts, a => a.Tick == i.Tick && a.Actor == i.Asker && a.Target == i.Asked && a.Kind == i.Kind);
            }
            Assert.Contains($"{i.Tick} ask {i.Asked} Invitation {i.Asker} act {i.ActId} {(i.Accepted ? "yes" : "no")}",
                r.Log.Where(l => l.StartsWith($"{i.Tick} ask ", StringComparison.Ordinal)).Select(l => l[..l.IndexOf(" margin", StringComparison.Ordinal)]));
        }
        // Every refusal is an act of the one asked, and nothing but an asking begins one.
        Assert.Equal(r.Invitations.Count(i => !i.Accepted), r.Acts.Count(a => a.Kind == "Refused"));
    }

    [Fact]
    public void TheAskerKnowsWhoTurnedThemDownAndFeelsIt()
    {
        SimResult r = Month.Value;
        foreach (Invitation no in r.Invitations.Where(i => !i.Accepted))
        {
            // The asker took part, so they know who said no, however little they know them.
            Assert.Contains(r.Log, l => l.Contains($" belief {no.Asker} {no.ActId} {no.Asked} Witnessed", StringComparison.Ordinal));
            Assert.Contains(r.Feelings, f => f.Holder == no.Asker && f.ActId == no.ActId && f.Toward == no.Asked && f.Change < 0);
        }
        Assert.DoesNotContain(r.Pursued, id => r.Acts[id].Kind == "Refused"); // a refusal is no act of the asker's gate
        Assert.All(r.Pursuits.Where(p => p.Acted), p => Assert.Contains(p.ActId, r.Pursued));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(1)]
    public void TheAskerWaitsBeforeAskingTheSamePersonAgain(int wait)
    {
        // Ann likes Bob and carries a game on her card; Bob dislikes Ann, so every asking is a clear no.
        const int Day = Clock.MinutesPerDay;
        static Villager Person(string name, int x, Dictionary<string, double> card) => new(name, name, "villager",
            new Temperament(0.5, 0.5, 0.5, 0.5), new Body(100, -1), null, new[] { new Haunt("Room", new Tile(x, 2), 0, Day, 1) },
            card, Array.Empty<string>());
        FeelingOptions f = FeelingOptions.WithDesire(("Ann", "Bob", 0.5), ("Bob", "Ann", -0.8));
        (f.Steer, f.DesireActs) = (true, true);
        f.AnswerOn = f.ReturnOn = f.MakeUpOn = f.RetaliateOn = f.AvoidOn = f.WithdrawOn = false;
        f.Acts.With("company,invitations");
        f.Acts.InviteAgainDays = wait;
        var town = new TownData
        {
            Cast = new[] { Person("Ann", 3, new Dictionary<string, double> { ["PlayedGame"] = 30 }), Person("Bob", 5, new Dictionary<string, double>()) },
            Places = new[] { new Location("Room", false, Enumerable.Repeat(new string('.', 40), 6).ToList()) },
            Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(),
            Acts = new[] { ActCatalog.Company[0] with { Allowed = Array.Empty<string>() }, ActCatalog.Invitations[0] },
            Feelings = f, Authority = new AuthorityOptions { ElectConstable = false },
            Body = new BodyOptions { AwakeHoursAtRest = 100_000 }, Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
        };
        SimResult r = new Simulation(5, town).Run(10);
        int[] days = r.Invitations.Where(i => i.Asker == "Ann" && i.Asked == "Bob").Select(i => Clock.Day(i.Tick)).ToArray();
        Assert.True(days.Length >= 2, $"Ann asked {days.Length} times");
        Assert.All(r.Invitations, i => Assert.False(i.Accepted));
        int shortest = days.Zip(days.Skip(1), (a, b) => b - a).Min();
        if (wait == 7)
            Assert.True(shortest >= 7, $"asked again after {shortest} days");
        else
            Assert.True(shortest < 7, "with a one-day wait she asks again within the week, so the week is the rule's doing");
    }

    [Fact]
    public void TheRecordingCarriesInvitationsOnlyWhenTheyAreOn()
    {
        static System.Text.Json.JsonElement Record(bool on) => System.Text.Json.JsonDocument.Parse(Replay.Json(new ReplayOptions
        { Seed = 7, Days = 7, Feelings = Options(on), Speech = new SpeechOptions { Enabled = true } })).RootElement;
        System.Text.Json.JsonElement with = Record(true), without = Record(false);
        Assert.False(without.TryGetProperty("invitations", out _));
        System.Text.Json.JsonElement rows = with.GetProperty("invitations");
        Assert.True(rows.GetArrayLength() > 0);
        Assert.Equal(new[] { "accepted", "actId", "asked", "asker", "kind", "margin", "tick" },
            rows[0].EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Contains(with.GetProperty("speech").EnumerateArray(), s => s.GetProperty("reply").GetBoolean());
        Assert.All(without.GetProperty("speech").EnumerateArray(), s => Assert.False(s.GetProperty("reply").GetBoolean()));
    }

    [Fact]
    public void SpeechRecordsTheOfferThenTheAnswer()
    {
        SimResult r = Month.Value;
        var kindOf = SpeechCatalog.Default.Lines.ToDictionary(l => l.Id, l => l.Kind);
        foreach (Invitation i in r.Invitations)
        {
            Speech[] said = r.Speech.Where(s => s.ActId == i.ActId).ToArray();
            Assert.Equal(2, said.Length);
            Assert.Equal((i.Asker, false, i.Kind), (said[0].Speaker, said[0].Reply, kindOf[said[0].LineId]));
            Assert.Equal((i.Asked, true, i.Kind + (i.Accepted ? ".Accepted" : ".Refused")), (said[1].Speaker, said[1].Reply, kindOf[said[1].LineId]));
        }
        Assert.All(r.Speech.Where(s => !r.Invitations.Any(i => i.ActId == s.ActId)), s => Assert.False(s.Reply));
    }

    [Fact]
    public void ARunIsTheSameTwiceAndSpeechChangesNothing()
    {
        SimResult a = Run(11, invitations: true, speech: true, days: 7), b = Run(11, invitations: true, speech: true, days: 7);
        Assert.Equal(a.Invitations, b.Invitations);
        Assert.Equal(a.Speech, b.Speech);
        Assert.Equal(Metrics.LogHash(a), Metrics.LogHash(Run(11, invitations: true, speech: false, days: 7)));
    }

    [Fact]
    public void AnInjectedGameIsNotAsked()
    {
        const int Day = Clock.MinutesPerDay;
        static Villager Person(string name, int x) => new(name, name, "villager", new Temperament(0.5, 0.5, 0.5, 0.5), new Body(100, -1), null,
            new[] { new Haunt("Room", new Tile(x, 2), 0, Day, 1) }, new Dictionary<string, double>(), Array.Empty<string>());
        FeelingOptions f = DefaultTown.Feelings();
        f.Steer = true;
        f.Acts.With("company,invitations");
        var town = new TownData
        {
            Cast = new[] { Person("Ann", 3), Person("Bob", 5) },
            Places = new[] { new Location("Room", false, Enumerable.Repeat(new string('.', 40), 6).ToList()) },
            Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(),
            Acts = new[] { ActCatalog.Company[0] with { Allowed = Array.Empty<string>(), FromMinute = 0, ToMinute = Day }, ActCatalog.Invitations[0] },
            Feelings = f, Authority = new AuthorityOptions { ElectConstable = false },
            Body = new BodyOptions { AwakeHoursAtRest = 100_000 }, Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
        };
        var sim = new Simulation(3, town);
        sim.Place(new[] { new Scenario("game", "PlayedGame", "Ann", "Bob", Day: 0, From: Clock.At(10), To: Clock.At(10) + 1) });
        SimResult r = sim.Run(1);
        Assert.Contains(r.Acts, a => a.Kind == "PlayedGame" && a.Actor == "Ann" && a.Target == "Bob" && a.Injected);
        Assert.Empty(r.Invitations);
    }
}
