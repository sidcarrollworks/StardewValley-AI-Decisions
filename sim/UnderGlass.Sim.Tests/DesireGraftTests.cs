using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// The desire gate's added rules in small scenes (phase 0d; rule 10; spec D21-D26, D28, D29, and
/// the first greeting): turning away, only the shy leaving, the third slight, no light ping-pong,
/// a stance that deepens, brawlers held back by fear, love that misses someone, pity at a
/// mishap, and a curt greeting. The scene convention is SteeringTests': sensitivity and
/// retention 0.5 (so sens is 1), one long room, no wandering, nobody tires, the scenes' own
/// rows, Ann at (3,2) and Bob at (5,2), each in their own household. Every scene starts from
/// <see cref="FeelingOptions.WithDesire"/> (steering, the gate, power weight 0, every added rule
/// off) and turns on only the rule it is about.
/// </summary>
public class DesireGraftTests
{
    private const int D = Clock.MinutesPerDay;
    private static readonly int Ten = Clock.At(10);

    /// <summary>Someone who stands at one spot in the room all day (or keeps the given haunts) and never tires.</summary>
    private static Villager V(string name, string household, int x, int y, double bold = 0.5, Dictionary<string, double>? acts = null,
        double understanding = 0.5, double sensitivity = 0.5, Haunt[]? haunts = null)
        => new(name, household, "villager", new Temperament(1.0, bold, understanding, 0.6, sensitivity, 0.5), new Body(100, -1), null,
            haunts ?? new[] { new Haunt("Room", new Tile(x, y), 0, D, 1) },
            acts ?? new Dictionary<string, double>(), Array.Empty<string>());

    /// <summary>A long open room.</summary>
    private static Location Room(string name = "Room") => new(name, false, Enumerable.Repeat(new string('.', 40), 6).ToList());

    private static BodyOptions Awake() => new() { AwakeHoursAtRest = 100_000 };

    // The scenes' own rows, so tuning the town doesn't move these tests; the light kinds as in the town.
    private static readonly Affect GiftRow = new(Patient.Target, 0.2, 0.3, 1, TargetIs.Chosen, Tilt: 1);
    private static readonly Affect ArguedRow = new(Patient.Target, -0.3, 0.3, 1, TargetIs.Chosen);
    private static readonly Affect HelpRow = new(Patient.Target, 0.3, 0.3, 1, TargetIs.Chosen, Tilt: 1);

