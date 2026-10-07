using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// Phase 0d.6 in small scenes (spec section 8): what is measured, hurts at home, households
/// arguing, contagion, being left out and recovering, patience, the coercion ratchet, expression,
/// and missing people. The convention is DesireSceneTests': one room (or two, for someone kept
/// apart), nobody tires, sensitivity and retention 0.5, the scenes' own feeling rows, scheduled
/// acts, and the gate with every added rule off unless a test turns one on.
/// </summary>
public class WithdrawalSceneTests
{
    private const int D = Clock.MinutesPerDay;
    private static readonly int Ten = Clock.At(10);

    internal static Haunt At(int x, int y, int from = 0, int to = D, string place = "Room") => new(place, new Tile(x, y), from, to, 1);

    /// <summary>Someone who spends the whole day at one spot and never tires.</summary>
    internal static Villager V(string name, string household, int x, int y, double bold = 0.5, double chat = 1.0, double self = 0.6,
        Dictionary<string, double>? acts = null, Dictionary<string, Kin>? family = null, string place = "Room")
        => new(name, household, "villager", new Temperament(chat, bold, 0.5, self), new Body(100, -1), null,
            new[] { At(x, y, place: place) }, acts ?? new Dictionary<string, double>(), Array.Empty<string>(), 30, family);

    internal static Location Room(string name = "Room") => new(name, false, Enumerable.Repeat(new string('.', 40), 6).ToList());

    internal static BodyOptions Awake() => new() { AwakeHoursAtRest = 100_000 };

    internal static readonly Affect GiftRow = new(Patient.Target, 0.2, 0.3, 1, TargetIs.Chosen, Tilt: 1);
    internal static readonly Affect ArguedRow = new(Patient.Target, -0.3, 0.3, 1, TargetIs.Chosen);
    internal static readonly Affect HelpRow = new(Patient.Target, 0.3, 0.3, 1, TargetIs.Chosen, Tilt: 1);

    /// <param name="argueJuice">An argument's juiciness: 3 is news, retold; under 2 nobody volunteers it.</param>
    internal static List<ActKind> Kinds(double gift = 0, double argue = 0, double argueJuice = 3.0) => new()
    {
        new("GaveGift", 1.5, 1, 1, 1, gift, Array.Empty<string>(), Affect: GiftRow),
        new("Argued", argueJuice, -1, 2, 10, argue, Array.Empty<string>(), MinAge: 13, Affect: ArguedRow),
        new("HelpedSomeone", 2.0, 1, 2, 5, 0, Array.Empty<string>(), MinAge: 10, Affect: HelpRow),
    };

    internal static Dictionary<string, double> Does(params string[] kinds) => kinds.ToDictionary(k => k, _ => 1.0);

    /// <summary>X0: a gift from someone in another household counts as kindness received and one
    /// from a housemate does not; a day beside someone from another household is a day with
    /// company and a day beside a housemate is not; one's own gift that is never returned is
    /// unanswered, but a gift that returns someone else's is not counted at all; and measuring
    /// changes nothing the town does.</summary>
    [Fact]
    public void WhatIsMeasured()
    {
        // Ann and her housemate Cy share one end of the room with Bob (another house); Dee (another
        // house) stands at the far end, out of company.
        var cast = new[] { V("Ann", "A", 3, 2), V("Cy", "A", 5, 2), V("Bob", "B", 4, 3), V("Dee", "C", 30, 2, bold: 0.0) };
        var scheduled = new List<(int, string, string)> { (Ten, "Bob", "GaveGift"), (Ten + 30, "Cy", "GaveGift") };
        SimResult Run(FeelingOptions o) => new Simulation(1, cast, new[] { Room() }, Kinds(), feelings: o, body: Awake(),
            scheduled: scheduled, wander: 0).Run(10);
        SimResult r = Run(FeelingOptions.WithDesire());

        PersonDays ann = r.Daily["Ann"];
        Act fromBob = Assert.Single(r.Acts, a => a.Actor == "Bob" && a.Kind == "GaveGift");
        Assert.Equal("Ann", fromBob.Target);
        // Cy's gift went to Ann or Bob; only Bob's to Ann counts for her.
        int fromStrangers = r.Acts.Count(a => a.Kind == "GaveGift" && a.Target == "Ann" && a.Actor is "Bob" or "Dee");
        Assert.Equal(fromStrangers, ann.KindIn.Sum());
        Assert.True(ann.Met[0]);
        Assert.DoesNotContain(true, r.Daily["Dee"].Met);
        // Ann returned Bob's gift; that return is never returned again, and is not counted.
        Assert.Contains(r.Acts, a => a.Actor == "Ann" && a.Kind == "GaveGift" && a.Target == "Bob" && a.About == fromBob.Id);
        Assert.Equal(0, ann.KindOut.Sum());
        // Bob's own gift was returned: counted, and answered.
        Assert.Equal((1, 0), (r.Daily["Bob"].KindOut.Sum(), r.Daily["Bob"].Unanswered.Sum()));
        // Free time out is counted: Ann's room is not her home.
        Assert.True(ann.OutMinutes[1] > 20 * 60);
        Assert.All(r.Daily.Values.SelectMany(d => d.LeftOut), e => Assert.InRange(e, 0, 1));
        Assert.Empty(r.Rules);
    }

