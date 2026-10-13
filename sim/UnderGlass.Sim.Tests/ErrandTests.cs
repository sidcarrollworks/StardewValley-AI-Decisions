using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Errands (needs spec): a need for one named thing, asked of someone through the general
/// Ask, promised, fetched from a counter during its sign's hours, and handed over (kept, late) or
/// never (broken, and the one let down knows); money moves and nothing is lost; speech names the
/// thing; with the switch off nothing runs.</summary>
public class ErrandTests
{
    private const int Day = Clock.MinutesPerDay;
    private const string Slices = "returns,company,welcome,repair,sides";
    private static readonly Lazy<SimResult> Month = new(() => Run(7, errands: true, speech: true, days: 28));

    private static FeelingOptions Options(bool errands)
    {
        FeelingOptions f = DefaultTown.Feelings();
        (f.Steer, f.Desire, f.DesireActs) = (true, true, true);
        f.Acts.With(errands ? Slices + ",errands" : Slices);
        return f;
    }

    private static SimResult Run(long seed, bool errands, bool speech, int days, Action<ActOptions>? tune = null)
    {
        FeelingOptions f = Options(errands);
        tune?.Invoke(f.Acts);
        var sim = new Simulation(seed, cast: ActCatalog.Cards(DefaultTown.Cast(), f.Acts), kinds: ActCatalog.Kinds(f.Acts), feelings: f);
        sim.ConfigureSpeech(new SpeechOptions { Enabled = speech });
        return sim.Run(days);
    }

    /// <summary>Ann needs tea, Bob (who likes her well, so a plain asking is a clear yes) is the only
    /// one in reach to ask, and Cal keeps the shop (open
    /// every day, all day, unless shut on some weekdays). Nobody sleeps, chats or wanders, and there
    /// are no roads: whoever sets out arrives. Everyone starts with 1000 in their own household.</summary>
    private static TownData Shop(Action<ActOptions>? tune = null, int[]? shut = null, string[]? bobWont = null, double regard = 0.9)
    {
        static Villager Person(string name, string place, int x, Job? job = null) => new(name, name, "villager",
            new Temperament(0.5, 0.5, 0.5, 0.5), new Body(100, -1), job, new[] { new Haunt(place, new Tile(x, 2), 0, Day, 1) },
            new Dictionary<string, double>(), Array.Empty<string>());
        FeelingOptions f = FeelingOptions.WithDesire(("Ann", "Bob", regard), ("Bob", "Ann", regard));
        (f.Steer, f.DesireActs) = (true, true);
        f.AnswerOn = f.ReturnOn = f.MakeUpOn = f.RetaliateOn = f.AvoidOn = f.WithdrawOn = false;
        f.Acts.Errands = true;
        f.Acts.NeedChancePerDay = 1;
        tune?.Invoke(f.Acts);
        var book = new ErrandBook(new[] { new Good("tea", "some tea", "Shop", 5) },
            new[] { new Counter("Shop", new Tile(2, 3), 0, Day, shut ?? Array.Empty<int>()) },
            new Dictionary<string, IReadOnlyList<string>> { ["Ann"] = new[] { "tea" } },
            new Dictionary<string, IReadOnlyList<string>> { ["Bob"] = bobWont ?? Array.Empty<string>() });
        var purses = new Dictionary<string, double> { ["Ann"] = 1000, ["Bob"] = 1000, ["Cal"] = 1000 };
        return new TownData
        {
            Cast = new[] { Person("Ann", "Room", 3), Person("Bob", "Room", 5), Person("Cal", "Shop", 2, new Job("Shop", new Tile(2, 2), 0, Day, Array.Empty<int>(), 1)) },
            Places = new[]
            {
                new Location("Room", false, Enumerable.Repeat(new string('.', 12), 6).ToList()),
                new Location("Shop", false, Enumerable.Repeat(new string('.', 12), 6).ToList()),
            },
            Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(),
            Acts = ActCatalog.Errands.Concat(ActCatalog.Invitations).ToArray(),
            Feelings = f, Authority = new AuthorityOptions { ElectConstable = false, Keepers = new Dictionary<string, string> { ["Shop"] = "Cal" } },
            Economy = new Economy(purses, Array.Empty<(string, double, string?)>(), new Dictionary<string, double>(),
                new Dictionary<string, string>(), new Dictionary<string, (double, double)>(), 0, 0, book),
            Body = new BodyOptions { AwakeHoursAtRest = 100_000 }, Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
        };
    }

