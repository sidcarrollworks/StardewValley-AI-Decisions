namespace UnderGlass.Sim;

/// <summary>Being left out in one season of the runs: E's mean over person-days and the 90th
/// percentile of person-season means; kindness received and days with company a person-season;
/// and the share of one's own kindness left unanswered.</summary>
public sealed record SeasonLeftOut(int Season, double MeanE, double P90E, double KindIn, double MetDays, double Unanswered);

/// <summary>A sustained spell (spec X0): days From to To inclusive. Hermit: the free hours out over
/// its last 28 days fell below 60% of the person's own first season (else only withdrawn).</summary>
public sealed record Spell(string Name, int From, int To, bool Hermit, double HoursFall, bool Ended);

/// <summary>One of the shyest five: boldness at the start, and by season the mean E, the stance at
/// the season's end and free hours out a day (means over runs).</summary>
public sealed record ShyRow(string Name, double Boldness, double[] LeftOut, double[] Stance, double[] HoursOut);

/// <summary>Hermits, brawlers and moods that spread across many runs (phase 0d.6; spec section 9).
/// Per year means per 112 days of run. A share of nothing is NaN.</summary>
public sealed record WithdrawalStats(int Runs,
    IReadOnlyList<SeasonLeftOut> BySeason,
    double WithdrawnPerYear, double HermitsPerYear, double HermitSpellsPerYear, double HermitsFromShyestThird,
    IReadOnlyList<(string Name, double PerYear)> TopHermits, IReadOnlyList<(string Name, double PerYear)> TopWithdrawn,
    double SeedYearsWithHermit, double HermitHoursFall,
    double BrawlersPerYear, double BrawlerSpellsPerYear, IReadOnlyList<(string Name, double PerYear)> TopBrawlers,
    double RecoveredWithin28, int RecoveryCases, double YearLongHermits, double YearLongHermitsExcused,
    double HermitsRecoveredWithin28, int HermitRecoveryCases,
    double MeanPower, double PowerSpread, double LowPowerShare, double SinkSeedYears,
    IReadOnlyList<(string Name, double Gave, double Caught)> Contagion,
    IReadOnlyList<ShyRow> Shyest,
    IReadOnlyDictionary<string, (double PerYear, double SumPerYear)> Rules,
    IReadOnlyList<(string Name, double MeanE)> MostLeftOut,
    double HomeArgumentsByGate, double HomeArgumentsAtRates,
    IReadOnlyList<double> GateGiftsByYear, double OccasionGiftsPerYear,
    IReadOnlyList<(string Name, int Spells)> HermitSpellsByPerson, IReadOnlyList<(string Name, int Spells)> WithdrawnSpellsByPerson);

public static class WithdrawalMetrics
{
    public const int SpellDays = 28;
    public const double HermitAt = -0.5, BrawlerAt = 0.5, RecoveredAbove = -0.3;
    /// <summary>A hermit's free hours out over a spell's last 28 days are below this share of their first season.</summary>
    public const double HoursOutShare = 0.6;
    public const double LowPower = 0.35, SinkPower = 0.3;
    /// <summary>A hermit through a whole year is excused when their exclusion over its last 28 days is still above this.</summary>
    public const double ExcusedAbove = 0.6;
    public const int Year = Clock.DaysPerSeason * 4;

    /// <summary>The shyest third of a cast: lowest boldness at the start, ties by name.</summary>
    public static IReadOnlyList<string> ShyestThird(IReadOnlyDictionary<string, Temperament> start)
        => start.OrderBy(p => p.Value.Boldness).ThenBy(p => p.Key, StringComparer.Ordinal).Take((start.Count + 2) / 3).Select(p => p.Key).ToList();

    /// <summary>A person's mean free hours out a day over days [from, to].</summary>
    private static double Hours(PersonDays d, int from, int to)
    {
        double sum = 0;
        for (int k = from; k <= to; k++)
            sum += d.OutMinutes[k];
        return sum / 60.0 / Math.Max(1, to - from + 1);
    }