    /// <summary>X0: one's own kindness to someone outside the household that is never returned is
    /// unanswered once its week is up, and being left out reads it against the town: Ann's gift to
    /// shy Dee goes unanswered while Bob's and Cy's gifts to each other are returned.</summary>
    [Fact]
    public void AKindnessNeverReturnedIsUnanswered()
    {
        var cast = new[] { V("Ann", "A", 3, 2), V("Dee", "C", 4, 2, bold: 0.0), V("Bob", "B", 20, 2), V("Cy", "D", 21, 2) };
        SimResult r = new Simulation(1, cast, new[] { Room() }, Kinds(), feelings: FeelingOptions.WithDesire(), body: Awake(),
            scheduled: new[] { (Ten, "Ann", "GaveGift"), (Ten, "Bob", "GaveGift"), (Ten + 60, "Cy", "GaveGift") }, wander: 0).Run(10);

        Assert.Empty(r.Acts.Where(a => a.Actor == "Dee"));
        PersonDays ann = r.Daily["Ann"];
        Assert.Equal((1, 1), (ann.KindOut.Sum(), ann.Unanswered.Sum()));
        Assert.Equal((0, 0), (r.Daily["Bob"].Unanswered.Sum(), r.Daily["Cy"].Unanswered.Sum()));
        Assert.Equal(1, r.Daily["Bob"].KindOut.Sum());
        int day = Array.IndexOf(ann.Unanswered, 1);
        Assert.InRange(day, 7, 8);
        // The town's median share is 0 (Bob 0, Cy 0, Ann 1): Ann's part is the whole 0.3.
        Assert.True(ann.LeftOut[day] >= 0.3 - 1e-12, $"{ann.LeftOut[day]}");
        Assert.True(ann.LeftOut[day] > r.Daily["Bob"].LeftOut[day]);
    }

    // ---- step b: hurts at home (X1), households argue (X2) --------------------------------------

    /// <summary>The gate with stance on and the given 0d.6 steps.</summary>
    internal static FeelingOptions Steps(string steps, params (string From, string To, double Regard)[] start)
    {
        FeelingOptions o = FeelingOptions.WithDesire(start);
        o.StanceOn = true;
        return o.With0d6(steps);
    }

    /// <summary>Bo argues at his housemate Ann on days 0, 3, 6 and 9 (the hostile cooldown is three days).</summary>
    private static SimResult Rows(FeelingOptions o, double annBold, int days = 11, double boRegardStart = 0.6)
    {
        var cast = new[] { V("Ann", "H", 3, 2, bold: annBold), V("Bo", "H", 5, 2, bold: 0.5, acts: Does("Argued")) };
        o.Start = new Dictionary<(string, string), double> { [("Ann", "Bo")] = boRegardStart };
        return new Simulation(1, cast, new[] { Room() }, Kinds(), feelings: o, body: Awake(),
            scheduled: new[] { 0, 3, 6, 9 }.Select(d => (d * D + Ten, "Bo", "Argued")).ToList(), wander: 0).Run(days);
    }

    /// <summary>Scene 10: rows at home move a shy person's stance toward withdrawn with hurts at
    /// home on, each by more than the last (repetition, up to double), and stir no motive; with it
    /// off they move nothing, as before.</summary>
    [Fact]
    public void RowsAtHomeWithdrawTheShy()
    {
        SimResult on = Rows(Steps("b"), annBold: 0.1);
        SimResult off = Rows(Steps(""), annBold: 0.1);

        Assert.Equal(4, on.Acts.Count(a => a is { Actor: "Bo", Kind: "Argued", Target: "Ann" }));
        Assert.All(off.Stances["Ann"], s => Assert.Equal(0, s));
        double[] st = on.Stances["Ann"];
        Assert.True(st[10] < -0.4, $"stance at day 10 {st[10]:0.000}");
        // Each row's drop (from the night before to the day of the row) is larger than the last.
        double[] drops = new[] { 0, 3, 6, 9 }.Select(d => (d == 0 ? 0 : st[d - 1] * 0.975) - st[d]).ToArray();
        Assert.True(drops.Zip(drops.Skip(1)).All(p => p.Second > p.First), string.Join(" ", drops.Select(x => x.ToString("0.000"))));
        Assert.True(drops[3] <= 2 * drops[0] + 1e-9);
        Assert.Empty(on.Stirred.Where(s => s.Holder == "Ann"));
        Assert.Equal(4, on.Rules["hurt at home"].Count);
        // The bold grow combative at home instead (Patterson's coercive family process).
        Assert.True(Rows(Steps("b"), annBold: 0.9).Stances["Ann"][10] > 0.3);
    }

    /// <summary>Scene 15: households argue. Ann, bold, answers her housemate's argument when she
    /// holds him below HomeCoverAt; at 0.6 love covers and she never does; with the switch off,
    /// families cover as before. Nobody keeps away at home.</summary>
    [Fact]
    public void HouseholdsArgueWhenRegardIsBad()
    {
        SimResult bad = Rows(Steps("b"), annBold: 0.9, days: 2, boRegardStart: 0.1);
        SimResult good = Rows(Steps("b"), annBold: 0.9, days: 2, boRegardStart: 0.6);
        SimResult off = Rows(Steps(""), annBold: 0.9, days: 2, boRegardStart: 0.1);

        Act answer = Assert.Single(bad.Acts, a => a is { Actor: "Ann", Kind: "Argued" });
        Assert.Equal(("Bo", 0), (answer.Target, answer.About));
        Assert.Contains(answer.Id, bad.Pursued);
        Assert.Contains(bad.Stirred, s => s is { Holder: "Ann", Subject: "Bo", Motive: DesireKind.Answer });
        Assert.Empty(good.Acts.Where(a => a.Actor == "Ann"));
        Assert.Empty(good.Stirred.Where(s => s.Holder == "Ann"));
        Assert.Empty(off.Acts.Where(a => a.Actor == "Ann"));
        Assert.Empty(off.Stirred);
        Assert.Empty(bad.Avoids);
    }