    [Fact]
    public void ErrandsAddTheirRowsAndTheSwitch()
    {
        string[] added = ActCatalog.Kinds(Options(true).Acts).Select(k => k.Name).Except(ActCatalog.Kinds(Options(false).Acts).Select(k => k.Name)).ToArray();
        Assert.Equal(new[] { "AskedFavour", "Fetched", "HandedOver", "LetDown", "Refused" }, added.Order(StringComparer.Ordinal));
        foreach (string kind in new[] { "AskedFavour", "Fetched", "HandedOver", "LetDown" })
            Assert.Equal("Errands", ActCatalog.SliceOf(kind));
        Assert.Contains("Errands", ActOptions.Switches);
        // With invitations on too, the refusal's row is there once.
        FeelingOptions both = Options(true);
        both.Acts.With("invitations");
        Assert.Single(ActCatalog.Kinds(both.Acts), k => k.Name == "Refused");
        Assert.All(ActCatalog.Errands, k => Assert.Equal(0, k.PerDay)); // only the errand's own rules begin them
    }

    [Fact]
    public void WithTheSwitchOffInWatchModeOrWithoutABookNothingRuns()
    {
        SimResult off = Run(7, errands: false, speech: false, days: 7);
        Assert.Empty(off.Needs);
        Assert.Empty(off.Favours);
        Assert.Empty(off.Promises);
        Assert.DoesNotContain(off.Acts, a => a.Kind is "AskedFavour" or "Fetched" or "HandedOver" or "LetDown");

        TownData watched = Shop(o => o.Watch = true);
        Assert.Empty(new Simulation(1, watched).Run(3).Needs);
        TownData bookless = Shop() with { Economy = Shop().Economy! with { Errands = null } };
        Assert.Empty(new Simulation(1, bookless).Run(3).Needs);
    }

    [Fact]
    public void ErrandsNeedTheirRows()
    {
        TownData town = Shop();
        Assert.Throws<ArgumentException>(() => new Simulation(1, town with { Acts = ActCatalog.Errands.ToArray() })); // no refusal row
    }

    [Fact]
    public void ANeedIsAskedPromisedFetchedAndHandedOver()
    {
        SimResult r = new Simulation(1, Shop()).Run(1);
        Need need = Assert.Single(r.Needs);
        Assert.Equal(("Ann", "tea", NeedState.Met), (need.Holder, need.Item, need.State));
        Assert.InRange(Clock.OfDay(need.Tick), Clock.At(8), Clock.At(20)); // noticed in waking hours
        Favour asked = Assert.Single(r.Favours);
        Assert.Equal(("Ann", "Bob", "tea", true), (asked.Asker, asked.Asked, asked.Item, asked.Accepted));
        Promise p = Assert.Single(r.Promises);
        Assert.Equal((PromiseKind.Errand, "Bob", "Ann", "tea", PromiseState.Kept, false), (p.Kind, p.By, p.To, p.Item, p.State, p.Late));
        Assert.Equal(need.Promise, p.Id);
        Assert.Equal(("AskedFavour", "Ann", "Bob"), (r.Acts[p.MadeAct].Kind, r.Acts[p.MadeAct].Actor, r.Acts[p.MadeAct].Target));
        Assert.Equal(("Fetched", "Bob", "Cal", "Shop"), (r.Acts[p.FetchedAct].Kind, r.Acts[p.FetchedAct].Actor, r.Acts[p.FetchedAct].Target, r.Acts[p.FetchedAct].Location));
        Act handed = r.Acts[p.SettledAct];
        Assert.Equal(("HandedOver", "Bob", "Ann", p.MadeAct), (handed.Kind, handed.Actor, handed.Target, handed.About));
        Assert.True(p.MadeAct < p.FetchedAct && p.FetchedAct < p.SettledAct);
        // Bob paid Cal for it and Ann paid Bob back: Bob is even, Ann is down by the price, Cal up by
        // it less what restocking sent out of town; and the town's money changed only by that.
        Assert.Equal(r.Purses["Bob"], r.Purses["Ann"] + 5, 6);
        Assert.Equal(1000 - 5, r.Purses["Ann"] - (r.Purses["Bob"] - 1000), 6);
        Assert.Equal(r.TownCash[^1] - r.TownCash[0], r.OutsideIn - r.OutsideOut, 6);
        // Ann was handed it face to face, and felt the kindness toward Bob.
        Assert.Contains(r.Feelings, x => x.Holder == "Ann" && x.ActId == handed.Id && x.Toward == "Bob" && x.Change > 0);
    }

