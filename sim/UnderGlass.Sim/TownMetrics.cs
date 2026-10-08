namespace UnderGlass.Sim;

/// <summary>One district of a grown town in the runs: its people, and per person a year its acts,
/// tellings heard, and feuds and friendships.</summary>
public sealed record DistrictStats(string District, int People, double ActsPerPerson, double HeardPerPerson, double FeudsPerPerson, double FriendshipsPerPerson);

/// <summary>
/// Is a grown town a bigger town or only a bigger crowd (town spec 6.3)? Tellings between people of
/// the same district against the share chance would give, each teller's listeners drawn evenly from
/// everyone else (the locality ratio: 1 is a crowd, higher is a town of neighbourhoods); how often a
/// story that began in a neighbourhood (its people's acts at home) stays in it on its first day, and
/// how often it crosses to another district within two; each district's acts, tellings heard, and
/// ties per person (a tie across two districts counts half in each); and how many people the median
/// person knows well (familiarity 0.4 or more) at the end (town spec 6.3: 15-45 at every size).
/// Districts: the core's people are "core"; a generated person's district is the neighbourhood their
/// front door opens onto. Read from the log and the results only.
/// </summary>
public sealed record TownStats(int Runs, IReadOnlyList<DistrictStats> Districts,
    double Tellings, double SameDistrict, double SameByChance, double LocalityRatio,
    int NeighbourhoodStories, double FirstDayLocal, double CrossedInTwoDays, double KnownWellMedian);

