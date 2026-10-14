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
/// Batch 2 (acts-batch2 spec 2.1) appends: Loud, heard through walls up to HearTiles without who
/// did it (rule 2); HeardStirs, hearsay about the holder stirs at corroboration strength (5b);
/// Going, reached by walking, to a door (a visit) or a person seen; Asked, the holder asks and on a
/// yes the person asked acts (a loan); KeeperOnly, only the place's keeper, toward a customer (a
/// ban); ToEnemy, exempt from "nobody is kind to someone they dislike"; Sized, joy scales with
/// Act.Amount over the debtor's weekly costs; MinExpression, the actor's expression must reach this.
public sealed record ActGate(double Form, double Min, IReadOnlyList<DesireKind> Serves,
    bool Light = false, bool NeedsCard = false, double PrideWeight = 0,
    double MinFamiliarity = 0, double MinRegard = -1, int MinAudience = 0,
    bool Loud = false, bool HeardStirs = false, Going Going = Going.None, bool Asked = false,
    bool KeeperOnly = false, bool ToEnemy = false, bool Sized = false, double MinExpression = 0);

/// <summary>How a gate act's actor reaches its target (acts-batch2 spec 2.5): in reach as today,
/// by walking to their home's door (a visit), or to where they are seen.</summary>
public enum Going { None, Door, Walk }

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
    /// <summary>Invitations (agreed order step 3; invitation spec): a game or a drink is asked for first,
    /// through the general Ask; a no is a Refused act the asker feels. Needs Company.</summary>
    public bool Invitations { get; set; }
    /// <summary>Errands (agreed order step 4; needs spec): people come to need one named thing, ask
    /// someone to fetch it, and the promise is kept, kept late or broken. Needs money and a town with
    /// an errands book.</summary>
    public bool Errands { get; set; }
    /// <summary>Hidden needs bars (needs bars spec, step 1): company and fun, known only to their owner,
    /// measured against their own usual life, kept and recorded. How they press is BarMode.</summary>
    public bool Bars { get; set; }
    /// <summary>The law 1 experiment: how the bars press. 0 watch (kept and recorded, read by nothing);
    /// 1 general (any gathering, the Saloon, any pastime lean); 2 particular (the places of one's own
    /// good chats and outings lean); 3 Spinoza (the bars lower the power of acting, nothing else).</summary>
    public int BarMode { get; set; }

    // Batch 2's switches (acts-batch2 spec 2.10), each with its slice; b2-0 builds the seams only,
    // so none has rows or rules yet.
    public bool Visits { get; set; }
    public bool Dates { get; set; }
    public bool Peace { get; set; }
    public bool Festivals { get; set; }
    public bool PublicConfront { get; set; }
    public bool ReadVerdicts { get; set; }
    public bool Settled { get; set; }
    public bool Amends { get; set; }
    public bool MadeScenes { get; set; }
    public bool Bans { get; set; }
    public bool Counter { get; set; }
    public bool Hosting { get; set; }
    public bool Debts { get; set; }
    public bool Loans { get; set; }
    public bool Items { get; set; }
    public bool Blunders { get; set; }
    public bool Contests { get; set; }
    public bool Partiality { get; set; }
    public bool Board { get; set; }

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
    /// <summary>After a refused invitation, the days before the asker invites that person again
    /// (acts-batch2 spec 2.3's AskAgainDays for invitations).</summary>
    public int InviteAgainDays { get; set; } = 7;
    /// <summary>What being tired weighs against saying yes to an invitation, at full tiredness (Sid,
    /// 2026-10-13: refusals come more readily when tired, busy or low). Eagerness to please, in
    /// proportion to the bond with the one asking, shrinks this and the next two.</summary>
    public double InviteTiredCost { get; set; } = 0.3;
    /// <summary>What an invitation weighs when the asked person is due to leave for work within
    /// InviteBusyMinutes.</summary>
    public double InviteBusyCost { get; set; } = 0.3;
    /// <summary>What a low mood weighs against saying yes, at the lowest mood.</summary>
    public double InviteLowCost { get; set; } = 0.3;
    /// <summary>How soon before leaving for work an invitation counts as badly timed.</summary>
    public int InviteBusyMinutes { get; set; } = 60;
    /// <summary>What family or a shared home adds to the obligation to say yes.</summary>
    public double InviteKinBond { get; set; } = 0.2;
    /// <summary>The chance each day that someone with things they use, and no need already, comes to
    /// need one of them.</summary>
    public double NeedChancePerDay { get; set; } = 0.1;
    /// <summary>How many days a need waits for someone to promise it before it lapses.</summary>
    public int NeedDays { get; set; } = 4;
    /// <summary>A promised errand is due at the end of the day this many days after the promise:
    /// Stardew's item delivery quests give two days.</summary>
    public int ErrandDays { get; set; } = 2;
    /// <summary>Handed over within this many days after it was due, an errand is late; after, broken.</summary>
    public int ErrandGraceDays { get; set; } = 1;
    /// <summary>After a no, the days before the same person is asked for a favour by the same asker.</summary>
    public int ErrandAskAgainDays { get; set; } = 3;
    /// <summary>The least time between two askings by the same person, in minutes.</summary>
    public int ErrandAskMinutes { get; set; } = 60;
    /// <summary>What family or a shared home adds to the obligation to fetch something.</summary>
    public double ErrandKinBond { get; set; } = 0.2;
    /// <summary>How soon a bar leans a free-time choice: factor min(BarCap, 1 + gain x pressure).</summary>
    public double BarPullGain { get; set; } = 1.0;
    /// <summary>The most a bar multiplies a free-time option or a draw by.</summary>
    public double BarCap { get; set; } = 2.0;
    /// <summary>How much loneliness grows Fond's and Missing's wish: min(BarWishCap, 1 + gain x pressure).</summary>
    public double BarWishGain { get; set; } = 0.5;
    public double BarWishCap { get; set; } = 1.5;
    /// <summary>What loneliness or boredom takes off the cost of saying yes to an invitation, at full pressure.</summary>
    public double BarAskEase { get; set; } = 0.2;
    /// <summary>Mode 3 (Spinoza): what a full bar takes off the power of acting.</summary>
    public double BarPowerWeight { get; set; } = 0.3;
    /// <summary>Lonely past max(LonelyFloorMinutes, LonelyTimes x one's usual longest waking stretch without a good chat).</summary>
    public int LonelyFloorMinutes { get; set; } = 360;
    public double LonelyTimes { get; set; } = 2.0;
    /// <summary>Bored past max(BoredFloorMinutes, BoredTimes x one's usual gap between outings).</summary>
    public int BoredFloorMinutes { get; set; } = 2880;
    public double BoredTimes { get; set; } = 2.5;
    /// <summary>An outing: this many minutes at a pastime in one stay.</summary>
    public int OutingMinutes { get; set; } = 60;
    /// <summary>Someone has a pastime with outings on at least this share of their days.</summary>
    public double RegularShare { get; set; } = 1.0 / 7;
    /// <summary>Days known before anyone is pressed.</summary>
    public int BarsSettleDays { get; set; } = 7;
    /// <summary>The share of a mishap's pity a witness feels at a hurt they saw, x understanding
    /// (acts spec 4.8: the understanding comfort).</summary>
    public double HurtPity { get; set; } = 0.5;
    /// <summary>Witnesses take sides only at a first strike: not at a stand-up, nor at an answer in a
    /// hostile exchange already going. Without it, every stand-up and every answer drew defenders of
    /// its own and sides spiralled into war (the spec's risk 3).</summary>
    public bool FirstStrikeSides { get; set; } = true;
    /// <summary>The share by which comfort cools the target's open Answer.</summary>
    public double ComfortCools { get; set; } = 0.25;

    /// <summary>The slices' switches by name, for the runner's --catalog.</summary>
    public static readonly IReadOnlyList<string> Switches = new[]
    {
        "Watch", "Returns", "Company", "Welcome", "Repair", "Sides", "Late",
        "Visits", "Dates", "Peace", "Festivals", "PublicConfront", "ReadVerdicts", "Settled", "Amends", "MadeScenes",
        "Bans", "Counter", "Hosting", "Debts", "Loans", "Items", "Blunders", "Contests", "Partiality", "Board",
        "Invitations", "Errands", "Bars",
    };

    /// <summary>Whether the slice with this switch name is on.</summary>
    public bool IsOn(string slice) => slice switch
    {
        nameof(Returns) => Returns,
        nameof(Company) => Company,
        nameof(Welcome) => Welcome,
        nameof(Repair) => Repair,
        nameof(Sides) => Sides,
        nameof(Late) => Late,
        nameof(Visits) => Visits,
        nameof(Dates) => Dates,
        nameof(Peace) => Peace,
        nameof(Festivals) => Festivals,
        nameof(PublicConfront) => PublicConfront,
        nameof(ReadVerdicts) => ReadVerdicts,
        nameof(Settled) => Settled,
        nameof(Amends) => Amends,
        nameof(MadeScenes) => MadeScenes,
        nameof(Bans) => Bans,
        nameof(Counter) => Counter,
        nameof(Hosting) => Hosting,
        nameof(Debts) => Debts,
        nameof(Loans) => Loans,
        nameof(Items) => Items,
        nameof(Blunders) => Blunders,
        nameof(Contests) => Contests,
        nameof(Partiality) => Partiality,
        nameof(Board) => Board,
        nameof(Invitations) => Invitations,
        nameof(Errands) => Errands,
        nameof(Bars) => Bars,
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
/// its margin. Nothing started. A motive the catalog would have stirred (Remorse with Repair; Defend
/// and Pity with Sides) is recorded too: then Kind names the motive or the act it would answer with,
/// and Margin holds the felt amount instead of a margin.</summary>
public sealed record CatalogWatched(int Tick, string Holder, string Subject, DesireKind Motive, string Kind, double Margin);
