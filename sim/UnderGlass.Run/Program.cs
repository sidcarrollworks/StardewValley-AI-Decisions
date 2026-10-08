using UnderGlass.Sim;

// Under Glass, phase 0a: run the gossip harness over many seeds and print the metrics.
//   dotnet run --project sim/UnderGlass.Run -- --seeds 200 --days 28            (natural acts)
//   dotnet run --project sim/UnderGlass.Run -- --seeds 200 --days 28 --inject   (one scandal placed per run)
//   dotnet run --project sim/UnderGlass.Run -- --log 7 --days 3                 (one seed's event log)
// Feelings (phase 0c): --feel off|observe|on (default: the town's own setting), --off <law>
// (sympathy, imitation, reconcile, kinds, freedom, presence, association, shame; repeatable),
// --plastic <x>, --target-base <x>, --fo <Name>=<value> for any other FeelingOptions knob, and
// --affect <Kind>=<joy>[,<plastic>] to change an act kind's feeling row (sweeps).
// The desire gate (phase 0d): --desire off|observe|on (observe: motives weighed, never acted on),
// --trait <Name>=<Trait>:<value> (repeatable) to set someone's character before the run, and
// --tensions <depth> to seed the starting tensions at -depth (0: none).
// Hermits, brawlers, moods that spread, missing people (phase 0d.6): --0d6 <steps> turns on those
// steps' switches (b-h, t the tone, m missing people; e.g. --0d6 bcd), --fo WithdrawalWatch=true
// watches them instead, and --acts <Name>=<Kind>:<weight> (repeatable) sets an act's weight on
// someone's card (e.g. Pam=Argued:0.5). A "withdrawal" block reports them (WithdrawalMetrics).
// Town growth (town spec T1): --forget <Name>=<value> sets forgetting (ForgettingOptions; on with FadePerDay > 0);
// --town <name> runs a grown town instead of the shipped one (pelican31: Towns.Named).
int seeds = 200, days = 28, from = 1;
long? logSeed = null;
bool inject = args.Contains("--inject");
var gossip = new GossipOptions();
// --town <name>: a grown town (Towns.Named) in place of the shipped one, its cast and options with it.
int townArg = Array.IndexOf(args, "--town");
TownData? town = townArg >= 0 && townArg + 1 < args.Length ? Towns.Named(args[townArg + 1]) : null;
IReadOnlyList<Villager> townCast = town?.Cast ?? DefaultTown.Cast();
var inv = System.Globalization.CultureInfo.InvariantCulture;
// Everything the runner prints reads the same on every machine, like the log (0c.0).
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.CurrentCulture = inv;
FeelingOptions feelings = town?.Feelings ?? DefaultTown.Feelings();
for (int i = 0; i < args.Length - 1; i++)
{
    switch (args[i])
    {
        case "--feel":
            feelings = args[i + 1] switch
            {
                "off" => FeelingOptions.Off,
                "observe" => Set(town?.Feelings ?? DefaultTown.Feelings(), "Steer", "false"),
                "on" => Set(town?.Feelings ?? DefaultTown.Feelings(), "Steer", "true"),
                _ => throw new ArgumentException("--feel off|observe|on"),
            };
            break;
        case "--seeds": seeds = int.Parse(args[i + 1]); break;
        case "--days": days = int.Parse(args[i + 1]); break;
        case "--from": from = int.Parse(args[i + 1]); break;
        case "--log": logSeed = long.Parse(args[i + 1]); break;
        case "--chat": gossip.ChatChance = double.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--retell": gossip.RetellFactor = double.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--every": gossip.ChatEveryMinutes = int.Parse(args[i + 1]); break;
        case "--fade": gossip.ScandalFadePerDay = double.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--tells": gossip.TellsPerDay = int.Parse(args[i + 1]); break;
        case "--forget": SetOption(gossip.Forgetting, args[i + 1]); break; // town spec E6: --forget FadePerDay=0.01
    }
}
// Applied after --feel, whatever the order on the line.
for (int i = 0; i < args.Length - 1; i++)
{
    switch (args[i])
    {
        case "--off": Set(feelings, char.ToUpperInvariant(args[i + 1][0]) + args[i + 1][1..], "false"); break;
        case "--plastic": feelings.PlasticScale = double.Parse(args[i + 1], inv); break;
        case "--target-base": feelings.TargetBase = double.Parse(args[i + 1], inv); break;
        case "--fo": Set(feelings, args[i + 1][..args[i + 1].IndexOf('=')], args[i + 1][(args[i + 1].IndexOf('=') + 1)..]); break;
        case "--desire":
            (feelings.Desire, feelings.DesireActs) = args[i + 1] switch
            {
                "off" => (false, true),
                "observe" => (true, false),
                "on" => (true, true),
                _ => throw new ArgumentException("--desire off|observe|on"),
            };
            break;
        case "--tensions": feelings.Start = DefaultTown.Tensions(double.Parse(args[i + 1], inv)); break;
        case "--0d6": feelings.With0d6(args[i + 1]); break;
    }
}
// --acts Name=Kind:weight, on a copy of the town's cast.
var castChanges = new List<(string Who, string Kind, double Weight)>();
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] != "--acts")
        continue;
    string spec = args[i + 1];
    int eq = spec.IndexOf('='), colon = spec.IndexOf(':');
    if (eq < 1 || colon < eq + 2)
        throw new ArgumentException("--acts <Name>=<Kind>:<weight>");
    string who = spec[..eq];
    if (!townCast.Any(v => v.Name == who))
        throw new ArgumentException($"--acts: nobody called {who}");
    castChanges.Add((who, spec[(eq + 1)..colon], double.Parse(spec[(colon + 1)..], inv)));
}
IReadOnlyList<Villager>? cast = castChanges.Count == 0 ? town?.Cast : townCast.Select(v =>
{
    var acts = new Dictionary<string, double>(v.Acts);
    foreach (var (who, kind, weight) in castChanges.Where(c => c.Who == v.Name))
        acts[kind] = weight;
    return v with { Acts = acts };
}).ToList();
// --trait Name=Trait:value, applied to each run's simulation before it starts.
var traits = new List<(string Who, Trait Trait, double Value)>();
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] != "--trait")
        continue;
    string spec = args[i + 1];
    int eq = spec.IndexOf('='), colon = spec.IndexOf(':');
    string? traitName = eq < 1 || colon < eq + 2 ? null
        : Enum.GetNames<Trait>().FirstOrDefault(n => string.Equals(n, spec[(eq + 1)..colon], StringComparison.OrdinalIgnoreCase));
    if (traitName is null) // a name only: Enum.TryParse would take "7" or "1" as a trait
        throw new ArgumentException("--trait <Name>=<Trait>:<value>, a trait one of " + string.Join(", ", Enum.GetNames<Trait>()));
    Trait trait = Enum.Parse<Trait>(traitName);
    string who = spec[..eq];
    if (!townCast.Any(v => v.Name == who))
        throw new ArgumentException($"--trait: nobody called {who}");
    traits.Add((who, trait, double.Parse(spec[(colon + 1)..], inv)));
}

