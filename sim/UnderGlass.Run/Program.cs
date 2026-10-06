using UnderGlass.Sim;

// Under Glass, phase 0a: run the gossip harness over many seeds and print the metrics.
//   dotnet run --project sim/UnderGlass.Run -- --seeds 200 --days 28            (natural acts)
//   dotnet run --project sim/UnderGlass.Run -- --seeds 200 --days 28 --inject   (one scandal placed per run)
//   dotnet run --project sim/UnderGlass.Run -- --log 7 --days 3                 (one seed's event log)
int seeds = 200, days = 28, from = 1;
long? logSeed = null;
bool inject = args.Contains("--inject");
var gossip = new GossipOptions();
for (int i = 0; i < args.Length - 1; i++)
{
    switch (args[i])
    {
        case "--seeds": seeds = int.Parse(args[i + 1]); break;
        case "--days": days = int.Parse(args[i + 1]); break;
        case "--from": from = int.Parse(args[i + 1]); break;
        case "--log": logSeed = long.Parse(args[i + 1]); break;
        case "--chat": gossip.ChatChance = double.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--retell": gossip.RetellFactor = double.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--every": gossip.ChatEveryMinutes = int.Parse(args[i + 1]); break;
        case "--fade": gossip.ScandalFadePerDay = double.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--tells": gossip.TellsPerDay = int.Parse(args[i + 1]); break;
    }
}

IReadOnlyList<ActKind> kinds = DefaultTown.Acts();
IReadOnlyList<(int, string, string)> Injected(long seed) => inject ? new[] { Harness.ScandalFor(seed, kinds) } : Array.Empty<(int, string, string)>();

if (logSeed is { } one)
{
    SimResult r = new Simulation(one, gossip: gossip, scheduled: Injected(one)).Run(days);
    foreach (string line in r.Log)
        Console.WriteLine($"{Clock.Format(int.Parse(line[..line.IndexOf(' ')]))} {line[(line.IndexOf(' ') + 1)..]}");
    Console.WriteLine($"hash {Metrics.LogHash(r)}");
    return;
}

var runs = Enumerable.Range(from, seeds).AsParallel().AsOrdered()
    .Select(s => new Simulation(s, gossip: gossip, scheduled: Injected(s)).Run(days)).ToList();
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
static bool PaidInFull(string l)
{
    var parts = l.Split(' ');
    int i = Array.IndexOf(parts, "paid");
    return i >= 0 && parts.Length > i + 4 && parts[i + 2] == parts[i + 4];
}
Console.WriteLine("reach: share of the town holding the story at the end; sat90: reached 90%+; band: 40-70% over 3+ days; died: never retold");
