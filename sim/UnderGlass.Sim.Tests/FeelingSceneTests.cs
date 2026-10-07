using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// Feelings watched in small scenes (phase 0c, rules F1-F15): seeds, who feels a theft and how
/// much, feeling with those we love and hate, re-attribution when a belief changes, hearsay and
/// its confirmation, mishaps, and a scene felt through the witness who told it. Every scene is
/// the long hall with wandering off, so distances are exact; sensitivity and retention are 0.5,
/// so sens is 1 and keep is 1 for mild feelings. Exact values are read from the feeling events,
/// since the night's drift moves the values at the end. None of these acts is aimed at someone
/// chosen and the regard steering reads is neutral where it matters (a grudge only makes a guess
/// likelier, and the guess scene checks its seed), so steering is left on.
/// </summary>
public class FeelingSceneTests
{
    private static Haunt At(int x, int y, int from = 0, int to = Clock.MinutesPerDay, string place = "Room")
        => new(place, new Tile(x, y), from, to, 1);

    /// <summary>Someone who stands at one spot in the room all day and never tires.</summary>
    private static Villager V(string name, string household, int x, int y, double bold = 0.5, double self = 0.6,
        string kind = "villager", int age = 30, double understanding = 0.5, params string[] friends)
        => Moving(name, household, new[] { At(x, y) }, bold, self, kind, age, understanding, friends);

    /// <summary>Someone whose day moves between spots, or rooms, at set hours.</summary>
    private static Villager Moving(string name, string household, Haunt[] haunts, double bold = 0.5, double self = 0.6,
        string kind = "villager", int age = 30, double understanding = 0.5, params string[] friends)
        => new(name, household, kind, new Temperament(1.0, bold, understanding, self), new Body(100, -1), null, haunts,
            new Dictionary<string, double>(), friends, age);

    private static Location Hall(string name = "Room") => new(name, false, Enumerable.Repeat(new string('.', 40), 6).ToList());

    /// <summary>10:00 on day 0, when everyone is up.</summary>
    private static readonly int Ten = Clock.At(10);

