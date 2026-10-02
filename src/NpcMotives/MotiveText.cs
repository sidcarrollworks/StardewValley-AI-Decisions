using System.Globalization;
using NpcDecision;
using NpcMemory;

namespace NpcMotives;

/// <summary>
/// The motives runner's words: the model's states and options (plain words, no instructions to
/// the model; docs/spec/laya.md) and the <c>[shadow]</c> log lines (docs/spec/motives.md,
/// "Player-visible behavior"). Pure; numbers use the invariant culture.
/// </summary>
public static class MotiveText
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>A short name for an act, for log lines and the viewer.</summary>
    public static string ActName(Act act) => act switch
    {
        Act.Emote => "emote",
        Act.Letter => "letter",
        Act.QueuedLine => "queued line",
        Act.Bubble => "bubble",
        Act.FarmVisit => "farm visit",
        Act.AskForHelp => "request",
        Act.WalkUp => "walk-up",
        Act.Visit => "visit",
        Act.Interrupt => "interrupt",
        _ => act.ToString(),
    };

    /// <summary>What the NPC would do, as a verb phrase ("write you a cold letter").</summary>
    public static string Verb(Act act, bool hostile, string? place = null) => act switch
    {
        Act.Emote => hostile ? "glare at you" : "wave at you",
        Act.Bubble => hostile ? "say something sharp to you" : "call out to you",
        Act.QueuedLine => hostile ? "tell you off the next time you talk" : "save something to say when you next talk",
        Act.Letter => hostile ? "write you a cold letter" : "write you a letter",
        Act.WalkUp => hostile ? "walk up and confront you" : "walk over to you",
        Act.Visit => place is null ? "go looking for you" : $"go looking for you at {place}",
        Act.Interrupt => "interrupt you",
        Act.AskForHelp => "ask you for help",
        Act.FarmVisit => "ask to visit your farm",
        _ => "do something about you",
    };

    /// <summary>A motive as an option for "which would they act on first?": what it moves the
    /// NPC to do, in plain words.</summary>
    public static string Option(Motive motive) => motive switch
    {
        Motive.MissingYou => "spend time with the player, whom they miss",
        Motive.Greeting => "say hello to the player",
        Motive.News => "tell the player some news",
        Motive.Grateful => "thank the player",
        Motive.Hurt => "let the player know they are hurt",
        Motive.Curious => "meet the newcomer",
        Motive.Worried => "check that the player is all right",
        Motive.Jealous => "let the player see they are jealous",
        Motive.WantsToTrade => "offer the player a trade",
        Motive.NeedsHelp => "ask the player for help",
        _ => "talk to the player",
    };

    /// <summary>The model's state for the motive choice: the NPC card, then its feelings.</summary>
    public static string ChoiceState(MotiveInputs i, MotiveDecision d)
    {
        var state = new DecisionState();
        if (!string.IsNullOrWhiteSpace(i.Card))
            state.Add(3, i.Card!);
        state.Add(2, Feelings(d));
        state.Add(1, "deciding what to do first");
        return state.Build();
    }

    /// <summary>The model's state for a close call (laya.md, "Question catalog"): the NPC card,
    /// the motive and its sources, then the act with effective boldness and cost.</summary>
    public static string CloseCallState(MotiveInputs i, MotiveDecision d)
    {
        var state = new DecisionState();
        if (!string.IsNullOrWhiteSpace(i.Card))
            state.Add(3, i.Card!);
        state.Add(2, Feelings(d));
        if (d.Pending is { } c)
            state.Add(1, string.Format(Inv,
                "thinking about: {0}\nboldness {1:0.00} + familiarity {2:0.00} + feeling {3:0.00} = {4:0.00}, against {5:0.00} needed",
                MotivesEngine.ActPhrase(c.Act, c.Hostile),
                d.Boldness, d.Familiarity, d.IntensityTerm, c.Effective, c.Cost));
        return state.Build();
    }

    /// <summary>The grudge question (motives.md, "Grudge and friendship loss").</summary>
    public static string GrudgeProposition(string npc) => $"would {npc} hold this against the player?";

    /// <summary>The model's state for the grudge question: the NPC card, the grudge, and what
    /// caused it lately (the negative entries of the last <paramref name="days"/> days).</summary>
    public static string GrudgeState(MotiveInputs i, int days)
    {
        var state = new DecisionState();
        if (!string.IsNullOrWhiteSpace(i.Card))
            state.Add(3, i.Card!);
        state.Add(2, string.Format(Inv, "grudge toward the player: {0:0.00} of 1", Math.Max(0, -i.RegardForPlayer)));
        string causes = GrudgeCauses(i.Diary, i.Now, days);
        if (causes.Length > 0)
            state.Add(1, "lately: " + causes);
        return state.Build();
    }

    /// <summary>The hurtful entries of the last <paramref name="days"/> days, newest first, in
    /// plain words ("stood up 2 days ago; got a gift they hated today"); at most five.</summary>
    public static string GrudgeCauses(IReadOnlyList<DiaryEntry> diary, int now, int days)
    {
        int since = now - days * GameClock.TicksPerDay;
        return string.Join("; ", diary
            .Where(e => e.AbsoluteTick >= since && e.AbsoluteTick <= now
                        && string.Equals(StressorTable.TargetOf(e), MotivesEngine.Player, StringComparison.OrdinalIgnoreCase)
                        && StressorTable.Of(e) is { Valence: < 0 })
            .OrderByDescending(e => e.AbsoluteTick)
            .Take(5)
            .Select(e => $"{Hurtful(e)} {Ago(GameClock.DayIndex(now) - GameClock.DayIndex(e.AbsoluteTick))}"));
    }

    /// <summary>The <c>[shadow]</c> line for one runner event (the mod adds the prefix).</summary>
    public static string Line(MotiveEvent e) => e.Kind switch
    {
        "Act" or "Pass" when e.Decision is not null => DecisionLine(e, e.Decision),
        "Blocked" => $"{e.Npc}: {e.Motive} waits ({e.Reason})",
        "Grudge" => string.Format(Inv, "{0} would lose friendship (grudge {1:0.00}: {2})", e.Npc, e.Grudge ?? 0, e.Reason),
        _ => $"{e.Npc}: {(e.Hostile ? "hostile " : "")}{(e.Act is { } a ? ActName(a) : "attempt")} {e.Kind.ToLowerInvariant()} ({e.Reason})",
    };

    private static string DecisionLine(MotiveEvent e, MotiveDecision d)
    {
        string motive = d.Chosen is null
            ? "no motive"
            : string.Format(Inv, "motive {0} {1:0.00} toward you ({2})", d.Chosen.Motive, d.Chosen.Strength, d.Chosen.Source);
        string outlook = string.Format(Inv, "outlook {0:+0.00;-0.00}", d.Mood.Outlook);

        // The close call the model was asked about, if any, then the act taken.
        string asked = "";
        if (d.Pending is { } pending && e.ModelP is { } p)
        {
            asked = string.Format(Inv, "{0}: {1} vs cost {2:0.00}, close call; Laya {3:0.00}, {4} tilts it to {5:0.00}: {6}",
                Named(pending), Parts(d, pending), pending.Cost, p, outlook, e.TiltedP ?? p,
                e.Kind == "Act" && e.Act == pending.Act ? "yes" : "no");
        }

        if (e.Kind == "Pass")
        {
            string why = asked.Length > 0 ? asked : d.Reason;
            return $"{e.Npc}: {motive}; {why}: passes";
        }

        ActCheck? taken = d.Checks.FirstOrDefault(c => c.Act == e.Act);
        string verb = Verb(e.Act ?? Act.Emote, e.Hostile);
        if (taken is null)
            return $"{e.Npc}: {motive}; {d.Reason}: would {verb}";
        if (d.Pending is { } asked2 && asked2.Act == taken.Act)
            return $"{e.Npc}: {motive}; {asked}: would {verb}";
        string clear = string.Format(Inv, "{0}: {1} vs cost {2:0.00}, clear yes", Named(taken), Parts(d, taken), taken.Cost);
        return asked.Length > 0
            ? $"{e.Npc}: {motive}; {asked}; {clear}: would {verb}"
            : $"{e.Npc}: {motive}; {clear}: would {verb}";
    }

    private static string Named(ActCheck c) => (c.Hostile ? "hostile " : "") + ActName(c.Act);

    private static string Parts(MotiveDecision d, ActCheck c) => string.Format(Inv,
        "boldness {0:0.00} + familiarity {1:0.00} + intensity {2:0.00} = {3:0.00}",
        d.Boldness, d.Familiarity, d.IntensityTerm, c.Effective);

    private static string Feelings(MotiveDecision d)
    {
        var lines = new List<string>();
        foreach (MotiveStrength m in d.Motives.OrderByDescending(m => m.Strength).ThenBy(m => (int)m.Motive).Take(5))
            lines.Add(string.Format(Inv, "{0} {1:0.00} ({2})", Word(m.Motive), m.Strength, m.Source));
        string feelings = lines.Count == 0 ? "nothing in particular" : string.Join("; ", lines);
        return string.Format(Inv, "feelings toward the player: {0}\nnet feeling: {1:+0.00;-0.00}\ntoday's outlook: {2:+0.00;-0.00}",
            feelings, d.NetFeeling, d.Mood.Outlook);
    }

    private static string Word(Motive m) => m switch
    {
        Motive.MissingYou => "misses them",
        Motive.News => "has news",
        Motive.Grateful => "grateful",
        Motive.Hurt => "hurt",
        Motive.Curious => "curious",
        Motive.Worried => "worried",
        Motive.WantsToTrade => "wants to trade",
        Motive.Jealous => "jealous",
        Motive.Greeting => "would say hello",
        Motive.NeedsHelp => "needs help",
        _ => m.ToString(),
    };

    private static string Hurtful(DiaryEntry e)
    {
        IReadOnlyDictionary<string, string> d = DiaryDetail.Parse(e.Detail);
        return e.Kind switch
        {
            "StoodUp" => "was stood up",
            "GiftReceived" => d.TryGetValue("taste", out string? taste) && taste == "Hate" ? "got a gift they hated" : "got a gift they disliked",
            "IgnoredBy" => "was ignored",
            "PassedBy" => "was walked past",
            "BrushedOff" => "was brushed off",
            "Criticized" => "was criticized",
            "BirthdayForgotten" => "had their birthday forgotten",
            "MissedVisit" => "missed a visit",
            "SawRummaging" => "saw the player going through the trash",
            "DanceAsked" => "was turned down for the dance",
            "MovieTogether" => "sat through a film they disliked",
            "Argued" => "argued",
            _ => e.Kind,
        };
    }

    private static string Ago(int days)
    {
        return days switch
        {
            0 => "today",
            1 => "yesterday",
            _ => $"{days} days ago",
        };
    }
}
