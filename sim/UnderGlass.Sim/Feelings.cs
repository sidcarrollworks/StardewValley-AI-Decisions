namespace UnderGlass.Sim;

/// <summary>
/// Knobs for feelings (phase 0c; design section 3a). Every number is a first guess for the sweeps
/// to settle. Enabled turns feelings on; Steer lets them steer decisions (rules S1-S10). With
/// Enabled on and Steer off, feelings are only watched: no act or belief changes. The law
/// switches turn single laws off for the ablations (experiment E2).
/// </summary>
public sealed class FeelingOptions
{
    public bool Enabled { get; set; } = true;
    /// <summary>Feelings steer decisions. No effect while Enabled is off.</summary>
    public bool Steer { get; set; } = true;

    // Law switches for the ablations.
    /// <summary>Law 5: sympathy and antipathy (feeling with those we love or hate).</summary>
    public bool Sympathy { get; set; } = true;
    /// <summary>Law 6: imitation and indignation (feeling with those like us).</summary>
    public bool Imitation { get; set; } = true;
    /// <summary>Law 8: love after conquered hate (III P44).</summary>
    public bool Reconcile { get; set; } = true;
    /// <summary>Law 9: regard for kinds of people.</summary>
    public bool Kinds { get; set; } = true;
    /// <summary>Law 10: blame by freedom and known hardship.</summary>
    public bool Freedom { get; set; } = true;
    /// <summary>Law 11: presence (seen, heard, told) weighs feelings.</summary>
    public bool Presence { get; set; } = true;
    /// <summary>Rule 17: a scandal costs the culprit's kin some standing.</summary>
    public bool Association { get; set; } = true;
    /// <summary>Rule 17: kin are ashamed of a scandal, more when it is public.</summary>
    public bool Shame { get; set; } = true;

    // Seeds (rule 5).
    public double Household { get; set; } = 0.6;
    public double Friend { get; set; } = 0.4;
    /// <summary>Starting tensions and ties that override the seed: (from, to) to regard.</summary>
    public IReadOnlyDictionary<(string From, string To), double> Start { get; set; } = new Dictionary<(string, string), double>();

    // Feeling with others (laws 5 and 6; rule 6).
    public double LoveAt { get; set; } = 0.2;
    public double LoveShare { get; set; } = 0.5;
    public double HateCap { get; set; } = 0.3;
    public double ImitateShare { get; set; } = 0.2;
    /// <summary>Scales the share of every feeling that becomes regard (sweeps; 0 keeps regard at the seed).</summary>
    public double PlasticScale { get; set; } = 1;

    // Presence and hearsay (law 11; rule 9).
    public double HearsayMood { get; set; } = 0.5;
    public double CredencePerRegard { get; set; } = 0.25;
    public double HeardNameWeight { get; set; } = 0.5;
    public double CorroboratedWeight { get; set; } = 0.25;

    // Keeping and repeats (rule 4).
    /// <summary>A feeling at least this strong is kept whatever the person's retention.</summary>
    public double SevereAt { get; set; } = 0.7;
    /// <summary>A kindness repeated within this many days counts half each time.</summary>
    public int RepeatDays { get; set; } = 7;
    /// <summary>One act moves one person's regard for one subject by at most this much in all.</summary>
    public double MaxPerAct { get; set; } = 0.5;

    // Love after conquered hate (law 8; III P44, VERIFY).
    public double ReconcileFrom { get; set; } = 0.2;
    public double ReconcileShare { get; set; } = 1.0;

    // Kinds and kin (law 9; rule 17).
    public double KindShare { get; set; } = 0.5;
    public double AssocShare { get; set; } = 0.2;

    // Being named, confronted, shamed (rule 16 note; rule 17).
    public double AccusedJoy { get; set; } = -0.3;
    public double ConfrontJoy { get; set; } = -0.3;
    public double EventPlastic { get; set; } = 0.5;
    public double ShameJoy { get; set; } = -0.05;
    public int ShameCap { get; set; } = 6;
    public int PublicHolders { get; set; } = 3;

    // Mood and the power of acting (laws 1 and 2; rule 4).
    public int MoodDays { get; set; } = 3;
    public double PowerScale { get; set; } = 0.4;
    public double NeedPower { get; set; } = 0.15;
    public double WantPower { get; set; } = 0.05;
    public double HeldPower { get; set; } = 0.10;
    /// <summary>The joy of one chat, x (0.5 + chattiness). It was 0.02: with 10-20 chats a day,
    /// company alone lifted everyone's power of acting to about 0.68 and flattened its spread
    /// (0.05), so the day's events hardly told.</summary>
    public double CompanyJoy { get; set; } = 0.005;