// <Name>=<value> for any switch or number of an options object.
static void SetOption(object o, string kv)
{
    int eq = kv.IndexOf('=');
    var prop = eq < 1 ? null : o.GetType().GetProperty(kv[..eq]);
    if (prop is null)
        throw new ArgumentException($"{kv}: expected <Name>=<value>, a name one of " + string.Join(", ", o.GetType().GetProperties().Select(p => p.Name)));
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    string value = kv[(eq + 1)..];
    object v = prop.PropertyType == typeof(bool) ? (object)bool.Parse(value)
        : prop.PropertyType == typeof(int) ? (object)int.Parse(value, inv)
        : prop.PropertyType == typeof(double) ? (object)double.Parse(value, inv)
        : throw new ArgumentException($"{kv[..eq]} can't be set from the command line");
    prop.SetValue(o, v);
}

static FeelingOptions Set(FeelingOptions o, string name, string value)
{
    var prop = typeof(FeelingOptions).GetProperty(name) ?? throw new ArgumentException($"no feeling option {name}");
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    // Each branch boxed on its own: a bare conditional would widen an int to a double.
    object v = prop.PropertyType == typeof(bool) ? (object)bool.Parse(value)
        : prop.PropertyType == typeof(int) ? (object)int.Parse(value, inv)
        : prop.PropertyType == typeof(double) ? (object)double.Parse(value, inv)
        : throw new ArgumentException($"{name} can't be set from the command line");
    prop.SetValue(o, v);
    return o;
}

IReadOnlyList<ActKind> kinds = town?.Acts ?? DefaultTown.Acts();
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] != "--affect")
        continue;
    string name = args[i + 1][..args[i + 1].IndexOf('=')];
    double[] v = args[i + 1][(args[i + 1].IndexOf('=') + 1)..].Split(',').Select(x => double.Parse(x, inv)).ToArray();
    if (!kinds.Any(k => string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase) && k.Affect is not null))
        throw new ArgumentException($"--affect: no act kind {name} with a feeling row");
    kinds = kinds.Select(k => string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase) && k.Affect is { } a
        ? k with { Affect = a with { Joy = v[0], Plastic = v.Length > 1 ? v[1] : a.Plastic } }
        : k).ToList();
}
IReadOnlyList<(int, string, string)> Injected(long seed) => inject ? new[] { Harness.ScandalFor(seed, kinds) } : Array.Empty<(int, string, string)>();
Simulation Make(long seed, FeelingOptions o)
{
    var sim = town is null
        ? new Simulation(seed, cast: cast, kinds: kinds, gossip: gossip, scheduled: Injected(seed), feelings: o)
        : new Simulation(seed, town with { Cast = cast ?? town.Cast, Acts = kinds, Gossip = gossip, Feelings = o }, Injected(seed));
    foreach (var (who, trait, value) in traits)
        sim.SetTrait(who, trait, value);
    return sim;
}

