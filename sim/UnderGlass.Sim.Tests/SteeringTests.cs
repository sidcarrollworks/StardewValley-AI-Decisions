using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Feelings steering decisions (phase 0c, rules S1-S10): whom an act is aimed at, who is
/// in the mood to act, who confronts, whom a witness suspects, a grudge as a motive, where a
/// household buys its groceries, and the log lines that say why. Every scene steers.</summary>
public class SteeringTests
{
    /// <summary>Someone who spends the whole day at one spot (where there is no home, they sleep
    /// there too) and never feels tired. Sensitivity and retention are 0.5, so feelings are felt
    /// at their row's size and kept in full.</summary>
    private static Villager V(string name, string household, int x, int y, double bold = 0.5, string kind = "villager",
        Dictionary<string, double>? acts = null, string place = "Room", int age = 30, Job? job = null,
        Dictionary<string, Kin>? family = null)
        => new(name, household, kind, new Temperament(1.0, bold, 0.5, 0.6), new Body(100, -1), job,
            new[] { new Haunt(place, new Tile(x, y), 0, Clock.MinutesPerDay, 1) },
            acts ?? new Dictionary<string, double>(), Array.Empty<string>(), age, family);

    /// <summary>A long open room.</summary>
    private static Location Room(string name = "Room") => new(name, false, Enumerable.Repeat(new string('.', 40), 6).ToList());

    /// <summary>Nobody tires during the run, so nobody collapses on a long one.</summary>
    private static BodyOptions Awake() => new() { AwakeHoursAtRest = 100_000 };

    private static readonly int Ten = Clock.At(10);

    // The scenes' own feeling rows: the town's first guesses, written out so tuning the town
    // doesn't move these tests.
    private static readonly Affect GiftRow = new(Patient.Target, 0.2, 0.3, 1, TargetIs.Chosen, Tilt: 1);
    private static readonly Affect ArguedRow = new(Patient.Target, -0.3, 0.3, 1, TargetIs.Chosen);
    private static readonly Affect StoleRow = new(Patient.Target, -0.5, 0.5, 1, TargetIs.Keeper);

    private static ActKind Gift(double perDay = 0, Affect? row = null)
        => new("GaveGift", 1.5, 1, 1, 1, perDay, Array.Empty<string>(), Affect: row ?? GiftRow);

    private static ActKind Argued(double perDay = 0, Affect? row = null)
        => new("Argued", 3.0, -1, 2, 10, perDay, Array.Empty<string>(), Affect: row ?? ArguedRow);

    private static Dictionary<string, double> Does(params string[] kinds) => kinds.ToDictionary(k => k, _ => 1.0);

    /// <summary>Feelings on and steering, with regard that starts away from the seed (from, to).</summary>
    private static FeelingOptions Steer(params (string From, string To, double Regard)[] start)
        => new() { Start = start.ToDictionary(s => (s.From, s.To), s => s.Regard) };

    /// <summary>Every change of one person's regard for another, in order.</summary>
    private static List<Felt> Changes(SimResult r, string holder, string toward)
        => r.Feelings.Where(f => f.Holder == holder && f.Toward == toward).ToList();

    private static bool Holds(SimResult r, string holder, string toward, string name)
        => r.Sentiments.Any(s => s.Holder == holder && s.Toward == toward && s.Name == name);

    /// <summary>T16b, the gift variant of T16: Tom gives Kim a generous gift (a row as strong as
    /// T16's theft). Ann loves Kim and is glad with her; Bea hates Kim, so Kim's joy is her
    /// sadness, and she cools on whoever caused it (envy; III P24, VERIFY).</summary>
    [Fact]
    public void AGiftToSomeoneLovedWarmsTheirFriends_AndStirsEnvyInTheirEnemies()
    {
        var cast = new[] { V("Tom", "T", 5, 2), V("Kim", "K", 6, 2), V("Ann", "A", 3, 2), V("Bea", "B", 7, 3) };
        FeelingOptions feelings = Steer(("Tom", "Kim", 1.0), ("Ann", "Kim", 0.6), ("Bea", "Kim", -0.6));
        feelings.TargetBase = 0.001; // Tom gives to the one he loves, all but surely
        ActKind generous = Gift(row: GiftRow with { Joy = 0.5, Plastic = 0.5 });
        SimResult r = new Simulation(1, cast, new[] { Room() }, new[] { generous }, feelings: feelings,
            scheduled: new[] { (Ten, "Tom", "GaveGift") }, wander: 0).Run(1);

        Assert.Equal("Kim", r.Acts[0].Target);
        Felt ann = Assert.Single(Changes(r, "Ann", "Tom"));
        Assert.Equal("Sympathy", ann.Route);
        Assert.Equal(0.075, ann.Change, 12);                  // 0.5 x 0.6 x 0.5, half of it regard
        Felt bea = Assert.Single(Changes(r, "Bea", "Tom"));
        Assert.Equal("Antipathy", bea.Route);
        Assert.Equal(-0.0375, bea.Change, 12);                // -0.5 x min(0.6, 0.3) x 0.5, half of it regard
        Assert.True(Holds(r, "Ann", "Tom", "Approving"));
        Assert.True(Holds(r, "Bea", "Tom", "Envious"));
        Assert.True(r.Regard[("Ann", "Tom")] > 0 && r.Regard[("Bea", "Tom")] < 0);
    }