    [Fact]
    public void ShutTheDayItIsDueItIsHandedOverLate()
    {
        SimResult r = new Simulation(1, Shop(o => o.ErrandDays = 0, shut: new[] { 0 })).Run(2);
        Promise p = r.Promises[0];
        Assert.Equal((PromiseState.Kept, true), (p.State, p.Late));
        Assert.Equal(1, Clock.Day(r.Acts[p.FetchedAct].Tick)); // bought on Tuesday, when it opened
        Assert.True(p.Settled >= p.DueTo && p.Settled < p.DueTo + Day);
    }

    [Fact]
    public void NeverOpenThePromiseBreaksAndTheOneLetDownKnows()
    {
        SimResult r = new Simulation(1, Shop(shut: Enumerable.Range(0, 7).ToArray())).Run(5);
        Promise p = r.Promises[0];
        Assert.Equal((PromiseState.Broken, -1), (p.State, p.FetchedAct));
        Assert.Equal(p.DueTo + Day, p.Settled); // ErrandGraceDays after it was due
        Act letDown = r.Acts[p.SettledAct];
        Assert.Equal(("LetDown", "Bob", "Ann", p.MadeAct), (letDown.Kind, letDown.Actor, letDown.Target, letDown.About));
        Assert.Equal(0, r.Witnesses[letDown.Id]); // nobody saw it happen: it is a thing that didn't
        Assert.Contains(r.Log, l => l.Contains($" belief Ann {letDown.Id} Bob Witnessed", StringComparison.Ordinal));
        Assert.Contains(r.Feelings, x => x.Holder == "Ann" && x.ActId == letDown.Id && x.Toward == "Bob" && x.Change < 0);
        Assert.Equal(NeedState.Lapsed, r.Needs[0].State);
        Assert.DoesNotContain(r.Log, l => l.Contains(" errand-bought ", StringComparison.Ordinal));
        Assert.Equal(r.Purses["Ann"], r.Purses["Bob"], 6); // no money moved
    }

    [Fact]
    public void WhatSomeoneWontFetchIsANoHoweverObligedTheyAre()
    {
        // Bob likes Ann well enough that asking is a clear yes; told he won't fetch tea, it is a no all
        // the same, and the record keeps how obliged he was (Sid, 2026-10-13: Penny and her mother).
        SimResult willing = new Simulation(1, Shop()).Run(1), unwilling = new Simulation(1, Shop(bobWont: new[] { "tea" })).Run(1);
        Favour yes = willing.Favours[0], no = unwilling.Favours[0];
        Assert.Equal((true, false), (yes.Accepted, yes.WontFetch));
        Assert.Equal((false, true), (no.Accepted, no.WontFetch));
        Assert.Equal(yes.Margin, no.Margin);
        Assert.True(no.Margin > new FeelingOptions().ClearBand, $"margin {no.Margin}");
        Assert.Equal(("Refused", "Bob", "Ann"), (unwilling.Acts[no.ActId].Kind, unwilling.Acts[no.ActId].Actor, unwilling.Acts[no.ActId].Target!));
        Assert.Contains(unwilling.Log, l => l.Contains($" ask Bob Errand Ann act {no.ActId} no ", StringComparison.Ordinal));
        Assert.Empty(unwilling.Promises);
        Assert.Equal(Math.Round(AskMath.Eagerness(new Temperament(0.5, 0.5, 0.5, 0.5)), 4), no.Eager);
    }

    [Fact]
    public void AfterANoTheAskerWaitsAndThenAsksAgain()
    {
        // Bob won't fetch tea, so every asking is a no; Ann waits ErrandAskAgainDays.
        SimResult r = new Simulation(1, Shop(o => (o.NeedDays, o.ErrandAskAgainDays) = (9, 3), bobWont: new[] { "tea" })).Run(9);
        int[] days = r.Favours.Select(f => Clock.Day(f.Tick)).ToArray();
        Assert.True(days.Length >= 2, $"asked {days.Length} times");
        Assert.All(r.Favours, f => Assert.False(f.Accepted));
        Assert.All(days.Zip(days.Skip(1)), d => Assert.True(d.Second - d.First >= 3, $"asked again after {d.Second - d.First} days"));
        Assert.Equal(r.Favours.Count, r.Acts.Count(a => a.Kind == "Refused" && a.Actor == "Bob" && a.Target == "Ann"));
        Assert.Empty(r.Promises);
    }

