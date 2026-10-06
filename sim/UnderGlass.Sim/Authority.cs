namespace UnderGlass.Sim;

/// <summary>Knobs for the authority (design rule 16, decided 2026-10-06). First guesses.</summary>
public sealed class AuthorityOptions
{
    /// <summary>The mayor, who takes reports and decides. Null: no authority in this world.</summary>
    public string? Mayor { get; set; }
    /// <summary>A constable fixed in advance; null to hold the opening vote.</summary>
    public string? Constable { get; set; }
    public bool ElectConstable { get; set; } = true;
    /// <summary>When the opening vote is counted: noon on day 0, at the town meeting.</summary>
    public int VoteMinute { get; set; } = Clock.At(12);
    /// <summary>Only the bold stand for constable.</summary>
    public double StandAt { get; set; } = 0.5;
    /// <summary>Who keeps each place: they count its stock, notice what is missing, and report
    /// what happens there as the victim.</summary>
    public IReadOnlyDictionary<string, string> Keepers { get; set; } = new Dictionary<string, string>();
    /// <summary>Where the constable walks in free time (a spot per place), and when.</summary>
    public IReadOnlyDictionary<string, Tile> Patrol { get; set; } = new Dictionary<string, Tile>();
    public IReadOnlyList<(int From, int To)> PatrolHours { get; set; } =
        new[] { (Clock.At(10), Clock.At(12)), (Clock.At(19), Clock.At(21)) };
    public int PatrolStopMinutes { get; set; } = 45;
    /// <summary>A witness or finder reports with this chance plus per-boldness x boldness, decided
    /// once per scandal. Victims and the constable always report.</summary>
    public double ReportBase { get; set; } = 0.2;
    public double ReportPerBoldness { get; set; } = 0.6;
    /// <summary>The weight a name needs before the mayor acts, and its lead over the next name.</summary>
    public double VerdictWeight { get; set; } = 0.6;
    public double VerdictLead { get; set; } = 2;
    /// <summary>Hearsay counts for this share of a first-hand account.</summary>
    public double HearsayWeight { get; set; } = 0.5;
    /// <summary>Being seen nearby counts for this share, split over the names given. It adds to a
    /// case but never decides one alone: a verdict needs a direct account (a sighting, hearsay of
    /// one, or a confession) naming the accused.</summary>
    public double NearbyWeight { get; set; } = 0.25;
    /// <summary>A family alibi takes back this share of <see cref="NearbyWeight"/> for the kin it
    /// vouches for: the constable knows families cover (rule 17), so it counts for half, and it
    /// only offsets being seen nearby, never a sighting or a confession.</summary>
    public double AlibiWeight { get; set; } = 0.5;
    /// <summary>Questioned, the culprit confesses with this chance plus per-timidity x (1 -
    /// boldness), once per interview.</summary>
    public double ConfessBase { get; set; } = 0.25;
    public double ConfessPerTimidity { get; set; } = 0.5;
    /// <summary>The chance the mayor goes easy on someone close (familiarity at least SwayCloseAt,
    /// or the same household). Lewis is just and fair, so it is very small (Sid, 2026-10-06).</summary>
    public double SwayChance { get; set; } = 0.02;
    public double SwayCloseAt { get; set; } = 0.5;
    /// <summary>How long detention lasts, and where.</summary>
    public int DetainMinutes { get; set; } = 24 * 60;
    public string LockupPlace { get; set; } = "";
    public Tile LockupSpot { get; set; }
    /// <summary>Where community service is done, in public view.</summary>
    public string ServicePlace { get; set; } = "";
    public Tile ServiceSpot { get; set; }
    /// <summary>Verdicts already on record at the start of the run, by person.</summary>
    public IReadOnlyDictionary<string, int> Record { get; set; } = new Dictionary<string, int>();
}

/// <summary>The authority's rules that need no world (design rule 16). Pure.</summary>
public static class Authority
{
    /// <summary>The act kind for a warning from the mayor; its actor is the person warned.</summary>
    public const string Warned = "WarnedByMayor";
    /// <summary>The act kind for being taken in to be detained; its actor is the person taken.</summary>
    public const string TakenIn = "TakenIn";
    /// <summary>The act kind for being questioned by the constable; its actor is the person questioned.</summary>
    public const string Questioned = "Questioned";

    /// <summary>The ladder: a first verdict is a warning, a second restitution and a fine, a third
    /// service, and from the fourth, detention.</summary>
    public static Consequence StepFor(int priorVerdicts) => priorVerdicts switch
    {
        <= 0 => Consequence.Warning,
        1 => Consequence.RestitutionAndFine,
        2 => Consequence.Service,
        _ => Consequence.Detained,
    };

    /// <summary>
    /// How the mayor weighs what he has been told. Each account that names someone counts its
    /// confidence, times 1 if first-hand or <see cref="AuthorityOptions.HearsayWeight"/> if heard,
    /// times the mayor's trust in the teller; a confession counts in full, whoever makes it. Names
    /// seen nearby add <see cref="AuthorityOptions.NearbyWeight"/>, split over the names in that
    /// account, times trust. The leading name is the accused only if it reaches
    /// <see cref="AuthorityOptions.VerdictWeight"/>, leads the next by
    /// <see cref="AuthorityOptions.VerdictLead"/>, and has at least one direct account: being
    /// nearby never decides a case alone. "Someone" counts for nobody.
    /// </summary>
    public static (string? Accused, double Weight, double Next) Weigh(IEnumerable<Account> accounts,
        Func<string, double> trust, AuthorityOptions o)
    {
        var totals = new SortedDictionary<string, double>(StringComparer.Ordinal);
        var near = new Dictionary<string, double>(StringComparer.Ordinal);
        var direct = new HashSet<string>(StringComparer.Ordinal);
        foreach (Account a in accounts)
        {
            if (a.Actor is { } who)
            {
                direct.Add(who);
                double w = who == a.From ? a.Confidence : a.Confidence * (a.FirstHand ? 1 : o.HearsayWeight) * trust(a.From);
                totals[who] = totals.GetValueOrDefault(who) + w;
                continue;
            }
            if (a.Nearby is { Count: > 0 } nearby)
                foreach (string n in nearby)
                    near[n] = near.GetValueOrDefault(n) + o.NearbyWeight / nearby.Count * trust(a.From);
            foreach (string n in a.Alibi ?? Array.Empty<string>())
                near[n] = near.GetValueOrDefault(n) - o.NearbyWeight * o.AlibiWeight * trust(a.From);
        }
        foreach (var (n, w) in near)
            if (w > 0)
                totals[n] = totals.GetValueOrDefault(n) + w;
        if (totals.Count == 0)
            return (null, 0, 0);
        var ranked = totals.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).ToList();
        double best = ranked[0].Value, next = ranked.Count > 1 ? ranked[1].Value : 0;
        bool decided = best >= o.VerdictWeight && best >= o.VerdictLead * next && direct.Contains(ranked[0].Key);
        return (decided ? ranked[0].Key : null, best, next);
    }

    /// <summary>Whether the mayor goes easy on the accused: only possible for someone close, and
    /// then with <see cref="AuthorityOptions.SwayChance"/>, by a seeded draw per case.</summary>
    public static bool Swayed(long seed, int actId, bool close, AuthorityOptions o)
        => close && Rng.Unit(seed, "sway", actId.ToString()) < o.SwayChance;
}
