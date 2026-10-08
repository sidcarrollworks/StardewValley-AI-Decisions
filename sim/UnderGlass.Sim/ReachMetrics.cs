namespace UnderGlass.Sim;

/// <summary>How exposed an act was as it began (batch 2 spec 5.4): Town, on a festival day or with
/// the town meeting on where it happened; Crowd, 5 or more witnesses or inside a gathering that is
/// on; Seen, 2 to 4 witnesses; Private, fewer.</summary>
public enum Exposure { Private, Seen, Crowd, Town }

/// <summary>Where a check's acts come from: scenarios placed in a scene, the harness's one placed
/// scandal a run (<see cref="Harness.ScandalFor"/>, as the runner's --inject), or the town's own
/// scandals with nothing placed.</summary>
public enum Placement { Scenarios, PlacedScandal, Natural }

/// <summary>A scenario check (batch 2 spec 5.5): its scene, how many runs of how many days, where
/// its acts come from, and its criterion. Scenarios gives a run's scenarios from its seed and the
/// town's kinds.</summary>
public sealed record Check(string Name, string Scene, int Seeds, int Days, Placement Placement, string Criterion,
    Func<long, IReadOnlyList<ActKind>, IReadOnlyList<Scenario>>? Scenarios = null);

/// <summary>A check's result: runs, the acts placed and counted, its numbers by name, whether it
/// passes (null when it is only reported), its criterion and a line to print.</summary>
public sealed record CheckResult(string Check, string Scene, int Runs, int Placed, int Counted,
    IReadOnlyDictionary<string, double> Values, bool? Pass, string Criterion, string Summary);

/// <summary>
/// Reach, measured against the people who knew the actor (batch 2 spec 5.4, slice m-0), and the
/// scenario checks that read it (5.5): Sid's answer that a scandal's reach depends on its context
/// replaces the fixed 40-70% band with a scene for each context and a criterion for each scene.
/// <list type="bullet">
/// <item>The circle (<see cref="SimResult.Circles"/>): everyone else who knew the actor at
/// familiarity <see cref="CircleAt"/> or more as the act's day began.</item>
/// <item>Holds by day d: their belief about the act was first got before day d ended (a belief's
/// GotTick is its first hearing, and later updates keep it). Heard: held, other than as a trace
/// found with no name.</item>
/// <item>CircleReach and CircleHeard: the circle's share holding it, or having heard it, by day d;
/// TownReach and TownHeard: the share of everyone else (HoldersByDay, HeardByDay).</item>
/// <item>Died: by the run's end no more had heard it than saw it. Grew: the last day it gained a
/// hearer, less its first day.</item>
/// </list>
/// Read from results only; no rule reads it.
/// </summary>
public static class ReachMetrics
{
    /// <summary>The familiarity toward the actor that puts someone in an act's circle.</summary>
    public const double CircleAt = 0.2;

    /// <summary>Circles smaller than this are counted apart from the medians.</summary>
    public const int SmallestCircle = 3;

    /// <summary>The checks m-0 can run today (the others need batch 2's levers or festivals).</summary>
    public static readonly IReadOnlyList<string> Today = new[] { "C2", "C3", "C4", "C6", "C10", "C11", "C13" };

    private static bool Holds(SimResult r, string p, int actId, int day, bool heardOnly)
        => r.Beliefs.TryGetValue(p, out var held) && held.TryGetValue(actId, out Belief? b)
           && b.GotTick < (day + 1) * Clock.MinutesPerDay && !(heardOnly && b.Source == Source.Found && b.Actor is null);

    private static double CircleShare(SimResult r, Act a, int day, bool heardOnly)
    {
        if (!r.Circles.TryGetValue(a.Id, out var circle) || circle.Count == 0)
            return double.NaN;
        return circle.Count(p => Holds(r, p, a.Id, day, heardOnly)) / (double)circle.Count;
    }

    /// <summary>The circle's share holding the act by the end of day <paramref name="day"/>.</summary>
    public static double CircleReach(SimResult r, Act a, int day) => CircleShare(r, a, day, heardOnly: false);