    [Fact]
    public void InTheTownEveryChainHoldsTogether()
    {
        SimResult r = Month.Value;
        Assert.NotEmpty(r.Needs);
        Assert.Contains(r.Favours, f => f.Accepted);
        Assert.Contains(r.Favours, f => !f.Accepted);
        Assert.Contains(r.Promises, p => p.State == PromiseState.Kept);
        var age = DefaultTown.Cast().ToDictionary(v => v.Name, v => v.Age);
        var book = DefaultTown.Errands();
        foreach (Favour f in r.Favours)
        {
            Act act = r.Acts[f.ActId];
            Assert.Equal(f.Tick, act.Tick);
            Assert.Equal(f.Accepted ? ("AskedFavour", f.Asker, f.Asked) : ("Refused", f.Asked, f.Asker), (act.Kind, act.Actor, act.Target!));
            Assert.True(age[f.Asked] >= book.Good(f.Item).MinAge, $"{f.Asked} asked for {f.Item}");
            Assert.Contains(f.Item, book.Uses[f.Asker]);
        }
        foreach (Promise p in r.Promises)
        {
            Need need = Assert.Single(r.Needs, n => n.Promise == p.Id);
            Assert.Equal((need.Holder, need.Item), (p.To, p.Item));
            Assert.Equal(p.Amount, book.Good(p.Item!).Price);
            if (p.FetchedAct >= 0)
            {
                Act fetched = r.Acts[p.FetchedAct];
                Assert.Equal(("Fetched", p.By, p.Place), (fetched.Kind, fetched.Actor, fetched.Location));
                Assert.True(book.CounterFor(p.Place).OpenAt(fetched.Tick), $"bought at {fetched.Tick} with the sign saying shut");
            }
            switch (p.State)
            {
                case PromiseState.Kept:
                    Assert.Equal(("HandedOver", p.By, p.To, p.MadeAct), (r.Acts[p.SettledAct].Kind, r.Acts[p.SettledAct].Actor, r.Acts[p.SettledAct].Target!, r.Acts[p.SettledAct].About));
                    Assert.Equal(p.Settled >= p.DueTo, p.Late);
                    Assert.True(p.FetchedAct >= 0);
                    Assert.Equal(NeedState.Met, need.State);
                    break;
                case PromiseState.Broken:
                    Assert.Equal(("LetDown", p.By, p.To), (r.Acts[p.SettledAct].Kind, r.Acts[p.SettledAct].Actor, r.Acts[p.SettledAct].Target!));
                    Assert.Equal(NeedState.Lapsed, need.State);
                    break;
                default:
                    Assert.Equal(PromiseState.Open, p.State);
                    Assert.Equal(NeedState.Promised, need.State);
                    break;
            }
        }
        Assert.Equal(r.Promises.Count, r.Favours.Count(f => f.Accepted));
        Assert.Equal(r.Promises.Count(p => p.State == PromiseState.Kept), r.Acts.Count(a => a.Kind == "HandedOver"));
        Assert.Equal(r.Promises.Count(p => p.State == PromiseState.Broken), r.Acts.Count(a => a.Kind == "LetDown"));
        // Money stays in town: what the town holds changed by exactly what came in and went out.
        Assert.Equal(r.TownCash[^1] - r.TownCash[0], r.OutsideIn - r.OutsideOut, 6);
        Assert.DoesNotContain(r.Pursued, id => r.Acts[id].Kind is "AskedFavour" or "Fetched" or "HandedOver" or "LetDown"); // no gate begins them
    }

    [Fact]
    public void PennyNeverFetchesBeerNotEvenForHerMother()
    {
        Favour[] all = new long[] { 1, 2, 3, 5, 6, 7 }.SelectMany(seed => Run(seed, errands: true, speech: false, days: 28).Favours).ToArray();
        foreach (Favour f in all)
            Assert.Equal(f.Asked == "Penny" && f.Item == "beer", f.WontFetch);
        Favour[] beer = all.Where(f => f.WontFetch).ToArray();
        Assert.Contains(beer, f => f.Asker == "Pam"); // her mother does ask
        Assert.All(beer, f => Assert.False(f.Accepted));
        Assert.Contains(beer, f => f.Margin > new FeelingOptions().ClearBand); // and would have had a yes for anything else
        Assert.Contains(all, f => f.Asked == "Penny" && f.Accepted); // anything else, she does (Pam's only need is beer)
        Assert.Equal(new[] { "beer" }, DefaultTown.Errands().WontFetch["Penny"]);
    }

