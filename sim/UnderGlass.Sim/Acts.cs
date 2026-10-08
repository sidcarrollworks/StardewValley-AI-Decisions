namespace UnderGlass.Sim;

/// <summary>
/// The gate's data for an act kind (rule 10; acts spec 2.1): its form cost and the least intensity
/// a motive needs to use it, the motives it may answer, and its limits. Light: a light act (never
/// retold, its own daily cap, never Ignored). NeedsCard: only those who carry the kind on their card
/// may use it. PrideWeight: the cost rises by this x (self-regard - 0.5). MinFamiliarity and
/// MinRegard: what the actor needs toward the target. MinAudience: how many others must be by.
/// Null on every shipped row: <see cref="Simulation.GateOf(ActKind, FeelingOptions)"/> builds their
/// gates from today's <see cref="FeelingOptions"/>.
/// </summary>
public sealed record ActGate(double Form, double Min, IReadOnlyList<DesireKind> Serves,
    bool Light = false, bool NeedsCard = false, double PrideWeight = 0,
    double MinFamiliarity = 0, double MinRegard = -1, int MinAudience = 0);

/// <summary>
/// The act catalog's switches and numbers (acts spec 2.3; Sid, 2026-10-08: "expand the amount of
/// actions a person can do"). Each switch turns on one slice of batch 1: its rows in
/// <see cref="ActCatalog"/> and the rules that go with them. All are off, and the town turns none
/// on, so the shipped town is the town before the catalog, byte for byte. Every switch except Late
/// acts only while the gate acts. The numbers are first guesses for the sweeps (acts spec 7).
/// Slice acts-0 builds the seams only: no switch has rows or rules yet.
/// </summary>
public sealed class ActOptions
{
    /// <summary>Every rule of the slices that are on works out what it would do and records it,
    /// and no act starts.</summary>
    public bool Watch { get; set; }
    /// <summary>acts-1: Thanked, Complimented and Joked; the rules for light kind acts; return in
    /// kind or less for the new rows.</summary>
    public bool Returns { get; set; }
    /// <summary>acts-2: PlayedGame and TreatedToDrink; the per-head draws; Fond's company acts.</summary>
    public bool Company { get; set; }
    /// <summary>acts-3: Welcomed and the Curious motive.</summary>
    public bool Welcome { get; set; }
    /// <summary>acts-4: Apologised, the Remorse motive and the apology's answer.</summary>
    public bool Repair { get; set; }
    /// <summary>acts-5: Mocked, StoodUpFor and Comforted; the Defend motive; Pity from hurts seen.</summary>
    public bool Sides { get; set; }
    /// <summary>acts-6: LateForWork. It can happen with feelings off, so it is off in every pinned run.</summary>
    public bool Late { get; set; }

    /// <summary>Light kind acts an actor may do in a day, counted apart from the light hostile cap.</summary>
    public int WarmPerDay { get; set; } = 3;
    /// <summary>Regard one ordered pair may gain in a day from light kind acts, all kinds together.</summary>
    public double WarmBudget { get; set; } = 0.01;
    /// <summary>Days after Fond is answered with a light kind act toward someone before it may be
    /// again (0: no wait). A small act doesn't reset missing someone (question 2, answer c), so
    /// without this Fond would compliment the same person every day they meet. 28 holds the gate's
    /// kindness over three years to 1.5 x year one (a sweep of 0, 3, 7, 14 and 28).</summary>
    public int FondWarmDays { get; set; } = 28;
    /// <summary>A warm act read cold is felt as -ColdShare x |joy|.</summary>
    public double ColdShare { get; set; } = 0.5;
    /// <summary>Familiarity below which someone is new (Curious).</summary>
    public double NewAt { get; set; } = 0.15;
    /// <summary>Familiarity gained both ways by Welcomed.</summary>
    public double WelcomeFamiliarity { get; set; } = 0.1;
    /// <summary>Curious intensity, x (0.5 + chattiness).</summary>
    public double CuriousBase { get; set; } = 0.25;
    /// <summary>The lowest regard for the target at which Remorse is felt.</summary>
    public double RemorseAt { get; set; }
    /// <summary>The share of the target's hurt the actor feels as Remorse.</summary>
    public double RemorseShare { get; set; } = 0.5;
    public int RemorseDays { get; set; } = 14;
    /// <summary>The lowest regard for the victim at which a witness wants to defend them.</summary>
    public double DefendAt { get; set; } = 0.4;
    public int DefendMinutes { get; set; } = 60;
    /// <summary>Regard given back by the first, second and third accepted apology for the same kind
    /// of act from the same person within ApologyDays (law 10's excuse, which wears out).</summary>
    public IReadOnlyList<double> ApologyExcuse { get; set; } = new[] { 0.5, 0.25, 0 };
    public int ApologyDays { get; set; } = 28;
    /// <summary>The cost of accepting an apology before any dislike: what it takes to let it go.</summary>
    public double ApologyCost { get; set; } = 0.4;
    /// <summary>Mood lost by an apologiser who is refused, x (0.5 + sensitivity).</summary>
    public double RefusedSting { get; set; } = 0.08;
    /// <summary>The share by which comfort cools the target's open Answer.</summary>
    public double ComfortCools { get; set; } = 0.25;

    /// <summary>The slices' switches by name, for the runner's --catalog.</summary>
    public static readonly IReadOnlyList<string> Switches = new[] { "Watch", "Returns", "Company", "Welcome", "Repair", "Sides", "Late" };

    /// <summary>Whether the slice with this switch name is on.</summary>
    public bool IsOn(string slice) => slice switch
    {
        nameof(Returns) => Returns,
        nameof(Company) => Company,
        nameof(Welcome) => Welcome,
        nameof(Repair) => Repair,
        nameof(Sides) => Sides,
        nameof(Late) => Late,
        _ => false,
    };

    /// <summary>A copy, for runs in parallel: options are mutable.</summary>
    public ActOptions Copy() => (ActOptions)MemberwiseClone();

    /// <summary>Turns on the switches named in a comma list, ignoring case (the runner's --catalog,
    /// for example "returns,company").</summary>
    public ActOptions With(string names)
    {
        foreach (string raw in names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string name = Switches.FirstOrDefault(s => string.Equals(s, raw, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"no catalog switch '{raw}': one of {string.Join(", ", Switches)}");
            typeof(ActOptions).GetProperty(name)!.SetValue(this, true);
        }
        return this;
    }
}

/// <summary>What the gate would have started from the catalog in watch mode (acts spec 2.3, 5):
/// at a minute, for a holder's motive toward a subject, the catalog row it would have used and
/// its margin. Nothing started.</summary>
public sealed record CatalogWatched(int Tick, string Holder, string Subject, DesireKind Motive, string Kind, double Margin);