    /// <summary>T32 (rule 4): Ann gives often, and Bob is the only one in reach. Each gift warms
    /// him, but a gift repeated within a week counts half each time; one given more than a week
    /// after the last counts in full again.</summary>
    [Fact]
    public void AGiftGoesToSomeoneInReach_WarmsThem_AndRepeatsCountLess()
    {
        var cast = new[] { V("Ann", "A", 3, 2, acts: Does("GaveGift")), V("Bob", "B", 5, 2) };
        SimResult r = new Simulation(1, cast, new[] { Room() }, new[] { Gift(perDay: 20) }, feelings: Steer(), wander: 0).Run(1);

        var gifts = r.Acts.Where(a => a.Kind == "GaveGift").ToList();
        Assert.True(gifts.Count >= 3);
        Assert.All(gifts, g => Assert.Equal("Bob", g.Target));
        var direct = Changes(r, "Bob", "Ann").Where(f => f.Route == "Direct").ToList();
        Assert.Equal(new[] { 0.06, 0.03, 0.015 }, direct.Take(3).Select(f => Math.Round(f.Raw, 12)));
        Assert.Equal(0.06, direct[0].Change, 12);
        Sentiment grateful = Assert.Single(r.Sentiments, s => s.Holder == "Bob" && s.Toward == "Ann");
        Assert.Equal("Grateful", grateful.Name);
        Assert.Equal("GaveGift", r.Acts[grateful.ActId].Kind);

        // Two gifts eight days apart: the second counts as much as the first.
        SimResult apart = new Simulation(1, cast, new[] { Room() }, new[] { Gift() }, feelings: Steer(), body: Awake(),
            scheduled: new[] { (Ten, "Ann", "GaveGift"), (8 * Clock.MinutesPerDay + Ten, "Ann", "GaveGift") }, wander: 0).Run(9);
        Assert.Equal(new[] { 0.06, 0.06 },
            Changes(apart, "Bob", "Ann").Where(f => f.Route == "Direct").Select(f => Math.Round(f.Raw, 12)));
    }

    /// <summary>S1 (law 4): an act is aimed only at someone within reach, 5 tiles. Ann loves Dee
    /// and Cal, but Dee stands 6 tiles off and Cal 10, so every gift goes to Bob beside her; with
    /// only those two in the room she gives nothing at all. Feelings that only watch don't hold her
    /// back, and her gifts then go to nobody in particular.</summary>
    [Fact]
    public void AnActIsAimedOnlyAtSomeoneInReach()
    {
        var outOfReach = new[] { V("Ann", "A", 3, 2, acts: Does("GaveGift")), V("Dee", "D", 9, 2), V("Cal", "C", 13, 2) };
        SimResult Run(IEnumerable<Villager> cast, FeelingOptions feelings)
            => new Simulation(1, cast.ToList(), new[] { Room() }, new[] { Gift(perDay: 20) }, feelings: feelings, wander: 0).Run(1);
        FeelingOptions Loving()
        {
            FeelingOptions f = Steer(("Ann", "Cal", 1.0), ("Ann", "Dee", 1.0));
            f.TargetBase = 0.001; // were Cal or Dee in reach, nearly every gift would go to them
            return f;
        }

        var gifts = Run(outOfReach.Append(V("Bob", "B", 5, 2)), Loving()).Acts.Where(a => a.Kind == "GaveGift").ToList();
        Assert.True(gifts.Count >= 3);
        Assert.All(gifts, g => Assert.Equal("Bob", g.Target));

        Assert.DoesNotContain(Run(outOfReach, Loving()).Acts, a => a.Kind == "GaveGift");
        var watched = Run(outOfReach, FeelingOptions.Observe).Acts.Where(a => a.Kind == "GaveGift").ToList();
        Assert.True(watched.Count >= 3);
        Assert.All(watched, g => Assert.Null(g.Target));
    }

    /// <summary>S1: someone busy is not chosen. Ann loves Bob and gives to him whenever she can;
    /// Cal stands by her, neutral. Her gifts are placed for 10:30 and 12:30. While Bob mends a fence
    /// from 10:00 to noon, her first gift goes to Cal, and her second, once he is free, to him. The
    /// same when he is busy as the one an argument is aimed at: whoever an act is aimed at is busy
    /// with it for as long as it lasts.</summary>
    [Fact]
    public void SomeoneBusyIsNotChosen()
    {
        var mending = new ActKind("MendedFence", 0.5, 0, 1, 120, 0, Array.Empty<string>());
        ActKind longArgument = Argued() with { DurationMinutes = 120 };
        var cast = new List<Villager> { V("Ann", "A", 3, 2), V("Bob", "B", 5, 2), V("Cal", "C", 3, 4) };
        SimResult Run(IReadOnlyList<Villager> who, ActKind busy, string busyActor)
        {
            FeelingOptions feelings = Steer(("Ann", "Bob", 1.0));
            feelings.TargetBase = 0.001; // Bob, when he is free, gets nearly every gift
            var scheduled = new[] { (Ten, busyActor, busy.Name), (Clock.At(10, 30), "Ann", "GaveGift"), (Clock.At(12, 30), "Ann", "GaveGift") };
            return new Simulation(1, who, new[] { Room() }, new[] { busy, Gift() }, feelings: feelings, scheduled: scheduled, wander: 0).Run(1);
        }
        string?[] Gifts(SimResult r) => r.Acts.Where(a => a.Kind == "GaveGift").Select(a => a.Target).ToArray();

        Assert.Equal(new[] { "Cal", "Bob" }, Gifts(Run(cast, mending, "Bob")));

        // Dee stands 5 tiles from Bob, out of reach of Ann and Cal, so she can only argue with him.
        cast.Add(V("Dee", "D", 10, 2));
        SimResult argued = Run(cast, longArgument, "Dee");
        Assert.Equal(("Dee", "Bob"), (argued.Acts[0].Actor, argued.Acts[0].Target));
        Assert.Equal(new[] { "Cal", "Bob" }, Gifts(argued));
    }

