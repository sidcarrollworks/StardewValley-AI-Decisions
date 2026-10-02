using NpcTemperament;

namespace NpcMotives;

/// <summary>
/// Why a character acts (docs/spec/motives.md, "Motives"). Saved as an int wherever it is stored,
/// so values are append-only: never insert or reorder.
/// </summary>
public enum Motive
{
    MissingYou = 0,
    News = 1,
    Grateful = 2,
    Hurt = 3,
    Curious = 4,
    Worried = 5,
    WantsToTrade = 6,
    Jealous = 7,
    Greeting = 8,
    NeedsHelp = 9,
}

/// <summary>What a character can do about a motive: the ladder's steps as acts with a cost
/// (motives.md, "Acts and their cost"). Not saved.</summary>
public enum Act
{
    Emote,
    Letter,
    QueuedLine,
    Bubble,
    FarmVisit,
    AskForHelp,
    WalkUp,
    Visit,
    Interrupt,
}

/// <summary>How the act rule judged one act: far enough above its cost to act without asking,
/// far enough below to skip, or a close call that goes to the model.</summary>
public enum CallKind
{
    ClearYes,
    CloseCall,
    ClearNo,
}

/// <summary>A stress one diary entry puts on a character, in the elastic part (it fades).
/// <see cref="Tick"/> is the source entry's tick: venting and a delivered thanks act on the
/// stresses from entries at or before the act.</summary>
public sealed record Stress(string Subject, Motive Motive, string Kind, int Valence, double Strength, int Tick = 0);

/// <summary>Today's mood: what recent events earned, the deterministic daily roll, whether today
/// is a rare out-of-character day, and the outlook they add up to (all -1..1).</summary>
public sealed record Mood(double Earned, double Roll, bool TailDay, double Outlook);

/// <summary>One motive toward a subject with its strength (0..1) and where it came from.</summary>
public sealed record MotiveStrength(Motive Motive, string Subject, double Strength, string Source);

/// <summary>
/// Everything the engine needs about one NPC at one moment, copied on the game thread from
/// memory (rule 2: no live positions). The mod builds it; tests build it by hand.
/// </summary>
public sealed record MotiveInputs(
    string Npc,
    int Now,                                   // absolute tick
    Temperament Temperament,
    int Hearts,                                // with the player, 0..14
    IReadOnlyList<NpcMemory.DiaryEntry> Diary, // the NPC's diary (at least the recent days), any order
    double RegardForPlayer,                    // saved regard toward the player, -1..1
    bool PlayerNear,                           // a fresh first-hand sighting this tick (the ladder's IsNear)
    bool SeenPlayerToday,
    bool HasMetPlayer,                         // any Talked entry ever (or the game's friendship record)
    bool KnowsOfPlayer,                        // any ledger entry about the player (seen or told)
    double BestNewsScore,                      // the planner's best news score for today/yesterday, 0 if none
    bool NewsShared,                           // that news was already delivered (SharedNews)
    bool GreetedToday,                         // a Greeting was already answered today
    int DaysSinceSighting,                     // own sighting or tip; large when never
    int IgnoredToday,                          // unanswered attempts today (frustration)
    int AttemptsLeftToday,                     // the town-wide daily cap's remainder
    int Seed,                                  // the per-save seed
    bool NewcomerWeek = false,
    bool HasLead = false,                      // a place to go looking (the ladder's Whereabouts lead)
    int? ThankedTick = null,                   // a delivered thanks: Grateful from entries at or before it is used up
    IReadOnlyList<int>? VentTicks = null,      // hostile acts: each vents the hurt from entries at or before it
    IReadOnlyCollection<Act>? Unavailable = null, // acts a cap rules out now (the runner's daily and weekly caps)
    string? Card = null,                       // the NPC card for the model's state (rendered on the game thread)
    string? LeadPlace = null,                  // where the lead points, for the shadow line ("would go looking at ...")
    int? LastTalkTick = null);                 // the last talk the runner saw; survives the diary's 500-entry trim

/// <summary>One act the rule weighed, with every part, so the playtest log and viewer can show
/// why (docs/spec/debug-tools.md, "Playtest log", the decision record).</summary>
public sealed record ActCheck(
    Act Act,
    bool Hostile,
    double Cost,
    double Effective,
    double Margin,
    CallKind Call,
    double FallbackP);

/// <summary>
/// The engine's answer for one NPC at one moment: its motives, mood and net feeling, the motive
/// it would act on and the act it chose (or why not). A close call carries
/// <see cref="Pending"/>: the caller asks the model and resolves it with
/// <see cref="MotivesEngine.ResolveClose"/>. Display and logging only until the ladder is
/// replaced; nothing here changes the game.
/// </summary>
public sealed record MotiveDecision(
    string Npc,
    IReadOnlyList<MotiveStrength> Motives,
    Mood Mood,
    double NetFeeling,                 // toward the player: + friendly, - hostile
    MotiveStrength? Chosen,            // the motive acted on; null when none
    double Boldness,
    double Familiarity,
    double IntensityTerm,
    double Frustration,
    IReadOnlyList<ActCheck> Checks,    // most expensive first
    Act? Result,                       // the act taken (clear yes, or the close call to ask about)
    ActCheck? Pending,                 // non-null when Result is a close call awaiting the model
    string Reason);

/// <summary>
/// One thing the motives runner did or recorded at a tick; in shadow, what the NPC WOULD do.
/// <see cref="Kind"/>: "Act" (an attempt), "Pass" (a motive the act rule or the model passed
/// over), "Blocked" (a cap or cooldown kept a motive from being weighed; at most once per NPC per
/// reason per day), "Responded", "Ignored", "Expired" (an attempt's outcome), "Grudge" (would
/// hold it against the player: the friendship penalty, which only logs in shadow).
/// </summary>
public sealed record MotiveEvent(
    int AbsoluteTick,
    string Npc,
    string Kind,
    Act? Act,
    Motive? Motive,
    bool Hostile,
    string Reason,
    MotiveDecision? Decision = null, // Act and Pass: every part of the decision
    double? ModelP = null,           // a close call's model answer, before the mood tilt
    double? TiltedP = null,          // the answer after the mood tilt
    string? Choice = null,           // the model's pick among motives, when it was asked
    double? Grudge = null,           // Grudge: the grudge that crossed the threshold
    double RegardRelief = 0);        // Grudge: how far the game thread raises regard afterwards

/// <summary>
/// What one diary entry did to a character (docs/spec/debug-tools.md, the <c>stress</c> and
/// <c>regard</c> records): the stress it put on them and the lasting mark it left in regard.
/// <see cref="Before"/> equals <see cref="After"/> when the entry left no mark.
/// </summary>
public sealed record RegardNote(
    string Observer,
    string Subject,
    string Kind,          // the diary kind; "Heard:GiftReceived" for confirmed hearsay; "relief" after a grudge
    double Magnitude,     // after sensitivity (and the hearsay factor)
    double Plastic,       // the plastic share used (the profile's, the yield share, or 0)
    double Retention,     // the retention factor applied; 1 when severe
    bool Severe,
    bool YieldCrossed,    // a yielding kind reached the yield point with this entry
    double Before,
    double After,
    string Cause);