    /// <summary>The circle's share that has heard of the act (seen or told, not only found) by the end of the day.</summary>
    public static double CircleHeard(SimResult r, Act a, int day) => CircleShare(r, a, day, heardOnly: true);

    /// <summary>The share of everyone else holding the act at the end of the day.</summary>
    public static double TownReach(SimResult r, Act a, int day) => r.HoldersByDay[a.Id][day] / (double)(r.CastSize - 1);

    /// <summary>The share of everyone else that has heard of the act at the end of the day.</summary>
    public static double TownHeard(SimResult r, Act a, int day) => r.HeardByDay[a.Id][day] / (double)(r.CastSize - 1);

    /// <summary>Never retold: at the run's end no more have heard it than saw it.</summary>
    public static bool Died(SimResult r, Act a) => r.HeardByDay[a.Id][^1] <= r.Witnesses.GetValueOrDefault(a.Id);

    /// <summary>The last day the act gained a hearer, less the day it happened.</summary>
    public static int Grew(SimResult r, Act a)
    {
        int[] byDay = r.HeardByDay[a.Id];
        int first = Clock.Day(a.Tick), last = first;
        for (int d = first + 1; d < byDay.Length; d++)
            if (byDay[d] > byDay[d - 1])
                last = d;
        return last - first;
    }

    /// <summary>How exposed the act was as it began (see <see cref="Exposure"/>).</summary>
    public static Exposure ExposureOf(SimResult r, Act a, IReadOnlyList<Gathering> gatherings)
    {
        var on = gatherings.Where(g => g.Place == a.Location && g.On(a.Tick)).ToList();
        if (Calendar.FestivalOn(Clock.Day(a.Tick)) is not null || on.Any(g => g.Name == "Meeting"))
            return Exposure.Town;
        int w = r.Witnesses.GetValueOrDefault(a.Id);
        if (w >= 5 || on.Any(g => a.At.Chebyshev(g.Center) <= g.Radius))
            return Exposure.Crowd;
        return w >= 2 ? Exposure.Seen : Exposure.Private;
    }

    /// <summary>Spearman's rank correlation, ties given their average rank; NaN under 3 pairs or
    /// with no spread.</summary>
    public static double Spearman(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        if (x.Count != y.Count)
            throw new ArgumentException("Spearman needs pairs");
        if (x.Count < 3)
            return double.NaN;
        double[] rx = Ranks(x), ry = Ranks(y);
        double mx = rx.Average(), my = ry.Average(), sxy = 0, sxx = 0, syy = 0;
        for (int i = 0; i < rx.Length; i++)
        {
            sxy += (rx[i] - mx) * (ry[i] - my);
            sxx += (rx[i] - mx) * (rx[i] - mx);
            syy += (ry[i] - my) * (ry[i] - my);
        }
        return sxx == 0 || syy == 0 ? double.NaN : sxy / Math.Sqrt(sxx * syy);
    }

    private static double[] Ranks(IReadOnlyList<double> v)
    {
        int[] order = Enumerable.Range(0, v.Count).OrderBy(i => v[i]).ThenBy(i => i).ToArray();
        var ranks = new double[v.Count];
        for (int i = 0; i < order.Length;)
        {
            int j = i;
            while (j + 1 < order.Length && v[order[j + 1]] == v[order[i]])
                j++;
            double rank = (i + j) / 2.0 + 1; // ties share their average rank
            for (int k = i; k <= j; k++)
                ranks[order[k]] = rank;
            i = j + 1;
        }
        return ranks;
    }

    /// <summary>The median, or NaN with none.</summary>
    public static double Median(IEnumerable<double> values)
    {
        var v = values.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
        return v.Count == 0 ? double.NaN : v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2;
    }