    /// <summary>S1: whoever an act is aimed at takes part in it, so they see it whole and know who
    /// did it. Bob stands 5 tiles from Ann, still in reach: as an onlooker he would see her gift
    /// at 0.6, too little to tell a stranger (0.725). As its recipient he sees it at 1, names her,
    /// and feels all of it: joy 0.2, and regard 0.2 x 0.3 = 0.06. Watched without steering, he is
    /// only an onlooker.</summary>
    [Fact]
    public void WhoeverAnActIsAimedAtTakesPartInIt()
    {
        var cast = new[] { V("Ann", "A", 3, 2), V("Bob", "B", 8, 2) };
        SimResult Run(FeelingOptions feelings) => new Simulation(1, cast, new[] { Room() }, new[] { Gift() }, feelings: feelings,
            scheduled: new[] { (Ten, "Ann", "GaveGift") }, wander: 0).Run(1);

        SimResult r = Run(Steer());
        Assert.Equal("Bob", Assert.Single(r.Acts).Target);
        Belief bob = r.Beliefs["Bob"][0];
        Assert.Equal((1.0, "Ann", 1.0), (bob.Clarity, bob.Actor, bob.Confidence));
        Felt direct = Assert.Single(Changes(r, "Bob", "Ann"));
        Assert.Equal("Direct", direct.Route);
        Assert.Equal(0.06, direct.Raw, 12);
        Assert.Equal(0.2, Assert.Single(r.Feelings, f => f.Holder == "Bob" && f.Toward is null).Mood, 12);

        SimResult watched = Run(FeelingOptions.Observe);
        Assert.Equal("Bob", Assert.Single(watched.Acts).Target);
        Assert.Equal(0.6, watched.Beliefs["Bob"][0].Clarity, 12);
        Assert.Null(watched.Beliefs["Bob"][0].Actor);
        Assert.Empty(Changes(watched, "Bob", "Ann"));
    }

    /// <summary>S1: an act placed for someone chosen waits until someone is in reach. Ann's gift is
    /// placed for 10:00, but nobody is in the room until Bob comes in at noon; she gives it the
    /// minute he does.</summary>
    [Fact]
    public void AnActAimedAtSomeoneWaitsUntilSomeoneIsInReach()
    {
        Villager bob = V("Bob", "B", 5, 2) with
        {
            Haunts = new[]
            {
                new Haunt("Away", new Tile(2, 2), 0, Clock.At(12), 1),
                new Haunt("Room", new Tile(5, 2), Clock.At(12), Clock.MinutesPerDay, 1),
            },
        };
        int arrived = -1;
        SimResult r = new Simulation(1, new[] { V("Ann", "A", 3, 2), bob }, new[] { Room(), Room("Away") }, new[] { Gift() },
            feelings: Steer(), scheduled: new[] { (Ten, "Ann", "GaveGift") }, wander: 0).Run(1, (m, sim) =>
        {
            if (arrived < 0 && sim.Where("Bob").Place == "Room")
                arrived = m;
        });

        Act gift = Assert.Single(r.Acts);
        Assert.True(arrived >= Clock.At(12));
        Assert.Equal((arrived, "Bob"), (gift.Tick, gift.Target));
    }

    /// <summary>T33 (law 4; III P25, VERIFY): Ann argues and gives often, with four people in
    /// reach. She dislikes Bob and loves Cal: hate argues, love gives, and people argue least with
    /// those they love. Counted over ten seeds of two days, about 300 of each act, because one
    /// seed's 30 arguments are too few to rank the two neutral people against Cal reliably.</summary>
    [Fact]
    public void ArgumentsGoToTheDisliked_GiftsToTheLoved()
    {
        var cast = new[]
        {
            V("Ann", "A", 5, 2, acts: Does("Argued", "GaveGift")), V("Bob", "B", 3, 2), V("Cal", "C", 7, 2),
            V("Dee", "D", 5, 4), V("Eve", "E", 6, 0),
        };
        var others = new[] { "Bob", "Cal", "Dee", "Eve" };
        var counts = new Dictionary<(string Kind, string Target), int>();
        for (long seed = 1; seed <= 10; seed++)
        {
            SimResult r = new Simulation(seed, cast, new[] { Room() }, new[] { Argued(perDay: 20), Gift(perDay: 20) },
                feelings: Steer(("Ann", "Bob", -0.6), ("Ann", "Cal", 0.6)), body: Awake(), wander: 0).Run(2);
            foreach (Act a in r.Acts.Where(a => a.Actor == "Ann" && a.Target is not null))
                counts[(a.Kind, a.Target!)] = counts.GetValueOrDefault((a.Kind, a.Target!)) + 1;
            Assert.True(r.Regard[("Bob", "Ann")] < 0);
            Assert.True(Holds(r, "Bob", "Ann", "Hurt"));
        }

        int Count(string kind, string target) => counts.GetValueOrDefault((kind, target));
        int MostOthers(string kind, string but) => others.Where(n => n != but).Max(n => Count(kind, n));
        int FewestOthers(string kind, string but) => others.Where(n => n != but).Min(n => Count(kind, n));
        string Tally(string kind) => string.Join(", ", others.Select(n => $"{n} {Count(kind, n)}"));

        Assert.True(Count("Argued", "Bob") > MostOthers("Argued", "Bob"), Tally("Argued"));
        Assert.True(Count("Argued", "Cal") < FewestOthers("Argued", "Cal"), Tally("Argued"));
        Assert.True(Count("GaveGift", "Cal") > MostOthers("GaveGift", "Cal"), Tally("GaveGift"));
        Assert.True(Count("GaveGift", "Bob") < FewestOthers("GaveGift", "Bob"), Tally("GaveGift"));
    }