    /// <summary>The sustained spells in one run (spec X0): runs of 28 days or more at or below -0.5
    /// (withdrawn; a hermit if the hours out fell too) or at or above +0.5 (a brawler).</summary>
    public static IReadOnlyList<Spell> Spells(SimResult r, bool brawlers)
        => r.Names.Where(n => r.Stances.ContainsKey(n))
            .SelectMany(n => SpellsOf(n, r.Stances[n], r.Daily.GetValueOrDefault(n), brawlers)).ToList();

    /// <summary>One person's sustained spells, from their stance by day and their days.</summary>
    public static IReadOnlyList<Spell> SpellsOf(string name, double[] stance, PersonDays? days, bool brawlers)
    {
        var spells = new List<Spell>();
        if (stance.Length == 0)
            return spells;
        double first = days is null ? double.NaN : Hours(days, 0, Math.Min(Clock.DaysPerSeason, stance.Length) - 1);
        int start = -1;
        for (int k = 0; k <= stance.Length; k++)
        {
            bool inSpell = k < stance.Length && (brawlers ? stance[k] >= BrawlerAt : stance[k] <= HermitAt);
            if (inSpell && start < 0)
                start = k;
            if (inSpell || start < 0)
                continue;
            int end = k - 1;
            if (end - start + 1 >= SpellDays)
            {
                double fall = days is null || first <= 0 ? double.NaN : 1 - Hours(days, end - SpellDays + 1, end) / first;
                bool hermit = !brawlers && !double.IsNaN(fall) && 1 - fall < HoursOutShare;
                spells.Add(new Spell(name, start, end, hermit, fall, k < stance.Length));
            }
            start = -1;
        }
        return spells;
    }