    // ---- step c: moods spread (X3) -------------------------------------------------------------

    /// <summary>Someone in the yard until noon and in the room after it.</summary>
    private static Villager TwoRooms(string name, string household, int x, int y, double bold = 0.5)
        => new(name, household, "villager", new Temperament(1.0, bold, 0.5, 0.6), new Body(100, -1), null,
            new[] { At(x, y, 0, Clock.At(12), "Yard"), At(x, y, Clock.At(12), D, "Room") },
            new Dictionary<string, double>(), Array.Empty<string>());

    /// <summary>Contagion's scene: in the morning, in the yard, Rae argues at Pam (or gives her a
    /// gift) with Penny there or not; from noon Pam and Penny share the room. The row is a quiet one
    /// (juiciness 1, never volunteered), so Pam does not tell Penny about it: told, Penny would feel
    /// her own share of it, and contagion would not pass Pam's on again. Penny's mood at the day's
    /// end, what she took by contagion, and the run.</summary>
    private static (double Mood, double Caught, SimResult R) Spread(bool contagion, string act = "Argued", bool pennySees = false,
        bool housemates = true, double pennyForPam = 0.6, double k = 0.05)
    {
        FeelingOptions o = Steps(contagion ? "c" : "", ("Penny", "Pam", pennyForPam));
        o.ContagionK = k;
        o.ContagionCap = 1;
        // Rae spends the afternoon at the far end of the room, out of Penny's company.
        Villager rae = TwoRooms("Rae", "R", 5, 2) with { Haunts = new[] { At(5, 2, 0, Clock.At(12), "Yard"), At(36, 5, Clock.At(12), D, "Room") } };
        var cast = new[]
        {
            TwoRooms("Pam", "T", 3, 2), rae,
            pennySees ? TwoRooms("Penny", housemates ? "T" : "P", 4, 3)
                : new Villager("Penny", housemates ? "T" : "P", "villager", new Temperament(1.0, 0.5, 0.5, 0.6), new Body(100, -1), null,
                    new[] { At(30, 2, 0, Clock.At(12), "Room"), At(4, 3, Clock.At(12), D, "Room") }, new Dictionary<string, double>(), Array.Empty<string>()),
        };
        var sim = new Simulation(1, cast, new[] { Room(), Room("Yard") }, Kinds(argueJuice: 1.0), feelings: o, body: Awake(),
            scheduled: new[] { (Ten, "Rae", act) }, wander: 0);
        SimResult r = sim.Run(1);
        return (sim.Mood("Penny"), r.Contagion["Penny"].Caught, r);
    }

    /// <summary>Scene 1: a sad housemate brings Penny down with contagion on, and not with it off.</summary>
    [Fact]
    public void ASadHousemateBringsYouDown()
    {
        var on = Spread(contagion: true);
        var off = Spread(contagion: false);
        Assert.True(on.R.Acts.Any(a => a is { Actor: "Rae", Kind: "Argued", Target: "Pam" }));
        Assert.Equal(0, off.Caught);
        Assert.True(on.Caught > 0);
        Assert.True(on.Mood < off.Mood - 0.005, $"{on.Mood:0.0000} against {off.Mood:0.0000}");
    }

    /// <summary>Scene 2: a glad friend lifts her, at friend strength.</summary>
    [Fact]
    public void AGladFriendLifts()
    {
        var on = Spread(contagion: true, act: "GaveGift", housemates: false, pennyForPam: 0.5);
        var off = Spread(contagion: false, act: "GaveGift", housemates: false, pennyForPam: 0.5);
        Assert.True(on.R.Acts.Any(a => a is { Actor: "Rae", Kind: "GaveGift", Target: "Pam" }));
        Assert.True(on.Mood > off.Mood + 0.001, $"{on.Mood:0.0000} against {off.Mood:0.0000}");
    }

    /// <summary>Scene 3: equal moods pass nothing, and nothing passes without a chat.</summary>
    [Fact]
    public void EqualMoodsAndNoChatPassNothing()
    {
        var cast = new[] { V("Ann", "A", 3, 2), V("Bob", "B", 5, 2) };
        SimResult Run(double chat) => new Simulation(1, cast, new[] { Room() }, Kinds(), feelings: Steps("c"), body: Awake(),
            gossip: new GossipOptions { ChatChance = chat }, wander: 0).Run(3);
        SimResult equal = Run(0.3);
        Assert.False(equal.Rules.ContainsKey("contagion"));
        Assert.True(equal.Feelings.Count == 0); // no act was felt: their moods are company alone, and equal
        var withAct = new Simulation(1, cast, new[] { Room() }, Kinds(), feelings: Steps("c"), body: Awake(),
            gossip: new GossipOptions { ChatChance = 0 }, scheduled: new[] { (Ten, "Ann", "Argued") }, wander: 0).Run(3);
        Assert.False(withAct.Rules.ContainsKey("contagion"));
    }