public static class TownMetrics
{
    /// <summary>Each person's district: "core" for the shipped town and the five, else the place their
    /// home's door opens onto.</summary>
    public static IReadOnlyDictionary<string, string> Districts(TownData town)
    {
        var core = Towns.Pelican31().Cast.Select(v => v.Name).ToHashSet(StringComparer.Ordinal);
        var district = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Villager v in town.Cast)
        {
            if (core.Contains(v.Name))
            {
                district[v.Name] = "core";
                continue;
            }
            Link? door = town.Links.FirstOrDefault(l => l.A == v.Home || l.B == v.Home);
            district[v.Name] = door is null ? "core" : door.A == v.Home ? door.B : door.A;
        }
        return district;
    }

    /// <summary>Each place's district: a neighbourhood's own place, and what hangs off it without
    /// passing through the core (its road, its shops, its homes), take its name; the rest are "core".</summary>
    public static IReadOnlyDictionary<string, string> PlaceDistricts(TownData town)
    {
        var core = Towns.Pelican31().Places.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var district = town.Places.ToDictionary(p => p.Name, _ => "core", StringComparer.Ordinal);
        var hoods = Districts(town).Values.Where(d => d != "core" && district.ContainsKey(d)).Distinct().OrderBy(d => d, StringComparer.Ordinal);
        foreach (string hood in hoods)
        {
            var queue = new Queue<string>();
            queue.Enqueue(hood);
            district[hood] = hood;
            while (queue.Count > 0)
            {
                string at = queue.Dequeue();
                foreach (Link l in town.Links)
                {
                    string? next = l.A == at ? l.B : l.B == at ? l.A : null;
                    if (next is null || core.Contains(next) || district[next] != "core")
                        continue;
                    district[next] = hood;
                    queue.Enqueue(next);
                }
            }
        }
        return district;
    }

    /// <summary>The share of tellings that would stay inside the teller's district by chance: each
    /// telling's listener drawn evenly from everyone but the teller, as in a crowd, averaged over the
    /// tellings there were. A district that talks more weighs more, and a silent one weighs nothing,
    /// so a well-mixed crowd scores 1 however unevenly its people talk.</summary>
    public static double ChanceSameDistrict(IReadOnlyList<(string Teller, string Listener)> tellings, IReadOnlyDictionary<string, string> district)
    {
        var size = district.Values.GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        int n = district.Count;
        if (tellings.Count == 0 || n < 2)
            return double.NaN;
        return tellings.Average(t => (size[district[t.Teller]] - 1) / (double)(n - 1));
    }

    public static TownStats Summarise(IReadOnlyList<SimResult> runs, TownData town)
    {
        var district = Districts(town);
        var placeDistrict = PlaceDistricts(town);
        var sizes = district.Values.GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        int n = district.Count;
        double years = Math.Max(1e-9, runs.Sum(r => r.Days) / (double)WithdrawalMetrics.Year);
        long tellings = 0, same = 0;
        var heard = new Dictionary<string, long>(StringComparer.Ordinal);
        var told = new List<(string, string)>();
        int stories = 0, local = 0, crossed = 0;
        foreach (SimResult r in runs)
        {
            foreach (string line in r.Log)
            {
                // "{minute} told {teller} {listener} {act}"
                int a = line.IndexOf(' ');
                if (a < 0 || string.CompareOrdinal(line, a + 1, "told ", 0, 5) != 0)
                    continue;
                string[] w = line.Split(' ');
                if (w.Length < 5 || !district.TryGetValue(w[2], out string? dt) || !district.TryGetValue(w[3], out string? dl))
                    continue;
                tellings++;
                if (dt == dl)
                    same++;
                heard[w[3]] = heard.GetValueOrDefault(w[3]) + 1;
                told.Add((w[2], w[3]));
            }
            // Stories that began in a neighbourhood: acts its people did at home (in a place of their
            // own district, not in the core's square or saloon). Where were they held on day one, and
            // did they reach another district within two days? Only stories that reached 3 or more.
            foreach (Act act in r.Acts)
            {
                if (!district.TryGetValue(act.Actor, out string? home) || home == "core" || placeDistrict.GetValueOrDefault(act.Location) != home)
                    continue;
                var holders = r.Beliefs.Where(b => b.Key != act.Actor && b.Value.ContainsKey(act.Id))
                    .Select(b => (Who: b.Key, At: b.Value[act.Id].GotTick)).ToList();
                if (holders.Count < 3)
                    continue;
                stories++;
                var firstDay = holders.Where(h => h.At < act.Tick + Clock.MinutesPerDay).ToList();
                if (firstDay.Count > 0 && firstDay.All(h => district.GetValueOrDefault(h.Who) == home))
                    local++;
                if (holders.Any(h => h.At < act.Tick + 2 * Clock.MinutesPerDay && district.GetValueOrDefault(h.Who) != home))
                    crossed++;
            }
        }
        // A tie inside a district counts whole there; a tie across two counts half in each, so the rows
        // add up to the town's.
        double Share(string a, string b, string d) => (district.GetValueOrDefault(a) == d ? 0.5 : 0) + (district.GetValueOrDefault(b) == d ? 0.5 : 0);
        var rows = sizes.OrderBy(s => s.Key == "core" ? 0 : 1).ThenBy(s => s.Key, StringComparer.Ordinal).Select(s =>
        {
            var people = district.Where(d => d.Value == s.Key).Select(d => d.Key).ToHashSet(StringComparer.Ordinal);
            double personYears = s.Value * years;
            int acts = runs.Sum(r => r.Acts.Count(x => people.Contains(x.Actor)));
            double feuds = runs.Sum(r => r.Ties.Where(t => t.What is "feud" or "kin-feud").Sum(t => Share(t.A, t.B, s.Key)));
            double friends = runs.Sum(r => r.Ties.Where(t => t.What == "friendship").Sum(t => Share(t.A, t.B, s.Key)));
            return new DistrictStats(s.Key, s.Value, acts / personYears, people.Sum(p => heard.GetValueOrDefault(p)) / personYears,
                feuds / personYears, friends / personYears);
        }).ToList();
        double sameShare = tellings == 0 ? double.NaN : same / (double)tellings;
        double byChance = ChanceSameDistrict(told, district);
        var knownWell = runs.Where(r => r.Familiarity.Count > 0)
            .SelectMany(r => r.Names.Select(a => r.Names.Count(b => b != a && r.Familiarity.TryGetValue((a, b), out double f) && f >= 0.4)))
            .OrderBy(x => x).ToList();
        double median = knownWell.Count == 0 ? double.NaN : knownWell[knownWell.Count / 2];
        return new TownStats(runs.Count, rows, tellings / Math.Max(1e-9, n * years), sameShare, byChance,
            byChance > 0 ? sameShare / byChance : double.NaN, stories,
            stories == 0 ? double.NaN : local / (double)stories, stories == 0 ? double.NaN : crossed / (double)stories, median);
    }
}