    private static ActKind Gift(double perDay = 0) => new("GaveGift", 1.5, 1, 1, 1, perDay, Array.Empty<string>(), Affect: GiftRow);
    private static readonly ActKind Argued = new("Argued", 3.0, -1, 2, 10, 0, Array.Empty<string>(), MinAge: 13, Affect: ArguedRow);
    private static readonly ActKind Help = new("HelpedSomeone", 2.0, 1, 2, 5, 0, Array.Empty<string>(), MinAge: 10, Affect: HelpRow);
    private static readonly ActKind Snubbed = new(Simulation.Snubbed, 0.5, -1, 1, 1, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, -0.15, 0.3, 1, TargetIs.Chosen));
    private static readonly ActKind TurnedAway = new(Simulation.TurnedAway, 0.5, -1, 1, 1, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, -0.05, 0.3, 1, TargetIs.Chosen));
    private static readonly ActKind Stumbled = new("Stumbled", 1.0, 0, 1, 1, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Actor, -0.1, 0, 0));

    /// <summary>The scene's kinds; a gift drawn at a rate when giftsPerDay is above 0.</summary>
    private static ActKind[] Kinds(double giftsPerDay = 0) => new[] { Gift(giftsPerDay), Argued, Help, Snubbed, TurnedAway, Stumbled };

    private static SimResult Run(IReadOnlyList<Villager> cast, FeelingOptions o, (int, string, string)[] scheduled, int days,
        double giftsPerDay = 0, Location[]? places = null, Action<int, Simulation>? each = null)
        => new Simulation(1, cast, places ?? new[] { Room() }, Kinds(giftsPerDay), feelings: o, scheduled: scheduled, wander: 0, body: Awake())
            .Run(days, each);

    private static (int, string, string)[] At(params (int Minute, string Actor, string Kind)[] acts) => acts.Select(a => (a.Minute, a.Actor, a.Kind)).ToArray();

    private static bool Hostile(Act a) => a.Kind is "Argued" or Simulation.Snubbed or Simulation.TurnedAway;

    /// <summary>Bob haunts the room (weight 1, all day) and also has a home to go to.</summary>
    private static Villager BobWithAHome(double bold, double sensitivity = 0.5)
        => V("Bob", "B", 5, 2, bold, sensitivity: sensitivity, haunts: new[] { new Haunt("Room", new Tile(5, 2), 0, D, 1) });

    private static readonly Location[] RoomAndBobsHome = { Room(), Room("Home:B") };

    // ---- D21 ------------------------------------------------------------------------------

    /// <summary>D21 (R9, light acts on): Bob, very shy, is argued at. Neither an argument (0.9 with
    /// the fear of one hit) nor a snub (0.6) is within his daring, and the best margin is below
    /// 0 (close calls would say no): he avoids Ann for a week, and turns away from her at once
    /// (no gate question: turning away needs no daring). His gifts go only to Cal while he avoids
    /// her. Cal stands 6 tiles from Ann, out of her reach, so her argument can only go to Bob.</summary>
    [Fact]
    public void TheShyTurnAway()
    {
        FeelingOptions o = FeelingOptions.WithDesire();
        o.LightActsOn = true;
        o.CloseCall = _ => false;
        var cast = new[] { V("Ann", "A", 3, 2), V("Bob", "B", 5, 2, bold: 0.05, acts: new() { ["GaveGift"] = 20 }), V("Cal", "C", 9, 2) };
        SimResult r = Run(cast, o, At((Ten, "Ann", "Argued")), 10, giftsPerDay: 20);

        Act argue = Assert.Single(r.Acts, a => a.Kind == "Argued");
        Assert.Equal(("Ann", "Bob"), (argue.Actor, argue.Target));
        Act turned = Assert.Single(r.Acts, a => a.Kind == Simulation.TurnedAway);
        Assert.Equal(("Bob", "Ann", argue.Id), (turned.Actor, turned.Target, turned.About));
        Assert.True(turned.Tick <= argue.Tick + Argued.DurationMinutes + 5, $"turned away at {turned.Tick}, argued at {argue.Tick}");
        Assert.Contains(turned.Id, r.Pursued);
        Assert.DoesNotContain(r.Acts, a => a.Actor == "Bob" && a.Kind is "Argued" or Simulation.Snubbed);

        var avoid = Assert.Single(r.Avoids);
        Assert.Equal(("Bob", "Ann", argue.Id, avoid.Tick + 7 * D), (avoid.Holder, avoid.Subject, avoid.Source, avoid.Until));
        Assert.Contains(r.Log, l => l.StartsWith($"{avoid.Tick} avoid Bob Ann act {argue.Id} margin -"));
        Assert.Contains(r.Pursuits, p => p.Holder == "Bob" && p.ActKind == Simulation.Snubbed && !p.Acted && p.Margin < 0);

        Assert.Single(r.LifeEvents, e => e.Person == "Bob" && e.Role == LifeRole.Avoided && e.Other == "Ann" && e.ActId == argue.Id);
        LifeEvent annDid = Assert.Single(r.LifeEvents, e => e.Person == "Ann" && e.Role == LifeRole.Did && e.ActId == argue.Id);
        Assert.Equal(Outcome.Avoided, annDid.Outcome);
        LifeEvent bobGot = Assert.Single(r.LifeEvents, e => e.Person == "Bob" && e.Role == LifeRole.Undergone && e.ActId == argue.Id);
        Assert.Equal(Outcome.Avoided, bobGot.Outcome);

        // While he avoids her, his gifts go only to Cal.
        var gifts = r.Acts.Where(a => a.Kind == "GaveGift" && a.Actor == "Bob" && a.Tick > avoid.Tick && a.Tick < avoid.Until).ToList();
        Assert.NotEmpty(gifts);
        Assert.All(gifts, g => Assert.Equal("Cal", g.Target));
    }

    // ---- D22 ------------------------------------------------------------------------------

    /// <summary>One scene for D22: Ann stays in the room; Bob haunts it and has a home; Ann argues
    /// at him once. Close calls say no, and with lightPerDay 0 he has no snub to fall back on.
    /// Returns the run and where Bob was at the end of each minute.</summary>
    private static (SimResult R, string[] BobAt) Shy(double bold, double sensitivity = 0.5, bool withdraw = true, double fearPerHit = 0.1,
        int lightPerDay = 2)
    {
        FeelingOptions o = FeelingOptions.WithDesire();
        o.LightActsOn = true;
        o.CloseCall = _ => false;
        o.WithdrawOn = withdraw;
        o.FearPerHit = fearPerHit;
        o.LightPerDay = lightPerDay;
        var bobAt = new string[8 * D];
        SimResult r = Run(new[] { V("Ann", "A", 3, 2), BobWithAHome(bold, sensitivity) }, o, At((Ten, "Ann", "Argued")), 8,
            places: RoomAndBobsHome, each: (m, s) => bobAt[m] = s.Where("Bob").Place);
        return (r, bobAt);
    }

    /// <summary>Bob's minutes in the room on days 1-6, while he avoids Ann.</summary>
    private static int RoomMinutes(string[] bobAt) => bobAt.Skip(D).Take(6 * D).Count(p => p == "Room");

    /// <summary>The minutes of the withdrawal lines in a run's log.</summary>
    private static List<int> Withdrawn(SimResult r)
        => r.Log.Where(l => l.Contains(" withdraws Bob home from Ann")).Select(l => int.Parse(l.Split(' ')[0])).ToList();

    /// <summary>D22 (R9, light acts on): only the shy leave. Bob at boldness 0.05 avoids Ann, goes
    /// home from her on the day she argues, and spends fewer minutes in the room over the next
    /// six days than when he may not withdraw. A bolder Bob (0.55) who avoids her stays, because
    /// he could still answer her (his margin for an argument now, from the grudge alone, is in
    /// the band); afraid of her (FearPerHit 0.5), he leaves too. Scene change: the spec's bolder
    /// Bob is 0.45 with sensitivity 0.5. With those, his margin when he avoids is the standing
    /// margin plus half the motive's event part (0.15), so any avoid at 0.45 puts the standing
    /// margin under -0.15 and he always leaves. With sensitivity 0 the argument is felt at 0.15,
    /// and at boldness 0.55 the margin when he avoids is just below 0 while the standing margin
    /// is about -0.08. lightPerDay 0 takes the snub away, so an argument is his only answer.</summary>
    [Fact]
    public void OnlyTheShyLeave()
    {
        var (shy, shyAt) = Shy(0.05);
        Act argue = shy.Acts[0];
        Assert.Equal(("Argued", "Ann", "Bob"), (argue.Kind, argue.Actor, argue.Target));
        Assert.Equal(("Bob", "Ann", argue.Id), (shy.Avoids[0].Holder, shy.Avoids[0].Subject, shy.Avoids[0].Source));
        Assert.True(shy.Withdrawals >= 1);
        Assert.Equal(shy.Withdrawals, Withdrawn(shy).Count);
        Assert.Equal(0, Clock.Day(Withdrawn(shy)[0]));
        Assert.All(Withdrawn(shy), m => Assert.Equal("Home:B", shyAt[m + 1])); // he goes home the next minute
        Assert.Contains(shy.LifeEvents, e => e.Person == "Bob" && e.Role == LifeRole.Withdrew && e.Other == "Ann");
        var (stays, staysAt) = Shy(0.05, withdraw: false);
        Assert.NotEmpty(stays.Avoids);
        Assert.Equal(0, stays.Withdrawals);
        int shyRoom = RoomMinutes(shyAt), stayRoom = RoomMinutes(staysAt);
        Assert.True(shyRoom < stayRoom, $"in the room {shyRoom} minutes withdrawing, {stayRoom} not");

        var (bold, _) = Shy(0.55, sensitivity: 0, fearPerHit: 0, lightPerDay: 0);
        Assert.Equal(("Bob", "Ann"), (Assert.Single(bold.Avoids).Holder, bold.Avoids[0].Subject));
        Assert.Contains(bold.Pursuits, p => p.Holder == "Bob" && p.ActKind == "Argued" && p.Call == "close-no" && p.Margin is < 0 and > -0.15);
        Assert.Equal(0, bold.Withdrawals);
        Assert.DoesNotContain(bold.Log, l => l.Contains(" withdraws "));

        var (afraid, afraidAt) = Shy(0.55, sensitivity: 0, fearPerHit: 0.5, lightPerDay: 0);
        Assert.Equal(("Bob", "Ann"), (afraid.Avoids[0].Holder, afraid.Avoids[0].Subject));
        Assert.Contains(afraid.Pursuits, p => p.Holder == "Bob" && p.ActKind == "Argued" && p.Call == "no" && p.Margin < -0.15);
        Assert.True(afraid.Withdrawals >= 1);
        Assert.All(Withdrawn(afraid), m => Assert.Equal("Home:B", afraidAt[m + 1]));
    }

    /// <summary>R9 and R1: turning away is "Begin(TurnedAway, target: s, about: source)", so when shy
    /// Bob goes home from Ann and turns away first, his cold shoulder cites the argument he is
    /// avoiding her over, and as a light answer to her own hostility it stirs nothing in her. In
    /// the build, the withdrawal's turning away cites no act (about -1): Ann takes it as an
    /// unprovoked slight, an Answer is stirred toward Bob, the third one marks her regard, and she
    /// snubs and then argues with the person who is keeping away from her.</summary>
    [Fact]
    public void TurningAwayOnTheWayHomeCitesTheAvoid()
    {
        var (r, _) = Shy(0.05);
        var turned = r.Acts.Where(a => a.Kind == Simulation.TurnedAway).ToList();
        Assert.True(turned.Count >= 2); // the first when the avoid starts, the rest on the way home
        Assert.All(turned, t => Assert.True(t.About >= 0 && r.Acts[t.About].Actor == "Ann", $"act {t.Id} about {t.About}"));
        Assert.DoesNotContain(r.Stirred, s => s.Holder == "Ann" && s.Source >= 0 && r.Acts[s.Source].Kind == Simulation.TurnedAway);
    }

    // ---- D23 ------------------------------------------------------------------------------

    /// <summary>D23 (rule 4, light acts on): Bob snubs Ann at 10:00 on days 0, 1 and 2. The third
    /// slight within five days leaves a mark: one regard move of -0.3 by the route "Mark", on
    /// day 2 only. Ann is bold 0 and answering is off, so nothing else happens between them.</summary>
    [Fact]
    public void TheThirdSlightMarks()
    {
        FeelingOptions o = FeelingOptions.WithDesire();
        o.LightActsOn = true;
        o.AnswerOn = false;
        SimResult r = Run(new[] { V("Ann", "A", 3, 2, bold: 0), V("Bob", "B", 5, 2) }, o,
            At((Ten, "Bob", Simulation.Snubbed), (D + Ten, "Bob", Simulation.Snubbed), (2 * D + Ten, "Bob", Simulation.Snubbed)), 4);

        var snubs = r.Acts.Where(a => a.Kind == Simulation.Snubbed).ToList();
        Assert.Equal(new[] { 0, 1, 2 }, snubs.Select(a => Clock.Day(a.Tick)));
        Assert.All(snubs, s => Assert.Equal(("Bob", "Ann"), (s.Actor, s.Target)));
        Felt mark = Assert.Single(r.Feelings, f => f.Route == "Mark");
        Assert.Equal(("Ann", "Bob", snubs[2].Id, 2), (mark.Holder, mark.Toward, mark.ActId, Clock.Day(mark.Tick)));
        Assert.Equal(-0.3, mark.Raw, 12);
        Assert.Equal(1, r.Marks);
        Assert.Contains(r.Log, l => l.Contains(" mark Ann Bob Snubbed x3"));
        Assert.Equal(3, r.Acts.Count);
    }

    // ---- D24 ------------------------------------------------------------------------------

    /// <summary>D24 (R1, light acts on): Ann argues at Bob. Bob (0.4) dares an argument only by a
    /// close call, which says no; a snub is clear, so he snubs her back. A light answer to Ann's
    /// own hostility stirs nothing in her, so the exchange ends at two hostile acts. Scene change:
    /// FearPerHit is 0, as in the spec's numbers; at 0.1 the hit Bob just took makes the snub cost
    /// 0.6 and only a close call too.</summary>
    [Fact]
    public void NoLightPingPong()
    {
        FeelingOptions o = FeelingOptions.WithDesire();
        o.LightActsOn = true;
        o.CloseCall = _ => false;
        o.FearPerHit = 0;
        SimResult r = Run(new[] { V("Ann", "A", 3, 2), V("Bob", "B", 5, 2, bold: 0.4) }, o, At((Ten, "Ann", "Argued")), 4);

        Act argue = Assert.Single(r.Acts, a => a.Kind == "Argued");
        Act snub = Assert.Single(r.Acts, a => a.Kind == Simulation.Snubbed);
        Assert.Equal(("Bob", "Ann", argue.Id), (snub.Actor, snub.Target, snub.About));
        Assert.Contains(r.Pursuits, p => p.Holder == "Bob" && p.ActKind == "Argued" && p.Call == "close-no");
        Assert.Contains(r.Pursuits, p => p.Holder == "Bob" && p.ActKind == Simulation.Snubbed && p.Call == "clear" && p.Acted);
        Assert.DoesNotContain(r.Stirred, s => s.Holder == "Ann");
        Assert.Equal(2, r.Acts.Count(Hostile));
        // Ann still feels the snub, in mood and regard.
        Assert.Contains(r.Feelings, f => f.Holder == "Ann" && f.ActId == snub.Id && f.Toward == "Bob" && f.Change < 0);
    }

    // ---- D25 ------------------------------------------------------------------------------

    /// <summary>One scene for D25: Ann argues at Bob (0.2) on days 0, 4 and 8, and Cal gives him a
    /// gift on day 9. Cal stands 6 tiles from Ann, so her arguments go to Bob and his gift does
    /// too. Avoidance is off, so the only extra home weight is the stance's, and StanceHome is 2
    /// so its pull home is plain in a 12-day run. Returns the run and Bob's minutes at home.</summary>
    private static (SimResult R, int HomeMinutes) Hurt(bool stance)
    {
        FeelingOptions o = FeelingOptions.WithDesire();
        o.StanceOn = stance;
        o.AvoidOn = false;
        o.StanceHome = 2;
        int home = 0;
        SimResult r = Run(new[] { V("Ann", "A", 3, 2), BobWithAHome(0.2), V("Cal", "C", 9, 2) }, o,
            At((Ten, "Ann", "Argued"), (4 * D + Ten, "Ann", "Argued"), (8 * D + Ten, "Ann", "Argued"), (9 * D + Ten, "Cal", "GaveGift")), 12,
            places: RoomAndBobsHome, each: (m, s) =>
            {
                if (s.Where("Bob") is { Place: "Home:B", Asleep: false })
                    home++;
            });
        return (r, home);
    }

    /// <summary>D25 (R10, stance on): each argument moves shy Bob's stance by 0.3 x (2 x 0.2 - 1),
    /// so it deepens: after the third it is below where the first left it, which is below 0. A
    /// withdrawn stance raises his home weight, so he spends more minutes at home than without
    /// stance. Cal's gift (felt 0.2) pulls the stance a fifth of the way toward 0 that night, on
    /// top of the night's fading, and never across it.</summary>
    [Fact]
    public void StanceDeepens()
    {
        var (r, home) = Hurt(stance: true);
        var argued = r.Acts.Where(a => a.Kind == "Argued").ToList();
        Assert.Equal(3, argued.Count);
        Assert.All(argued, a => Assert.Equal(("Ann", "Bob"), (a.Actor, a.Target)));
        double[] bob = r.Stances["Bob"];
        int first = Clock.Day(argued[0].Tick), third = Clock.Day(argued[2].Tick);
        Assert.True(bob[first] < 0);
        Assert.Equal(-0.18 * 0.975, bob[first], 9);
        Assert.True(bob[third] < bob[first], $"after the third {bob[third]}, after the first {bob[first]}");
        Assert.All(r.Stances.Where(p => p.Key != "Bob").SelectMany(p => p.Value), s => Assert.Equal(0, s));

        Act gift = Assert.Single(r.Acts, a => a.Kind == "GaveGift" && a.Actor == "Cal");
        Assert.Equal("Bob", gift.Target);
        int g = Clock.Day(gift.Tick);
        Assert.True(g > third);
        Assert.Contains(r.Stirred, s => s.Holder == "Bob" && s.Motive == DesireKind.Return && s.Source == gift.Id);
        Assert.Equal(bob[g - 1] * (1 - 0.2) * 0.975, bob[g], 9);
        Assert.True(bob[g] < 0);

        var (flat, flatHome) = Hurt(stance: false);
        Assert.All(flat.Stances["Bob"], s => Assert.Equal(0, s));
        Assert.True(home > flatHome, $"at home {home} minutes with stance, {flatHome} without");
    }

    // ---- D26 ------------------------------------------------------------------------------

    private static SimResult Brawl(double fearPerHit, Func<Pursuit, bool?>? closeCall = null)
    {
        FeelingOptions o = FeelingOptions.WithDesire();
        o.FearPerHit = fearPerHit;
        o.CloseCall = closeCall;
        return Run(new[] { V("Ann", "A", 3, 2, bold: 0.98), V("Bob", "B", 5, 2, bold: 0.98) }, o, At((Ten, "Ann", "Argued")), 21);
    }

    /// <summary>D26 (R5): two brawlers (boldness 0.98) answer each other's arguments, each as soon
    /// as the cooldown allows, and feud. The cooldown holds each direction to at most 7 in 21
    /// days, 3 days apart. Fear of the other (0.3 a hit) caps it: by the fourth hit an argument is
    /// no longer clear, and with every close call saying no, at most 3 a direction.</summary>
    [Fact]
    public void BrawlersFeud_FearCaps()
    {
        SimResult r = Brawl(0.1);
        Assert.Contains(r.Ties, t => (t.A, t.B, t.What) == ("Ann", "Bob", "feud"));
        foreach (string who in new[] { "Ann", "Bob" })
        {
            var ticks = r.Acts.Where(a => a.Kind == "Argued" && a.Actor == who).Select(a => a.Tick).ToList();
            Assert.InRange(ticks.Count, 6, 7);
            for (int i = 1; i < ticks.Count; i++)
                Assert.True(ticks[i] - ticks[i - 1] >= 3 * D);
        }
        // Bob's first answer comes as soon as Ann's argument ends: the cooldown is per ordered pair.
        Act first = r.Acts[0], answer = r.Acts[1];
        Assert.Equal(("Bob", first.Id), (answer.Actor, answer.About));
        Assert.True(answer.Tick <= first.Tick + 60);

        SimResult afraid = Brawl(0.3, _ => false);
        foreach (string who in new[] { "Ann", "Bob" })
            Assert.InRange(afraid.Acts.Count(a => a.Kind == "Argued" && a.Actor == who), 1, 3);
        Assert.Contains(afraid.Pursuits, p => p.ActKind == "Argued" && p.Call == "close-no");
    }

    // ---- D28 ------------------------------------------------------------------------------

    /// <summary>D28 (R3, Fond on): Ann loves Bob (0.5), but Bob comes into the room only from 10:00
    /// to 10:30, so they never have a day together (60 minutes). Fond grows with the days apart:
    /// 0.3 x 2/7 = 0.086 on day 2, below a gift's minimum (0.1), and 0.129 on day 3. Her first
    /// act toward him is a gift on day 3, motive Fond, logged as "regard"; her own kindness resets
    /// the days, so the next comes no sooner than day 6. Bob returns each gift, and his return of
    /// it is not returned.</summary>
    [Fact]
    public void FondMissesThem()
    {
        FeelingOptions o = FeelingOptions.WithDesire(("Ann", "Bob", 0.5));
        o.FondOn = true;
        var bob = V("Bob", "B", 5, 2, haunts: new[]
        {
            new Haunt("Away", new Tile(2, 2), 0, Ten, 1), new Haunt("Room", new Tile(5, 2), Ten, Ten + 30, 1),
            new Haunt("Away", new Tile(2, 2), Ten + 30, D, 1),
        });
        SimResult r = Run(new[] { V("Ann", "A", 3, 2), bob }, o, Array.Empty<(int, string, string)>(), 10,
            places: new[] { Room(), Room("Away") });

        Assert.Equal(0.3 * 2 / 7, DesireMath.Fond(0.5, 2, o), 12);
        var annGifts = r.Acts.Where(a => a.Actor == "Ann").ToList();
        Assert.True(annGifts.Count >= 2);
        Assert.All(annGifts, a => Assert.Equal(("GaveGift", "Bob", -1), (a.Kind, a.Target, a.About)));
        Assert.Equal(3, Clock.Day(annGifts[0].Tick));
        Assert.True(Clock.Day(annGifts[1].Tick) >= 6);
        Pursuit fond = Assert.Single(r.Pursuits, p => p.ActId == annGifts[0].Id);
        Assert.Equal((DesireKind.Fond, -1, "clear"), (fond.Motive, fond.Source, fond.Call));
        Assert.True(fond.Intensity >= o.GiftMin && fond.Intensity < 0.3);
        Assert.Contains(r.Log, l => l.StartsWith($"{annGifts[0].Tick} desire Ann Fond Bob act regard: GaveGift"));

        Act back = Assert.Single(r.Acts, a => a.Actor == "Bob" && a.About == annGifts[0].Id);
        Assert.Equal(("GaveGift", "Ann"), (back.Kind, back.Target));
        Assert.DoesNotContain(r.Stirred, s => s.Holder == "Ann");
        Assert.DoesNotContain(r.Acts, a => a.About == back.Id);
    }

    // ---- D29 ------------------------------------------------------------------------------

    private static SimResult Mishap(bool pity, string deeHousehold = "D")
    {
        FeelingOptions o = FeelingOptions.WithDesire();
        o.PityOn = pity;
        return Run(new[] { V("Cal", "C", 5, 2), V("Dee", deeHousehold, 3, 2, bold: 0.6, sensitivity: 1.0) }, o,
            At((Ten, "Cal", "Stumbled")), 1);
    }

    /// <summary>D29 (R4, Pity on): Dee sees Cal stumble two tiles away and helps him within the
    /// hour, motive Pity, citing the stumble. Without Pity she does not, nor as his housemate
    /// (families cover). Scene change: Dee's sensitivity is 1 (sens 1.5), not 0.5. At sens 1 pity
    /// is 2 x 0.1 x 1 = 0.2, exactly help's minimum, and it has faded below it by the first
    /// weighing five minutes later, so it lapses unacted; at sens 1.5 it is 0.3.</summary>
    [Fact]
    public void PityHelps()
    {
        SimResult r = Mishap(pity: true);
        Act stumble = Assert.Single(r.Acts, a => a.Kind == "Stumbled");
        Act help = Assert.Single(r.Acts, a => a.Kind == "HelpedSomeone");
        Assert.Equal(("Dee", "Cal", stumble.Id), (help.Actor, help.Target, help.About));
        Assert.True(help.Tick - stumble.Tick <= 60);
        Stirring stir = Assert.Single(r.Stirred, s => s.Holder == "Dee");
        Assert.Equal(("Cal", DesireKind.Pity, stumble.Id), (stir.Subject, stir.Motive, stir.Source));
        Assert.Equal(0.3, stir.Felt, 12);
        Pursuit p = Assert.Single(r.Pursuits, p => p.ActId == help.Id);
        Assert.Equal(DesireKind.Pity, p.Motive);
        Assert.Contains(help.Id, r.Pursued);

        SimResult off = Mishap(pity: false);
        Assert.DoesNotContain(off.Acts, a => a.Kind == "HelpedSomeone");
        Assert.Empty(off.Stirred);

        SimResult home = Mishap(pity: true, deeHousehold: "C");
        Assert.DoesNotContain(home.Acts, a => a.Kind == "HelpedSomeone");
        Assert.Empty(home.Stirred);
    }

    // ---- the first greeting (R13) ---------------------------------------------------------

    private static SimResult Greetings(bool tone)
    {
        FeelingOptions o = FeelingOptions.WithDesire();
        o.ToneOn = tone;
        o.Tone = 0.3; // ten times the town's, so curt and warm greetings both come in a week
        return Run(new[] { V("Ann", "A", 3, 2, understanding: 0), V("Bob", "B", 5, 2) }, o, Array.Empty<(int, string, string)>(), 7);
    }

    /// <summary>R13 (Tone on): a day's first meeting may be taken as curt. Ann (understanding 0)
    /// takes one from Bob as curt: she is a little saddened, cools on Bob by the route "Tone",
    /// and an Answer is stirred toward him (felt 0.1, source -1, alone too weak to act on). The log
    /// names its cause: her own power, understanding and regard. A warm greeting stirs nothing.
    /// With Tone off there are no greetings taken either way.</summary>
    [Fact]
    public void ACurtGreetingStirsAnswer_AndSaysWhy()
    {
        SimResult r = Greetings(tone: true);
        var curt = r.Log.Where(l => l.Contains(" tone curt Ann Bob ")).ToList();
        Assert.NotEmpty(curt);
        foreach (string line in curt)
        {
            int m = int.Parse(line.Split(' ')[0]);
            Assert.Matches(@"^\d+ tone curt Ann Bob because power \d\.\d\d understanding 0\.00 regard [+-]\d\.\d\d$", line);
            Stirring s = Assert.Single(r.Stirred, s => s.Tick == m && s.Holder == "Ann");
            Assert.Equal(("Bob", DesireKind.Answer, -1), (s.Subject, s.Motive, s.Source));
            Assert.Equal(0.1, s.Felt, 12);
            Felt f = Assert.Single(r.Feelings, f => f.Tick == m && f.Holder == "Ann" && f.Route == "Tone");
            Assert.Equal("Bob", f.Toward);
            Assert.True(f.Change < 0);
        }
        // Warm greetings (either way) stir nothing; only curt ones stir.
        var warm = r.Log.Where(l => l.Contains(" tone warm ")).ToList();
        Assert.NotEmpty(warm);
        Assert.All(warm, l => Assert.DoesNotContain(r.Stirred, s => s.Tick == int.Parse(l.Split(' ')[0]) && s.Holder == l.Split(' ')[3]));
        Assert.Equal(r.Log.Count(l => l.Contains(" tone curt ")), r.Stirred.Count(s => s.Source == -1));
        // One curt greeting is too weak to act on (0.1, an argument needs 0.2); three in a week add
        // up, and Ann argues with Bob over them. The argument cites no act.
        Assert.True(curt.Count >= 3);
        Act argue = Assert.Single(r.Acts);
        Assert.Equal(("Argued", "Ann", "Bob", -1), (argue.Kind, argue.Actor, argue.Target, argue.About));
        Assert.True(argue.Tick > int.Parse(curt[2].Split(' ')[0]));
        Pursuit why = Assert.Single(r.Pursuits, p => p.ActId == argue.Id);
        Assert.Equal((DesireKind.Answer, -1), (why.Motive, why.Source));

        SimResult off = Greetings(tone: false);
        Assert.DoesNotContain(off.Log, l => l.Contains(" tone "));
        Assert.Empty(off.Stirred);
        Assert.DoesNotContain(off.Feelings, f => f.Route == "Tone");
    }
}