    /// <param name="cast">The cast the runs used, for kin and households (default: the town's).</param>
    public static WithdrawalStats Summarise(IReadOnlyList<SimResult> runs, IReadOnlyList<Villager>? cast = null)
    {
        var byName = (cast ?? DefaultTown.Cast()).ToDictionary(v => v.Name);
        bool Close(string a, string b) => byName.TryGetValue(a, out Villager? va) && byName.TryGetValue(b, out Villager? vb)
                                          && (va.Household == vb.Household || va.KinOf(b) is not null || vb.KinOf(a) is not null);
        double years = Math.Max(1e-9, runs.Sum(r => r.Days) / (double)Year);
        static double Mean(IEnumerable<double> xs) { double s = 0; int n = 0; foreach (double x in xs) { s += x; n++; } return n == 0 ? double.NaN : s / n; }
        static double Pct(List<double> sorted, double p) => sorted.Count == 0 ? double.NaN : sorted[Math.Min(sorted.Count - 1, (int)(p * sorted.Count))];
        IReadOnlyList<(string, double)> Top(IEnumerable<string> names) => names.GroupBy(n => n)
            .Select(g => (g.Key, g.Count() / years)).OrderByDescending(x => x.Item2).ThenBy(x => x.Key, StringComparer.Ordinal).Take(5).ToList();

        // Being left out, by season.
        var bySeason = new List<SeasonLeftOut>();
        int length = runs.Count == 0 ? 0 : runs.Min(r => r.Days);
        for (int q = 0; q * Clock.DaysPerSeason < length; q++)
        {
            int from = q * Clock.DaysPerSeason, to = Math.Min(length, from + Clock.DaysPerSeason) - 1;
            var personMeans = new List<double>();
            double eSum = 0, kind = 0, met = 0;
            int eN = 0, personSeasons = 0, kindOut = 0, unanswered = 0;
            foreach (SimResult r in runs)
            {
                foreach (PersonDays d in r.Daily.Values)
                {
                    double pe = 0;
                    for (int k = from; k <= to; k++)
                    {
                        pe += d.LeftOut[k];
                        kind += d.KindIn[k];
                        met += d.Met[k] ? 1 : 0;
                        kindOut += d.KindOut[k];
                        unanswered += d.Unanswered[k];
                    }
                    eSum += pe;
                    eN += to - from + 1;
                    personMeans.Add(pe / (to - from + 1));
                    personSeasons++;
                }
            }
            personMeans.Sort();
            bySeason.Add(new SeasonLeftOut(q, eN == 0 ? double.NaN : eSum / eN, Pct(personMeans, 0.9),
                personSeasons == 0 ? double.NaN : kind / personSeasons, personSeasons == 0 ? double.NaN : met / personSeasons,
                kindOut == 0 ? double.NaN : unanswered / (double)kindOut));
        }

        // Sustained spells.
        var withdrawn = new List<(SimResult R, Spell S)>();
        var brawl = new List<(SimResult R, Spell S)>();
        foreach (SimResult r in runs)
        {
            withdrawn.AddRange(Spells(r, brawlers: false).Select(s => (r, s)));
            brawl.AddRange(Spells(r, brawlers: true).Select(s => (r, s)));
        }
        var hermits = withdrawn.Where(x => x.S.Hermit).ToList();
        double Distinct(IEnumerable<(SimResult R, Spell S)> xs) => xs.Select(x => (x.R, x.S.Name)).Distinct().Count() / years;
        static IReadOnlyList<(string, int)> ByPerson(IEnumerable<(SimResult R, Spell S)> xs) => xs.GroupBy(x => x.S.Name)
            .Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).ThenBy(x => x.Key, StringComparer.Ordinal).ToList();
        double fromShy = hermits.Count == 0 ? double.NaN
            : hermits.Count(x => ShyestThird(x.R.CharactersAtStart).Contains(x.S.Name)) / (double)hermits.Count;
        int seedYears = 0, withHermit = 0, yearLong = 0, excused = 0;
        foreach (SimResult r in runs)
        {
            var mine = hermits.Where(x => x.R == r).Select(x => x.S).ToList();
            for (int y = 0; (y + 1) * Year <= r.Days; y++)
            {
                seedYears++;
                int y0 = y * Year, y1 = y0 + Year - 1;
                if (mine.Any(s => s.From <= y1 && s.To >= y0))
                    withHermit++;
                foreach (Spell s in mine.Where(s => s.From <= y0 + Clock.DaysPerSeason && s.To >= y1))
                {
                    yearLong++;
                    double e = Mean(Enumerable.Range(y1 - SpellDays + 1, SpellDays).Select(k => r.Daily[s.Name].LeftOut[k]));
                    if (e > ExcusedAbove)
                        excused++;
                }
            }
        }
        // Recovery: of the withdrawn spells (and of the hermits') that end with 28 days of run left,
        // back above -0.3 within them.
        int cases = 0, recovered = 0, hermitCases = 0, hermitsRecovered = 0;
        foreach (var (r, s) in withdrawn.Where(x => x.S.Ended && x.S.To + SpellDays < x.R.Days))
        {
            cases++;
            hermitCases += s.Hermit ? 1 : 0;
            double[] st = r.Stances[s.Name];
            for (int k = s.To + 1; k <= s.To + SpellDays; k++)
                if (st[k] > RecoveredAbove) { recovered++; hermitsRecovered += s.Hermit ? 1 : 0; break; }
        }

        // The power of acting.
        var powers = runs.SelectMany(r => r.PowerByDay.Values.SelectMany(v => v)).ToList();
        double meanPower = Mean(powers);
        double spread = powers.Count > 1 ? Math.Sqrt(Mean(powers.Select(p => (p - meanPower) * (p - meanPower)))) : double.NaN;
        int sinkYears = 0, powerYears = 0;
        foreach (SimResult r in runs)
        {
            for (int y = 0; (y + 1) * Year <= r.Days; y++)
            {
                powerYears++;
                bool sink = false;
                foreach (double[] p in r.PowerByDay.Values)
                {
                    double sum = 0;
                    for (int k = y * Year; k < (y + 1) * Year && !sink; k++)
                    {
                        sum += p[k];
                        if (k - SpellDays >= y * Year)
                            sum -= p[k - SpellDays];
                        if (k - y * Year >= SpellDays - 1 && sum / SpellDays < SinkPower)
                            sink = true;
                    }
                }
                if (sink)
                    sinkYears++;
            }
        }

