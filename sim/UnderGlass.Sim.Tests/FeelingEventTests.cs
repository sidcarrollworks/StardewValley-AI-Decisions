using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// Feelings at work in small scenes (phase 0c, T23-T31): being named, confronted and shamed, the
/// standing a culprit's family loses, grudges and friendships over the nights, the power of
/// acting, and the run-level checks on the default town. Exact values are read from the feeling
/// events (Raw as asked, Change as applied), since the night moves regard at the end.
/// </summary>
public class FeelingEventTests
{
    private static Haunt At(string place, int x, int y, int from = 0, int to = Clock.MinutesPerDay)
        => new(place, new Tile(x, y), from, to, 1);

    /// <summary>Sensitivity and retention 0.5 (felt in full, kept in full below 0.7); never tires.</summary>
    private static Villager V(string name, string household, IReadOnlyList<Haunt> haunts, int age = 30,
        double understanding = 0.5, Dictionary<string, Kin>? family = null)
        => new(name, household, "villager", new Temperament(1.0, 0.5, understanding, 0.6), new Body(100, -1), null, haunts,
            new Dictionary<string, double>(), Array.Empty<string>(), age, family);

    private static Location Room(string name = "Room", int w = 40, int h = 6)
        => new(name, false, Enumerable.Repeat(new string('.', w), h).ToList());

    private static readonly int Ten = Clock.At(10);
    private static bool DayEnd(int m) => Clock.OfDay(m) == Clock.MinutesPerDay - 1;

