using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// The desire gate in small scenes (phase 0d, rule 10; D13-D20, D27, D30-D32): the hurt answer
/// back, a pair cools down between arguments, a kindness is returned once, a declined help falls
/// back to a gift (story 2), families cover, love does not confront, a close call is asked once
/// until the motive moves, the shy avoid (story 4), character is read when the gate weighs,
/// a changed belief drops a motive, hearsay stirs nothing except in the robbed keeper, and someone
/// argued with over their own theft gave cause. The scene convention is SteeringTests': one long
/// room, nobody wanders or tires, sensitivity and retention 0.5 (felt at the row's size), the
/// scenes' own rows, scheduled acts at 10:00 on day 0, and <see cref="FeelingOptions.WithDesire"/>
/// (power weight 0, every added rule off) unless a test says otherwise.
/// </summary>
public class DesireSceneTests
{
    private const int D = Clock.MinutesPerDay;
    private static readonly int Ten = Clock.At(10);

    private static Haunt At(int x, int y, int from = 0, int to = D) => new("Room", new Tile(x, y), from, to, 1);

    /// <summary>Someone who spends the whole day at one spot in the room and never tires.</summary>
    private static Villager V(string name, string household, int x, int y, double bold = 0.5,
        Dictionary<string, double>? acts = null, Dictionary<string, Kin>? family = null)
        => Moving(name, household, new[] { At(x, y) }, bold, acts: acts, family: family);

    /// <summary>Someone whose day moves between spots at set hours.</summary>
    private static Villager Moving(string name, string household, Haunt[] haunts, double bold = 0.5, double self = 0.6,
        string kind = "villager", int age = 30, Dictionary<string, double>? acts = null, Dictionary<string, Kin>? family = null)
        => new(name, household, kind, new Temperament(1.0, bold, 0.5, self), new Body(100, -1), null, haunts,
            acts ?? new Dictionary<string, double>(), Array.Empty<string>(), age, family);

    private static Location Room() => new("Room", false, Enumerable.Repeat(new string('.', 40), 6).ToList());

    /// <summary>Nobody tires during the run.</summary>
    private static BodyOptions Awake() => new() { AwakeHoursAtRest = 100_000 };

    // The scenes' own feeling rows, so tuning the town doesn't move these tests.
    private static readonly Affect GiftRow = new(Patient.Target, 0.2, 0.3, 1, TargetIs.Chosen, Tilt: 1);
    private static readonly Affect ArguedRow = new(Patient.Target, -0.3, 0.3, 1, TargetIs.Chosen);
    private static readonly Affect HelpRow = new(Patient.Target, 0.3, 0.3, 1, TargetIs.Chosen, Tilt: 1);

    private static ActKind Gift(double perDay = 0) => new("GaveGift", 1.5, 1, 1, 1, perDay, Array.Empty<string>(), Affect: GiftRow);
    private static ActKind Argued() => new("Argued", 3.0, -1, 2, 10, 0, Array.Empty<string>(), MinAge: 13, Affect: ArguedRow);
    private static ActKind Help() => new("HelpedSomeone", 2.0, 1, 2, 5, 0, Array.Empty<string>(), MinAge: 10, Affect: HelpRow);