if (logSeed is { } one)
{
    SimResult r = Make(one, feelings).Run(days);
    foreach (string line in r.Log)
        Console.WriteLine($"{Clock.Format(int.Parse(line[..line.IndexOf(' ')]))} {line[(line.IndexOf(' ') + 1)..]}");
    if (feelings.Enabled)
    {
        // For reading a seed (0c gate, check 7): the strongest feelings and what caused them.
        Console.WriteLine();
        Console.WriteLine("strongest sentiments at the end:");
        foreach (Sentiment s in r.Sentiments.OrderByDescending(s => s.Strength).ThenBy(s => s.Holder, StringComparer.Ordinal).Take(10))
        {
            if (s.ActId < 0) // a greeting's tone cites its minute, not an act
            {
                Console.WriteLine($"  {s.Holder} {s.Name} toward {s.Toward} {s.Strength:0.00} (x{s.Count}), since {Clock.Format(s.Since)}: a greeting at {Clock.Format(-2 - s.ActId)}");
                continue;
            }
            Act cause = r.Acts[s.ActId];
            Console.WriteLine($"  {s.Holder} {s.Name} toward {s.Toward} {s.Strength:0.00} (x{s.Count}), since {Clock.Format(s.Since)}: act {cause.Id} {cause.Kind} by {cause.Actor}{(cause.Target is { } t ? " to " + t : "")} at {cause.Location} {Clock.Format(cause.Tick)}");
        }
        Console.WriteLine("ties: " + (r.Ties.Count == 0 ? "none" : string.Join("; ", r.Ties.Select(t => $"d{t.Day} {t.What} {t.A}-{t.B}"))));
        Console.WriteLine("shop switches: " + (r.ShopSwitches.Count == 0 ? "none" : string.Join("; ", r.ShopSwitches.Select(s => $"d{s.Day} {s.Household} {s.From}->{s.To}"))));
        var moved = r.Regard.Select(p => (p.Key, R: p.Value, D: p.Value - r.Baseline[p.Key])).Where(x => Math.Abs(x.D) >= 0.05)
            .OrderBy(x => x.D).ThenBy(x => x.Key.From, StringComparer.Ordinal).ThenBy(x => x.Key.To, StringComparer.Ordinal).ToList();
        Console.WriteLine("regard moved 0.05 or more: " + (moved.Count == 0 ? "none" : string.Join(", ", moved.Select(x => $"{x.Key.From}->{x.Key.To} {x.R:0.00} ({x.D:+0.00;-0.00})"))));
    }
    Console.WriteLine($"hash {Metrics.LogHash(r)}");
    if (r.Daily.Count > 0)
    {
        // The shyest five's year (0d.6), after the hash and outside the log.
        foreach (ShyRow x in WithdrawalMetrics.Summarise(new[] { r }).Shyest)
            Console.WriteLine($"shy {x.Name} (boldness {x.Boldness:0.00}) by season: left out {string.Join(" ", x.LeftOut.Select(v => v.ToString("0.00", inv)))}; "
                + $"stance at the end {string.Join(" ", x.Stance.Select(S2))}; free hours out a day {string.Join(" ", x.HoursOut.Select(v => v.ToString("0.0", inv)))}");
        foreach (Spell s in WithdrawalMetrics.Spells(r, brawlers: false).Concat(WithdrawalMetrics.Spells(r, brawlers: true)))
            Console.WriteLine($"spell {s.Name} d{s.From}-d{s.To} {(s.Hermit ? "hermit" : WithdrawalMetrics.Spells(r, true).Contains(s) ? "brawler" : "withdrawn")}{(double.IsNaN(s.HoursFall) ? "" : $", hours out {-s.HoursFall:+0%;-0%}")}");
    }
    return;
}

var clock = System.Diagnostics.Stopwatch.StartNew();
var runs = Enumerable.Range(from, seeds).AsParallel().AsOrdered()
    .Select(s => Make(s, Copy(feelings)).Run(days)).ToList();
double runSeconds = clock.Elapsed.TotalSeconds;

// Each run gets its own options object: they are mutable, and runs go in parallel.
static FeelingOptions Copy(FeelingOptions o)
{
    var c = new FeelingOptions();
    foreach (var p in typeof(FeelingOptions).GetProperties().Where(p => p.CanWrite))
        p.SetValue(c, p.GetValue(o));
    return c;
}
RunStats stats = Metrics.Summarise(runs, kinds, injectedOnly: inject);
Console.WriteLine($"Under Glass 0a: {stats.Runs} seeds x {days} days, {runs[0].CastSize} villagers{(inject ? ", one injected scandal each" : "")}");
Console.WriteLine();
Console.WriteLine("group     acts  witn  reach  sat90  band  died  days  right  wrong  unknown");
foreach (GroupStats g in stats.Groups)
    Console.WriteLine($"{g.Group,-9} {g.Acts,5} {g.MeanWitnesses,5:0.0} {g.MeanReach,6:P0} {g.Saturated,6:P0} {g.InBand,5:P0} {g.Died,5:P0} {g.MeanDaysSpreading,5:0.0} {g.ActorRight,6:P0} {g.ActorWrong,6:P0} {g.ActorUnknown,8:P0}");