    /// <summary>T34: Ann and Bob start cool on each other (-0.25) and both argue. Each argument at
    /// the other deepens the dislike, and the disliked are argued with more, so a feud grows; Cal
    /// and Dee stand in reach, neutral.</summary>
    [Fact]
    public void AFeudGrowsWhenArgumentsGoBothWays()
    {
        var cast = new[]
        {
            V("Ann", "A", 4, 2, acts: Does("Argued")), V("Bob", "B", 6, 2, acts: Does("Argued")),
            V("Cal", "C", 5, 4), V("Dee", "D", 5, 0),
        };
        SimResult r = new Simulation(1, cast, new[] { Room() }, new[] { Argued(perDay: 10) },
            feelings: Steer(("Ann", "Bob", -0.25), ("Bob", "Ann", -0.25)), body: Awake(), wander: 0).Run(7);

        Assert.Contains(r.Ties, t => t.A == "Ann" && t.B == "Bob" && t.What == "feud");
        int Count(string target) => r.Acts.Count(a => a.Actor == "Ann" && a.Kind == "Argued" && a.Target == target);
        Assert.True(Count("Bob") > Count("Cal"));
        Assert.True(Count("Bob") > Count("Dee"));
    }

    /// <summary>T35 (law 8; III P44, VERIFY): Dem argues at Seb (raw -0.3), then helps him three
    /// times the same day (raw 0.18, 0.09, 0.045: each repeat counts half). The third help carries
    /// Seb past zero at +0.015, and love after a conquered hate gains back the depth of that hate,
    /// 0.3, bounded by the 0.315 of love given since. In another room Dem2 helps Kit the same way
    /// with no quarrel first, and Kit's regard saturates: 0.18, 0.2538, 0.287379.</summary>
    [Fact]
    public void HateConqueredByLoveEndsAbovePlainFriendship()
    {
        ActKind quarrel = Argued(row: ArguedRow with { Joy = -1 });
        var help = new ActKind("HelpedSomeone", 2.0, 1, 2, 5, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Target, 0.6, 0.3, 1, TargetIs.Chosen, Tilt: 1));
        var cast = new[]
        {
            V("Dem", "D", 3, 2), V("Seb", "S", 5, 2),
            V("Dem2", "D2", 3, 2, place: "Room2"), V("Kit", "K", 5, 2, place: "Room2"),
        };
        var scheduled = new List<(int, string, string)> { (Ten, "Dem", "Argued") };
        foreach (int hour in new[] { 11, 12, 13 })
        {
            scheduled.Add((Clock.At(hour), "Dem", "HelpedSomeone"));
            scheduled.Add((Clock.At(hour), "Dem2", "HelpedSomeone"));
        }
        SimResult r = new Simulation(1, cast, new[] { Room(), Room("Room2") }, new[] { quarrel, help }, feelings: Steer(),
            scheduled: scheduled, wander: 0).Run(1);