    /// <summary>Scene 4: at home, love softens the pull down: Penny holding Pam at 0.8 takes 0.6 of
    /// what she takes holding her at 0.</summary>
    [Fact]
    public void LoveSoftensTheBadAtHome()
    {
        double loved = Spread(contagion: true, pennyForPam: 0.8, k: 0.01).Caught;
        double not = Spread(contagion: true, pennyForPam: 0.0, k: 0.01).Caught;
        Assert.True(not > 0);
        Assert.InRange(loved / not, 0.5, 0.7);
    }

    /// <summary>Scene 5: when Penny saw the argument too, what she and Pam felt about it is not
    /// passed between them again; only what the other felt alone is.</summary>
    [Fact]
    public void ASharedActIsNotFeltTwice()
    {
        var shared = Spread(contagion: true, pennySees: true);
        var apart = Spread(contagion: true, pennySees: false);
        Assert.Contains(shared.R.Feelings, f => f.Holder == "Penny" && f.ActId == 0 && f.Mood != 0); // she felt it herself
        Assert.True(shared.Caught < apart.Caught / 5, $"{shared.Caught:0.0000} against {apart.Caught:0.0000}");
    }

    /// <summary>Scene 6: five people in a low mood chatting with one person all day give her no
    /// more than the day's cap.</summary>
    [Fact]
    public void TheCapHolds()
    {
        var names = new[] { "Ada", "Bea", "Cal", "Dot", "Eve" };
        var cast = names.Select((n, i) => TwoRooms(n, "L" + n, 3 + i, 2)).Append(TwoRooms("Rae", "R", 20, 2))
            .Append(new Villager("Penny", "P", "villager", new Temperament(1.0, 0.5, 0.5, 0.6), new Body(100, -1), null,
                new[] { At(30, 2, 0, Clock.At(12), "Room"), At(5, 3, Clock.At(12), D, "Room") }, new Dictionary<string, double>(), Array.Empty<string>()))
            .ToArray();
        // Rae walks along the line arguing at each in turn (each argument lasts 10 minutes).
        var rae = cast.Single(v => v.Name == "Rae") with
        {
            Haunts = names.Select((_, i) => At(3 + i, 3, Clock.At(8) + i * 60, Clock.At(8) + (i + 1) * 60, "Yard")).ToArray(),
        };
        cast = cast.Select(v => v.Name == "Rae" ? rae : v).ToArray();
        FeelingOptions o = Steps("c");
        o.ContagionK = 0.5;
        SimResult r = new Simulation(1, cast, new[] { Room(), Room("Yard") }, Kinds(), feelings: o, body: Awake(),
            scheduled: names.Select((n, i) => (Clock.At(8) + i * 60 + 20, "Rae", "Argued")).ToList(), wander: 0).Run(1);
        Assert.True(r.Acts.Count(a => a is { Actor: "Rae", Kind: "Argued" }) >= 3);
        Assert.True(r.Contagion["Penny"].Caught > 0.01);
        Assert.InRange(r.Contagion["Penny"].Net, -o.ContagionCap - 1e-12, o.ContagionCap + 1e-12);
        Assert.True(r.Contagion["Penny"].Net < -0.04); // the cap binds
    }

    // ---- step d: left out (X4, X5) --------------------------------------------------------------

    private static readonly string[] Others = { "Bob", "Cy", "Dee", "Eve" };

    /// <summary>The left-out scene: Bob, Cy, Dee and Eve (four households) spend their days in the
    /// room giving each other gifts; Ann spends hers alone in the far room. Others may visit.</summary>
    private static SimResult LeftOut(FeelingOptions o, double annBold = 0.02, double annSens = 0.5, double annChat = 1.0, int days = 84,
        IEnumerable<Villager>? more = null, IEnumerable<(int, string, string)>? scheduled = null)
    {
        var cast = new List<Villager>
        {
            new("Ann", "A", "villager", new Temperament(annChat, annBold, 0.5, 0.6, annSens), new Body(100, -1), null,
                new[] { At(3, 2, place: "Far") }, new Dictionary<string, double>(), Array.Empty<string>()),
        };
        cast.AddRange(Others.Select((n, i) => V(n, n, 3 + 2 * i, 2, acts: Does("GaveGift"))));
        if (more is not null)
            cast.AddRange(more);
        return new Simulation(1, cast, new[] { Room(), Room("Far") }, Kinds(gift: 2.0), feelings: o, body: Awake(),
            scheduled: scheduled?.ToList(), wander: 0).Run(days);
    }

    /// <summary>Step d with a test's own rate (0.04 a day: the town's is tuned in section 12).</summary>
    private static FeelingOptions LeftOutOptions(string steps = "d", params (string From, string To, double Regard)[] start)
    {
        FeelingOptions o = Steps(steps, start);
        o.LeftOutRate = 0.04;
        return o;
    }

    /// <summary>Scene 7: the shy who are left out withdraw: Ann, boldness 0.02, with no kindness
    /// and no company while the others have both, is withdrawn for 28 days and more. The others,
    /// included, are not.</summary>
    [Fact]
    public void TheShyLeftOutWithdraw()
    {
        SimResult r = LeftOut(LeftOutOptions());
        Assert.True(r.Daily["Ann"].LeftOut.Skip(28).All(e => e > 0.6), "Ann is left out");
        Spell s = Assert.Single(WithdrawalMetrics.Spells(r, brawlers: false));
        Assert.Equal("Ann", s.Name);
        Assert.True(s.To - s.From + 1 >= 28);
        Assert.All(Others, n => Assert.True(r.Stances[n].Min() > -0.1, n));
        Assert.True(r.Rules["left out"].Count > 0);
        // Off, nothing moves her.
        Assert.All(LeftOut(Steps("")).Stances["Ann"], st => Assert.Equal(0, st));
    }

