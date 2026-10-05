using UnderGlass.Sim;

// Under Glass, phase 0a: run the gossip harness over many seeds and print the metrics.
//   dotnet run --project sim/UnderGlass.Run -- --seeds 200 --days 28            (natural acts)
//   dotnet run --project sim/UnderGlass.Run -- --seeds 200 --days 28 --inject   (one scandal placed per run)
//   dotnet run --project sim/UnderGlass.Run -- --log 7 --days 3                 (one seed's event log)
int seeds = 200, days = 28, from = 1;
long? logSeed = null;
bool inject = args.Contains("--inject");
for (int i = 0; i < args.Length - 1; i++)
{
    switch (args[i])
    {
        case "--seeds": seeds = int.Parse(args[i + 1]); break;
        case "--days": days = int.Parse(args[i + 1]); break;
        case "--from": from = int.Parse(args[i + 1]); break;
        case "--log": logSeed = long.Parse(args[i + 1]); break;
    }
}

IReadOnlyList<ActKind> kinds = DefaultTown.Acts();
IReadOnlyList<(int, string, string)> Injected(long seed) => inject ? new[] { Harness.ScandalFor(seed, kinds) } : Array.Empty<(int, string, string)>();

if (logSeed is { } one)
{
    SimResult r = new Simulation(one, scheduled: Injected(one)).Run(days);
    foreach (string line in r.Log)
        Console.WriteLine($"{Clock.Format(int.Parse(line[..line.IndexOf(' ')]))} {line[(line.IndexOf(' ') + 1)..]}");
    Console.WriteLine($"hash {Metrics.LogHash(r)}");
    return;
}

var runs = Enumerable.Range(from, seeds).AsParallel().AsOrdered()
    .Select(s => new Simulation(s, scheduled: Injected(s)).Run(days)).ToList();
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
Console.WriteLine("reach: share of the town holding the story at the end; sat90: reached 90%+; band: 40-70% over 3+ days; died: never retold");