    // The light kinds, as the town has them (spec 2.6).
    private static readonly ActKind SnubbedKind = new(Simulation.Snubbed, 0.5, -1, 1, 1, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, -0.15, 0.3, 1, TargetIs.Chosen));
    private static readonly ActKind TurnedAwayKind = new(Simulation.TurnedAway, 0.5, -1, 1, 1, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, -0.05, 0.3, 1, TargetIs.Chosen));

    private static readonly ActKind StoleKind = new(Simulation.Stole, 4.5, -1, 1, 1, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, -0.5, 0.5, 1, TargetIs.Keeper));

    private static List<ActKind> Kinds(double gift = 0) => new() { Gift(gift), Argued(), Help(), SnubbedKind, TurnedAwayKind, StoleKind };

    private static Dictionary<string, double> Does(params string[] kinds) => kinds.ToDictionary(k => k, _ => 1.0);

    private static AuthorityOptions Keeps(string keeper) => new()
    {
        ElectConstable = false, Keepers = new Dictionary<string, string> { ["Room"] = keeper },
    };

    /// <summary>D13's scene: Ann (3,2) argues at Bob (5,2) at 10:00 on day 0, and nobody else is in
    /// the room. Ann has Argued in her list and Bob has none; no rates.</summary>
    private static SimResult Argument(FeelingOptions? feelings = null, int days = 4, double annBold = 0.5, Villager? bob = null,
        Action<Simulation>? before = null, params (int Tick, string Actor, string Kind)[] more)
    {
        var cast = new[] { V("Ann", "A", 3, 2, bold: annBold, acts: Does("Argued")), bob ?? V("Bob", "B", 5, 2, bold: 0.9) };
        var sim = new Simulation(1, cast, new[] { Room() }, Kinds(), feelings: feelings ?? FeelingOptions.WithDesire(), body: Awake(),
            scheduled: new[] { (Ten, "Ann", "Argued") }.Concat(more).ToList(), wander: 0);
        before?.Invoke(sim);
        return sim.Run(days);
    }

    private static List<Act> ActsBy(SimResult r, string actor, string kind) => r.Acts.Where(a => a.Actor == actor && a.Kind == kind).ToList();

    private static bool Hostile(Act a) => a.Kind is "Argued" or Simulation.Snubbed or Simulation.TurnedAway;

    /// <summary>D13: Bob, bold 0.9 and with no acts of his own, answers Ann's argument within two
    /// ticks of it ending, citing it: I = 0.3 felt + the grudge, a clear call. Ann's own act stirs
    /// nothing in her; what she holds cites Bob's answer, and her one close call on day 3, after
    /// the cooldown, comes out no for seed 1, so she does not argue again.</summary>
    [Fact]
    public void TheHurtAnswerBack()
    {
        SimResult r = Argument();

        Act answer = Assert.Single(ActsBy(r, "Bob", "Argued"));
        Assert.Equal(("Ann", 0), (answer.Target, answer.About));
        Assert.True(answer.Tick <= Ten + 20);
        Assert.Contains(answer.Id, r.Pursued);
        Pursuit why = Assert.Single(r.Pursuits, p => p.Holder == "Bob");
        Assert.Equal(("Ann", DesireKind.Answer, 0, "Argued", "clear", true, answer.Id),
            (why.Subject, why.Motive, why.Source, why.ActKind, why.Call, why.Acted, why.ActId));
        Assert.True(why.Intensity >= 0.3);
        Assert.Contains(r.Log, l => l.StartsWith($"{answer.Tick} desire Bob Answer Ann act 0: Argued "));

        Assert.DoesNotContain(r.Stirred, s => s.Holder == "Ann" && s.Source == 0);
        Assert.All(r.Stirred.Where(s => s.Holder == "Ann"), s => Assert.Equal(answer.Id, s.Source));
        Assert.Equal(0, Assert.Single(ActsBy(r, "Ann", "Argued")).Id);
    }

    /// <summary>D14: both bold 0.9. Each answers the other, but never inside three days of their
    /// own last argument with the other: the cooldown is per ordered pair, so Bob's first answer
    /// still comes within the hour of Ann's act.</summary>
    [Fact]
    public void ThePairCoolsDown()
    {
        var bob = V("Bob", "B", 5, 2, bold: 0.9, acts: Does("Argued"));
        SimResult r = Argument(days: 10, annBold: 0.9, bob: bob);

        foreach (var (actor, target) in new[] { ("Ann", "Bob"), ("Bob", "Ann") })
        {
            var ticks = r.Acts.Where(a => a.Kind == "Argued" && a.Actor == actor && a.Target == target).Select(a => a.Tick).ToList();
            Assert.True(ticks.Count >= 2, $"{actor} argued with {target} {ticks.Count} times");
            Assert.All(ticks.Zip(ticks.Skip(1)), p => Assert.True(p.Second - p.First >= 3 * D, $"{actor}: {p.First} then {p.Second}"));
        }
        Act first = ActsBy(r, "Bob", "Argued")[0];
        Assert.Equal(0, first.About);
        Assert.True(first.Tick - Ten <= 60);
    }

    /// <summary>D15: Ann gives Bob a gift; Bob, bold 0.5, gives one back, citing it. A return of
    /// one's own kindness is not returned again, so there is no third gift in the week, and both
    /// sides of the first gift are recorded as returned.</summary>
    [Fact]
    public void KindnessReturnedOnce()
    {
        var cast = new[] { V("Ann", "A", 3, 2), V("Bob", "B", 5, 2) };
        SimResult r = new Simulation(1, cast, new[] { Room() }, Kinds(), feelings: FeelingOptions.WithDesire(), body: Awake(),
            scheduled: new[] { (Ten, "Ann", "GaveGift") }, wander: 0).Run(7);

        var gifts = r.Acts.Where(a => a.Kind == "GaveGift").ToList();
        Assert.Equal(2, gifts.Count);
        Assert.Equal(("Ann", "Bob"), (gifts[0].Actor, gifts[0].Target));
        Assert.Equal(("Bob", "Ann", gifts[0].Id), (gifts[1].Actor, gifts[1].Target, gifts[1].About));
        Stirring stir = Assert.Single(r.Stirred);
        Assert.Equal(("Bob", "Ann", DesireKind.Return, 0), (stir.Holder, stir.Subject, stir.Motive, stir.Source));
        Assert.DoesNotContain(r.Stirred, s => s.Holder == "Ann");

        LifeEvent did = Assert.Single(r.LifeEvents, e => e is { Person: "Ann", Role: LifeRole.Did, ActId: 0 });
        Assert.Equal((Outcome.Returned, gifts[1].Tick), (did.Outcome, did.ResolvedTick));
        LifeEvent got = Assert.Single(r.LifeEvents, e => e is { Person: "Bob", Role: LifeRole.Undergone, ActId: 0 });
        Assert.Equal(("Ann", Outcome.Returned), (got.Other, got.Outcome));
    }

    /// <summary>D16, story 2: Ann helps Bob, bold 0.35. Returning a help in person is a close call
    /// (margin about +0.14) and a gift a clear yes (about +0.24). When the close call says no,
    /// he gives a gift instead; when it says yes, he helps.</summary>
    [Fact]
    public void Story2_DeclinedHelpFallsBackToGift()
    {
        SimResult Run(bool answer)
        {
            var cast = new[] { V("Ann", "A", 3, 2), V("Bob", "B", 5, 2, bold: 0.35) };
            FeelingOptions f = FeelingOptions.WithDesire();
            f.CloseCall = _ => answer;
            return new Simulation(1, cast, new[] { Room() }, Kinds(), feelings: f, body: Awake(),
                scheduled: new[] { (Ten, "Ann", "HelpedSomeone") }, wander: 0).Run(2);
        }

        SimResult no = Run(false);
        Act back = Assert.Single(no.Acts, a => a.Actor == "Bob");
        Assert.Equal(("GaveGift", "Ann", 0), (back.Kind, back.Target, back.About));
        Assert.Empty(ActsBy(no, "Bob", "HelpedSomeone"));
        var weighed = no.Pursuits.Where(p => p.Holder == "Bob").ToList();
        Assert.Equal(new[] { ("HelpedSomeone", "close-no"), ("GaveGift", "clear") }, weighed.Select(p => (p.ActKind, p.Call)));
        Assert.Equal(weighed[0].Tick, weighed[1].Tick);
        Assert.InRange(weighed[0].Margin, 0, 0.15);
        Assert.True(weighed[1].Margin > 0.15);

        SimResult yes = Run(true);
        Act help = Assert.Single(yes.Acts, a => a.Actor == "Bob");
        Assert.Equal(("HelpedSomeone", "Ann", 0), (help.Kind, help.Target, help.About));
        Assert.Equal("close-yes", Assert.Single(yes.Pursuits, p => p.Holder == "Bob").Call);
    }

    /// <summary>D17: as D13, with Bob as Ann's housemate, and then as her brother in another
    /// house. Families cover: nothing is stirred and nobody answers.</summary>
    [Fact]
    public void FamiliesCover()
    {
        SimResult housemate = Argument(bob: V("Bob", "A", 5, 2, bold: 0.9));
        SimResult brother = Argument(bob: V("Bob", "B", 5, 2, bold: 0.9, family: new() { ["Ann"] = Kin.Sibling }));

        foreach (SimResult r in new[] { housemate, brother })
        {
            Assert.Equal("Bob", Assert.Single(r.Acts).Target);
            Assert.Empty(r.Stirred);
            Assert.Empty(r.Pursuits);
        }
        // The control: strangers in separate houses do answer.
        Assert.Single(ActsBy(Argument(), "Bob", "Argued"));
    }

    /// <summary>D18: Bob, bold 0.9, still loves Ann after her argument (start 0.6, then 0.35):
    /// he wants to make up and gives her a gift; he does not argue. At 0.1 he answers. (The spec's
    /// middle value 0.3 ends at 0.193 after the argument, under LoveAt, because his regard for her
    /// kind falls too; 0.35 ends at 0.243, which is what the spec intends.)</summary>
    [Fact]
    public void LoveDoesNotConfront()
    {
        foreach (double start in new[] { 0.6, 0.35 })
        {
            SimResult r = Argument(FeelingOptions.WithDesire(("Bob", "Ann", start)));
            Stirring stir = Assert.Single(r.Stirred);
            Assert.Equal(("Bob", DesireKind.MakeUp), (stir.Holder, stir.Motive));
            Act gift = Assert.Single(r.Acts, a => a.Actor == "Bob");
            Assert.Equal(("GaveGift", "Ann", 0), (gift.Kind, gift.Target, gift.About));
            Assert.Empty(ActsBy(r, "Bob", "Argued"));
        }

        SimResult cool = Argument(FeelingOptions.WithDesire(("Bob", "Ann", 0.1)));
        Assert.Equal(DesireKind.Answer, cool.Stirred.First(s => s.Holder == "Bob").Motive);
        Assert.Equal(0, Assert.Single(ActsBy(cool, "Bob", "Argued")).About);
        Assert.Empty(ActsBy(cool, "Bob", "GaveGift"));
    }

    /// <summary>D19: Bob at bold 0.475 is in the band toward Ann (margin about -0.08) and every
    /// close call says no. Ann stays in reach all day, yet the question is asked once on day 0 and
    /// the answer stands until the motive moves by 0.1 or more: as it fades, and when Ann argues
    /// again on day 4. No more than two close calls a day.</summary>
    [Fact]
    public void TheQuestionWaits()
    {
        var calls = new List<Pursuit>();
        FeelingOptions f = FeelingOptions.WithDesire();
        f.CloseCall = p =>
        {
            calls.Add(p);
            return false;
        };
        int again = 4 * D + Ten;
        SimResult r = Argument(f, days: 6, bob: V("Bob", "B", 5, 2, bold: 0.475), more: (again, "Ann", "Argued"));

        Assert.Single(calls, c => c.Tick < D);
        Assert.InRange(calls[0].Margin, -0.15, 0.15);
        Assert.Contains(r.Pursuits, p => p.Tick < D && p.Call == "stands");
        Assert.All(calls.Zip(calls.Skip(1)), p => Assert.True(Math.Abs(p.Second.Intensity - p.First.Intensity) >= 0.1));

        int second = Assert.Single(r.Acts, a => a.Actor == "Ann" && a.Tick >= again).Id;
        Pursuit after = calls.First(c => c.Source == second);
        Pursuit last = calls.Last(c => c.Tick < after.Tick);
        Assert.True(after.Intensity - last.Intensity >= 0.1);
        Assert.True(after.Tick - again <= 20);

        Assert.Empty(ActsBy(r, "Bob", "Argued"));
        Assert.All(r.Pursuits.Where(p => p.Call is "close-yes" or "close-no").GroupBy(p => p.Tick / D),
            g => Assert.True(g.Count() <= 2, $"day {g.Key}: {g.Count()} close calls"));

        // With no answer left to stand (AskAgainStep 0), each weighing would ask again: the two
        // slots a day are what stop him.
        FeelingOptions eager = FeelingOptions.WithDesire();
        eager.AskAgainStep = 0;
        eager.CloseCall = _ => false;
        SimResult slots = Argument(eager, days: 3, bob: V("Bob", "B", 5, 2, bold: 0.475));
        var drawn = slots.Pursuits.Where(p => p.Call is "close-yes" or "close-no").GroupBy(p => p.Tick / D).ToList();
        Assert.Equal(2, drawn.Single(g => g.Key == 0).Count());
        Assert.All(drawn, g => Assert.True(g.Count() <= 2, $"day {g.Key}: {g.Count()} close calls"));
    }

    /// <summary>D20, story 4: Bob, bold 0.05, gives gifts often. After Ann argues at him he cannot
    /// answer (a clear no), so he keeps away from her for a week: no hostile act and no gift to her,
    /// while his gifts to Cal go on. Gifts to Ann come back after the week. Cal stands at (9,2),
    /// in Bob's reach and out of Ann's, so her argument can only be aimed at Bob.</summary>
    [Fact]
    public void TheShyAvoid_Story4()
    {
        var cast = new[]
        {
            V("Ann", "A", 3, 2, acts: Does("Argued")), V("Bob", "B", 5, 2, bold: 0.05, acts: Does("GaveGift")), V("Cal", "C", 9, 2),
        };
        SimResult r = new Simulation(1, cast, new[] { Room() }, Kinds(gift: 20), feelings: FeelingOptions.WithDesire(), body: Awake(),
            scheduled: new[] { (Ten, "Ann", "Argued") }, wander: 0).Run(10);

        Act argument = Assert.Single(r.Acts, a => a.Kind == "Argued");
        Assert.Equal(("Ann", "Bob"), (argument.Actor, argument.Target));
        Assert.DoesNotContain(r.Acts, a => a.Actor == "Bob" && Hostile(a));
        var avoid = Assert.Single(r.Avoids);
        Assert.Equal(("Bob", "Ann", argument.Id), (avoid.Holder, avoid.Subject, avoid.Source));
        Assert.InRange(avoid.Tick, Ten, Ten + 20);
        Assert.Equal(avoid.Tick + 7 * D, avoid.Until);
        Assert.Contains(r.Log, l => l.StartsWith($"{avoid.Tick} avoid Bob Ann act {argument.Id} margin -"));
        Assert.Contains(r.LifeEvents, e => e is { Person: "Bob", Other: "Ann", Role: LifeRole.Avoided });

        var gifts = ActsBy(r, "Bob", "GaveGift");
        bool During(Act a) => a.Tick >= avoid.Tick && a.Tick < avoid.Until;
        Assert.DoesNotContain(gifts, g => g.Target == "Ann" && During(g));
        Assert.Contains(gifts, g => g.Target == "Cal" && During(g));
        Assert.Contains(gifts, g => g.Target == "Ann" && g.Tick >= avoid.Until);
    }

    /// <summary>D27: character is read when the gate weighs. D13 as is, Bob answers; with his
    /// boldness set to 0.02 before the run, the same scene ends in an avoid.</summary>
    [Fact]
    public void CharacterIsReadNow()
    {
        SimResult bold = Argument();
        Assert.Single(ActsBy(bold, "Bob", "Argued"));
        Assert.Empty(bold.Avoids);

        Simulation? kept = null;
        SimResult shy = Argument(before: s =>
        {
            s.SetTrait("Bob", Trait.Boldness, 0.02);
            kept = s;
        });
        Assert.Equal(0.02, kept!.TraitOf("Bob", Trait.Boldness));
        Assert.DoesNotContain(shy.Acts, a => a.Actor == "Bob" && Hostile(a));
        Assert.Contains(shy.Log, l => l.Contains(" avoid Bob Ann act 0 "));
        var avoid = Assert.Single(shy.Avoids);
        Assert.Equal(("Bob", "Ann"), (avoid.Holder, avoid.Subject));
        Assert.Equal("no", Assert.Single(shy.Pursuits).Call);
    }

    /// <summary>D30: FeelingSceneTests' corrected guess, with Gus keeping the room. Tom steals from
    /// him; Gus, bold and low in self-regard, sees it from 7 tiles and names a guess, Nat, whom he
    /// already dislikes (seed 6). Ann, his housemate, saw it close up and tells him it was Tom. The
    /// motive toward Nat is dropped, with a record of it, and one toward Tom follows. At 13:00 Nat
    /// walks up beside Gus: with the motive gone, Gus does not have it out with him, though he is
    /// bold and still dislikes him. (The spec says Answer; the target of an act aimed at someone
    /// always names its actor, so a changed cause needs a theft from a keeper, and the motive is
    /// Retaliate.)</summary>
    [Fact]
    public void ReattributionDrops()
    {
        var cast = new[]
        {
            Moving("Tom", "T", new[] { At(3, 2) }), Moving("Ann", "G", new[] { At(5, 2) }),
            Moving("Gus", "G", new[] { At(10, 2) }, bold: 0.8, self: 0.3),
            Moving("Nat", "N", new[] { At(30, 2, 0, Clock.At(13)), At(12, 2, Clock.At(13)) }, kind: "old man", age: 70),
            Moving("Kim", "K", new[] { At(38, 2) }),
        };
        SimResult r = new Simulation(6, cast, new[] { Room() }, Kinds(), authority: Keeps("Gus"), gossip: new GossipOptions { ChatChance = 10 },
            feelings: FeelingOptions.WithDesire(("Gus", "Nat", -0.5)), body: Awake(),
            scheduled: new[] { (Ten, "Tom", Simulation.Stole) }, wander: 0).Run(1);

        Assert.Contains(r.Log, l => l.EndsWith(" belief Gus 0 Nat Witnessed 4.5"));
        Assert.Equal("Tom", r.Beliefs["Gus"][0].Actor);
        Stirring guess = Assert.Single(r.Stirred, s => s.Subject == "Nat");
        Assert.Equal(("Gus", DesireKind.Retaliate, 0), (guess.Holder, guess.Motive, guess.Source));
        LifeEvent dropped = Assert.Single(r.LifeEvents, e => e.Role == LifeRole.Dropped);
        Assert.Equal(("Gus", "Nat", 0, Outcome.None), (dropped.Person, dropped.Other, dropped.ActId, dropped.Outcome));
        Assert.Equal(guess.Felt, dropped.Severity, 12);
        Stirring named = Assert.Single(r.Stirred, s => s.Subject == "Tom");
        Assert.Equal(("Gus", DesireKind.Retaliate, 0), (named.Holder, named.Motive, named.Source));
        Assert.True(named.Tick >= dropped.Tick && dropped.Tick > guess.Tick);
        Assert.Equal((Simulation.Stole, "Tom"), (Assert.Single(r.Acts).Kind, r.Acts[0].Actor));
        Assert.DoesNotContain(r.Pursuits, p => p.Subject == "Nat");
    }

    /// <summary>D31: Ann argues at Bob at 10:00 and steals from Dee's room at 10:30, both out of
    /// Dee's sight (she stands 33 tiles off). Bob, too shy to answer, joins Dee at 11:00 and tells
    /// her both. Hearsay of the argument stirs nothing in her; hearsay of the theft from her own
    /// room does (rule 9: the keeper's own trade).</summary>
    [Fact]
    public void HearsayStirsNothing_ExceptTheRobbedKeeper()
    {
        var cast = new[]
        {
            Moving("Ann", "A", new[] { At(3, 2) }),
            Moving("Bob", "B", new[] { At(5, 2, 0, Clock.At(11)), At(36, 2, Clock.At(11)) }, bold: 0.3),
            Moving("Dee", "D", new[] { At(38, 2) }),
        };
        SimResult r = new Simulation(1, cast, new[] { Room() }, Kinds(), authority: Keeps("Dee"), gossip: new GossipOptions { ChatChance = 10 },
            feelings: FeelingOptions.WithDesire(), body: Awake(),
            scheduled: new[] { (Ten, "Ann", "Argued"), (Clock.At(10, 30), "Ann", Simulation.Stole) }, wander: 0).Run(1);

        Act argument = Assert.Single(r.Acts, a => a.Kind == "Argued");
        Act theft = Assert.Single(r.Acts, a => a.Kind == Simulation.Stole);
        Assert.Equal(("Bob", "Dee"), (argument.Target, theft.Target));
        Belief heard = r.Beliefs["Dee"][argument.Id];
        Assert.Equal(("Ann", Source.Told), (heard.Actor, heard.Source));
        Assert.DoesNotContain(r.Stirred, s => s.Holder == "Dee" && s.Source == argument.Id);

        Belief robbed = r.Beliefs["Dee"][theft.Id];
        Assert.Equal(("Ann", Source.Told), (robbed.Actor, robbed.Source));
        Stirring stir = Assert.Single(r.Stirred, s => s.Holder == "Dee");
        Assert.Equal(("Ann", DesireKind.Retaliate, theft.Id), (stir.Subject, stir.Motive, stir.Source));
    }

    /// <summary>D32: Bob steals at Ann's room beside her. Ann, the keeper, has it out with him: an
    /// argument citing the theft. Argued with over his own scandal, Bob gave cause: it is recorded,
    /// and no motive to answer is stirred.</summary>
    [Fact]
    public void GaveCause()
    {
        var cast = new[] { V("Ann", "A", 3, 2), V("Bob", "B", 5, 2) };
        SimResult r = new Simulation(1, cast, new[] { Room() }, Kinds(), authority: Keeps("Ann"), feelings: FeelingOptions.WithDesire(),
            body: Awake(), scheduled: new[] { (Ten, "Bob", Simulation.Stole) }, wander: 0).Run(2);

        Act theft = r.Acts[0];
        Assert.Equal((Simulation.Stole, "Bob", "Ann"), (theft.Kind, theft.Actor, theft.Target));
        Act argument = Assert.Single(r.Acts, a => a.Kind == "Argued");
        Assert.Equal(("Ann", "Bob", theft.Id), (argument.Actor, argument.Target, argument.About));
        Assert.Equal(DesireKind.Retaliate, Assert.Single(r.Stirred).Motive);

        LifeEvent cause = Assert.Single(r.LifeEvents, e => e.Role == LifeRole.GaveCause);
        Assert.Equal(("Bob", "Ann", argument.Id), (cause.Person, cause.Other, cause.ActId));
        Assert.Contains(r.Log, l => l.EndsWith($" gave-cause Bob Ann act {argument.Id}"));
        Assert.DoesNotContain(r.Stirred, s => s.Holder == "Bob");
        Assert.Equal(2, r.Acts.Count);
    }

    /// <summary>P2 in a scene: with DesireActs off, D13's motive is stirred and weighed, the
    /// gate's lines go to the motive log and not the run log, and no act starts.</summary>
    [Fact]
    public void WithDesireActsOff_MotivesAreOnlyLogged()
    {
        FeelingOptions f = FeelingOptions.WithDesire();
        f.DesireActs = false;
        SimResult watched = Argument(f);

        Assert.Equal(0, Assert.Single(watched.Acts).Id);
        Assert.Empty(watched.Pursued);
        Assert.Empty(watched.Avoids);
        Assert.Equal(DesireKind.Answer, Assert.Single(watched.Stirred).Motive);
        Assert.NotEmpty(watched.Pursuits);
        Assert.All(watched.Pursuits, p => Assert.Equal((false, -1), (p.Acted, p.ActId)));
        Assert.Equal("clear", watched.Pursuits[0].Call);
        Assert.Contains("609 stirred Bob Answer Ann act 0 felt 0.30", watched.MotiveLog);
        Assert.DoesNotContain(watched.Log, l => l.Contains(" stirred ") || l.Contains(" desire "));

        SimResult acting = Argument();
        Assert.Empty(acting.MotiveLog);
        Assert.Contains("609 stirred Bob Answer Ann act 0 felt 0.30", acting.Log);
    }
}