    [Fact]
    public void SpeechNamesTheThingAndAnswers()
    {
        SimResult r = Month.Value;
        var kindOf = SpeechCatalog.Default.Lines.ToDictionary(l => l.Id, l => l.Kind);
        var phrase = DefaultTown.Errands().Goods.ToDictionary(g => g.Name, g => g.Phrase);
        foreach (Favour f in r.Favours)
        {
            Speech[] said = r.Speech.Where(s => s.ActId == f.ActId).ToArray();
            Assert.Equal(2, said.Length);
            Assert.Equal((f.Asker, false, "AskedFavour"), (said[0].Speaker, said[0].Reply, kindOf[said[0].LineId]));
            Assert.Equal((f.Asked, true, f.Accepted ? "AskedFavour.Accepted" : "AskedFavour.Refused"), (said[1].Speaker, said[1].Reply, kindOf[said[1].LineId]));
            if (SpeechCatalog.Default.Lines.Single(l => l.Id == said[0].LineId).Text.Contains("{item}", StringComparison.Ordinal))
                Assert.Contains(phrase[f.Item], said[0].Line, StringComparison.Ordinal);
        }
        foreach (Promise p in r.Promises.Where(p => p.State == PromiseState.Kept))
        {
            Speech[] said = r.Speech.Where(s => s.ActId == p.SettledAct).ToArray();
            Assert.Equal(2, said.Length);
            Assert.Equal((p.By, p.Late ? "HandedOver.Late" : "HandedOver"), (said[0].Speaker, kindOf[said[0].LineId]));
            Assert.Equal((p.To, true, "HandedOver.Thanks"), (said[1].Speaker, said[1].Reply, kindOf[said[1].LineId]));
        }
        Assert.DoesNotContain(r.Speech, s => s.Line.Contains('{') || s.Line.Contains('}'));
        Assert.DoesNotContain(r.Speech, s => kindOf[s.LineId] == "Fetched");
    }

    [Fact]
    public void OnlyErrandLinesMayNameTheThing()
    {
        string Pack(string kind) => $$"""{"schema":1,"provenance":"test","lines":[{"id":"a","kind":"{{kind}}","archetypes":[],"relationship":[],"mood":[],"text":"Here is {item} for you, friend.","tags":[]}]}""";
        Assert.Single(SpeechCatalog.Read(Pack("HandedOver.Late")).Lines);
        Assert.Throws<InvalidDataException>(() => SpeechCatalog.Read(Pack("GaveGift")));
        var line = SpeechCatalog.Read(Pack("AskedFavour")).Lines[0];
        Assert.Equal("Here is some tea for you, friend.", SpeechCatalog.Render(line, "Ann", "some tea"));
        // Every errand kind has plain lines, so a speaker without an archetype always has something to say.
        foreach (string kind in new[] { "AskedFavour", "AskedFavour.Accepted", "AskedFavour.Refused", "HandedOver", "HandedOver.Late", "HandedOver.Thanks" })
            Assert.True(SpeechCatalog.Default.Lines.Count(l => l.Kind == kind && l.Archetypes.Count == 0) >= 3, kind);
    }

    [Fact]
    public void ARunIsTheSameTwiceAndSpeechChangesNothing()
    {
        SimResult a = Run(11, errands: true, speech: true, days: 7), b = Run(11, errands: true, speech: true, days: 7);
        Assert.Equal(a.Favours, b.Favours);
        Assert.Equal(a.Needs, b.Needs);
        Assert.Equal(a.Speech, b.Speech);
        Assert.Equal(Metrics.LogHash(a), Metrics.LogHash(Run(11, errands: true, speech: false, days: 7)));
    }

    [Fact]
    public void TheRecordingCarriesErrandsOnlyWhenTheyAreOn()
    {
        static System.Text.Json.JsonElement Record(bool on) => System.Text.Json.JsonDocument.Parse(Replay.Json(new ReplayOptions
        { Seed = 7, Days = 7, Feelings = Options(on), Speech = new SpeechOptions { Enabled = true } })).RootElement;
        System.Text.Json.JsonElement with = Record(true), without = Record(false);
        foreach (string table in new[] { "needs", "favours", "promises" })
            Assert.False(without.TryGetProperty(table, out _), table);
        Assert.Equal(new[] { "accepted", "actId", "asked", "asker", "bond", "busy", "eager", "item", "low", "margin", "running", "short", "tick", "tired", "wontFetch" },
            with.GetProperty("favours")[0].EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Errand", with.GetProperty("promises")[0].GetProperty("kind").GetString());
        Assert.Contains(with.GetProperty("needs").EnumerateArray(), n => n.GetProperty("state").GetString() is "Met" or "Promised" or "Open" or "Lapsed");
    }
}