    /// <summary>The check called <paramref name="name"/> (any case), among <see cref="Today"/>.</summary>
    public static Check Named(string name) => name.ToUpperInvariant() switch
    {
        "C2" => new Check("C2", "RummagedInBin in the Square while Noon or the Market is on, inside it, 5+ within 8 tiles", 400, 14,
            Placement.Scenarios, "witnessed by 5+: median circle reach at the end 50-90%, and 25%+ of runs over 70%",
            (_, _) => new[] { new Scenario("C2", "RummagedInBin", Places: new[] { "Square" }, AtHub: true, MinInRange: 5) }),
        "C3" => new Check("C3", "RummagedInBin at the ClinicYard, exactly 1 within 8 tiles, a loner, no trace", 400, 14,
            Placement.Scenarios, "the circle has heard it at the end in 20% or less in 70%+ of runs; it dies in about half",
            (_, _) => new[] { new Scenario("C3", "RummagedInBin", Places: new[] { "ClinicYard" }, MinInRange: 1, MaxInRange: 1,
                Onlookers: Onlookers.Loners, NoTrace: true) }),
        "C4" => new Check("C4", "the placed scandal's kind with nobody within 8 tiles", 400, 14,
            Placement.Scenarios, "where found, median circle reach at the end 30% or less; the share never found reported",
            (seed, kinds) => new[] { new Scenario("C4", Harness.ScandalFor(seed, kinds).Kind, MaxInRange: 0) }),
        "C6" => new Check("C6", "Stole with only kin and housemates within 8 tiles, at least one", 400, 14,
            Placement.Scenarios, "nobody outside the household knows who did it a week on, in 95%+ of runs (knowing only that it happened is reported)",
            (_, _) => new[] { new Scenario("C6", "Stole", MinInRange: 1, Onlookers: Onlookers.KinOnly) }),
        "C10" => new Check("C10", "the placed scandal, as --inject places it", 400, 14,
            Placement.PlacedScandal, "witnesses rank with circle reach at the end: Spearman 0.5+"),
        "C11" => new Check("C11", "the town's own scandals, nothing placed", 200, 112,
            Placement.Natural, "circle reach 14 days on: median 40-70%, 10%+ at 90%+, 15%+ under 20%"),
        "C13" => new Check("C13", "the placed scandal, as --inject places it, when witnessed", 400, 14,
            Placement.PlacedScandal, "heard by 40-70% of the town and growing 3+ days, in 52%+ at 26"),
        _ => throw new ArgumentException($"no check {name}: the checks are {string.Join(", ", Today)}"),
    };

    /// <summary>The acts a check measures in a run.</summary>
    public static IEnumerable<Act> Measured(Check check, SimResult r, IReadOnlyList<ActKind> kinds) => check.Placement switch
    {
        Placement.Scenarios => r.Acts.Where(a => r.Scenarios.TryGetValue(a.Id, out string? s) && s == check.Name),
        Placement.PlacedScandal => r.Acts.Where(a => a.Injected && !r.Scenarios.ContainsKey(a.Id)),
        _ => r.Acts.Where(a => !a.Injected && kinds.First(k => k.Name == a.Kind).IsScandal),
    };