    private static readonly ActKind Stole = new(Simulation.Stole, 4.5, -1, 1, 1, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, -0.5, 0.5, 1, TargetIs.Keeper));
    private static readonly ActKind Warned = new(Authority.Warned, 3.0, -1, 1, 5, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Actor, -0.3, 0.4, 0.4, TargetIs.Given));
    private static readonly ActKind Stumbled = new("Stumbled", 1.0, 0, 1, 1, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Actor, -0.1, 0, 0));
    private static readonly ActKind DrunkScene = new("DrunkScene", 3.0, -1, 2, 20, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Onlookers, -0.15, 0.3, 0.6, Tilt: -1));

    /// <summary>What a neutral bystander feels at a theft from Kim: imitation at likeness 0.5 (the
    /// same stage and kind) and understanding 0.5, so 0.2 x 0.75 x -0.5 x 0.75 = -0.05625.</summary>
    private const double Bystander = 0.2 * (0.5 + 0.5 * 0.5) * -0.5 * (1 - 0.5 * 0.5);

    /// <summary>Kim keeps the room; no mayor unless one is given.</summary>
    private static AuthorityOptions KimKeeps(string? mayor = null, double reportBase = 0.2) => new()
    {
        Mayor = mayor, ElectConstable = false, ReportBase = reportBase,
        Keepers = new Dictionary<string, string> { ["Room"] = "Kim" },
    };

    /// <summary>Nobody chats, so nobody is told anything and no company lifts a mood.</summary>
    private static GossipOptions Quiet() => new() { ChatChance = 0 };
    private static GossipOptions Chatty() => new() { ChatChance = 10 };

    /// <summary>One act at 10:00 on day 0; feelings on unless the scene passes its own.</summary>
    private static (Simulation Sim, SimResult R) Scene(long seed, IReadOnlyList<Villager> cast, IReadOnlyList<ActKind> kinds,
        string actor, string kind, FeelingOptions? feelings = null, GossipOptions? gossip = null, AuthorityOptions? authority = null,
        int days = 1, IReadOnlyList<Location>? places = null, Action<int, Simulation>? each = null)
    {
        var sim = new Simulation(seed, cast, places ?? new[] { Hall() }, kinds, gossip: gossip, authority: authority ?? KimKeeps(),
            scheduled: new[] { (Ten, actor, kind) }, wander: 0, feelings: feelings ?? new FeelingOptions());
        return (sim, sim.Run(days, each));
    }

    private static Dictionary<(string, string), double> Start(params ((string, string) Pair, double Regard)[] pairs)
        => pairs.ToDictionary(p => p.Pair, p => p.Regard);

    /// <summary>The regard events of one holder toward one subject (a name, or "kind:...") for an act.</summary>
    private static List<Felt> Toward(SimResult r, string holder, string toward, int act = 0)
        => r.Feelings.Where(f => f.Holder == holder && f.Toward == toward && f.ActId == act).ToList();

    /// <summary>The mood events of one holder for an act.</summary>
    private static List<Felt> Moods(SimResult r, string holder, int act = 0)
        => r.Feelings.Where(f => f.Holder == holder && f.Toward is null && f.ActId == act).ToList();

    /// <summary>The minute a teller told a listener about act 0.</summary>
    private static int Told(SimResult r, string teller, string listener)
        => int.Parse(r.Log.Single(l => l.EndsWith($" told {teller} {listener} 0")).Split(' ')[0]);

    [Fact]
    public void RegardIsSeededFromTheCard()
    {
        var town = new Simulation(1);
        Assert.Equal(0.6, town.PersonalRegard("Pierre", "Caroline"));
        Assert.Equal(0.6, town.PersonalRegard("Shane", "Marnie"));  // he rents a room at the ranch: a housemate, not kin
        Assert.Equal(0.4, town.PersonalRegard("Lewis", "Marnie"));  // friends
        Assert.Equal(0.4, town.PersonalRegard("Marnie", "Lewis"));
        Assert.Equal(0, town.PersonalRegard("Alex", "Lewis"));
        var names = DefaultTown.Cast().Select(v => v.Name).ToList();
        foreach (string n in names.Where(n => n != DefaultTown.Newcomer))
        {
            Assert.Equal(0, town.PersonalRegard(n, DefaultTown.Newcomer)); // nobody knows the newcomer yet
            Assert.Equal(0, town.PersonalRegard(DefaultTown.Newcomer, n));
        }
        // 84 of the 650 ordered pairs start at 0.4 or more: 44 in households, 40 between friends. (86
        // and 46 before the starting tensions, 0d.5: Sebastian toward Demetrius and Abigail toward
        // Pierre start at -0.3, not a housemate's 0.6.)
        var pairs = names.SelectMany(a => names.Where(b => b != a).Select(b => (A: a, B: b))).ToList();
        var home = DefaultTown.Cast().ToDictionary(v => v.Name, v => v.Household);
        var close = pairs.Where(p => town.PersonalRegard(p.A, p.B) >= 0.4).ToList();
        Assert.Equal(650, pairs.Count);
        Assert.Equal(84, close.Count);
        Assert.Equal(44, close.Count(p => home[p.A] == home[p.B]));
        Assert.Equal(-0.3, town.PersonalRegard("Sebastian", "Demetrius"));
        Assert.Equal(0.6, town.PersonalRegard("Demetrius", "Sebastian"));
        Assert.All(pairs, p => Assert.Equal(town.PersonalRegard(p.A, p.B), town.Baseline(p.A, p.B)));

        // Starting tensions override the seed, one way only, and are where regard heals back to.
        var cast = new[] { V("Ann", "A", 3, 2), V("Bob", "B", 5, 2), V("Cal", "A", 7, 2) };
        var feelings = new FeelingOptions { Start = Start((("Ann", "Bob"), -0.5), (("Cal", "Ann"), 0.1)) };
        var sim = new Simulation(1, cast, new[] { Hall() }, Array.Empty<ActKind>(), wander: 0, feelings: feelings);
        Assert.Equal(-0.5, sim.PersonalRegard("Ann", "Bob"));
        Assert.Equal(-0.5, sim.Baseline("Ann", "Bob"));
        Assert.Equal(0, sim.PersonalRegard("Bob", "Ann"));
        Assert.Equal(0.1, sim.PersonalRegard("Cal", "Ann"));        // over the household's 0.6
        Assert.Equal(0.1, sim.Baseline("Cal", "Ann"));
        Assert.Equal(0.6, sim.PersonalRegard("Ann", "Cal"));
        SimResult r = sim.Run(2);
        Assert.Equal(-0.5, r.Baseline[("Ann", "Bob")]);
        Assert.Equal(-0.5, r.Regard[("Ann", "Bob")]);                 // nothing happened, so nothing moved
        Assert.Equal(0.1, r.Regard[("Cal", "Ann")]);
    }

    /// <summary>Tom steals from Kim beside her; Far sees it from 7 tiles and can't tell who. The
    /// room is quiet, so nobody tells Far the name.</summary>
    [Fact]
    public void TheKeeperWhoSawTheThiefHatesThem_AFarStrangerBlamesAKind()
    {
        var cast = new[] { V("Tom", "T", 3, 2), V("Kim", "K", 5, 2), V("Far", "F", 10, 2) };
        var (sim, r) = Scene(1, cast, new[] { Stole }, "Tom", Simulation.Stole, gossip: Quiet());

        Felt kim = Assert.Single(Toward(r, "Kim", "Tom"));
        Assert.Equal("Direct", kim.Route);
        Assert.Equal(-0.25, kim.Change, 12);                           // -0.5 x clarity 1 x plastic 0.5
        Assert.Contains(r.Sentiments, s => s is { Holder: "Kim", Toward: "Tom", Name: "Hurt", ActId: 0 });
        Assert.True(r.PowerByDay["Kim"][0] < 0.5);

        Assert.Null(r.Beliefs["Far"][0].Actor);
        Assert.Empty(Toward(r, "Far", "Tom"));
        Felt far = Assert.Single(Toward(r, "Far", "kind:villager"));
        Assert.Equal("Kind", far.Route);
        Assert.Equal(-0.05625, Bystander, 12);
        Assert.Equal(-0.05625 * 0.3 * 0.5 * 0.75, far.Raw, 12);        // f0 x clarity x plastic x (1 - U/2)
        Assert.True(sim.Regard("Far", "Tom") < 0);
    }

    /// <summary>The same theft with presence switched off: Far, who saw it from 7 tiles (clarity
    /// 0.3), feels it and blames the kind as if he had seen it close up. Then with presence on and
    /// Far understanding (0.8): he blames the kind less, by 1 - 0.8/2 = 0.6, against 0.75 at 0.5.</summary>
    [Fact]
    public void AFarStrangerBlamesTheKindInFullWithoutPresence_AndLessTheMoreHeUnderstands()
    {
        var cast = new[] { V("Tom", "T", 3, 2), V("Kim", "K", 5, 2), V("Far", "F", 10, 2) };
        var (_, r) = Scene(1, cast, new[] { Stole }, "Tom", Simulation.Stole, new FeelingOptions { Presence = false }, Quiet());

        Belief seen = r.Beliefs["Far"][0];
        Assert.Equal(((string?)null, 0.3), (seen.Actor, Math.Round(seen.Clarity, 12)));
        Assert.Equal(Bystander, Assert.Single(Moods(r, "Far")).Mood, 12);    // s is 1, not the clarity
        Felt far = Assert.Single(Toward(r, "Far", "kind:villager"));
        Assert.Equal("Kind", far.Route);
        Assert.Equal(Bystander * 1 * 0.5 * 1 * 1 * 0.75, far.Raw, 12);     // f0 x 1 x plastic x freedom x keep x (1 - U/2)
        Assert.Equal(-0.25, Assert.Single(Toward(r, "Kim", "Tom")).Change, 12); // a clear view is weighed as before

        var wise = new[] { V("Tom", "T", 3, 2), V("Kim", "K", 5, 2), V("Far", "F", 10, 2, understanding: 0.8) };
        var (_, w) = Scene(1, wise, new[] { Stole }, "Tom", Simulation.Stole, gossip: Quiet());
        const double f0 = 0.2 * 0.75 * -0.5 * (1 - 0.5 * 0.8);            // he imitates less, too
        Felt mood = Assert.Single(Moods(w, "Far"));
        Assert.Equal(f0 * 0.3, mood.Mood, 12);
        Felt kind = Assert.Single(Toward(w, "Far", "kind:villager"));
        Assert.Equal(0.6, kind.Raw / (mood.Mood * 0.5), 12);                // raw / (f0 x clarity x plastic)
    }

    [Fact]
    public void CloserWitnessesFeelMore()
    {
        // Both know Tom (friends: familiarity 0.5), so Wes names him from 4 tiles, at clarity 0.6.
        var cast = new[]
        {
            V("Tom", "T", 3, 2), V("Ann", "A", 5, 2, friends: "Tom"), V("Wes", "W", 7, 2, friends: "Tom"), V("Kim", "K", 38, 2),
        };
        var (_, r) = Scene(1, cast, new[] { Stole }, "Tom", Simulation.Stole);

        Assert.Equal("Tom", r.Beliefs["Wes"][0].Actor);
        double ann = Toward(r, "Ann", "Tom")[0].Change, wes = Toward(r, "Wes", "Tom")[0].Change;
        Assert.True(ann < 0);
        Assert.Equal(0.6, wes / ann, 9);
    }

    [Fact]
    public void FeelingsFollowThoseWeLoveAndHate()
    {
        // Ann loves Kim, Bea hates her, Cal is neutral; all three see Tom steal from her close up.
        var cast = new[]
        {
            V("Tom", "T", 5, 2), V("Kim", "K", 7, 2), V("Ann", "A", 3, 2), V("Bea", "B", 5, 4), V("Cal", "C", 3, 4),
        };
        var feelings = new FeelingOptions { Start = Start((("Ann", "Kim"), 0.6), (("Bea", "Kim"), -0.6)) };
        var (_, r) = Scene(1, cast, new[] { Stole }, "Tom", Simulation.Stole, feelings);

        Felt ann = Toward(r, "Ann", "Tom")[0], bea = Toward(r, "Bea", "Tom")[0], cal = Toward(r, "Cal", "Tom")[0];
        Assert.Equal(("Sympathy", -0.075), (ann.Route, Math.Round(ann.Change, 12)));   // 0.5 x 0.6 x -0.5, x plastic 0.5
        Assert.Equal(("Antipathy", 0.0375), (bea.Route, Math.Round(bea.Change, 12)));  // her sadness is Bea's joy
        Assert.Equal(("Imitation", -0.028125), (cal.Route, Math.Round(cal.Change, 12)));

        Assert.True(Moods(r, "Ann").Sum(f => f.Mood) < 0);
        Assert.True(Moods(r, "Bea").Sum(f => f.Mood) > 0);
        Assert.Contains(r.Sentiments, s => s is { Holder: "Ann", Toward: "Tom", Name: "Indignant", ActId: 0 });
        Assert.Contains(r.Sentiments, s => s is { Holder: "Cal", Toward: "Tom", Name: "Indignant", ActId: 0 });
        Assert.Contains(r.Sentiments, s => s is { Holder: "Bea", Toward: "Tom", Name: "Pleased", ActId: 0 });
    }

    /// <summary>Story 1. The keeper's theft as above, with Ann beside Kim naming Tom and telling
    /// Far: what Far held against "someone, a villager" is taken back exactly and moves onto Tom.</summary>
    [Fact]
    public void SomeoneBecomesANameAndTheFeelingMovesOntoIt()
    {
        var cast = new[] { V("Tom", "T", 3, 2), V("Kim", "K", 5, 2), V("Far", "F", 10, 2), V("Ann", "A", 5, 2) };
        double kindAtNight = double.NaN;
        var (_, r) = Scene(1, cast, new[] { Stole }, "Tom", Simulation.Stole, gossip: Chatty(),
            each: (m, s) => { if (m == Clock.MinutesPerDay - 2) kindAtNight = s.KindRegard("Far", "villager"); });

        Belief far = r.Beliefs["Far"][0];
        Assert.Equal(("Tom", Source.Witnessed), (far.Actor, far.Source));
        var kinds = Toward(r, "Far", "kind:villager");
        Felt seen = Assert.Single(kinds, f => f.Route == "Kind");
        Felt back = Assert.Single(kinds, f => f.Route == "Reattributed");
        Assert.Equal(-seen.Change, back.Change, 12);
        Felt named = Assert.Single(Toward(r, "Far", "Tom"));
        Assert.Equal(("HeardName", back.Tick), (named.Basis, named.Tick));
        Assert.Equal(Bystander * 0.5 * 0.3 * far.Confidence * 0.5, named.Raw, 12); // heard-name weight x clarity x confidence
        Felt spill = Assert.Single(kinds, f => f.Route == "Spill");
        Assert.Equal(spill.Change, kindAtNight, 12);                 // only the spill from Tom is left on his kind
    }

    /// <summary>Tom steals from Kim. Gus, bold and low in self-regard, sees it from 7 tiles and
    /// names a guess: Nat, at the far end of the room, whom he already dislikes (-0.5, his
    /// baseline too, so the night leaves it be; with feelings steering, the dislike makes Nat the
    /// likelier guess, and seed 6 still gives him). Ann, his housemate, saw it close up and tells
    /// him it was Tom (0.9 beats his 0.8). Gus loves Kim (1.0); his indignation at Nat counts half
    /// on a regard already at -0.5, so sentiments are kept from 0.01 here.</summary>
    [Fact]
    public void AFeelingFollowsTheBelievedCause_WhenAGuessIsCorrected()
    {
        var cast = new[]
        {
            V("Tom", "T", 3, 2), V("Ann", "G", 5, 2), V("Gus", "G", 10, 2, bold: 0.8, self: 0.3),
            V("Nat", "N", 30, 2, kind: "old man", age: 70), V("Kim", "K", 38, 2),
        };
        var feelings = new FeelingOptions { Start = Start((("Gus", "Kim"), 1.0), (("Gus", "Nat"), -0.5)), SentimentMin = 0.01 };
        var (sim, r) = Scene(6, cast, new[] { Stole }, "Tom", Simulation.Stole, feelings, Chatty());

        Assert.Contains(r.Log, l => l.EndsWith(" belief Gus 0 Nat Witnessed 4.5"));          // his guess
        Assert.Contains(r.Log, l => l.Contains(" sentiment Gus Nat Indignant ") && l.EndsWith(" act 0"));
        Belief gus = r.Beliefs["Gus"][0];
        Assert.Equal("Tom", gus.Actor);
        Assert.True(gus.Confidence > 0.8);

        // At -0.5 a further drop counts half, so what was applied is not what was asked for; the
        // correction takes back what was applied, and its row still says what was asked for.
        Felt guess = Assert.Single(Toward(r, "Gus", "Nat"), f => f.Route != "Reattributed");
        Felt back = Assert.Single(Toward(r, "Gus", "Nat"), f => f.Route == "Reattributed");
        Assert.True(guess.Tick < back.Tick);
        Assert.NotEqual(guess.Raw, guess.Change, 6);
        Assert.Equal(guess.Raw * 0.5, guess.Change, 12);
        Assert.Equal(-guess.Change, back.Change, 12);
        Assert.Equal(-guess.Raw, back.Raw, 12);
        Assert.Equal(-0.5, sim.PersonalRegard("Gus", "Nat"), 9);
        Assert.Equal(0, Toward(r, "Gus", "Nat").Sum(f => f.Change), 12);
        Assert.Equal(0, Toward(r, "Gus", "kind:old man").Sum(f => f.Change), 12);           // Nat's kind is let off too
        Assert.True(sim.PersonalRegard("Gus", "Tom") < 0);
        Assert.DoesNotContain(r.Sentiments, s => s.Holder == "Gus" && s.Toward == "Nat");
    }

    [Fact]
    public void HearsayMovesMoodOnly_UntilASecondIndependentTellerAgrees()
    {
        // Ann and Bea see Tom steal close up; Cal stands 9 tiles from it, within 8 of both.
        var cast = new[] { V("Tom", "T", 3, 2), V("Kim", "K", 38, 2), V("Ann", "A", 5, 2), V("Bea", "B", 4, 4), V("Cal", "C", 12, 2) };
        var (_, r) = Scene(1, cast, new[] { Stole }, "Tom", Simulation.Stole, gossip: Chatty(), days: 2);

        int second = Math.Max(Told(r, "Ann", "Cal"), Told(r, "Bea", "Cal"));
        var cal = r.Feelings.Where(f => f.Holder == "Cal" && f.ActId == 0).ToList();
        Assert.Equal(("Told", (string?)null, 0.0), (cal[0].Basis, cal[0].Toward, cal[0].Change));
        Assert.True(cal[0].Mood < 0);
        Assert.DoesNotContain(cal, f => f.Toward is not null && f.Tick < second);
        Felt agreed = Assert.Single(cal, f => f.Toward == "Tom");
        Assert.Equal(("Corroborated", second), (agreed.Basis, agreed.Tick));
        Assert.Equal(Bystander * 0.25 * r.Beliefs["Cal"][0].Confidence * 0.5, agreed.Raw, 12);

        // Only Ann sees it. Dee hears it from her and tells Cal, who comes in at 13:00 and knows
        // Tom well enough to be worth telling; Ann tells Cal herself the next day. One source told
        // twice: Cal's mood moves, his regard doesn't.
        var echo = new[]
        {
            V("Tom", "T", 3, 2), V("Kim", "K", 38, 2), V("Ann", "A", 5, 2), V("Dee", "D", 12, 2),
            Moving("Cal", "C", new[] { At(2, 2, 0, Clock.At(13), "Away"), At(12, 4, Clock.At(13)) }, friends: "Tom"),
        };
        var (_, e) = Scene(1, echo, new[] { Stole }, "Tom", Simulation.Stole, gossip: Chatty(), days: 2,
            places: new[] { Hall(), Hall("Away") });
        Assert.True(Told(e, "Dee", "Cal") < Told(e, "Ann", "Cal"));
        Assert.Equal(new[] { "Dee", "Ann" }, e.Beliefs["Cal"][0].Chain);   // both tellings go back to Ann
        Assert.NotEmpty(Moods(e, "Cal"));
        Assert.DoesNotContain(e.Feelings, f => f.Holder == "Cal" && f.Toward is not null);
    }

    /// <summary>Tom steals; Ann sees it and tells Cal, 9 tiles off, that morning. May, the mayor,
    /// comes in at 14:00; Ann reports at once, and May warns Tom where he stands. Cal walks over
    /// at 14:00 and sees the warning.</summary>
    [Fact]
    public void APublicWarningSeenFirstHandConfirmsHearsay()
    {
        var cast = new[]
        {
            V("Tom", "T", 3, 2), V("Ann", "A", 5, 2), V("Kim", "K", 38, 2),
            Moving("Cal", "C", new[] { At(12, 2, 0, Clock.At(14)), At(6, 2, Clock.At(14)) }),
            Moving("May", "M", new[] { At(2, 2, 0, Clock.At(14), "Away"), At(8, 2, Clock.At(14)) }),
        };
        var (_, r) = Scene(1, cast, new[] { Stole, Warned }, "Tom", Simulation.Stole, gossip: Chatty(),
            authority: KimKeeps(mayor: "May", reportBase: 1), places: new[] { Hall(), Hall("Away") });

        Assert.Contains(r.Accounts, a => a is { From: "Ann", Actor: "Tom", ActId: 0 });
        Act warning = Assert.Single(r.Acts, a => a.Kind == Authority.Warned);
        Assert.Equal(("Tom", "May", 0), (warning.Actor, warning.Target, warning.About));
        Belief heard = r.Beliefs["Cal"][0];
        Assert.Equal((Source.Told, "Ann"), (heard.Source, heard.Chain[0]));
        Assert.Equal("Tom", r.Beliefs["Cal"][warning.Id].Actor);     // he saw the warning himself

        Assert.True(Moods(r, "Cal")[0].Tick < warning.Tick);        // he had heard it before
        var calTom = Toward(r, "Cal", "Tom");
        Assert.All(calTom, f => Assert.True(f.Tick > warning.Tick)); // and his regard didn't move until then
        Felt confirmed = Assert.Single(calTom);
        Assert.Equal("Confirmed", confirmed.Basis);
        Assert.Equal(Bystander * 0.5 * heard.Confidence * 0.5, confirmed.Raw, 12);
    }

    /// <summary>Sam stumbles beside Ann, who is fond of him. The room is quiet, so no chat lifts
    /// anyone's mood.</summary>
    [Fact]
    public void AMishapSaddensFriendsButBlamesNobody()
    {
        var cast = new[] { V("Sam", "S", 3, 2), V("Ann", "A", 5, 2) };
        var feelings = new FeelingOptions { Start = Start((("Ann", "Sam"), 0.6)) };
        var (sim, r) = Scene(1, cast, new[] { Stumbled }, "Sam", "Stumbled", feelings, Quiet());

        Felt ann = Assert.Single(Moods(r, "Ann"));
        Assert.Equal("Sympathy", ann.Route);
        Assert.Equal(0.5 * 0.6 * -0.1, ann.Mood, 12);
        Assert.True(sim.Mood("Ann") < 0);
        Felt sam = Assert.Single(Moods(r, "Sam"));
        Assert.Equal(("Undergone", -0.1), (sam.Route, Math.Round(sam.Mood, 12)));
        Assert.DoesNotContain(r.Feelings, f => f.Change != 0);
    }

    /// <summary>Bob makes a drunk scene that only Wit sees. Wit tells Mid, who saw nothing, and Mid
    /// tells Lis and Str, out of Wit's reach and out of sight of the scene. Lis loves Wit and
    /// dislikes Mid; Str, an old man with nothing in common with Wit, shares Mid's home. Told
    /// twice over, the scene is still felt through Wit, the witness at the end of the chain: with
    /// Mid as the patient, Lis would be glad (antipathy) and Str would feel with a housemate.
    /// Mid's story is worth 1.05 to him (0.35 x 3), so here stories are passed on from 1, to two
    /// listeners a day.</summary>
    [Fact]
    public void ADrunkSceneTold_IsFeltThroughTheWitnessWhoSawIt()
    {
        var cast = new[]
        {
            V("Bob", "B", 3, 2), V("Wit", "W", 8, 2), V("Mid", "M", 14, 2), V("Lis", "L", 20, 2),
            V("Str", "M", 20, 4, kind: "old man", age: 70),
        };
        Assert.Equal(0, Feelings.Likeness(cast[4], cast[1]));
        Assert.Equal(0.25, Feelings.Likeness(cast[4], cast[2]));
        var feelings = new FeelingOptions { Start = Start((("Lis", "Wit"), 0.6), (("Lis", "Mid"), -0.6)) };
        var gossip = new GossipOptions { ChatChance = 10, VolunteerLevel = 1, TellsPerDay = 2 };
        var famLis = new double[2 * Clock.MinutesPerDay];
        var famStr = new double[2 * Clock.MinutesPerDay];
        var (_, r) = Scene(1, cast, new[] { DrunkScene }, "Bob", "DrunkScene", feelings, gossip, days: 2,
            each: (m, s) => (famLis[m], famStr[m]) = (s.Familiarity("Lis", "Mid"), s.Familiarity("Str", "Mid")));

        Assert.Equal(new[] { "Mid", "Wit" }, r.Beliefs["Lis"][0].Chain);
        Assert.Equal(new[] { "Mid", "Wit" }, r.Beliefs["Str"][0].Chain);
        Felt lis = Assert.Single(Moods(r, "Lis")), str = Assert.Single(Moods(r, "Str"));
        Assert.Equal(("Sympathy", "Imitation"), (lis.Route, str.Route));
        Assert.True(lis.Mood < 0 && str.Mood < 0);
        Assert.True(Math.Abs(lis.Mood) > Math.Abs(str.Mood));

        // f0 from Wit, x hearsay 0.5 x credence in Mid, the teller who told them (F5).
        var o = new FeelingOptions();
        Assert.Equal(lis.Tick, Told(r, "Mid", "Lis"));
        Assert.Equal(str.Tick, Told(r, "Mid", "Str"));
        Assert.Equal(0.5 * 0.6 * -0.15 * 0.5 * Feelings.Credence(famLis[lis.Tick], -0.6, 0.5, o), lis.Mood, 12);        // loves Wit
        Assert.Equal(0.2 * 0.5 * -0.15 * 0.75 * 0.5 * Feelings.Credence(famStr[str.Tick], 0.6, 0.5, o), str.Mood, 12); // like Wit in nothing
        Assert.DoesNotContain(r.Feelings, f => f.Holder is "Lis" or "Str" && f.Toward is not null);
    }
}