    // Healing (rule 5).
    public double DriftPerDay { get; set; } = 0.005;
    public int ContactMinutes { get; set; } = 60;
    public double ContactHeal { get; set; } = 0.01;

    // Sentiments (Sims 4; principle 5).
    public double SentimentMin { get; set; } = 0.02;
    /// <summary>Sentiments fade by this factor a night: a 28-day half-life.</summary>
    public double SentimentKeepPerDay { get; set; } = 0.975;
    public int SentimentsPerPair { get; set; } = 3;
    /// <summary>Regard changes at least this big are written to the log.</summary>
    public double LogAt { get; set; } = 0.005;

    // Steering (S1-S10).
    public double TargetBase { get; set; } = 0.2;
    public double TiltScale { get; set; } = 1;
    public double CloseTieAt { get; set; } = 0.4;
    public double SwayLoveAt { get; set; } = 0.4;
    public double ReportPerHate { get; set; } = 0.3;
    public double SuspectPerRegard { get; set; } = 0.5;
    public double GuessPerHate { get; set; } = 1;
    public double CoverAt { get; set; } = 0.4;
    public double ConfrontPerHate { get; set; } = 2;
    public double GrievanceWeight { get; set; } = 0.5;
    public double GrievanceAt { get; set; } = 0.2;
    public double ChainPull { get; set; } = 2;
    public double ShopMargin { get; set; } = 0.3;
    public int ShopHoldDays { get; set; } = 28;

    // Ties (metrics).
    public double FeudAt { get; set; } = -0.3;
    public double FriendAt { get; set; } = 0.4;

    // ---- The desire gate (phase 0d; rule 10). Each has effect only while feelings steer. ----
    /// <summary>The gate's own switch: acts toward a person on a motive about that person.
    /// Worlds built by tests keep 0c unless they turn it on; the town turns it on.</summary>
    public bool Desire { get; set; }
    /// <summary>Off: motives are stirred and weighed, and logged apart, but no act starts.</summary>
    public bool DesireActs { get; set; } = true;
    public bool AnswerOn { get; set; } = true;
    public bool ReturnOn { get; set; } = true;
    public bool MakeUpOn { get; set; } = true;
    public bool RetaliateOn { get; set; } = true;
    /// <summary>The hurt who cannot answer keep away for a week; and go home rather than stay near.</summary>
    public bool AvoidOn { get; set; } = true;
    public bool WithdrawOn { get; set; } = true;
    /// <summary>Light hostile acts (a snub, turning away), the third-slight mark, and avoidance and
    /// withdrawal open to the moderately shy.</summary>
    public bool LightActsOn { get; set; }
    /// <summary>A lasting stance: hurt makes the bold combative and the shy withdrawn.</summary>
    public bool StanceOn { get; set; }
    /// <summary>Love wants to be near (law 4): a gift to someone loved and not seen for days.</summary>
    public bool FondOn { get; set; }
    /// <summary>Pity at a witnessed mishap stirs help (III P27, VERIFY).</summary>
    public bool PityOn { get; set; }
    /// <summary>A day's first meeting can be taken as warm or curt.</summary>
    public bool ToneOn { get; set; }
    /// <summary>A seam for Laya and for tests: a non-null answer decides a close call.</summary>
    public Func<Pursuit, bool?>? CloseCall { get; set; }