        // Each pair spent the day together, so neither friendship faded at night.
        Assert.Equal(0.315, r.Regard[("Seb", "Dem")], 9);
        Assert.Equal(0.287379, r.Regard[("Kit", "Dem2")], 9);
        Assert.True(r.Regard[("Seb", "Dem")] > r.Regard[("Kit", "Dem2")]);
        int thirdHelp = r.Acts.Last(a => a.Actor == "Dem" && a.Kind == "HelpedSomeone").Id;
        Assert.Contains(r.Sentiments, s => s.Holder == "Seb" && s.Toward == "Dem" && s.Name == "Reconciled" && s.ActId == thirdHelp);
        Assert.Contains(r.Ties, t => t.A == "Seb" && t.B == "Dem" && t.What == "reconciled");
        Assert.DoesNotContain(r.Ties, t => t.What == "reconciled" && t.A == "Kit");
    }

    /// <summary>T36 (law 1; rule 15): Pam's household is short, so her power of acting is 0.35;
    /// Gil's is comfortable, at 0.5. Both drink and give with the same weight, each beside a
    /// housemate and out of the other's sight, and nobody chats, so nothing else moves their mood.
    /// The sad drink more; the glad give more. The same seeds with the tilt off are the control:
    /// the tilt weighs Pam's drinks at 1.15 and her gifts at 0.85 against Gil's 1, so it should
    /// lift each ratio by 0.15 to 0.18 over the control's, and must lift it by at least 0.1
    /// (measured: Pam drinks 1.14 times as often as Gil against 1.02, Gil gives 1.12 times as
    /// often as Pam against 0.95).</summary>
    [Fact]
    public void TheSadDrinkMore_TheGladGiveMore()
    {
        var drunk = new ActKind("DrunkScene", 3.0, -1, 1, 1, 20, Array.Empty<string>(), MinAge: 18,
            Affect: new Affect(Patient.Onlookers, -0.15, 0.3, 0.6, Tilt: -1));
        var cast = new[]
        {
            V("Pam", "P", 3, 2, acts: Does("DrunkScene", "GaveGift")), V("Pat", "P", 5, 2),
            V("Gil", "G", 30, 2, acts: Does("DrunkScene", "GaveGift")), V("Gia", "G", 32, 2),
        };
        var eco = new Economy(new Dictionary<string, double> { ["P"] = -200, ["G"] = 1000 }, Array.Empty<(string, double, string?)>(),
            new Dictionary<string, double>(), new Dictionary<string, string>(), new Dictionary<string, (double, double)>(),
            TownStipend: 0, TownStart: 0);
        (int PamDrinks, int GilDrinks, int PamGifts, int GilGifts) Count(double tiltScale)
        {
            int pamDrinks = 0, gilDrinks = 0, pamGifts = 0, gilGifts = 0;
            for (long seed = 1; seed <= 50; seed++)
            {
                FeelingOptions feelings = Steer();
                feelings.TiltScale = tiltScale;
                SimResult r = new Simulation(seed, cast, new[] { Room() }, new[] { drunk, Gift(perDay: 20) }, feelings: feelings,
                    economy: eco, money: new MoneyOptions { WantChancePerDay = 0, SpendShare = 0 },
                    gossip: new GossipOptions { ChatChance = 0 }, body: Awake(), wander: 0).Run(2);
                if (seed == 1)
                {
                    Assert.Equal(0.35, r.PowerByDay["Pam"][0], 9);
                    Assert.Equal(0.5, r.PowerByDay["Gil"][0], 9);
                }
                pamDrinks += r.Acts.Count(a => a.Kind == "DrunkScene" && a.Actor == "Pam");
                gilDrinks += r.Acts.Count(a => a.Kind == "DrunkScene" && a.Actor == "Gil");
                pamGifts += r.Acts.Count(a => a.Kind == "GaveGift" && a.Actor == "Pam");
                gilGifts += r.Acts.Count(a => a.Kind == "GaveGift" && a.Actor == "Gil");
            }
            return (pamDrinks, gilDrinks, pamGifts, gilGifts);
        }

        var tilted = Count(1);
        Assert.True(tilted.PamDrinks > tilted.GilDrinks, $"Pam drank {tilted.PamDrinks} times, Gil {tilted.GilDrinks}");
        Assert.True(tilted.GilGifts > tilted.PamGifts, $"Gil gave {tilted.GilGifts} times, Pam {tilted.PamGifts}");

        var flat = Count(0);
        double drinks = tilted.PamDrinks / (double)tilted.GilDrinks, flatDrinks = flat.PamDrinks / (double)flat.GilDrinks;
        double gifts = tilted.GilGifts / (double)tilted.PamGifts, flatGifts = flat.GilGifts / (double)flat.PamGifts;
        Assert.True(drinks >= flatDrinks + 0.1, $"Pam drank {drinks:0.000} times as often as Gil, {flatDrinks:0.000} untilted");
        Assert.True(gifts >= flatGifts + 0.1, $"Gil gave {gifts:0.000} times as often as Pam, {flatGifts:0.000} untilted");
    }

    /// <summary>GossipTests' confrontation scene: Bob rummages in Kim's bin, and five people, all
    /// equally bold, see him close up, Kim among them. With <paramref name="zedForBob"/>, Zed too,
    /// bolder than anyone, holding Bob at that regard.</summary>
    private static SimResult Confronting(double? zedForBob)
    {
        var bin = new ActKind("RummagedInBin", 4.0, -1, 1, 1, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Target, -0.3, 0.5, 1, TargetIs.Keeper));
        var cast = new List<Villager>
        {
            V("Ann", "H", 3, 2), V("Bob", "B", 5, 2), V("Cal", "H", 7, 2), V("Dee", "H", 5, 4), V("Eve", "H", 4, 4),
            V("Kim", "K", 6, 4),
        };
        var start = new List<(string, string, double)>();
        if (zedForBob is { } regard)
        {
            cast.Add(V("Zed", "Z", 4, 0, bold: 0.9));
            start.Add(("Zed", "Bob", regard));
        }
        var authority = new AuthorityOptions { Keepers = new Dictionary<string, string> { ["Room"] = "Kim" } };
        return new Simulation(3, cast, new[] { Room() }, new[] { bin }, authority: authority, feelings: Steer(start.ToArray()),
            scheduled: new[] { (Ten, "Bob", "RummagedInBin") }, wander: 0).Run(2);
    }

    /// <summary>T37 (rule 9, "preferring the person harmed"; III P25, VERIFY): the keeper whose bin
    /// it was feels it most, so dislikes Bob most, and confronts him. Zed, the boldest, confronts
    /// Bob when Bob is nothing to him, and never when he loves him.</summary>
    [Fact]
    public void TheHarmedConfront_AndNobodyConfrontsSomeoneTheyLove()
    {
        Confrontation c = Assert.Single(Confronting(zedForBob: null).Confrontations);
        Assert.Equal(("Kim", "Bob", true), (c.By, c.Target, c.Correct));

        Assert.Equal("Zed", Assert.Single(Confronting(zedForBob: 0).Confrontations).By);

        Confrontation loved = Assert.Single(Confronting(zedForBob: 0.6).Confrontations);
        Assert.Equal("Bob", loved.Target);
        Assert.NotEqual("Zed", loved.By);
    }

    /// <summary>T40 (law 9; III P46, VERIFY): Far sees "someone, a young man" (Tom) steal from 7
    /// tiles, and comes to think a little less of young men. From noon two strangers stand near
    /// him: Yan, a young man, and Ola, an old woman. When Tom steals again unseen, Far suspects Yan
    /// before Ola, though Far knows them equally little and name order would put Ola first. The
    /// prejudice weighs only as far as someone is a stranger, so it fades as Far comes to know Yan.</summary>
    [Fact]
    public void PrejudiceRanksStrangersOfAKindFirst_AndFadesWithAcquaintance()
    {
        var stole = new ActKind("Stole", 4.5, -1, 1, 1, 0, Array.Empty<string>(), Affect: StoleRow);
        Villager FromNoon(string name, string kind, int x, int y) => V(name, name, x, y, kind: kind) with
        {
            Haunts = new[]
            {
                new Haunt("Away", new Tile(2, 2), 0, Clock.At(12), 1),
                new Haunt("Room", new Tile(x, y), Clock.At(12), Clock.MinutesPerDay, 1),
            },
        };
        var cast = new[]
        {
            V("Tom", "T", 3, 2, kind: "young man"), V("Far", "F", 10, 2),
            FromNoon("Yan", "young man", 13, 2), FromNoon("Ola", "old woman", 13, 4),
        };
        var samples = new List<(double Lean, double Kind, double Familiarity)>();
        SimResult r = new Simulation(1, cast, new[] { Room(), Room("Away") }, new[] { stole }, feelings: Steer(),
            scheduled: new[] { (Ten, "Tom", "Stole"), (Clock.At(14), "Tom", "Stole") }, wander: 0).Run(1, (m, sim) =>
        {
            if (m == Clock.At(15) || m == Clock.At(23))
                samples.Add((sim.Regard("Far", "Yan") - sim.PersonalRegard("Far", "Yan"), sim.KindRegard("Far", "young man"),
                    sim.Familiarity("Far", "Yan")));
        });

        Assert.All(r.Beliefs["Far"].Values, b => Assert.Null(b.Actor));     // "someone", both times
        var suspects = r.Beliefs["Far"][1].Suspects!.ToList();
        Assert.Contains("Ola", suspects);
        Assert.Contains("Yan", suspects);
        Assert.True(suspects.IndexOf("Yan") < suspects.IndexOf("Ola"), string.Join(",", suspects));

        var (afternoon, evening) = (samples[0], samples[1]);
        Assert.True(afternoon.Kind < 0);
        Assert.Equal(afternoon.Kind, evening.Kind);             // nothing new about young men in between
        Assert.True(evening.Familiarity > afternoon.Familiarity);
        foreach (var s in samples)
            Assert.True(Math.Abs(s.Lean) <= (1 - s.Familiarity) * Math.Abs(s.Kind) + 1e-12);
        Assert.True(Math.Abs(evening.Lean) < Math.Abs(afternoon.Lean));
    }

    /// <summary>MoneyTests' temptation scene with nobody watching and no want: Abi is in the store
    /// from 10:00, and its keeper Kim is not. At 9:00 Kim has a row with Abi in the square (raw
    /// -0.3), so Abi holds a sentiment toward her.</summary>
    private static SimResult Grudge(double abiForKim)
    {
        var abi = V("Abi", "A", 5, 2, acts: Does("Stole"), age: 20) with
        {
            Haunts = new[]
            {
                new Haunt("Square", new Tile(5, 2), Clock.At(6), Clock.At(10), 1000),
                new Haunt("Store", new Tile(5, 2), Clock.At(10), Clock.At(20), 1000),
            },
        };
        var cast = new[] { abi, V("Kim", "K", 6, 2, place: "Square") };
        var stole = new ActKind("Stole", 4.5, -1, 1, 1, 0, new[] { "Store" }, MinAge: 13, Affect: StoleRow);
        var eco = new Economy(new Dictionary<string, double> { ["A"] = 1000, ["K"] = 1000 }, Array.Empty<(string, double, string?)>(),
            new Dictionary<string, double>(), new Dictionary<string, string>(),
            new Dictionary<string, (double, double)> { ["Abi"] = (500, 500) }, TownStipend: 0, TownStart: 0);
        var money = new MoneyOptions { WantChancePerDay = 0, WantPatienceDays = 1, StealBase = 1, SpendShare = 0 };
        var authority = new AuthorityOptions { Keepers = new Dictionary<string, string> { ["Store"] = "Kim" } };
        return new Simulation(2, cast, new[] { Room("Store"), Room("Home:A"), Room("Square") },
            new[] { stole, Argued(row: ArguedRow with { Joy = -1 }) }, authority: authority, economy: eco, money: money,
            feelings: Steer(("Abi", "Kim", abiForKim)), scheduled: new[] { (Clock.At(9), "Kim", "Argued") },
            body: Awake(), wander: 0).Run(3);
    }

    /// <summary>T41 (rule 17): a grudge against the keeper is a motive. Abi, who has long disliked
    /// Kim (-0.8), steals from her store with nobody watching, and the log says why. Holding only
    /// the morning's row against her, she doesn't.</summary>
    [Fact]
    public void AGrudgeAgainstTheKeeperIsAMotive()
    {
        SimResult sour = Grudge(abiForKim: -0.8);
        Assert.Equal(("Kim", "Abi"), (sour.Acts[0].Actor, sour.Acts[0].Target));
        Assert.Contains(sour.Motives, x => x.Who == "Abi" && x.Motive == "grievance");
        Assert.Equal("Stole", sour.Acts[sour.Motives.First(x => x.Motive == "grievance").ActId].Kind);
        Assert.Contains(sour.Log, l => l.EndsWith(" why Abi Stole Kim Hurt since d0 act 0"));

        SimResult mild = Grudge(abiForKim: 0);
        Assert.Empty(mild.Motives);
        Assert.DoesNotContain(mild.Acts, a => a.Kind == "Stole");
    }

    /// <summary>MoneyTests' payday scene with Kim keeping the store: Ann earns 500 a week from
    /// away, her son Kid gets 50, and they buy groceries at <paramref name="shop"/>. Paydays fall
    /// on days 0, 7, ..., 35; a household may change shop once it has held its choice 28 days.</summary>
    private static SimResult Shopping(double annForKim, string shop = "Store")
    {
        var cast = new[]
        {
            V("Kim", "Shop", 5, 2, place: "Store", job: new Job("Store", new Tile(5, 2), 0, Clock.MinutesPerDay, Array.Empty<int>(), 1)),
            V("Ann", "H", 8, 2, place: "Store"), V("Kid", "H", 9, 2, place: "Store", age: 10),
        };
        var eco = new Economy(new Dictionary<string, double> { ["Shop"] = 500, ["H"] = 1000 },
            new (string, double, string?)[] { ("Ann", 500, null) }, new Dictionary<string, double> { ["Kid"] = 50 },
            new Dictionary<string, string> { ["H"] = shop }, new Dictionary<string, (double, double)>(), TownStipend: 0, TownStart: 0);
        var authority = new AuthorityOptions { Keepers = new Dictionary<string, string> { ["Store"] = "Kim" } };
        return new Simulation(1, cast, new[] { Room("Store") }, Array.Empty<ActKind>(), authority: authority, economy: eco,
            money: new MoneyOptions { SpendShare = 0, WantChancePerDay = 0 }, feelings: Steer(("Ann", "Kim", annForKim)),
            wander: 0).Run(36);
    }

    /// <summary>T42 (rule 10; rule 12): a household that dislikes the store's keeper enough leaves
    /// for the chain at the first payday after a season, and pays the chain's price from then;
    /// one only a little cool stays; a chain household fond of the keeper comes back. Money moves
    /// only through purses and the town's edge.</summary>
    [Fact]
    public void AHouseholdThatTurnsOnTheKeeperShopsAtTheChain_AndMoneyStillAddsUp()
    {
        const int Week = 500 - 75 - 50; // Ann's wage, less what she keeps and Kid's allowance

        SimResult sour = Shopping(annForKim: -0.3);
        Assert.Equal(new[] { (28, "H", "Store", "Mart") }, sour.ShopSwitches);
        Assert.Contains(sour.Log, l => l.EndsWith(" shop H Store Mart (store -0.30, chain 0.20)"));
        Assert.Equal(1000 + 6 * Week - 4 * 200 - 2 * 180, sour.Purses["H"], 6); // the chain's price from day 28

        SimResult cool = Shopping(annForKim: -0.05);
        Assert.Empty(cool.ShopSwitches);
        Assert.Equal(1000 + 6 * Week - 6 * 200, cool.Purses["H"], 6);

        SimResult fond = Shopping(annForKim: 0.6, shop: "Mart");
        Assert.Equal(new[] { (28, "H", "Mart", "Store") }, fond.ShopSwitches);
        Assert.Equal(1000 + 6 * Week - 4 * 180 - 2 * 200, fond.Purses["H"], 6);

        foreach (SimResult r in new[] { sour, cool, fond })
            Assert.Equal(r.TownCash[^1] - r.TownCash[0], r.OutsideIn - r.OutsideOut, 6);
    }

    private static readonly ActKind FamilyStole = new("Stole", 4.5, -1, 1, 1, 0, Array.Empty<string>(), MinAge: 13, Affect: StoleRow);

    /// <summary>FamilyTests' scene: Kim keeps the shop; her son Kid steals beside her; Ann, a
    /// neighbour, sees it too; May is mayor.</summary>
    private static SimResult KeeperAndChild(FeelingOptions feelings)
    {
        var row = new ActKind(Simulation.FamilyRow, 3.0, -1, 2, 15, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Actor, -0.3, 0.3, 1, TargetIs.Given));
        var cast = new[]
        {
            V("Kim", "K", 5, 2, age: 45, job: new Job("Room", new Tile(5, 2), 0, Clock.MinutesPerDay, Array.Empty<int>(), 1),
                family: new() { ["Kid"] = Kin.Child }),
            V("Kid", "K", 3, 2, age: 20, family: new() { ["Kim"] = Kin.Parent }),
            V("Ann", "A", 4, 3),
            V("May", "M", 9, 2, age: 60),
        };
        var authority = new AuthorityOptions
        {
            Mayor = "May", ElectConstable = false, ReportBase = 1,
            Keepers = new Dictionary<string, string> { ["Room"] = "Kim" },
        };
        return new Simulation(2, cast, new[] { Room() }, new[] { FamilyStole, row }, authority: authority, feelings: feelings,
            scheduled: new[] { (Ten, "Kid", "Stole") }, wander: 0).Run(1);
    }

    /// <summary>FamilyTests' scene: Tom steals; Wes and May (the mayor) see "someone" and suspect
    /// him; his mother Ma, who lives with him, is questioned too.</summary>
    private static SimResult QuestionedFamily(FeelingOptions feelings)
    {
        var questioned = new ActKind(Authority.Questioned, 2.5, -1, 1, 10, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Actor, -0.15, 0.3, 0.4, TargetIs.Given));
        var cast = new[]
        {
            V("Tom", "T", 3, 2, age: 25, family: new() { ["Ma"] = Kin.Parent }),
            V("Ma", "T", 12, 4, age: 50, family: new() { ["Tom"] = Kin.Child }),
            V("Wes", "W", 10, 2),
            V("May", "M", 11, 2, age: 60),
        };
        var authority = new AuthorityOptions { Mayor = "May", ElectConstable = false, ConfessBase = 0, ConfessPerTimidity = 0 };
        return new Simulation(3, cast, new[] { Room() }, new[] { FamilyStole, questioned }, authority: authority, feelings: feelings,
            scheduled: new[] { (Ten, "Tom", "Stole") }, wander: 0).Run(1);
    }

    /// <summary>Accounts compared by value (records compare lists by reference).</summary>
    private static string Canon(Account a)
        => $"{a.ActId} {a.From} {a.Actor} {a.Confidence:R} {a.FirstHand} {a.Tick} [{string.Join(",", a.Nearby ?? Array.Empty<string>())}] "
           + $"{a.Since} {a.Until} [{string.Join(",", a.Alibi ?? Array.Empty<string>())}]";

    /// <summary>T43 (rule 17): FamilyTests' scenes, their acts given feeling rows, with feelings
    /// steering and with feelings off. Kin still never report or retell, and questioned kin still
    /// vouch: the same accounts, confrontations and tellings.</summary>
    [Fact]
    public void FamiliesStillCover()
    {
        foreach (var scene in new Func<FeelingOptions, SimResult>[] { KeeperAndChild, QuestionedFamily })
        {
            SimResult off = scene(FeelingOptions.Off), on = scene(new FeelingOptions());
            Assert.Equal(off.Accounts.Select(Canon), on.Accounts.Select(Canon));
            Assert.Equal(off.Confrontations, on.Confrontations);
            Assert.Equal(off.Log.Where(l => l.Contains(" told ")), on.Log.Where(l => l.Contains(" told ")));
        }

        SimResult keeper = KeeperAndChild(new FeelingOptions());
        Assert.True(keeper.Regard[("Kim", "Kid")] < keeper.Baseline[("Kim", "Kid")]); // she feels it
        Assert.DoesNotContain(keeper.Accounts, a => a.From == "Kim");                  // and still never tells the mayor
        Assert.Contains(keeper.Acts, a => a.Kind == Simulation.FamilyRow && a.Actor == "Kid" && a.Target == "Kim");
        Account ma = QuestionedFamily(new FeelingOptions()).Accounts.Last(a => a.From == "Ma");
        Assert.Contains("Tom", ma.Alibi!);
    }

    /// <summary>T44 (S10; principle 5): Bob gives Ann a gift, so she is grateful to him; when she
    /// gives in return, the log says why, citing that sentiment and the act it came from. Feelings
    /// that only watch steer nothing, so they give no reason.</summary>
    [Fact]
    public void FeelingSteeredActsSayWhy()
    {
        var cast = new[] { V("Ann", "A", 3, 2), V("Bob", "B", 5, 2) };
        var scheduled = new[] { (Ten, "Bob", "GaveGift"), (Clock.At(11), "Ann", "GaveGift") };
        SimResult r = new Simulation(1, cast, new[] { Room() }, new[] { Gift() }, feelings: Steer(),
            scheduled: scheduled, wander: 0).Run(1);

        Assert.Equal(("Bob", "Ann"), (r.Acts[0].Actor, r.Acts[0].Target));
        Assert.Equal(("Ann", "Bob"), (r.Acts[1].Actor, r.Acts[1].Target));
        Assert.Contains(r.Log, l => l.EndsWith(" why Ann GaveGift Bob Grateful since d0 act 0"));
        Assert.DoesNotContain(r.Log, l => l.Contains(" why Bob "));   // he held nothing toward her when he gave

        SimResult watched = new Simulation(1, cast, new[] { Room() }, new[] { Gift() }, feelings: FeelingOptions.Observe,
            scheduled: scheduled, wander: 0).Run(1);
        Assert.DoesNotContain(watched.Log, l => l.Contains(" why "));
    }
}