    /// <summary>Scene 9: the bold who are left out barely move (shy squared: 0.04 of the push at 0.8).</summary>
    [Fact]
    public void TheBoldLeftOutBarelyMove()
    {
        SimResult r = LeftOut(LeftOutOptions(), annBold: 0.8);
        Assert.True(r.Stances["Ann"][^1] > -0.1, $"{r.Stances["Ann"][^1]:0.000}");
        Assert.True(r.Stances["Ann"][^1] < 0);
    }

    /// <summary>Scene 8 (invariant; step d's criterion): a shy person who is not left out, in the
    /// room with the others and giving and getting kindness as they do, is never withdrawn, on any
    /// of 20 seeds, even at a stronger rate than scene 7's. (Being left out is relative to the
    /// town: a weekly gift and a weekly day together, where everyone else has both daily, still
    /// counts as left out, and then only a friend protects.)</summary>
    [Fact]
    public void TheShyIncludedDoNot()
    {
        for (long seed = 1; seed <= 20; seed++)
        {
            var cast = new List<Villager> { V("Ann", "A", 11, 2, bold: 0.02, acts: Does("GaveGift")) };
            cast.AddRange(Others.Select((n, i) => V(n, n, 3 + 2 * i, 2, acts: Does("GaveGift"))));
            FeelingOptions o = LeftOutOptions();
            o.LeftOutRate = 0.08;
            SimResult r = new Simulation(seed, cast, new[] { Room() }, Kinds(gift: 2.0), feelings: o, body: Awake(), wander: 0).Run(84);
            Assert.True(r.Daily["Ann"].KindIn.Sum() >= 10, $"seed {seed}");
            Assert.True(r.Daily["Ann"].LeftOut.Average() < 0.2, $"seed {seed}: {r.Daily["Ann"].LeftOut.Average():0.00}");
            Assert.Empty(WithdrawalMetrics.Spells(r, brawlers: false));
        }
    }

    /// <summary>Scene 11: scene 7 plus a friend seen weekly: Fay, whom Ann holds at 0.6, works in
    /// the far room on Mondays. Ann's stance stays above -0.3, and well above scene 7's.</summary>
    [Fact]
    public void AFriendBuffers()
    {
        var fay = new Villager("Fay", "F", "villager", new Temperament(1.0, 0.5, 0.5, 0.6), new Body(100, -1),
            new Job("Far", new Tile(5, 2), Clock.At(9), Clock.At(17), new[] { 1, 2, 3, 4, 5, 6 }, 1.0),
            new[] { At(30, 2) }, new Dictionary<string, double>(), Array.Empty<string>());
        SimResult buffered = LeftOut(LeftOutOptions("d", ("Ann", "Fay", 0.6)), more: new[] { fay });
        SimResult alone = LeftOut(LeftOutOptions());
        Assert.True(buffered.Stances["Ann"].Min() > -0.3, $"{buffered.Stances["Ann"].Min():0.000}");
        Assert.True(buffered.Stances["Ann"].Min() > alone.Stances["Ann"].Min() / 2);
    }

    /// <summary>Scene 12: a stranger's gift to someone withdrawn eases InclusionShare (0.3) of what
    /// a friend's gift eases. Gus looks in on Ann for half an hour each morning (not a day of
    /// company) and gives her a gift on day 56; the two runs differ only in how Ann holds him.</summary>
    [Fact]
    public void AStrangersGiftIsNotEnough()
    {
        var gus = new Villager("Gus", "G", "villager", new Temperament(1.0, 0.5, 0.5, 0.6), new Body(100, -1), null,
            new[] { At(30, 2, 0, Clock.At(10)), At(4, 3, Clock.At(10), Clock.At(10, 30), "Far"), At(30, 2, Clock.At(10, 30), D) },
            new Dictionary<string, double>(), Array.Empty<string>());
        var gift = new[] { (56 * D + Clock.At(10, 5), "Gus", "GaveGift") };
        SimResult none = LeftOut(LeftOutOptions(), more: new[] { gus }, days: 57);
        SimResult stranger = LeftOut(LeftOutOptions(), more: new[] { gus }, scheduled: gift, days: 57);
        SimResult friend = LeftOut(LeftOutOptions("d", ("Ann", "Gus", 0.6)), more: new[] { gus }, scheduled: gift, days: 57);
        Assert.Single(stranger.Acts, a => a is { Actor: "Gus", Kind: "GaveGift", Target: "Ann" });
        Assert.True(none.Stances["Ann"][55] < -0.3, "she is withdrawn by then");
        Assert.Equal(none.Stances["Ann"][55], friend.Stances["Ann"][55]);
        double easedByFriend = friend.Stances["Ann"][56] - none.Stances["Ann"][56];
        double easedByStranger = stranger.Stances["Ann"][56] - none.Stances["Ann"][56];
        Assert.True(easedByFriend > 0);
        Assert.InRange(easedByStranger / easedByFriend, 0.25, 0.35);
        Assert.Equal(1, stranger.Rules["inclusion discounted"].Count);
        Assert.False(friend.Rules.ContainsKey("inclusion discounted"));
    }