Console.WriteLine();
Console.WriteLine($"confrontations a season: {stats.ConfrontationsPerSeason:0.0}; at the right person: {stats.ConfrontationsRight:P0}; scandals confronted: {stats.ScandalsConfronted:P0}");
Console.WriteLine("natural acts a year, town-wide: " + string.Join(", ", stats.PerYear.Select(p => $"{p.Key.ToString().ToLowerInvariant()} {p.Value:0.#}")));
BodyStats b = stats.Body;
static string Hm(double h) => $"{(int)h % 24:00}:{(int)Math.Round(h % 1 * 60) % 60:00}";
Console.WriteLine($"sleep: bed {Hm(b.MeanBedtime)} (spread {b.BedtimeSpread:0.0} h), up {Hm(b.MeanWake)}, {b.MeanSleepHours:0.0} h a night; alarms slept through {b.MissedAlarmShare:P0}; late for work {b.LatePerSeason:0.#} a season; collapses {b.CollapsesPerSeason:0.#} a season");
{
    // Who knows whom at the end (town spec E6): the share of ordered pairs known at all (KnowsActorAt)
    // and known well (KnowsAt), and, with forgetting on, the ties lost from the start.
    var fam = runs.SelectMany(r => r.Familiarity.Values).ToList();
    if (fam.Count > 0)
        Console.WriteLine($"familiarity at the end: mean {fam.Average():0.000}; known ({gossip.KnowsActorAt:0.##}+) {fam.Count(f => f >= gossip.KnowsActorAt) / (double)fam.Count:P1}, "
            + $"well ({gossip.KnowsAt:0.##}+) {fam.Count(f => f >= gossip.KnowsAt) / (double)fam.Count:P1}"
            + (gossip.Forgetting.On ? $"; forgetting on ({gossip.Forgetting.FadePerDay:0.###} a day)" : "; forgetting off"));
}
if (inject)
{
    var placed = runs.SelectMany(r => r.Acts.Where(a => a.Injected).Select(a => (r, a))).ToList();
    var scenes = placed.Select(p => p.r.Scenes[p.a.Id]).ToList();
    Console.WriteLine($"around a placed scandal as it began: within 8 tiles {scenes.Average(s => s.InRange):0.00}, same place farther {scenes.Average(s => s.SamePlace):0.00}, elsewhere {scenes.Average(s => s.Elsewhere):0.0}, asleep {scenes.Average(s => s.Asleep):0.0}; nobody within 8 tiles {scenes.Count(s => s.InRange == 0) / (double)scenes.Count:P0}");
    var seen = placed.Where(p => p.r.Witnesses.GetValueOrDefault(p.a.Id) > 0).ToList();
    double Reach((SimResult r, Act a) p) => p.r.HoldersByDay[p.a.Id][^1] / (double)(p.r.CastSize - 1);
    int Days((SimResult r, Act a) p)
    {
        int[] byDay = p.r.HoldersByDay[p.a.Id];
        int first = Clock.Day(p.a.Tick), last = first;
        for (int d = first + 1; d < byDay.Length; d++) if (byDay[d] > byDay[d - 1]) last = d;
        return last - first;
    }
    bool InBand((SimResult r, Act a) p) => Reach(p) >= Metrics.BandLow && Reach(p) <= Metrics.BandHigh && Days(p) >= 3;
    Console.WriteLine($"witnessed by someone: {seen.Count / (double)placed.Count:P0} of placed scandals; of those, reach {seen.Average(Reach):P0}, in the band {seen.Count(InBand) / (double)seen.Count:P0}, reached 90%+ {seen.Count(p => Reach(p) >= 0.9) / (double)seen.Count:P0}, never retold {seen.Count(p => p.r.HoldersByDay[p.a.Id][^1] <= p.r.Witnesses[p.a.Id]) / (double)seen.Count:P0}");
    Console.WriteLine($"  witnessed: under 40% {seen.Count(p => Reach(p) < 0.4) / (double)seen.Count:P0}; 40-70% in under 3 days {seen.Count(p => Reach(p) >= 0.4 && Reach(p) <= 0.7 && Days(p) < 3) / (double)seen.Count:P0}; in band {seen.Count(InBand) / (double)seen.Count:P0}; over 70% {seen.Count(p => Reach(p) > 0.7) / (double)seen.Count:P0}; mean days growing {seen.Average(Days):0.0}");
    double Heard((SimResult r, Act a) p) => p.r.HeardByDay[p.a.Id][^1] / (double)(p.r.CastSize - 1);
    int HeardDays((SimResult r, Act a) p)
    {
        int[] byDay = p.r.HeardByDay[p.a.Id];
        int first = Clock.Day(p.a.Tick), last = first;
        for (int d = first + 1; d < byDay.Length; d++) if (byDay[d] > byDay[d - 1]) last = d;
        return last - first;
    }
    Console.WriteLine($"  witnessed, by sight and gossip only (not counting those who only found a trace): reach {seen.Average(Heard):P0}, in band {seen.Count(p => Heard(p) >= Metrics.BandLow && Heard(p) <= Metrics.BandHigh && HeardDays(p) >= 3) / (double)seen.Count:P0}, over 70% {seen.Count(p => Heard(p) > 0.7) / (double)seen.Count:P0}");
    foreach (var g in placed.GroupBy(p => p.a.Location).OrderBy(g => g.Key))
        Console.WriteLine($"  at {g.Key,-11} {g.Count(),4} runs, within 8 tiles {g.Average(p => p.r.Scenes[p.a.Id].InRange):0.00}, witnesses {g.Average(p => p.r.Witnesses.GetValueOrDefault(p.a.Id)):0.00}, reach {g.Average(p => p.r.HoldersByDay[p.a.Id][^1] / (double)(p.r.CastSize - 1)):P0}");
}
// The authority (design rule 16).
var constables = runs.Where(r => r.Constable is not null).GroupBy(r => r.Constable!).OrderByDescending(g => g.Count()).ThenBy(g => g.Key);
Console.WriteLine("constable voted in: " + string.Join(", ", constables.Select(g => $"{g.Key} {g.Count() / (double)runs.Count:P0}")));
var verdicts = runs.SelectMany(r => r.Verdicts.Select(v => (r, v))).Where(x => !inject || x.r.Acts[x.v.ActId].Injected).ToList();
var scandalActs = runs.SelectMany(r => r.Acts.Where(a => kinds.First(k => k.Name == a.Kind).IsScandal && (!inject || a.Injected)).Select(a => (r, a))).ToList();
if (scandalActs.Count > 0)
{
    bool Known((SimResult r, Act a) x) => x.r.Beliefs.Values.Any(h => h.ContainsKey(x.a.Id));
    bool FoundOnly((SimResult r, Act a) x) => x.r.Witnesses.GetValueOrDefault(x.a.Id) == 0 && x.r.Beliefs.Values.Any(h => h.TryGetValue(x.a.Id, out var b) && b.Source == Source.Found);
    bool Reported((SimResult r, Act a) x) => x.r.Accounts.Any(c => c.ActId == x.a.Id);
    double n = scandalActs.Count;
    Console.WriteLine($"{(inject ? "placed" : "natural")} scandals: known to anyone {scandalActs.Count(Known) / n:P0} (found from a trace only {scandalActs.Count(FoundOnly) / n:P0}); reported to the mayor {scandalActs.Count(Reported) / n:P0}; decided {verdicts.Count / n:P0}, of which right {verdicts.Count(x => x.v.Correct) / (double)Math.Max(1, verdicts.Count):P0}, let off {verdicts.Count(x => x.v.LetOff)}; warnings given {runs.Sum(r => r.Acts.Count(a => a.Kind == Authority.Warned))}, taken in {runs.Sum(r => r.Acts.Count(a => a.Kind == Authority.TakenIn))}");
}
if (scandalActs.Count > 0)
{
    // Piecing together "someone", and the constable's interviews.
    bool Suspected((SimResult r, Act a) x) => x.r.Beliefs.Values.Any(h => h.TryGetValue(x.a.Id, out var b) && b.Suspects is { Count: > 0 });
    bool CulpritSuspected((SimResult r, Act a) x) => x.r.Beliefs.Values.Any(h => h.TryGetValue(x.a.Id, out var b) && b.Suspects?.Contains(x.a.Actor) == true);
    var nameless = scandalActs.Where(x => !x.r.Beliefs.Values.Any(h => h.TryGetValue(x.a.Id, out var b) && b.Actor is not null && b.Source != Source.Told)).ToList();
    var asked = scandalActs.SelectMany(x => x.r.Interviews.Where(i => i.ActId == x.a.Id).Select(i => (x.a, i))).ToList();
    var solvedByInterview = verdicts.Count(x => x.r.Interviews.Any(i => i.ActId == x.v.ActId && i.Confessed));
    double n2 = scandalActs.Count;
    Console.WriteLine($"suspicion: someone suspected in {scandalActs.Count(Suspected) / n2:P0} of scandals, the culprit among the suspects in {scandalActs.Count(CulpritSuspected) / n2:P0}; nobody named the culprit first-hand in {nameless.Count / n2:P0}");
    Console.WriteLine($"interviews: {asked.Count / n2:0.0} a scandal, {asked.Count(q => q.i.Who != q.a.Actor) / (double)Math.Max(1, asked.Count):P0} of them innocent people; confessions {asked.Count(q => q.i.Confessed)}; verdicts after a confession {solvedByInterview}; wrong verdicts {verdicts.Count(x => !x.v.Correct)}");
}
// Families (design rule 17).
int Count(string kind) => runs.Sum(r => r.Acts.Count(a => a.Kind == kind));
int Logged(string word) => runs.Sum(r => r.Log.Count(l => l.Contains(word)));
Console.WriteLine($"families: rows {Count(Simulation.FamilyRow)}, kept in the family {Logged(" kept-in-family ")}, family alibis {Logged("; vouched for ")}; sibling squabbles {Count("Squabbled")}");
var late = runs.SelectMany(r => r.Acts.Where(a => a.Kind == Simulation.OutLate)).ToList();
double seasonsRun = runs.Sum(r => r.Days) / (double)Clock.DaysPerSeason;
Console.WriteLine($"acting normal: seen out at an odd hour {late.Count / seasonsRun:0.0} times a season; most often " + string.Join(", ", late.GroupBy(a => a.Actor).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Take(5).Select(g => $"{g.Key} {g.Count() / seasonsRun:0.0}")) + "; at hours " + string.Join(",", late.GroupBy(a => Clock.OfDay(a.Tick) / 60).OrderByDescending(g => g.Count()).Take(4).Select(g => $"{g.Key}:00")));
// Money (phase 0b).
if (runs[0].TownCash.Count > 0)
{
    double leak = runs.Max(r => Math.Abs(r.TownCash[^1] - r.TownCash[0] - (r.OutsideIn - r.OutsideOut)));
    Console.WriteLine($"money: town cash {runs.Average(r => r.TownCash[0]):0} -> {runs.Average(r => r.TownCash[^1]):0} g on average; in from outside {runs.Average(r => r.OutsideIn) / (days / 7.0):0} g a week, out {runs.Average(r => r.OutsideOut) / (days / 7.0):0}; largest unexplained change {leak:0.000} g");
    Console.WriteLine("  town cash by season end: " + string.Join(" -> ", Enumerable.Range(0, days / Clock.DaysPerSeason + 1)
        .Select(q => Math.Min(q * Clock.DaysPerSeason, runs[0].TownCash.Count - 1)).Select(i => $"{runs.Average(r => r.TownCash[i]):0}")));
    var households = runs[0].Purses.Keys.OrderBy(k => k, StringComparer.Ordinal);
    Console.WriteLine("  purses at the end: " + string.Join(", ", households.Select(h => $"{h} {runs.Average(r => r.Purses[h]):0}")));
    Console.WriteLine($"  households in debt at the end: {runs.Average(r => r.Purses.Count(p => p.Key != Simulation.Town && p.Value < 0)):0.0} a run");
    var motives = runs.SelectMany(r => r.Motives.Select(x => (r, x))).ToList();
    double years = runs.Sum(r => r.Days) / (Clock.DaysPerSeason * 4.0);
    Console.WriteLine($"tempted scandals: {motives.Count / years:0.0} a year; by motive " + string.Join(", ", motives.GroupBy(x => x.x.Motive.Split(' ')[0]).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count() / years:0.0}"))
        + "; by who " + string.Join(", ", motives.GroupBy(x => x.x.Who).OrderByDescending(g => g.Count()).Take(6).Select(g => $"{g.Key} {g.Count() / years:0.0}"))
        + "; kinds " + string.Join(", ", motives.GroupBy(x => x.r.Acts[x.x.ActId].Kind).Select(g => $"{g.Key} {g.Count() / years:0.0}")));
    Console.WriteLine($"ladder paid: fines paid in full {runs.Sum(r => r.Log.Count(l => l.Contains(" paid ") && PaidInFull(l)))}, part {runs.Sum(r => r.Log.Count(l => l.Contains(" paid ") && !PaidInFull(l)))}; service {runs.Sum(r => r.Acts.Count(a => a.Kind == Simulation.Service))}");
}
// A signed number to three places, with no "-0.000"; "none" for a mean over nothing.
static string Sg(double x) => double.IsNaN(x) ? "none"
    : (Math.Round(x, 3) + 0.0).ToString("+0.000;-0.000;0.000", System.Globalization.CultureInfo.InvariantCulture);