        // Contagion, and the shyest five.
        var contagion = runs.SelectMany(r => r.Contagion).GroupBy(p => p.Key)
            .Select(g => (Name: g.Key, Gave: g.Sum(p => p.Value.Gave) / years, Caught: g.Sum(p => p.Value.Caught) / years))
            .OrderByDescending(x => x.Gave).ThenBy(x => x.Name, StringComparer.Ordinal).ToList();
        var shyest = new List<ShyRow>();
        if (runs.Count > 0 && runs[0].CharactersAtStart.Count > 0 && runs[0].Daily.Count > 0)
        {
            int seasons = (length + Clock.DaysPerSeason - 1) / Clock.DaysPerSeason;
            foreach (var (name, c) in runs[0].CharactersAtStart.OrderBy(p => p.Value.Boldness).ThenBy(p => p.Key, StringComparer.Ordinal).Take(5))
            {
                double[] e = new double[seasons], st = new double[seasons], h = new double[seasons];
                for (int q = 0; q < seasons; q++)
                {
                    int from = q * Clock.DaysPerSeason, to = Math.Min(length, from + Clock.DaysPerSeason) - 1;
                    e[q] = Mean(runs.Select(r => Mean(Enumerable.Range(from, to - from + 1).Select(k => r.Daily[name].LeftOut[k]))));
                    st[q] = Mean(runs.Select(r => r.Stances[name][to]));
                    h[q] = Mean(runs.Select(r => Hours(r.Daily[name], from, to)));
                }
                shyest.Add(new ShyRow(name, c.Boldness, e, st, h));
            }
        }
        var watched = runs.SelectMany(r => r.Rules).GroupBy(p => p.Key)
            .ToDictionary(g => g.Key, g => (g.Sum(p => p.Value.Count) / years, g.Sum(p => p.Value.Sum) / years));
        var mostLeftOut = runs.SelectMany(r => r.Daily).GroupBy(p => p.Key)
            .Select(g => (Name: g.Key, MeanE: Mean(g.SelectMany(p => p.Value.LeftOut))))
            .OrderByDescending(x => x.MeanE).ThenBy(x => x.Name, StringComparer.Ordinal).Take(5).ToList();

        return new WithdrawalStats(runs.Count, bySeason,
            Distinct(withdrawn), Distinct(hermits), hermits.Count / years, fromShy,
            Top(hermits.Select(x => x.S.Name)), Top(withdrawn.Select(x => x.S.Name)),
            seedYears == 0 ? double.NaN : withHermit / (double)seedYears,
            hermits.Count == 0 ? double.NaN : Mean(hermits.Select(x => x.S.HoursFall)),
            Distinct(brawl), brawl.Count / years, Top(brawl.Select(x => x.S.Name)),
            cases == 0 ? double.NaN : recovered / (double)cases, cases,
            seedYears == 0 ? double.NaN : (yearLong - excused) / (double)seedYears,
            seedYears == 0 ? double.NaN : excused / (double)seedYears,
            hermitCases == 0 ? double.NaN : hermitsRecovered / (double)hermitCases, hermitCases,
            meanPower, spread, powers.Count == 0 ? double.NaN : powers.Count(p => p < LowPower) / (double)powers.Count,
            powerYears == 0 ? double.NaN : sinkYears / (double)powerYears,
            contagion, shyest, watched, mostLeftOut,
            runs.Sum(r => r.Acts.Count(a => a.Kind == "Argued" && a.Target is { } t && Close(a.Actor, t) && r.Pursued.Contains(a.Id))) / years,
            runs.Sum(r => r.Acts.Count(a => a.Kind == "Argued" && a.Target is { } t && Close(a.Actor, t) && !r.Pursued.Contains(a.Id) && !a.Injected)) / years,
            Enumerable.Range(0, Math.Max(0, length / Year)).Select(y => runs.Average(r => r.Pursued.Count(id => r.Acts[id].Kind == "GaveGift"
                && Clock.Day(r.Acts[id].Tick) / Year == y))).ToList(),
            runs.Sum(r => r.Pursued.Count(id => r.Acts[id] is { Kind: "GaveGift", Target: { } t } a && byName.TryGetValue(t, out Villager? v)
                && (Calendar.IsBirthday(v.Birthday, Clock.Day(a.Tick)) || Calendar.FestivalOn(Clock.Day(a.Tick)) is not null))) / years,
            ByPerson(hermits), ByPerson(withdrawn));
    }
}