    /// <summary>Scene 13, its first half (the second is pure: the included pull is x Sens): high
    /// sensitivity withdraws faster when left out.</summary>
    [Fact]
    public void SensitivityCutsBothWays()
    {
        double high = LeftOut(LeftOutOptions(), annSens: 0.9, days: 21).Stances["Ann"][20];
        double low = LeftOut(LeftOutOptions(), annSens: 0.1, days: 21).Stances["Ann"][20];
        Assert.True(high < low * 2, $"{high:0.000} against {low:0.000}");
    }

    /// <summary>Scene 21 (Sid's answer 3): a bold, quiet person alone all day is a content loner, less
    /// left out than a shy one alone all day.</summary>
    [Fact]
    public void AContentLonerIsNotLeftOut()
    {
        var bea = new Villager("Bea", "Z", "villager", new Temperament(0.2, 0.1, 0.5, 0.6), new Body(100, -1), null,
            new[] { At(30, 2, place: "Far") }, new Dictionary<string, double>(), Array.Empty<string>());
        SimResult r = LeftOut(LeftOutOptions(), annBold: 0.7, annChat: 0.2, more: new[] { bea }, days: 30);
        Assert.Equal(0.4, r.Daily["Ann"].LeftOut[29], 9);   // no kindness; time alone does not count
        Assert.Equal(0.7, r.Daily["Bea"].LeftOut[29], 9);
    }

    // ---- step e: the dials (X6) -----------------------------------------------------------------

    /// <summary>Scene 7 with a home for Ann (her free time goes to the far room or home): withdrawn,
    /// she stays in. With the dials her free hours out fall by more than a quarter from her first
    /// season to her third, and by more than without them; and the hermit measure sees it.</summary>
    [Fact]
    public void TheWithdrawnStayIn()
    {
        SimResult Run(string steps)
        {
            var cast = new List<Villager>
            {
                new("Ann", "A", "villager", new Temperament(1.0, 0.02, 0.5, 0.6), new Body(100, -1), null,
                    new[] { At(3, 2, place: "Far") }, new Dictionary<string, double>(), Array.Empty<string>()),
            };
            cast.AddRange(Others.Select((n, i) => V(n, n, 3 + 2 * i, 2, acts: Does("GaveGift"))));
            return new Simulation(1, cast, new[] { Room(), Room("Far"), Room("Home:A") }, Kinds(gift: 2.0), feelings: LeftOutOptions(steps),
                body: Awake(), wander: 0).Run(84);
        }
        double Fall(SimResult r) => 1 - r.Daily["Ann"].OutMinutes.Skip(56).Average() / r.Daily["Ann"].OutMinutes.Take(28).Average();
        SimResult dials = Run("de"), plain = Run("d");
        Assert.True(Fall(dials) >= 0.25, $"{Fall(dials):P0}");
        Assert.True(Fall(dials) > Fall(plain), $"{Fall(dials):P0} against {Fall(plain):P0}");
        Assert.Contains(WithdrawalMetrics.Spells(dials, brawlers: false), s => s.Name == "Ann" && s.Hermit);
        Assert.True(dials.Rules["dial: home"].Count > 0);
    }

    // ---- step f: recovery (X7) ------------------------------------------------------------------

    /// <summary>Scene 18: with recovery on, being left out starts again each season: E is 0 in a
    /// season's first week (the run's first too), and back once the week is past.</summary>
    [Fact]
    public void AFreshStartEachSeason()
    {
        double[] e = LeftOut(LeftOutOptions("df"), days: 40).Daily["Ann"].LeftOut;
        Assert.All(e.Take(6), x => Assert.Equal(0, x));
        Assert.All(e.Skip(28).Take(6), x => Assert.Equal(0, x));
        Assert.True(e[27] > 0.6 && e[35] > 0.6, $"{e[27]:0.00} {e[35]:0.00}");
        double[] plain = LeftOut(LeftOutOptions("d"), days: 40).Daily["Ann"].LeftOut;
        Assert.True(plain[30] > 0.6);
    }

    /// <summary>X7: one's own kindness returned eases a stance, in full (being understood). Ann, shy,
    /// is rowed with at home on days 0, 3 and 6, then gives Bob a gift on day 8, which he returns.</summary>
    [Fact]
    public void AKindnessReturnedEases()
    {
        SimResult Run(string steps)
        {
            // Bo comes by only for his rows (9:50 to 10:40); Ann gives her gift at noon, with only Bob in reach.
            // Bob stands out of Bo's reach (people argue less with those they love, so Bo would pick him).
            Villager bo = V("Bo", "H", 7, 2) with { Haunts = new[] { At(30, 2, 0, Clock.At(9, 50)), At(7, 2, Clock.At(9, 50), Clock.At(10, 40)), At(30, 2, Clock.At(10, 40), D) } };
            var cast = new[] { V("Ann", "H", 3, 2, bold: 0.1), bo, V("Bob", "B", 0, 5) };
            return new Simulation(1, cast, new[] { Room() }, Kinds(), feelings: Steps(steps), body: Awake(),
                scheduled: new[] { 0, 3, 6 }.Select(d => (d * D + Ten, "Bo", "Argued")).Append((8 * D + Clock.At(12), "Ann", "GaveGift")).ToList(),
                wander: 0).Run(9);
        }
        SimResult with = Run("bf"), without = Run("b");
        Act gift = Assert.Single(with.Acts, a => a is { Actor: "Ann", Kind: "GaveGift" });
        Assert.Contains(with.Acts, a => a.Actor == "Bob" && a.Kind == "GaveGift" && a.About == gift.Id);
        Assert.Equal(without.Stances["Ann"][7], with.Stances["Ann"][7]); // retention 0.5 keeps 0.975, so the same until then
        Assert.True(without.Stances["Ann"][7] < -0.2, string.Join(" ", without.Stances["Ann"].Select(x => x.ToString("0.000"))) + " acts " + string.Join(",", without.Acts.Select(a => $"{a.Actor}>{a.Target}:{a.Kind}@d{Clock.Day(a.Tick)}")));
        Assert.True(with.Stances["Ann"][8] > without.Stances["Ann"][8] + 0.02, $"{with.Stances["Ann"][8]:0.000} against {without.Stances["Ann"][8]:0.000}");
        Assert.Equal(1, with.Rules["recovery: answered"].Count);
    }

