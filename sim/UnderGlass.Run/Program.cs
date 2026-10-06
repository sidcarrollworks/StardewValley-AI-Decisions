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
    foreach (var g in placed.GroupBy(p => p.a.Location).OrderBy(g => g.Key))
        Console.WriteLine($"  at {g.Key,-11} {g.Count(),4} runs, within 8 tiles {g.Average(p => p.r.Scenes[p.a.Id].InRange):0.00}, witnesses {g.Average(p => p.r.Witnesses.GetValueOrDefault(p.a.Id)):0.00}, reach {g.Average(p => p.r.HoldersByDay[p.a.Id][^1] / (double)(p.r.CastSize - 1)):P0}");
}
Console.WriteLine("reach: share of the town holding the story at the end; sat90: reached 90%+; band: 40-70% over 3+ days; died: never retold");