    // Rule 10's gate: boldness + 0.5 x familiarity + 0.5 x intensity reaches the act's cost.
    public double FamiliarityWeight { get; set; } = 0.5;
    public double IntensityWeight { get; set; } = 0.5;
    /// <summary>Law 1: + PowerWeight x (power of acting - 0.5) in effective boldness.</summary>
    public double PowerWeight { get; set; }
    public double GiftCost { get; set; } = 0.4;
    public double HelpCost { get; set; } = 0.5;
    /// <summary>An argument is a walk-up (0.5) and a snub a wave (0.2), each + the hostile surcharge.</summary>
    public double ArgueForm { get; set; } = 0.5;
    public double SnubForm { get; set; } = 0.2;
    public double HostileSurcharge { get; set; } = 0.3;
    public double GiftMin { get; set; } = 0.1;
    public double HelpMin { get; set; } = 0.2;
    public double ArgueMin { get; set; } = 0.2;
    public double SnubMin { get; set; } = 0.1;
    /// <summary>A margin more than this from the cost is decided; inside, a seeded close call.</summary>
    public double ClearBand { get; set; } = 0.15;
    public double MoodTilt { get; set; } = 0.1;
    public int SlotsPerDay { get; set; } = 2;
    public double AskAgainStep { get; set; } = 0.1;
    public int HostileCooldownDays { get; set; } = 3;
    public int LightPerDay { get; set; } = 2;
    /// <summary>The event part of a motive fades to nothing over this many days.</summary>
    public int MotiveDays { get; set; } = 7;
    /// <summary>Fear of greater harm (law 4, III P39, VERIFY): + cost per hostile act the subject did
    /// to the holder, as the holder saw it, within FearDays; and per member of the subject's
    /// household in reach.</summary>
    public double FearPerHit { get; set; } = 0.1;
    public int FearDays { get; set; } = 28;
    public double FearPerKin { get; set; }
    public int AvoidDays { get; set; } = 7;
    public int MarkCount { get; set; } = 3;
    public int MarkDays { get; set; } = 5;
    public double MarkSize { get; set; } = 0.3;
    public double StanceWeight { get; set; } = 0.5;
    public double StanceKeepPerDay { get; set; } = 0.975;
    public double StanceHome { get; set; } = 1.0;
    public int FondDays { get; set; } = 7;
    public double PityScale { get; set; } = 2;
    public int PityMinutes { get; set; } = 60;
    public double Tone { get; set; } = 0.03;
    public double ToneJoy { get; set; } = 0.1;
    public int OutcomeDays { get; set; } = 7;
    /// <summary>Scales the town-wide rates of gifts, help and arguments while the gate runs.</summary>
    public double AimedRateScale { get; set; } = 1.0;

    /// <summary>For tests: steering with the desire gate, its added rules off, and the given starting regard.</summary>
    public static FeelingOptions WithDesire(params (string From, string To, double Regard)[] start)
        => new() { Desire = true, Start = start.ToDictionary(x => (x.From, x.To), x => x.Regard) };

    /// <summary>No feelings at all: every run is as it was before phase 0c.</summary>
    public static FeelingOptions Off => new() { Enabled = false, Steer = false };

    /// <summary>Feelings are felt and measured but steer nothing.</summary>
    public static FeelingOptions Observe => new() { Steer = false };
}

/// <summary>
/// The rules of feeling that need no world (phase 0c; design section 3a). Pure. The Spinoza
/// references are from memory (VERIFY against Curley's translation).
/// </summary>
public static class Feelings
{
    /// <summary>Moves regard by d, damped only the way it already leans, so love and hate are
    /// harder to deepen than to begin, and always in [-1, 1].</summary>
    public static double Saturate(double r, double d)
    {
        double v = r != 0 && Math.Sign(d) == Math.Sign(r) ? r + d * (1 - Math.Abs(r)) : r + d;
        return Math.Clamp(v, -1, 1);
    }

    /// <summary>A rational squash into (-1, 1), for mood.</summary>
    public static double Squash(double x) => x / (1 + Math.Abs(x));

    /// <summary>
    /// The felt amount f0 (laws 2, 5, 6 and 12; III P21-P24, P27, VERIFY): what someone feels at an
    /// act whose patient feels <paramref name="joy"/>. Their own: in full. Someone they love: a
    /// share of it. Someone they hate: the opposite (a hated person's sadness is joy, III P24).
    /// Anyone else, or someone unknown: a smaller share, more for people like them, less for the
    /// understanding (III P27, imitation and indignation). Sens is 0.5 + sensitivity. The size
    /// comes from the act's row, never from the patient's own mind.
    /// </summary>
    public static (double F0, string Route) Base(double joy, double sens, bool self, bool known, double regard,
        double likeness, double understanding, bool onlookers, FeelingOptions o)
    {
        if (self)
            return (joy * sens, onlookers ? "Onlooker" : "Direct");
        if (known && o.Sympathy && regard >= o.LoveAt)
            return (o.LoveShare * regard * joy * sens, "Sympathy");
        if (known && o.Sympathy && regard <= -o.LoveAt)
            return (-o.LoveShare * Math.Min(-regard, o.HateCap) * joy * sens, "Antipathy");
        if (!o.Imitation)
            return (0, "Imitation");
        return (o.ImitateShare * (0.5 + 0.5 * likeness) * joy * (1 - 0.5 * understanding) * sens, "Imitation");
    }

    /// <summary>How alike two people are (law 6): a quarter each for the same household, the same
    /// life stage, the same kind of person, and work at the same place.</summary>
    public static double Likeness(Villager a, Villager b)
        => (a.Household == b.Household ? 0.25 : 0) + (a.Stage == b.Stage ? 0.25 : 0) + (a.Kind == b.Kind ? 0.25 : 0)
           + (a.Job is { } ja && b.Job is { } jb && ja.Place == jb.Place ? 0.25 : 0);