    // ---- step g: patience (X8), the coercion ratchet (X9) ---------------------------------------

    /// <summary>Scene 16: patience with someone combative lasts a couple of rounds. Bob argues at
    /// Ann on days 0, 3 and 6 (she still loves him, so each argument stirs a wish to make up, a
    /// gift: margin about 0.25, less what her own stance takes off). After the first two she makes up; after the third her patience
    /// (2.5 rounds, less refills) has run out, her kind daring falls by most of PatienceDrop, and
    /// the close call says no. Without patience she makes up all three times.</summary>
    [Fact]
    public void PatienceRunsOut()
    {
        SimResult Run(string steps)
        {
            // They meet only for the half hour of each argument, so familiarity barely grows.
            var bob = new Villager("Bob", "B", "villager", new Temperament(1.0, 0.9, 0.5, 0.6), new Body(100, -1), null,
                new[] { At(30, 2, 0, Clock.At(9, 50)), At(4, 2, Clock.At(9, 50), Clock.At(10, 40)), At(30, 2, Clock.At(10, 40), D) },
                new Dictionary<string, double>(), Array.Empty<string>());
            FeelingOptions o = Steps(steps, ("Ann", "Bob", 0.8));
            o.CloseCall = _ => false;
            return new Simulation(1, new[] { V("Ann", "A", 3, 2, bold: 0.42), bob }, new[] { Room() }, Kinds(), feelings: o, body: Awake(),
                scheduled: new[] { 0, 3, 6 }.Select(d => (d * D + Ten, "Bob", "Argued")).ToList(), wander: 0).Run(7);
        }
        int[] MadeUp(SimResult r) => r.Acts.Where(a => a is { Actor: "Ann", Kind: "GaveGift" }).Select(a => Clock.Day(a.Tick)).ToArray();
        SimResult patient = Run("g"), plain = Run("");
        Assert.Equal(3, plain.Acts.Count(a => a is { Actor: "Bob", Kind: "Argued" }));
        Assert.Equal(new[] { 0, 3, 6 }, MadeUp(plain));
        string why = string.Join(" | ", patient.Pursuits.Where(p => p.Holder == "Ann" && Clock.Day(p.Tick) >= 3)
            .Select(p => $"{Clock.Format(p.Tick)} {p.Motive} {p.ActKind} I {p.Intensity:0.000} eff {p.Effective:0.000} cost {p.Cost:0.000} margin {p.Margin:+0.000;-0.000} {p.Call}").Take(12));
        Assert.True(new[] { 0, 3 }.SequenceEqual(MadeUp(patient)), $"{string.Join(",", MadeUp(patient))} :: {why}");
        Assert.True(patient.Rules["impatience"].Sum > 0.2);
    }

    /// <summary>Scene 14 (B2): an argument met by keeping away makes the arguer bolder. Bold Ann
    /// argues at shy Bob, who cannot answer and keeps away; her stance rises with coercion on.</summary>
    [Fact]
    public void WinningMakesBolder()
    {
        SimResult Run(string steps)
        {
            var cast = new[] { V("Ann", "A", 3, 2, bold: 0.95), V("Bob", "B", 5, 2, bold: 0.05) };
            return new Simulation(1, cast, new[] { Room() }, Kinds(), feelings: Steps(steps), body: Awake(),
                scheduled: new[] { (Ten, "Ann", "Argued") }, wander: 0).Run(2);
        }
        SimResult won = Run("g"), plain = Run("");
        Assert.Single(won.Avoids);
        Assert.Equal(0, plain.Stances["Ann"][1]);
        Assert.True(won.Stances["Ann"][0] > 0.01, $"{won.Stances["Ann"][0]:0.000}");
        Assert.Equal(1, won.Rules["coerced"].Count);
    }

    // ---- step h: expression (X10) ---------------------------------------------------------------