// A signed number to two places, with no "-0.00".
static string S2(double x) => double.IsNaN(x) ? "none" : (Math.Round(x, 2) + 0.0).ToString("+0.00;-0.00;0.00", System.Globalization.CultureInfo.InvariantCulture);
// A share, or "n/a" when there was nothing to share out.
static string Pc(double x) => double.IsNaN(x) ? "n/a" : x.ToString("P0", System.Globalization.CultureInfo.InvariantCulture);

static bool PaidInFull(string l)
{
    var parts = l.Split(' ');
    int i = Array.IndexOf(parts, "paid");
    return i >= 0 && parts.Length > i + 4 && parts[i + 2] == parts[i + 4];
}
// Feelings (phase 0c).
if (feelings.Enabled)
{
    FeelingStats f = FeelingMetrics.Summarise(runs, kinds, townCast, (town?.Economy ?? DefaultTown.TownEconomy()).GroceriesAt, feelings);
    Console.WriteLine($"feelings ({(feelings.Steer ? "steering" : "observed only")}{(feelings.PlasticScale != 1 ? $", plastic {feelings.PlasticScale}" : "")}): run time {runSeconds:0.0} s");
    Console.WriteLine($"  power of acting: mean {f.MeanPower:0.00} (spread {f.PowerSpread:0.00}), under 0.3 {f.LowPowerShare:P0}, over 0.7 {f.HighPowerShare:P0}; lowest " + string.Join(", ", f.LowestPower.Select(x => $"{x.Name} {x.Power:0.00}")));
    foreach (RegardSpread s in f.BySnapshot)
        Console.WriteLine($"  regard at d{s.Day}: mean change {Sg(s.MeanChange)}, p5 {s.P5:0.00} p50 {s.P50:0.00} p95 {s.P95:0.00}; under -0.2 {s.UnderMinus02:P1}, 0.4+ {s.AtLeast04:P1}, moved 0.1+ {s.Moved01:P1}; kin {s.KinMean:0.00}, others {s.NonKinMean:0.00}");
    Console.WriteLine($"  ties a year: feuds {f.FeudsPerYear:0.00}, in families {f.KinFeudsPerYear:0.00}, friendships {f.FriendshipsPerYear:0.00}, reconciliations {f.ReconciliationsPerYear:0.00}; seeds with a new feud and a new friendship {f.SeedsWithFeudAndFriendship:P0}; war towns {Pc(f.WarTowns)}, dead towns {Pc(f.DeadTowns)}{(double.IsNaN(f.DeadTowns) ? " (runs under a year)" : "")}");
    Console.WriteLine("  top feuds " + string.Join(", ", f.TopFeuds.Select(t => $"{t.A}-{t.B} {t.Seeds}")) + "; top friendships " + string.Join(", ", f.TopFriendships.Select(t => $"{t.A}-{t.B} {t.Seeds}")));
    Console.WriteLine("  sentiments a season: " + string.Join(", ", f.SentimentsPerSeason.Select(p => $"{p.Key} {p.Value:0.#}")) + $"; share of regard change with a sentiment {Pc(f.SentimentShare)}");
    Console.WriteLine($"  toward a culprit, mean change by how it was known: witnessed {Sg(f.DropWitnessed)}, saw and heard the name {Sg(f.DropHeardName)}, told twice {Sg(f.DropCorroborated)}, confirmed {Sg(f.DropConfirmed)}");
    Console.WriteLine($"  kin shame: {f.ShameStepsPerScandal:0.0} steps a scandal, kin regard change {Sg(f.KinDropPerScandal)} a scandal; named innocents questioned who resent a namer {Pc(f.InnocentsResentingNamer)}; wrongly confronted {Sg(f.WrongConfrontDrop)}; bystanders on the constable {Sg(f.BystanderRegardForConstable)}");
    Console.WriteLine($"  aimed acts: gifts and help to someone loved (0.4+) {Pc(f.GiftsToLoved)}; arguments with someone disliked {Pc(f.ArgumentsToDisliked)}, inside a household {Pc(f.ArgumentsInHouseholds)}; news {f.NewsPerYear:0} and trivia {f.TriviaPerYear:0} a year");
    Console.WriteLine($"  grievance thefts {f.GrievancePerYear:0.00} a year; shop switches {f.ShopSwitchesPerYear:0.00} a year; households at the chain by season " + string.Join(" -> ", f.HouseholdsAtChainBySeason.Select(x => $"{x:0.0}")));
    Console.WriteLine("  regard for kinds at the end: " + string.Join(", ", f.MeanKindRegard.Select(p => $"{p.Key} {Sg(p.Value)}")) + $"; for the newcomer {Sg(f.RegardTowardNewcomer)}, the newcomer's {Sg(f.RegardFromNewcomer)}");
}
// The desire gate (phase 0d; spec section 10).
if (feelings.Enabled && feelings.Steer && feelings.Desire)
{
    DesireStats g = DesireMetrics.Summarise(runs, townCast, feelings);
    static string Per(IEnumerable<(string Name, double PerYear)> xs) => xs.Any() ? string.Join(", ", xs.Select(x => $"{x.Name} {x.PerYear:0.0}")) : "none";
    Console.WriteLine($"desire ({(feelings.DesireActs ? "acting" : "watched only")}{(traits.Count > 0 ? ", " + string.Join(", ", traits.Select(t => $"{t.Who} {t.Trait} {t.Value}")) : "")}; tensions {(feelings.Start.Count == 0 ? "none" : string.Join(", ", feelings.Start.OrderBy(p => p.Key.From, StringComparer.Ordinal).ThenBy(p => p.Key.To, StringComparer.Ordinal).Select(p => $"{p.Key.From}->{p.Key.To} {p.Value:0.00}")))}), a year:");
    Console.WriteLine("  stirred: " + string.Join(", ", g.StirredPerYear.Select(p => $"{p.Key} {p.Value:0.0}")));
    Console.WriteLine("  weighed: " + string.Join("; ", g.WeighedPerYear.GroupBy(p => p.Key.Motive).Select(m => $"{m.Key} " + string.Join(" ", m.Select(p => $"{p.Key.Call} {p.Value:0.0}")))));
    Console.WriteLine("  acts by the gate: " + string.Join(", ", g.GateActsPerYear.Select(p => $"{p.Key} {p.Value:0.0}")) + "; at the town's rates: " + string.Join(", ", g.RateActsPerYear.Select(p => $"{p.Key} {p.Value:0.0}")));
    Console.WriteLine($"  across households: arguments {g.CrossHouseholdArgumentsPerYear:0.0}, answered in kind within " + string.Join(", ", DesireMetrics.AnswerDays.Select((d, k) => $"{d} d {Pc(g.AnsweredWithin[k])}")) + $"; kindness returned within 7 d {Pc(g.ReturnedWithin7)}; pairs arguing 2+ each way {g.PairsTwoEachWayPerRun:0.00} a run, 3+ {g.PairsThreeEachWayPerRun:0.00}");
    Console.WriteLine($"  keeping away: gave cause {g.GaveCausePerYear:0.0}, avoids {g.AvoidsPerYear:0.0} ({Per(g.TopAvoiders)}), withdrawals {g.WithdrawalsPerYear:0.0}, turned away {g.TurnedAwayPerYear:0.0}, snubs {g.SnubsPerYear:0.0}, marks {g.MarksPerYear:0.0}");
    Console.WriteLine($"  top gate arguers {Per(g.TopGateArguers)}; feuds involving {g.FeudConcentration.Name}: {Pc(g.FeudConcentration.Share)}");
    Console.WriteLine("  under -0.2 at season ends (across households / kin or home / across, not seeded): " + string.Join(", ", g.Dislike.Select(x => $"d{x.Day} {x.Across:P1}/{x.KinOrHome:P1}/{x.AcrossUnseeded:P1}")));
    Console.WriteLine("  outcomes across households: " + string.Join("; ", g.Outcomes.GroupBy(p => p.Key.Role).Select(r => $"{r.Key} " + string.Join(" ", r.Select(p => $"{p.Key.Outcome} {p.Value:P0}")))));
    Console.WriteLine("  stance at season ends (p10/p50/p90, hermits, brawlers a town): " + string.Join(", ", g.Stance.Select(x => $"d{x.Day} {S2(x.P10)}/{S2(x.P50)}/{S2(x.P90)} {x.Hermits:0.0} {x.Brawlers:0.0}")) + $"; ever a hermit {g.HermitsPerRun:0.0}, a brawler {g.BrawlersPerRun:0.0} a run");
    foreach (FringeRow? x in new[] { g.Boldest, g.Shyest })
        if (x is not null)
            Console.WriteLine($"  fringe {(x == g.Boldest ? "boldest" : "shyest")} {x.Name} (boldness {x.Boldness:0.00}): acts {x.ActsPerYear:0}, did {x.DidPerYear:0.0}, underwent {x.UndergonePerYear:0.0}, avoided {x.AvoidsPerYear:0.0}, withdrew {x.WithdrawalsPerYear:0.0}; free hours out a day {x.Season1HoursOut:0.0} in season 1, {x.Season4HoursOut:0.0} in season 4 (or the last); stance at the end {S2(x.EndStance)}");
    Console.WriteLine($"  town median of avoided and withdrew: {g.MedianAvoidedAndWithdrewPerYear:0.0} a person a year");
    var ends = runs[0].Stances.Keys.Select(n => (Name: n, End: runs.Average(r => r.Stances[n][^1]))).OrderBy(x => x.End).ThenBy(x => x.Name, StringComparer.Ordinal).ToList();
    Console.WriteLine("  stance at the end, mean: most withdrawn " + string.Join(", ", ends.Take(3).Select(x => $"{x.Name} {S2(x.End)}")) + "; most combative " + string.Join(", ", ends.AsEnumerable().Reverse().Take(3).Select(x => $"{x.Name} {S2(x.End)}")));

    // Hermits, brawlers, moods that spread (phase 0d.6; spec section 9).
    WithdrawalStats w = WithdrawalMetrics.Summarise(runs, cast ?? townCast);
    string[] on = new[] { ("b", feelings.HomeHurtOn || feelings.HouseholdGateOn), ("c", feelings.ContagionOn), ("d", feelings.LeftOutOn || feelings.InclusionDiscountOn),
        ("e", feelings.DialsOn), ("f", feelings.RecoveryOn), ("g", feelings.PatienceOn || feelings.CoercionOn), ("h", feelings.ShowOn), ("t", feelings.ToneOn), ("m", feelings.MissingOn) }
        .Where(x => x.Item2).Select(x => x.Item1).ToArray();
    Console.WriteLine($"withdrawal (0d.6 steps {(on.Length == 0 ? "none" : string.Join("", on))}{(feelings.WithdrawalWatch ? ", watched only" : "")}{(castChanges.Count > 0 ? ", acts " + string.Join(", ", castChanges.Select(c => $"{c.Who} {c.Kind} {c.Weight}")) : "")}):");
    Console.WriteLine("  left out by season (mean E, p90 of person means; kindness received, days with company a person-season; unanswered): "
        + string.Join(", ", w.BySeason.Select(s => $"s{s.Season + 1} {s.MeanE:0.00}/{s.P90E:0.00}; {s.KindIn:0.0}, {s.MetDays:0.0}; {Pc(s.Unanswered)}")));
    Console.WriteLine("  most left out (mean E): " + string.Join(", ", w.MostLeftOut.Select(x => $"{x.Name} {x.MeanE:0.00}")));
    Console.WriteLine($"  withdrawn (28 d at -0.5 or below) {w.WithdrawnPerYear:0.00} people a seed-year ({Per(w.TopWithdrawn)}); hermits (and hours out under 60% of their first season) {w.HermitsPerYear:0.00} ({w.HermitSpellsPerYear:0.00} spells; {Per(w.TopHermits)}), from the shyest third {Pc(w.HermitsFromShyestThird)}, seed-years with one {Pc(w.SeedYearsWithHermit)}, hours out fell {Pc(w.HermitHoursFall)}");
    Console.WriteLine($"  spells by person in {runs.Count} runs: hermits " + (w.HermitSpellsByPerson.Count == 0 ? "none" : string.Join(", ", w.HermitSpellsByPerson.Select(x => $"{x.Name} {x.Spells}")))
        + "; withdrawn " + (w.WithdrawnSpellsByPerson.Count == 0 ? "none" : string.Join(", ", w.WithdrawnSpellsByPerson.Select(x => $"{x.Name} {x.Spells}"))));
    Console.WriteLine($"  brawlers (28 d at +0.5 or above) {w.BrawlersPerYear:0.00} people a seed-year ({w.BrawlerSpellsPerYear:0.00} spells; {Per(w.TopBrawlers)}); arguments at home a year: by the gate {w.HomeArgumentsByGate:0.0}, at the town's rates {w.HomeArgumentsAtRates:0.0}");
    Console.WriteLine($"  gifts by the gate a year, by year of the run: {string.Join(", ", w.GateGiftsByYear.Select(x => x.ToString("0", inv)))}; on a birthday or a festival {w.OccasionGiftsPerYear:0.0}");
    Console.WriteLine($"  recovery: back above -0.3 within 28 d of a withdrawn spell's end {Pc(w.RecoveredWithin28)} of {w.RecoveryCases} (of a hermit's {Pc(w.HermitsRecoveredWithin28)} of {w.HermitRecoveryCases}); a hermit through a whole year in {Pc(w.YearLongHermits)} of seed-years (and {Pc(w.YearLongHermitsExcused)} still left out above 0.6)");
    Console.WriteLine($"  power of acting: mean {w.MeanPower:0.000}, spread {w.PowerSpread:0.000}, person-days below 0.35 {w.LowPowerShare:P1}, seed-years with a sink (28-day mean below 0.3) {Pc(w.SinkSeedYears)}");
    if (w.Contagion.Any(c => c.Gave > 0 || c.Caught > 0))
    {
        var pam = w.Contagion.FirstOrDefault(c => c.Name == "Pam");
        var penny = w.Contagion.FirstOrDefault(c => c.Name == "Penny");
        Console.WriteLine("  contagion a year, passed on: " + string.Join(", ", w.Contagion.Take(5).Select(c => $"{c.Name} {c.Gave:0.000}"))
            + "; taken: " + string.Join(", ", w.Contagion.OrderByDescending(c => c.Caught).ThenBy(c => c.Name, StringComparer.Ordinal).Take(5).Select(c => $"{c.Name} {c.Caught:0.000}"))
            + $"; Pam {pam.Gave:0.000}/{pam.Caught:0.000}, Penny {penny.Gave:0.000}/{penny.Caught:0.000}");
    }
    foreach (ShyRow x in w.Shyest)
        Console.WriteLine($"  shy {x.Name} ({x.Boldness:0.00}) by season: left out {string.Join(" ", x.LeftOut.Select(v => v.ToString("0.00", inv)))}; stance {string.Join(" ", x.Stance.Select(S2))}; free hours out {string.Join(" ", x.HoursOut.Select(v => v.ToString("0.0", inv)))}");
    if (w.Rules.Count > 0)
        Console.WriteLine($"  rules, a year (times, sum){(feelings.WithdrawalWatch ? ", watched only" : "")}: " + string.Join(", ", w.Rules.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key} {p.Value.PerYear:0.0} {Sg(p.Value.SumPerYear)}")));
}
Console.WriteLine("reach: share of the town holding the story at the end; sat90: reached 90%+; band: 40-70% over 3+ days; died: never retold");