    /// <summary>How much of a feeling is kept (rule 4): a severe one in full; a mild one by retention.</summary>
    public static double Keep(double felt, double retention, FeelingOptions o)
        => Math.Abs(felt) >= o.SevereAt ? 1 : 0.5 + retention;

    /// <summary>A known hardship excuses (law 10; III P49, VERIFY): housemates live from one purse,
    /// so they know it was short, and the understanding forgive it more.</summary>
    public static double Excuse(bool housemates, double need, double understanding)
        => housemates ? need * (0.5 + 0.5 * understanding) : 0;

    /// <summary>How much of a feeling is blamed on its cause (law 10): by how freely they acted,
    /// less a known hardship.</summary>
    public static double Phi(double freedom, double excuse, FeelingOptions o)
        => o.Freedom ? freedom * (1 - excuse) : 1;

    /// <summary>How much someone believes a teller (rule 9): today's 0.5 + 0.5 x familiarity, and
    /// more for someone liked, less for someone disliked, unless they are understanding.</summary>
    public static double Credence(double familiarity, double regard, double understanding, FeelingOptions o)
        => Math.Clamp(0.5 + 0.5 * familiarity + o.CredencePerRegard * (1 - understanding) * regard, 0.25, 1);

    /// <summary>Love after conquered hate (law 8; III P44, VERIFY): when love given since the
    /// deepest point of a hate carries regard past zero, it gains back up to the depth of that
    /// hate, never more than the love actually given.</summary>
    public static double ReconcileBonus(double trough, double conquered, FeelingOptions o)
        => trough <= -o.ReconcileFrom ? o.ReconcileShare * Math.Min(-trough, conquered) : 0;

    /// <summary>What one mood entry weighs after <paramref name="ageMinutes"/>: linearly less over
    /// <see cref="FeelingOptions.MoodDays"/> days, then nothing (rule 4).</summary>
    public static double MoodWeight(int ageMinutes, FeelingOptions o)
        => Math.Max(0, 1 - ageMinutes / (double)(o.MoodDays * Clock.MinutesPerDay));

    /// <summary>The power of acting, 0..1 (laws 1 and 2): 0.5, less what presses on someone (need,
    /// an unmet want, being held), plus their mood.</summary>
    public static double PowerOf(double conditions, double mood, FeelingOptions o)
        => Math.Clamp(0.5 + conditions + o.PowerScale * mood, 0, 1);

    /// <summary>How the power of acting tilts the choice of who acts (law 1; rule 15): the joyful
    /// give and help more, the sad drink more.</summary>
    public static double PowerFactor(int tilt, double power, FeelingOptions o)
        => Math.Max(0, 1 + o.TiltScale * tilt * (power - 0.5));

    /// <summary>Whom an act is aimed at (law 4; III P25, VERIFY): love gives and helps, hate
    /// argues, and people argue less with those they love. Pull is the sign of the act's joy x
    /// regard for the person.</summary>
    public static double TargetWeight(double joy, double regard, FeelingOptions o)
    {
        double pull = Math.Sign(joy) * regard;
        return (o.TargetBase + Math.Max(0, pull)) * (1 - Math.Max(0, -pull));
    }

    /// <summary>Whether the mayor counts someone as close (rule 16): a housemate, someone he knows
    /// well and doesn't dislike, or someone he loves. At regard 0 this is the test before 0c.</summary>
    public static bool Close(bool household, double familiarity, double regard, double swayCloseAt, FeelingOptions o)
        => household || familiarity >= swayCloseAt && regard > -o.LoveAt || regard >= o.SwayLoveAt;

    /// <summary>The chance a witness reports a scandal (III P25, VERIFY): today's, less for a
    /// culprit they love, more for one they hate.</summary>
    public static double ReportChance(double reportBase, double perBoldness, double boldness, double regard, FeelingOptions o)
        => (reportBase + perBoldness * boldness) * (1 - Math.Max(0, regard)) + o.ReportPerHate * Math.Max(0, -regard);

    /// <summary>
    /// Being named or confronted (rule 16 note; law 10; III P40 schol., VERIFY). The mood felt, and
    /// the regard change toward each of <paramref name="namers"/>: an innocent resents whoever
    /// named them, split over the namers; the guilty feel shame instead (more with low
    /// self-regard), and no resentment.
    /// </summary>
    public static (double Mood, double PerNamer) AccusedSplit(double joy, double sens, double selfRegard, bool guilty,
        int namers, double retention, FeelingOptions o)
    {
        double f = joy * sens * (guilty ? 1.5 - selfRegard : 1);
        if (guilty || namers == 0)
            return (f, 0);
        return (f, f / namers * o.EventPlastic * Keep(f, retention, o));
    }