    /// <summary>Scene 17: the same hurt withdraws a masker more and passes on less of her mood than
    /// someone who lets it out. In the yard in the morning, Rae has a quiet row with Ann (expression
    /// 0.25) and Sal with Bea (0.85), far apart; from noon Ann sits by Cy and Bea by Dot, in the
    /// room, where neither listener saw the row. Both are of middling boldness, so what shows moves
    /// neither stance; what is held pulls Ann toward withdrawal.</summary>
    [Fact]
    public void PennyHoldsWhatPamLetsOut()
    {
        Villager Shows(string name, string household, int x, double expression) => new(name, household, "villager",
            new Temperament(1.0, 0.5, 0.5, 0.6, 0.5, 0.5, expression), new Body(100, -1), null,
            new[] { At(x, 2, 0, Clock.At(12), "Yard"), At(x, 2, Clock.At(12), D) }, new Dictionary<string, double>(), Array.Empty<string>());
        Villager Rows(string name, int x) => V(name, name, x, 3) with
        {
            Haunts = new[] { At(x, 3, 0, Clock.At(12), "Yard"), At(x, 3, Clock.At(12), D, "Away") },
        };
        var cast = new[]
        {
            Shows("Ann", "A", 3, 0.25), Rows("Rae", 4), V("Cy", "C", 5, 2),
            Shows("Bea", "B", 30, 0.85), Rows("Sal", 31), V("Dot", "D", 32, 2),
        };
        // Company joy off, so the only mood in the room is the row's: a masker also shows less of her
        // everyday gladness (Gross and John), which would pull a glad listener toward neutral all day.
        SimResult Run(string steps)
        {
            FeelingOptions o = Steps(steps);
            o.CompanyJoy = 0;
            return new Simulation(1, cast, new[] { Room(), Room("Yard"), Room("Away") }, Kinds(argueJuice: 1.0), feelings: o, body: Awake(),
                scheduled: new[] { (Ten, "Rae", "Argued"), (Ten, "Sal", "Argued") }, wander: 0).Run(1);
        }
        SimResult r = Run("ch");
        Assert.Contains(r.Acts, a => a is { Actor: "Rae", Kind: "Argued", Target: "Ann" });
        Assert.Contains(r.Acts, a => a is { Actor: "Sal", Kind: "Argued", Target: "Bea" });
        Assert.True(r.Stances["Ann"][0] < r.Stances["Bea"][0] - 0.03, $"{r.Stances["Ann"][0]:0.000} against {r.Stances["Bea"][0]:0.000}");
        // What the listener beside each took: less from the masker.
        Assert.True(r.Contagion["Cy"].Net > r.Contagion["Dot"].Net, $"Cy {r.Contagion["Cy"].Net:0.0000} against Dot {r.Contagion["Dot"].Net:0.0000}");
        Assert.True(r.Contagion["Dot"].Net < 0);
        // Without expression the two are alike (stance moves only by boldness, 0.5 here).
        SimResult plain = Run("c");
        Assert.Equal(plain.Stances["Ann"][0], plain.Stances["Bea"][0], 9);
    }

    // ---- missing people (X12) -------------------------------------------------------------------

    /// <summary>Scene 19: a steady pair (together daily) misses nothing, so Ann gives Bob a gift on
    /// his birthday (day 5), not from days apart; and a friend not seen for three weeks is missed.</summary>
    [Fact]
    public void SteadyFriendsGiveOnBirthdays()
    {
        SimResult Run(string steps, int days, bool apart)
        {
            Villager bob = V("Bob", "B", 5, 2) with { Birthday = new YearDay(0, 6) };
            if (apart) // Bob is away until day 20
                bob = bob with { Haunts = new[] { At(4, 3, 0, D, "Away") } };
            FeelingOptions o = Steps(steps, ("Ann", "Bob", 0.7), ("Bob", "Ann", 0.7));
            o.FondOn = true;
            var sim = new Simulation(1, new[] { V("Ann", "A", 3, 2), bob }, new[] { Room(), Room("Away") }, Kinds(), feelings: o, body: Awake(), wander: 0);
            return sim.Run(days);
        }
        SimResult steady = Run("m", 10, apart: false);
        var fond = steady.Pursuits.Where(p => p is { Holder: "Ann", Motive: DesireKind.Fond, Acted: true }).ToList();
        Assert.NotEmpty(fond);
        Assert.All(fond, p => Assert.Equal(5, Clock.Day(p.Tick)));
        Assert.Empty(Run("", 10, apart: false).Pursuits.Where(p => p is { Holder: "Ann", Motive: DesireKind.Fond, Acted: true }));
        Assert.True(steady.Rules["missing: occasion"].Count > 0);
    }

    /// <summary>X12: hedonic adaptation: Bob gives Ann a gift every eight days (outside rule 4's week);
    /// with missing people on, each is felt less than the one before while they keep coming within
    /// four weeks; without it, each is felt the same.</summary>
    [Fact]
    public void RepeatedGiftsFeelLess()
    {
        double[] Felt(string steps)
        {
            var cast = new[] { V("Ann", "A", 3, 2, bold: 0.0), V("Bob", "B", 5, 2) };
            SimResult r = new Simulation(1, cast, new[] { Room() }, Kinds(), feelings: Steps(steps), body: Awake(),
                scheduled: Enumerable.Range(0, 5).Select(k => (k * 8 * D + Ten, "Bob", "GaveGift")).ToList(), wander: 0).Run(34);
            var gifts = r.Acts.Where(a => a is { Actor: "Bob", Kind: "GaveGift", Target: "Ann" }).Select(a => a.Id).ToList();
            Assert.Equal(5, gifts.Count);
            return gifts.Select(id => r.Feelings.Where(f => f.Holder == "Ann" && f.ActId == id).Sum(f => f.Mood)).ToArray();
        }
        double[] adapted = Felt("m"), plain = Felt("");
        Assert.All(plain, x => Assert.Equal(plain[0], x, 9));
        Assert.Equal(plain[0], adapted[0], 9);
        Assert.Equal(plain[0] * 0.85, adapted[1], 9);
        Assert.Equal(plain[0] * 0.85 * 0.85, adapted[2], 9);
        Assert.Equal(plain[0] * 0.85 * 0.85 * 0.85, adapted[3], 9);
        Assert.Equal(adapted[3], adapted[4], 9); // the first has left the four weeks
    }
}