    /// <summary>Works out a check over its runs (each run made as the check says). The town's cast
    /// gives households (C6); its gatherings, exposure (C11).</summary>
    public static CheckResult Evaluate(Check check, IReadOnlyList<SimResult> runs, IReadOnlyList<ActKind> kinds,
        IReadOnlyList<Villager> cast, IReadOnlyList<Gathering> gatherings)
    {
        var acts = runs.SelectMany(r => Measured(check, r, kinds).Select(a => (R: r, A: a))).ToList();
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        int counted;
        bool? pass;
        string summary;
        static string Pc(double x) => double.IsNaN(x) ? "n/a" : x.ToString("P0", System.Globalization.CultureInfo.InvariantCulture);
        static double Share<T>(IReadOnlyCollection<T> xs, Func<T, bool> f) => xs.Count == 0 ? double.NaN : xs.Count(f) / (double)xs.Count;
        int Last((SimResult R, Act A) x) => x.R.Days - 1;
        switch (check.Name)
        {
            case "C2":
            {
                var crowd = acts.Where(x => x.R.Witnesses.GetValueOrDefault(x.A.Id) >= 5).Select(x => CircleReach(x.R, x.A, Last(x))).Where(v => !double.IsNaN(v)).ToList();
                counted = crowd.Count;
                values["median"] = Median(crowd);
                values["over70"] = Share(crowd, v => v > 0.7);
                values["witnessed5"] = Share(acts, x => x.R.Witnesses.GetValueOrDefault(x.A.Id) >= 5);
                pass = counted > 0 && values["median"] is >= 0.5 and <= 0.9 && values["over70"] >= 0.25;
                summary = $"witnessed by 5+ in {Pc(values["witnessed5"])} of placed runs; of those, circle reach at the end median {Pc(values["median"])}, over 70% in {Pc(values["over70"])}";
                break;
            }
            case "C3":
            {
                var heard = acts.Select(x => (Heard: CircleHeard(x.R, x.A, Last(x)), Died: Died(x.R, x.A), W: x.R.Witnesses.GetValueOrDefault(x.A.Id)))
                    .Where(x => !double.IsNaN(x.Heard)).ToList();
                counted = heard.Count;
                values["quiet"] = Share(heard, x => x.Heard <= 0.2);
                values["died"] = Share(heard, x => x.Died);
                values["median"] = Median(heard.Select(x => x.Heard));
                values["seen"] = Share(heard, x => x.W >= 1);
                pass = counted > 0 && values["quiet"] >= 0.7;
                summary = $"the one in range saw it in {Pc(values["seen"])}; the circle had heard it at the end in 20% or less in {Pc(values["quiet"])} of runs (median {Pc(values["median"])}); never retold in {Pc(values["died"])}";
                break;
            }
            case "C4":
            {
                var found = acts.Where(x => x.R.HoldersByDay[x.A.Id][^1] > 0).ToList();
                var reach = found.Select(x => CircleReach(x.R, x.A, Last(x))).Where(v => !double.IsNaN(v)).ToList();
                counted = reach.Count;
                values["found"] = Share(acts, x => x.R.HoldersByDay[x.A.Id][^1] > 0);
                values["median"] = Median(reach);
                pass = counted > 0 && values["median"] <= 0.3;
                summary = $"found in {Pc(values["found"])} of placed runs; where found, circle reach at the end median {Pc(values["median"])}";
                break;
            }
            case "C6":
            {
                // A leak names the culprit: rule 17's cover keeps who did it in the family, while a
                // trace found or a story with the wrong name says only that something happened.
                var household = cast.ToDictionary(v => v.Name, v => v.Household);
                var leaks = new List<string>();
                int kept = 0, unnamed = 0;
                foreach (var (r, a) in acts)
                {
                    int week = Math.Min(Clock.Day(a.Tick) + 7, r.Days - 1);
                    var outside = r.Beliefs.Where(p => household[p.Key] != household[a.Actor] && p.Value.TryGetValue(a.Id, out Belief? b)
                                                        && b.GotTick < (week + 1) * Clock.MinutesPerDay)
                        .Select(p => (Who: p.Key, B: p.Value[a.Id])).OrderBy(p => p.B.GotTick).ThenBy(p => p.Who, StringComparer.Ordinal).ToList();
                    var named = outside.Where(p => p.B.Actor == a.Actor).ToList();
                    if (named.Count == 0)
                    {
                        kept++;
                        unnamed += outside.Count > 0 ? 1 : 0;
                    }
                    else
                        leaks.Add($"{named[0].B.Source}{(named[0].B.Chain.Count > 0 ? " from " + string.Join(" < ", named[0].B.Chain) : "")}");
                }
                counted = acts.Count;
                values["kept"] = acts.Count == 0 ? double.NaN : kept / (double)acts.Count;
                values["unnamed"] = acts.Count == 0 ? double.NaN : unnamed / (double)acts.Count;
                values["placed"] = runs.Count == 0 ? double.NaN : acts.Count / (double)runs.Count;
                pass = counted > 0 && values["kept"] >= 0.95;
                summary = $"placed in {Pc(values["placed"])} of runs; nobody outside the household knew who a week on in {Pc(values["kept"])}"
                    + $" (in {Pc(values["unnamed"])} someone outside knew only that it happened)"
                    + (leaks.Count == 0 ? "" : "; the first outsider to know who: " + string.Join(", ", leaks.GroupBy(l => l).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(4).Select(g => $"{g.Key} ({g.Count()})")));
                break;
            }
            case "C10":
            {
                var pairs = acts.Select(x => (W: (double)x.R.Witnesses.GetValueOrDefault(x.A.Id), Reach: CircleReach(x.R, x.A, Last(x))))
                    .Where(x => !double.IsNaN(x.Reach)).ToList();
                counted = pairs.Count;
                values["spearman"] = Spearman(pairs.Select(p => p.W).ToList(), pairs.Select(p => p.Reach).ToList());
                pass = !double.IsNaN(values["spearman"]) && values["spearman"] >= 0.5;
                summary = $"Spearman of witnesses with circle reach at the end {values["spearman"]:0.00} over {counted} placed scandals";
                break;
            }
            case "C11":
            {
                var at14 = acts.Where(x => Clock.Day(x.A.Tick) + 14 <= Last(x))
                    .Select(x => (x.A.Kind, Exposure: ExposureOf(x.R, x.A, gatherings), Circle: x.R.Circles.GetValueOrDefault(x.A.Id)?.Count ?? 0,
                        Reach: CircleReach(x.R, x.A, Clock.Day(x.A.Tick) + 14))).ToList();
                var counts = at14.Where(x => x.Circle >= SmallestCircle && !double.IsNaN(x.Reach)).ToList();
                counted = counts.Count;
                values["median"] = Median(counts.Select(x => x.Reach));
                values["over90"] = Share(counts, x => x.Reach >= 0.9);
                values["under20"] = Share(counts, x => x.Reach < 0.2);
                values["smallCircles"] = at14.Count - counts.Count;
                pass = counted > 0 && values["median"] is >= 0.4 and <= 0.7 && values["over90"] >= 0.1 && values["under20"] >= 0.15;
                summary = $"{counted} scandals with a circle of {SmallestCircle}+ ({values["smallCircles"]:0} with less, apart): circle reach 14 days on median {Pc(values["median"])}, 90%+ {Pc(values["over90"])}, under 20% {Pc(values["under20"])}"
                    + "; by kind " + string.Join(", ", counts.GroupBy(x => x.Kind).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()} median {Pc(Median(g.Select(x => x.Reach)))}, under 20% {Pc(Share(g.ToList(), x => x.Reach < 0.2))}"))
                    + "; by exposure " + string.Join(", ", counts.GroupBy(x => x.Exposure).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()} median {Pc(Median(g.Select(x => x.Reach)))}"));
                break;
            }
            case "C13":
            {
                var seen = acts.Where(x => x.R.Witnesses.GetValueOrDefault(x.A.Id) >= 1).ToList();
                counted = seen.Count;
                values["band"] = Share(seen, x => TownHeard(x.R, x.A, Last(x)) is >= Metrics.BandLow and <= Metrics.BandHigh && Grew(x.R, x.A) >= 3);
                values["witnessed"] = Share(acts, x => x.R.Witnesses.GetValueOrDefault(x.A.Id) >= 1);
                pass = null; // reported, never gated (batch 2 spec, preface)
                summary = $"witnessed in {Pc(values["witnessed"])} of placed runs; of those, heard by 40-70% of the town and growing 3+ days in {Pc(values["band"])}";
                break;
            }
            default:
                throw new ArgumentException($"no measure for check {check.Name}");
        }
        return new CheckResult(check.Name, check.Scene, runs.Count, acts.Count, counted, values, pass, check.Criterion, summary);
    }
}