    /// <summary>The sentiment a regard change leaves (Sims 4; F18), or null.</summary>
    public static string? SentimentName(string route, double change) => route switch
    {
        "Direct" or "Undergone" => change > 0 ? "Grateful" : "Hurt",
        "Onlooker" or "Sympathy" or "Imitation" => change > 0 ? "Approving" : "Indignant",
        "Antipathy" => change > 0 ? "Pleased" : "Envious",
        "Kind" or "Spill" or "Association" => change > 0 ? "Approving" : "Wary",
        "Accused" or "Confronted" => change < 0 ? "Wronged" : null,
        "Shame" => change < 0 ? "Ashamed" : null,
        "Reconciled" => change > 0 ? "Reconciled" : null,
        _ => null,
    };
}

/// <summary>The desire gate's rules that need no world (rule 10). Pure. No trait is clamped and
/// no archetype's range assumed: boldness 0.02 and 0.98 give finite, ordered answers.</summary>
public static class DesireMath
{
    /// <summary>A motive's intensity (R2): what is left of the event, and for a hostile motive the
    /// grudge too (dislike, never liking). A friendly motive is the event alone, whatever the regard.
    /// Nothing once the event has faded.</summary>
    public static double Intensity(double eventPart, bool hostile, double regard)
        => eventPart <= 0 ? 0 : Math.Min(1, eventPart + (hostile ? Math.Max(0, -regard) : 0));

    /// <summary>How daring someone is toward a person: boldness, stance (a withdrawn stance lowers
    /// all daring, a combative one raises only hostile daring), familiarity, the motive's intensity,
    /// and the power of acting (law 1).</summary>
    public static double Effective(double boldness, double stance, bool hostile, double familiarity, double intensity,
        double power, FeelingOptions o)
        => boldness + o.StanceWeight * (hostile ? stance : Math.Min(0, stance))
           + o.FamiliarityWeight * familiarity + o.IntensityWeight * intensity + o.PowerWeight * (power - 0.5);

    public static double Cost(double form, bool hostile, double fear, FeelingOptions o)
        => form + (hostile ? o.HostileSurcharge + fear : 0);

    /// <summary>logistic(8 x margin) = 0.5 + 0.5 tanh(4m), with tanh in its [3/2] Pade form: within
    /// 7e-6 on the close-call band, and no Exp.</summary>
    public static double CloseCallChance(double margin)
    {
        double y = 4 * margin;
        return 0.5 + 0.5 * y * (15 + y * y) / (15 + 6 * y * y);
    }

    /// <summary>Mood tilts a close call: the glad toward kindness, the sad toward hostility.</summary>
    public static double Tilted(double p, double mood, bool hostile, FeelingOptions o)
        => Math.Clamp(p + o.MoodTilt * mood * (hostile ? -1 : 1), 0, 1);

    public static string Call(double margin, FeelingOptions o) => margin > o.ClearBand ? "clear" : margin < -o.ClearBand ? "no" : "close";

    /// <summary>The part of a motive an event gave it, fading linearly over the window.</summary>
    public static double EventPart(double felt, int since, int m, int windowMinutes)
        => felt * Math.Max(0, 1 - (m - since) / (double)windowMinutes);

    /// <summary>Love wants to be near (law 4): love above LoveAt, growing with days apart.</summary>
    public static double Fond(double regard, int daysApart, FeelingOptions o)
        => Math.Max(0, regard - o.LoveAt) * Math.Min(1, daysApart / (double)o.FondDays);

    public static double Pity(double joy, double sens, double regard, double clarity, FeelingOptions o)
        => o.PityScale * Math.Abs(joy) * sens * (1 + Math.Max(0, regard)) * clarity;

    /// <summary>Hurt moves a stance: the bold toward combat, the shy toward withdrawal.</summary>
    public static double StanceAfterHurt(double stance, double felt, double boldness)
        => Math.Clamp(stance + felt * (2 * boldness - 1), -1, 1);

    /// <summary>Kindness pulls a stance toward 0, never across it.</summary>
    public static double StanceAfterKindness(double stance, double felt)
        => stance - Math.Min(1, felt) * stance;

    /// <summary>The chances a day's first meeting is taken as warm, or as curt.</summary>
    public static (double Warm, double Curt) Tone(double power, double understanding, double regard, FeelingOptions o)
        => (o.Tone * 2 * power * (1 + regard), o.Tone * 4 * (1 - power) * (1 - understanding) * (1 - regard));
}