    // The test's own act kinds, with the town's feeling rows (a theft and a bin hurt the keeper;
    // the official is believed to act less freely).
    private static readonly ActKind Stole = new("Stole", 4.5, -1, 1, 1, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, -0.5, 0.5, 1, TargetIs.Keeper));
    private static readonly ActKind Bin = new("RummagedInBin", 4.0, -1, 1, 1, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, -0.3, 0.5, 1, TargetIs.Keeper));
    private static readonly ActKind Warned = new(Authority.Warned, 3.0, -1, 1, 5, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Actor, -0.3, 0.4, 0.4, TargetIs.Given));
    private static readonly ActKind Questioned = new(Authority.Questioned, 2.5, -1, 1, 10, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Actor, -0.15, 0.3, 0.4, TargetIs.Given));
    /// <summary>A test row: a hard argument, -1 felt in full, so its target's regard falls by 0.3.</summary>
    private static readonly ActKind Argued = new("Argued", 3.0, -1, 2, 10, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, -1, 0.3, 1, TargetIs.Chosen));
    private static readonly ActKind GaveGift = new("GaveGift", 1.5, 1, 1, 1, 0, Array.Empty<string>(),
        Affect: new Affect(Patient.Target, 0.2, 0.3, 1, TargetIs.Chosen, Tilt: 1));

    /// <summary>SuspicionTests' questioning with feeling rows: Tom steals; Wes and May (the mayor)
    /// see "someone" from 7 and 8 tiles and suspect him; May questions Tom, who never confesses
    /// and says he saw Wes, so Wes is questioned too.</summary>
    private static SimResult Questioning()
    {
        var cast = new[]
        {
            V("Tom", "T", new[] { At("Room", 3, 2) }), V("Wes", "W", new[] { At("Room", 10, 2) }),
            V("May", "M", new[] { At("Room", 11, 2) }),
        };
        var authority = new AuthorityOptions { Mayor = "May", ElectConstable = false, ConfessBase = 0, ConfessPerTimidity = 0 };
        return new Simulation(3, cast, new[] { Room() }, new[] { Stole, Warned, Questioned }, authority: authority,
            scheduled: new[] { (Ten, "Tom", "Stole") }, wander: 0, feelings: new FeelingOptions()).Run(1);
    }

    /// <summary>
    /// Kid (15) steals at Kim's shop at 10:00. Three neighbours, Ned, Nia and Wil, step in beside
    /// him from 9:55 to 10:05 (too short to chat with each other) and see it close up. The first
    /// <paramref name="visitors"/> of them then call on Pat, his mother, one an hour from 11:00 in
    /// "Away", where May the mayor sits within sight of Pat but too far from the callers to meet them.
    /// </summary>
    private static (Simulation Sim, SimResult R) Theft(int visitors)
    {
        var cast = new List<Villager>
        {
            V("Kid", "P", new[] { At("Room", 10, 2) }, age: 15, family: new() { ["Pat"] = Kin.Parent }),
            V("Pat", "P", new[] { At("Away", 20, 10) }, age: 45, family: new() { ["Kid"] = Kin.Child }),
            V("Kim", "K", new[] { At("Room", 38, 5) }),
            V("May", "M", new[] { At("Away", 20, 18) }),
        };
        var neighbours = new[] { ("Ned", 8, 2), ("Nia", 12, 2), ("Wil", 10, 4) };
        var places = new List<Location> { Room(), Room("Away", 40, 20) };
        for (int i = 0; i < neighbours.Length; i++)
        {
            var (name, x, y) = neighbours[i];
            string yard = "Yard" + name;
            places.Add(Room(yard, 6, 6));
            var haunts = new List<Haunt> { At(yard, 2, 2, 0, Clock.At(9, 55)), At("Room", x, y, Clock.At(9, 55), Clock.At(10, 5)) };
            if (i < visitors)
                haunts.AddRange(new[]
                {
                    At(yard, 2, 2, Clock.At(10, 5), Clock.At(11 + i)), At("Away", 20, 2, Clock.At(11 + i), Clock.At(12 + i)),
                    At(yard, 2, 2, Clock.At(12 + i)),
                });
            else
                haunts.Add(At(yard, 2, 2, Clock.At(10, 5)));
            cast.Add(V(name, name, haunts));
        }
        var authority = new AuthorityOptions
        {
            Mayor = "May", ElectConstable = false, ReportBase = 1,
            Keepers = new Dictionary<string, string> { ["Room"] = "Kim" },
        };
        var sim = new Simulation(5, cast, places, new[] { Stole }, authority: authority,
            gossip: new GossipOptions { ChatChance = 10 }, scheduled: new[] { (Ten, "Kid", "Stole") }, wander: 0,
            feelings: new FeelingOptions());
        return (sim, sim.Run(1));
    }

    /// <summary>
    /// Two people. The patient spends the whole day at one spot in the room. The actor either
    /// stays beside them all day, or lives in another room and steps in only from 9:50 to 10:15
    /// (under the hour that counts as a day together). At 10:00 on day 0 the actor does the act
    /// to the patient, the only one in reach. Returns the patient's personal regard for the actor
    /// at the end of each day.
    /// </summary>
    private static (SimResult R, List<double> Regard) Pair(ActKind kind, string actor, string patient, bool together, int days,
        double understanding = 0.5, GossipOptions? gossip = null)
    {
        var visits = together
            ? new[] { At("Room", 7, 2) }
            : new[] { At("Room2", 5, 2, 0, Clock.At(9, 50)), At("Room", 7, 2, Clock.At(9, 50), Clock.At(10, 15)), At("Room2", 5, 2, Clock.At(10, 15)) };
        var cast = new[] { V(patient, "P", new[] { At("Room", 5, 2) }, understanding: understanding), V(actor, "A", visits) };
        var regard = new List<double>();
        SimResult r = new Simulation(1, cast, new[] { Room(), Room("Room2") }, new[] { kind }, gossip: gossip,
                scheduled: new[] { (Ten, actor, kind.Name) }, wander: 0, feelings: new FeelingOptions())
            .Run(days, (m, s) =>
            {
                if (DayEnd(m))
                    regard.Add(s.PersonalRegard(patient, actor));
            });
        return (r, regard);
    }

    private static Felt Toward(SimResult r, string holder, string toward, string route)
        => Assert.Single(r.Feelings, f => f.Holder == holder && f.Toward == toward && f.Route == route);

    // ---- named, confronted, shamed (F13, F14, F10) -----------------------------------------

    /// <summary>T23. Wes did nothing, yet May and Tom both named him as nearby: he resents them,
    /// split between the two. Tom, who did it, is ashamed instead and resents nobody, and blames
    /// the questioner less than an innocent does, since he knows why (law 10).</summary>
    [Fact]
    public void AnInnocentSuspectResentsWhoeverNamedThem_TheCulpritIsAshamed()
    {
        SimResult r = Questioning();
        Assert.Contains(r.Interviews, i => i.ActId == 0 && i.Who == "Tom" && !i.Confessed);
        Assert.Contains(r.Interviews, i => i.ActId == 0 && i.Who == "Wes");

        Felt wes = Toward(r, "Wes", "Tom", "Accused");
        Assert.Equal(0, wes.ActId);
        Assert.True(wes.Change < 0);
        Assert.Equal(-0.3 / 2 * 0.5, wes.Raw, 12);             // two namers: May's own account and Tom's
        Assert.Equal(0, Toward(r, "Wes", "May", "Accused").ActId);
        Assert.Contains(r.Sentiments, s => s is { Holder: "Wes", Toward: "Tom", Name: "Wronged", ActId: 0 });

        Assert.Contains(r.Sentiments, s => s is { Holder: "Tom", Toward: "Tom", Name: "Ashamed", ActId: 0 });
        Assert.DoesNotContain(r.Feelings, f => f.Holder == "Tom" && f.Route == "Accused" && f.Toward is not null);
        Felt shame = Assert.Single(r.Feelings, f => f.Holder == "Tom" && f.Route == "Accused");
        Assert.Equal(-0.3 * (1.5 - 0.6), shame.Mood, 12);       // more sadness, felt as shame

        // Questioned by May: the guilty blame her by (1 - understanding) of what the innocent do.
        Felt tomByMay = Toward(r, "Tom", "May", "Undergone");
        Felt wesByMay = Toward(r, "Wes", "May", "Undergone");
        Assert.Equal(-0.15 * 0.3 * 0.4, wesByMay.Raw, 12);
        Assert.Equal(wesByMay.Raw * (1 - 0.5), tomByMay.Raw, 12);
        Assert.Equal(tomByMay.Raw, tomByMay.Change, 12);        // Tom held nothing against May before
    }

    /// <summary>T24. Bob is confronted over his own act: shame, and no grudge against whoever
    /// confronted him.</summary>
    [Fact]
    public void TheConfrontedCulpritIsAshamed()
    {
        // GossipTests' confrontation: four neighbours of one household stand close and see Bob rummage.
        var cast = new[]
        {
            V("Ann", "H", new[] { At("Room", 3, 2) }), V("Bob", "B", new[] { At("Room", 5, 2) }),
            V("Cal", "H", new[] { At("Room", 7, 2) }), V("Dee", "H", new[] { At("Room", 5, 4) }),
            V("Eve", "H", new[] { At("Room", 4, 4) }),
        };
        SimResult r = new Simulation(3, cast, new[] { Room() }, new[] { Bin },
            scheduled: new[] { (Ten, "Bob", "RummagedInBin") }, wander: 0, feelings: new FeelingOptions()).Run(2);

        Confrontation c = Assert.Single(r.Confrontations);
        Assert.Equal("Bob", c.Target);
        Assert.True(c.Correct);
        Assert.Contains(r.Sentiments, s => s is { Holder: "Bob", Toward: "Bob", Name: "Ashamed", ActId: 0 });
        Assert.DoesNotContain(r.Feelings, f => f.Holder == "Bob" && f.Route == "Confronted" && f.Toward is not null);
        Felt felt = Assert.Single(r.Feelings, f => f.Holder == "Bob" && f.Route == "Confronted");
        Assert.Equal(-0.3 * (1.5 - 0.6), felt.Mood, 12);
    }

    /// <summary>T25. Each person Pat learns knows of her son's theft is a step of shame and of
    /// sadness at him (rule 17). Three callers shame her three times as much as one, no more.
    /// She still covers for him: she never tells the mayor beside her, nor anyone else.</summary>
    [Fact]
    public void ShameFallsOnTheCulpritsKin_MoreWhenItIsPublic()
    {
        var (_, one) = Theft(visitors: 1);
        var (_, three) = Theft(visitors: 3);

        static List<Felt> Steps(SimResult r) => r.Feelings.Where(f => f.Holder == "Pat" && f.Route == "Shame" && f.Toward == "Kid").ToList();
        Felt step = Assert.Single(Steps(one));
        Assert.Equal(0, step.ActId);
        Assert.Equal(-0.05 * (1.5 - 0.6) * 0.5, step.Raw, 12);
        Assert.InRange(Steps(three).Count, 3, new FeelingOptions().ShameCap);

        double alone = Steps(one).Sum(f => f.Change), public3 = Steps(three).Sum(f => f.Change);
        Assert.True(alone < 0);
        Assert.True(public3 < alone);                              // more when more people know
        Assert.True(Math.Abs(public3) <= 3 * Math.Abs(alone) + 1e-12);

        foreach (SimResult r in new[] { one, three })
        {
            Assert.Equal("Kid", r.Beliefs["Pat"][0].Actor);
            Assert.Contains(r.Sentiments, s => s is { Holder: "Pat", Toward: "Kid", Name: "Ashamed" });
            Assert.DoesNotContain(r.Accounts, a => a.From == "Pat");     // May sat beside her all day
            Assert.DoesNotContain(r.Log, l => l.Contains(" told Pat ") && l.EndsWith(" 0"));
        }
    }

    /// <summary>T26. Wil, no kin of Kid's, sees the theft: a little of what he feels against Kid
    /// falls on Kid's mother, as far as Wil doesn't know her (rule 17, shame by association).</summary>
    [Fact]
    public void AScandalCostsTheCulpritsFamilySomeStanding()
    {
        var (sim, r) = Theft(visitors: 0);
        Felt atKid = Toward(r, "Wil", "Kid", "Imitation");
        Felt atPat = Toward(r, "Wil", "Pat", "Association");
        double fam = sim.Familiarity("Wil", "Pat");              // they never met: as seeded
        Assert.Equal(0.25, fam, 12);
        Assert.True(atKid.Raw < 0);
        Assert.Equal(0, atPat.ActId);
        Assert.Equal(0.2 * (1 - fam) * (1 - 0.5 * 0.5) * atKid.Raw, atPat.Raw, 12);
        Assert.True(atPat.Change < 0);
        Assert.DoesNotContain(r.Feelings, f => f.Holder == "Pat" && f.Route == "Association"); // never from kin
    }

    // ---- the nights (F17) ------------------------------------------------------------------

    /// <summary>T27. A grudge heals 0.005 a night apart; on days together with no new slight it
    /// heals faster, more so for the understanding; it never heals past where the pair started.</summary>
    [Fact]
    public void AGrudgeFadesSlowlyApart_FasterTogether_NeverPastTheBaseline()
    {
        var (r, apart) = Pair(Argued, "Bob", "Ann", together: false, days: 6);
        Felt hurt = Toward(r, "Ann", "Bob", "Direct");
        Assert.Equal(-0.3, hurt.Raw, 12);
        Assert.Equal(-0.3, hurt.Change, 12);
        for (int k = 0; k < apart.Count; k++)
            Assert.Equal(-0.3 + 0.005 * (k + 1), apart[k], 9);

        var (_, together) = Pair(Argued, "Bob", "Ann", together: true, days: 22);
        Assert.Equal(-0.295, together[0], 9);                     // day 0: slighted that day, drift only
        for (int k = 1; k < together.Count; k++)
            Assert.Equal(Math.Min(0, -0.295 + 0.015 * k), together[k], 9);
        Assert.All(together, v => Assert.True(v <= 0));
        Assert.Equal(0, together[^1]);

        var (_, wise) = Pair(Argued, "Bob", "Ann", together: true, days: 8, understanding: 0.9);
        var (_, rash) = Pair(Argued, "Bob", "Ann", together: true, days: 8, understanding: 0.1);
        Assert.Equal(wise[0], rash[0], 12);
        for (int k = 1; k < wise.Count; k++)
        {
            Assert.Equal(-0.295 + (0.005 + 0.01 * 1.4) * k, wise[k], 9);
            Assert.Equal(-0.295 + (0.005 + 0.01 * 0.6) * k, rash[k], 9);
            Assert.True(wise[k] > rash[k]);
        }
    }

    /// <summary>T28. A gift warms the one given it by 0.06. Apart it fades 0.005 a night back to
    /// where it started; on days together it holds.</summary>
    [Fact]
    public void AFriendshipHoldsOnDaysTogether_AndFadesApart()
    {
        var (r, apart) = Pair(GaveGift, "Ann", "Bob", together: false, days: 14);
        Felt warmed = Toward(r, "Bob", "Ann", "Direct");
        Assert.Equal(0.06, warmed.Raw, 12);
        Assert.Equal(0.06, warmed.Change, 12);
        for (int k = 0; k < apart.Count; k++)
            Assert.Equal(Math.Max(0, 0.06 - 0.005 * (k + 1)), apart[k], 9);
        Assert.Equal(0, apart[^1]);

        var (_, together) = Pair(GaveGift, "Ann", "Bob", together: true, days: 14);
        Assert.All(together, v => Assert.Equal(0.06, v, 12));
    }

    // ---- the power of acting (F16) ---------------------------------------------------------

    /// <summary>T29. Power is 0.5 for someone untouched, lower for a household short of money,
    /// and rises with joy for three days. Nobody here chats, since company is a small joy too.</summary>
    [Fact]
    public void ThePowerOfActingFollowsJoySadnessAndConditions()
    {
        var quiet = new GossipOptions { ChatChance = 0 };

        // (a) Acts with no feeling rows: nothing is felt.
        var plain = new[]
        {
            new ActKind("RummagedInBin", 4.0, -1, 1, 1, 0, Array.Empty<string>()),
            new ActKind("GaveGift", 1.5, 1, 1, 1, 0, Array.Empty<string>()),
        };
        var cast = new[]
        {
            V("Ann", "A", new[] { At("Room", 3, 2) }), V("Bob", "B", new[] { At("Room", 5, 2) }), V("Cal", "C", new[] { At("Room", 7, 2) }),
        };
        SimResult a = new Simulation(1, cast, new[] { Room() }, plain, gossip: quiet,
            scheduled: new[] { (Ten, "Bob", "RummagedInBin"), (Clock.At(11), "Cal", "GaveGift") }, wander: 0,
            feelings: new FeelingOptions()).Run(3);
        Assert.Equal(2, a.Acts.Count);
        Assert.Empty(a.Feelings);
        Assert.All(a.PowerByDay.Values.SelectMany(d => d), p => Assert.Equal(0.5, p));

        // (b) A purse short of a week's groceries presses; a full one doesn't.
        var money = new[] { V("Pam", "T", new[] { At("Room", 5, 2) }), V("Gil", "G", new[] { At("Room2", 5, 2) }) };
        var eco = new Economy(new Dictionary<string, double> { ["T"] = -200, ["G"] = 1000 }, Array.Empty<(string, double, string?)>(),
            new Dictionary<string, double>(), new Dictionary<string, string>(), new Dictionary<string, (double, double)>(), TownStipend: 0, TownStart: 0);
        SimResult b = new Simulation(1, money, new[] { Room(), Room("Room2") }, Array.Empty<ActKind>(), economy: eco,
            money: new MoneyOptions { WantChancePerDay = 0 }, wander: 0, feelings: new FeelingOptions()).Run(3);
        Assert.All(b.PowerByDay["Pam"], p => Assert.Equal(0.5 - 0.15, p, 12));
        Assert.All(b.PowerByDay["Gil"], p => Assert.Equal(0.5, p, 12));

        // (c) One gift: the joy fades over three days, and power with it.
        var (c, _) = Pair(GaveGift, "Ann", "Bob", together: false, days: 5, gossip: quiet);
        Felt joy = Assert.Single(c.Feelings, f => f.Holder == "Bob" && f.Toward is null);
        Assert.Equal(0.2, joy.Mood, 12);
        double[] bob = c.PowerByDay["Bob"];
        double left = joy.Mood * (1 - (Clock.MinutesPerDay - 1 - joy.Tick) / (3.0 * Clock.MinutesPerDay));
        Assert.Equal(0.5 + 0.4 * left / (1 + left), bob[0], 12);
        Assert.True(bob[0] > bob[1] && bob[1] > bob[2] && bob[2] > 0.5);
        Assert.Equal(0.5, bob[3], 12);
        Assert.Equal(0.5, bob[4], 12);
        Assert.All(c.PowerByDay["Ann"], p => Assert.Equal(0.5, p, 12));  // the giver feels nothing for her own act
    }

    // ---- the default town (E0) -------------------------------------------------------------

    /// <summary>The town as it ships (feelings watched), and with feelings steering.</summary>
    private static readonly (string Mode, Func<FeelingOptions> Options)[] Modes =
    {
        ("observe", () => { FeelingOptions o = DefaultTown.Feelings(); o.Steer = false; return o; }),
        ("town", DefaultTown.Feelings),
        ("steer", () => new FeelingOptions { Start = DefaultTown.Tensions() }),
    };

    /// <summary>T30. Nobody's regard moves over an act they know nothing of: they hold a belief
    /// about it, it was done to them, or they were questioned, judged or confronted over it.
    /// Natural scandals are too rare for four weeks, so each run has one placed by the harness;
    /// seed 6 also brings a verdict, a confrontation and, in the town as it ships, kin shame.</summary>
    [Fact]
    public void EveryRegardChangeCitesAnActItsHolderKnows()
    {
        var all = new List<Felt>();
        foreach (var (mode, options) in Modes)
        {
            foreach (long seed in new long[] { 1, 2, 3, 6 })
            {
                SimResult r = new Simulation(seed, scheduled: new[] { Harness.ScandalFor(seed, DefaultTown.Acts()) },
                    feelings: options()).Run(28);
                var named = r.Interviews.Select(i => (i.ActId, i.Who))
                    .Concat(r.Verdicts.Select(v => (v.ActId, v.Accused)))
                    .Concat(r.Confrontations.Select(c => (c.ActId, c.Target)))
                    .ToHashSet();
                var changes = r.Feelings.Where(f => f.Change != 0).ToList();
                Assert.NotEmpty(changes);
                foreach (Felt f in changes)
                {
                    bool knows = r.Beliefs[f.Holder].ContainsKey(f.ActId)
                                 || f.Route == "Undergone" && r.Acts[f.ActId].Actor == f.Holder
                                 || named.Contains((f.ActId, f.Holder));
                    Assert.True(knows, $"{mode} seed {seed}: {f}");
                }
                Assert.All(r.Sentiments, s => Assert.InRange(s.ActId, 0, r.Acts.Count - 1));
                all.AddRange(changes);
            }
        }
        // The placed scandals reach the rules for being questioned, shame and association too.
        Assert.Contains(all, f => f.Route == "Undergone");
        Assert.Contains(all, f => f.Route == "Accused");
        Assert.Contains(all, f => f.Route == "Shame");
        Assert.Contains(all, f => f.Route == "Association");
    }

    /// <summary>T31. Regard (personal, effective and for kinds) stays in [-1, 1] and the power of
    /// acting in [0, 1] at every day's end; a seed always gives the same run, and another seed a
    /// different one.</summary>
    [Fact]
    public void RegardAndPowerStayInRange_AndTheSameSeedGivesTheSameRun()
    {
        var names = DefaultTown.Cast().Select(v => v.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var kinds = DefaultTown.Cast().Select(v => v.Kind).Distinct().ToList();
        void AtDayEnd(int m, Simulation s)
        {
            if (!DayEnd(m))
                return;
            foreach (string a in names)
            {
                Assert.InRange(s.Power(a), 0, 1);
                Assert.InRange(s.Mood(a), -1, 1);
                foreach (string k in kinds)
                    Assert.InRange(s.KindRegard(a, k), -1, 1);
                foreach (string b in names)
                {
                    Assert.InRange(s.PersonalRegard(a, b), -1, 1);
                    Assert.InRange(s.Regard(a, b), -1, 1);
                }
            }
        }

        foreach (var (mode, options) in Modes)
        {
            var hashes = new List<string>();
            for (long seed = 1; seed <= 3; seed++)
            {
                SimResult r = new Simulation(seed, feelings: options()).Run(28, AtDayEnd);
                Assert.All(r.Regard.Values, v => Assert.InRange(v, -1, 1));
                Assert.All(r.KindRegard.Values, v => Assert.InRange(v, -1, 1));
                Assert.All(r.RegardSnapshots.SelectMany(x => x.Regard), v => Assert.InRange(v, -1, 1));
                Assert.All(r.PowerByDay.Values.SelectMany(d => d), p => Assert.InRange(p, 0, 1));
                Assert.Contains(r.Log, l => l.Contains(" regard "));      // feelings were at work
                hashes.Add(Metrics.LogHash(r));
                if (seed > 1)
                    continue;
                SimResult again = new Simulation(seed, feelings: options()).Run(28);
                Assert.Equal(Metrics.LogHash(r), Metrics.LogHash(again));
                Assert.Equal(r.Feelings, again.Feelings);
                Assert.Equal(r.Sentiments, again.Sentiments);
            }
            Assert.Equal(hashes.Count, hashes.Distinct().Count());     // seed + 1 gives another run
        }
    }
}
