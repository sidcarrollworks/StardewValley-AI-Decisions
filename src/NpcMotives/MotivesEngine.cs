using NpcMemory;
using NpcTemperament;

namespace NpcMotives;

/// <summary>
/// The motives engine (docs/spec/motives.md, D24): a character acts only when it has a motive with
/// a subject AND its effective boldness (boldness + familiarity + intensity) reaches the act's
/// cost. Pure and deterministic over a copied <see cref="MotiveInputs"/>; never touches the game
/// and never calls the model. A close call comes back as <see cref="MotiveDecision.Pending"/> for
/// the caller to ask Laya about, then <see cref="ResolveClose"/>.
/// <para>Today the subject is the player; NPC subjects (opinions, town-life.md) use the same
/// stresses and regard and come later.</para>
/// </summary>
public sealed class MotivesEngine
{
    public const string Player = "Player";

    private readonly MotiveOptions _o;

    public MotivesEngine(MotiveOptions? options = null) => _o = options ?? new MotiveOptions();

    public MotiveOptions Options => _o;

    /// <summary>Feelings net into one signed feeling per subject; tasks stand alone.</summary>
    public static bool IsFeeling(Motive m)
        => m is Motive.Grateful or Motive.MissingYou or Motive.Greeting or Motive.Curious or Motive.Hurt or Motive.Jealous;

    public static bool IsNegative(Motive m) => m is Motive.Hurt or Motive.Jealous;

    // ---- motives ---------------------------------------------------------------------------------

    /// <summary>Every motive toward the player with its strength (zero-strength ones omitted).</summary>
    public IReadOnlyList<MotiveStrength> MotivesOf(MotiveInputs i, IReadOnlyList<Stress> stresses)
    {
        Temperament t = i.Temperament;
        var list = new List<MotiveStrength>();
        void add(Motive m, double strength, string source)
        {
            double s = Math.Clamp(strength, 0, 1);
            if (s > 1e-6)
                list.Add(new MotiveStrength(m, Player, s, source));
        }

        int hearts = Math.Clamp(i.Hearts, 0, 14);

        // MissingYou: days since the last talk, from 2 hearts (motives.md table).
        if (hearts >= 2)
        {
            // The diary's last Talked, or the runner's own record of the last talk, which the
            // 500-entry trim never drops (playtest 2026-10-02: a busy villager adds ~50 entries a day).
            int? lastTalk = i.Diary
                .Where(e => e.Kind == "Talked" && Is(e.Subject, Player) && e.AbsoluteTick <= i.Now)
                .Select(e => (int?)e.AbsoluteTick).DefaultIfEmpty(null).Max();
            if (i.LastTalkTick is { } kept && kept <= i.Now && (lastTalk is null || kept > lastTalk))
                lastTalk = kept;
            if (lastTalk is { } talked)
            {
                double days = (i.Now - talked) / (double)GameClock.TicksPerDay;
                add(Motive.MissingYou, Math.Min(1, days / 7) * (0.3 + 0.07 * hearts) * (0.5 + t.Warmth),
                    $"last talked {days:0.#} days ago");
            }
        }

        // Greeting: near someone familiar, once a day.
        if (i.PlayerNear && !i.GreetedToday && (hearts >= 2 || i.RegardForPlayer >= 0.2))
            add(Motive.Greeting, 0.15 * (0.5 + t.Warmth), "the player is right here");

        // News: the planner's best news, until it is shared.
        if (i.BestNewsScore > 0 && !i.NewsShared)
            add(Motive.News, i.BestNewsScore / 5 * (0.5 + t.Chattiness), $"news score {i.BestNewsScore:0.#}");

        // Feelings from the elastic stresses (plus the grudge for Hurt). A delivered thanks uses
        // up the gratitude for everything before it; the warm part stays in regard.
        int thanked = i.ThankedTick ?? int.MinValue;
        double grateful = stresses.Where(s => s.Motive == Motive.Grateful && Is(s.Subject, Player) && s.Tick > thanked
                                              && !StressorTable.IsMoodOnly(s.Kind))
            .Sum(s => s.Strength);
        add(Motive.Grateful, grateful, "recent kindness");
        double grudge = Math.Max(0, -i.RegardForPlayer);
        double hurt = stresses.Where(s => s.Motive == Motive.Hurt && Is(s.Subject, Player)).Sum(s => s.Strength) + grudge;
        add(Motive.Hurt, hurt, grudge > 0 ? $"recent hurt; grudge {grudge:0.00}" : "recent hurt");
        double jealous = stresses.Where(s => s.Motive == Motive.Jealous && Is(s.Subject, Player)).Sum(s => s.Strength);
        add(Motive.Jealous, jealous, "saw or heard of a gift to someone else");

        // Curious: knows of the player but never met them.
        if (!i.HasMetPlayer && i.KnowsOfPlayer)
            add(Motive.Curious, (i.NewcomerWeek ? 0.6 : 0.3) * (0.5 + t.Curiosity), "heard of the newcomer");

        // Worried: 4+ hearts and no sighting or tip for 3+ days. Only about someone it has
        // seen or heard of: with no ledger entry at all there is nothing to miss yet (a save
        // from before the mod kept memory has hearts but no sightings).
        if (hearts >= 4 && i.KnowsOfPlayer && i.DaysSinceSighting >= 3)
            add(Motive.Worried, Math.Min(1, (i.DaysSinceSighting - 2) / 5.0) * (0.5 + t.Fear),
                $"no news of the player for {i.DaysSinceSighting} days");

        return list;
    }

    /// <summary>Net feeling toward the player: positive feelings minus negative ones, swayed by the
    /// day's outlook.</summary>
    public double Net(IReadOnlyList<MotiveStrength> motives, Mood mood)
        => motives.Where(m => IsFeeling(m.Motive)).Sum(m => IsNegative(m.Motive) ? -m.Strength : m.Strength)
           + _o.MoodSway * mood.Outlook;

    // ---- the act rule --------------------------------------------------------------------------

    /// <summary>The acts a motive may use (motives.md, "Acts and their cost"). Farm visits are
    /// deferred (Sid, 2026-10-02), so no motive uses them yet.</summary>
    public static IReadOnlyList<Act> AllowedActs(Motive chosen, bool feeling, double newsScore)
    {
        var acts = new List<Act>();
        if (feeling)
        {
            acts.Add(Act.Emote);
            acts.Add(Act.Bubble);
            if (chosen != Motive.Greeting)
            {
                acts.Add(Act.QueuedLine);
                acts.Add(Act.WalkUp);
                if (chosen != Motive.Curious)
                    acts.Add(Act.Letter);
            }
            if (chosen is Motive.MissingYou or Motive.Hurt)
                acts.Add(Act.Visit);
            if (chosen == Motive.Hurt)
                acts.Add(Act.Interrupt);
            return acts;
        }
        switch (chosen)
        {
            case Motive.News:
                acts.AddRange(new[] { Act.Bubble, Act.QueuedLine, Act.Letter, Act.WalkUp });
                if (newsScore >= 4)
                    acts.AddRange(new[] { Act.Visit, Act.Interrupt });
                break;
            case Motive.Worried:
                acts.AddRange(new[] { Act.Bubble, Act.QueuedLine, Act.Letter, Act.WalkUp, Act.Visit, Act.Interrupt });
                break;
            case Motive.NeedsHelp:
                acts.Add(Act.AskForHelp);
                break;
            case Motive.WantsToTrade:
                acts.AddRange(new[] { Act.Bubble, Act.Letter, Act.WalkUp });
                break;
        }
        return acts;
    }

    /// <summary>Whether an act can happen now (the ladder's Available, plus avoidance and the
    /// runner's caps).</summary>
    public bool Available(Act act, MotiveInputs i, bool friendlyInPerson, double net)
    {
        if (i.Unavailable?.Contains(act) == true)
            return false; // a daily or weekly cap has ruled it out
        bool inPerson = act is Act.Emote or Act.Bubble or Act.WalkUp or Act.Visit or Act.Interrupt;
        if (inPerson && friendlyInPerson && net <= -_o.AvoidLevel)
            return false; // avoiding the player: no friendly face-to-face
        return act switch
        {
            Act.Emote or Act.Bubble or Act.Interrupt => i.PlayerNear,
            Act.WalkUp => i.PlayerNear,
            Act.Visit => !i.PlayerNear && i.HasLead && i.Hearts >= 2,
            Act.QueuedLine => i.SeenPlayerToday,
            Act.Letter => !i.SeenPlayerToday && i.Hearts >= 2,
            Act.AskForHelp => i.Hearts >= 2,
            _ => false,
        };
    }

    /// <summary>
    /// Decides what an NPC does now. Picks the strongest candidate motive (the caller may override
    /// with the model's choice via <see cref="DecideFor"/>), then tries its allowed, available acts
    /// from the most expensive down: a clear yes is taken; a close call on a bigger act is returned
    /// as <see cref="MotiveDecision.Pending"/> for the model.
    /// </summary>
    public MotiveDecision Decide(MotiveInputs i)
    {
        Context c = Prepare(i);
        MotiveStrength? strongest = c.Candidates.OrderByDescending(m => m.Strength).ThenBy(m => (int)m.Motive).FirstOrDefault();
        return DecideFor(i, c, strongest);
    }

    /// <summary>The candidate motives (feelings netted into one, plus tasks) that could act now:
    /// what the model's "which would <npc> act on first?" question chooses among.</summary>
    public IReadOnlyList<MotiveStrength> Candidates(MotiveInputs i) => Prepare(i).Candidates;

    /// <summary>Decides with a given motive (e.g. the one the model chose among
    /// <see cref="Candidates"/>).</summary>
    public MotiveDecision DecideFor(MotiveInputs i, MotiveStrength? chosen) => DecideFor(i, Prepare(i), chosen);

    private sealed record Context(IReadOnlyList<Stress> Stresses, IReadOnlyList<MotiveStrength> Motives, Mood Mood,
        double Net, IReadOnlyList<MotiveStrength> Candidates);

    private Context Prepare(MotiveInputs i)
    {
        IReadOnlyList<Stress> stresses = NpcMotives.Stresses.Vent(
            NpcMotives.Stresses.Elastic(i.Diary, i.Now, i.Temperament, i.Hearts, _o), i.VentTicks, _o);
        Mood mood = MoodRoll.Today(i.Npc, i.Now, i.Seed, i.Temperament, stresses, _o);
        IReadOnlyList<MotiveStrength> motives = MotivesOf(i, stresses);
        double net = Net(motives, mood);

        var candidates = new List<MotiveStrength>();
        // The feeling, netted: labelled by its strongest motive on the net's side.
        if (Math.Abs(net) >= _o.MinMotiveForAction && motives.Any(m => IsFeeling(m.Motive)))
        {
            MotiveStrength? label = motives
                .Where(m => IsFeeling(m.Motive) && IsNegative(m.Motive) == net < 0)
                .OrderByDescending(m => m.Strength).ThenBy(m => (int)m.Motive).FirstOrDefault();
            if (label is not null)
                candidates.Add(label with { Strength = Math.Min(1, Math.Abs(net)), Source = label.Source + $"; net {net:+0.00;-0.00}" });
        }
        candidates.AddRange(motives.Where(m => !IsFeeling(m.Motive) && m.Strength >= _o.MinMotiveForAction));
        return new Context(stresses, motives, mood, net, candidates);
    }

    private MotiveDecision DecideFor(MotiveInputs i, Context c, MotiveStrength? chosen)
    {
        Temperament t = i.Temperament;
        double frustration = i.IgnoredToday * _o.FrustrationStep * (t.Boldness >= 0.5 ? 1 : -1);

        MotiveDecision Nothing(string reason, double familiarity = 0, double intensityTerm = 0, IReadOnlyList<ActCheck>? checks = null)
            => new(i.Npc, c.Motives, c.Mood, c.Net, chosen, t.Boldness, familiarity, intensityTerm, frustration,
                checks ?? Array.Empty<ActCheck>(), null, null, reason);

        if (chosen is null)
            return Nothing("no motive: nobody acts without a reason");

        bool feeling = IsFeeling(chosen.Motive);
        bool hostile = feeling && c.Net < 0;
        double intensity = Math.Max(0, chosen.Strength + frustration);
        if (i.AttemptsLeftToday <= _o.StrongReserve && intensity < _o.StrongIntensity)
            return Nothing($"only {i.AttemptsLeftToday} attempts left today; kept for strong motives");

        int hearts = Math.Clamp(i.Hearts, 0, 14);
        double familiarity = hostile
            ? _o.HostileFamiliarityPerGrudge * Math.Max(0, -i.RegardForPlayer)
            : _o.FamiliarityPerHeart * Math.Min(hearts, 10) + _o.FamiliarityPerPositiveRegard * Math.Max(0, i.RegardForPlayer);
        double intensityTerm = _o.IntensityWeight * intensity;
        double effective = t.Boldness + familiarity + intensityTerm;

        var checks = new List<ActCheck>();
        var tooWeak = new List<Act>();
        foreach (Act act in AllowedActs(chosen.Motive, feeling, i.BestNewsScore)
                     .OrderByDescending(a => _o.ActCost[a]).ThenBy(a => (int)a))
        {
            if (!Available(act, i, friendlyInPerson: !hostile, c.Net))
                continue;
            if (intensity < _o.MinStrengthFor(act))
            {
                tooWeak.Add(act); // a reason too small for this act, however bold
                continue;
            }
            double cost = _o.ActCost[act] + (hostile ? _o.HostileSurcharge : 0);
            double margin = effective - cost;
            CallKind call = margin >= _o.ClearBand ? CallKind.ClearYes : margin <= -_o.ClearBand ? CallKind.ClearNo : CallKind.CloseCall;
            checks.Add(new ActCheck(act, hostile, cost, effective, margin, call, Fallback(margin)));
        }

        // Most expensive first: a clear yes is taken; a close call on a bigger act goes to the model
        // first, and if the model says no the caller falls back to the biggest clear yes.
        foreach (ActCheck check in checks)
        {
            if (check.Call == CallKind.ClearYes)
                return new MotiveDecision(i.Npc, c.Motives, c.Mood, c.Net, chosen, t.Boldness, familiarity, intensityTerm,
                    frustration, checks, check.Act, null, $"{check.Act}: {check.Effective:0.00} vs cost {check.Cost:0.00}, clear yes");
            if (check.Call == CallKind.CloseCall)
                return new MotiveDecision(i.Npc, c.Motives, c.Mood, c.Net, chosen, t.Boldness, familiarity, intensityTerm,
                    frustration, checks, check.Act, check, $"{check.Act}: {check.Effective:0.00} vs cost {check.Cost:0.00}, close call");
        }
        if (checks.Count == 0 && tooWeak.Count > 0)
            return Nothing(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "motive {0:0.00} too weak for {1}", intensity,
                    string.Join(", ", tooWeak.Select(a => string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0} (needs {1:0.00})", MotiveText.ActName(a), _o.MinStrengthFor(a))))),
                familiarity, intensityTerm, checks);
        if (checks.Count == 0)
        {
            // Say when a cap, not the situation, ruled the acts out.
            List<Act> capped = AllowedActs(chosen.Motive, feeling, i.BestNewsScore)
                .Where(a => i.Unavailable?.Contains(a) == true
                            && Available(a, i with { Unavailable = null }, friendlyInPerson: !hostile, c.Net))
                .ToList();
            return Nothing(capped.Count == 0
                    ? "no allowed act is possible right now"
                    : "the acts it could use are capped or already waiting: " + string.Join(", ", capped.Select(MotiveText.ActName)),
                familiarity, intensityTerm, checks);
        }
        return Nothing("every possible act is out of reach", familiarity, intensityTerm, checks);
    }

    /// <summary>The close-call fallback answer when the model is down: 0.5 + margin / (2 x band).</summary>
    public double Fallback(double margin) => Math.Clamp(0.5 + margin / (2 * _o.ClearBand), 0, 1);

    /// <summary>
    /// Resolves a pending close call with the model's yes probability <paramref name="p"/>: the
    /// day's outlook tilts it (toward friendly acts on a good day, hostile ones on a bad day), then
    /// a cut at 0.5, no random draw (motives.md, "Deciding a close call"). On a no, the biggest
    /// clear yes below it, if any.
    /// </summary>
    public Act? ResolveClose(MotiveDecision d, double p)
    {
        if (d.Pending is null)
            return d.Result;
        if (Tilt(d, p) >= 0.5)
            return d.Pending.Act;
        return d.Checks.Where(c => c.Call == CallKind.ClearYes).Select(c => (Act?)c.Act).FirstOrDefault();
    }

    /// <summary>A close call's answer after the mood tilt: <c>p + MoodTilt x outlook</c> for a
    /// friendly act, <c>p - MoodTilt x outlook</c> for a hostile one; a NaN answer (no model) uses
    /// the pending check's fallback. NaN when nothing is pending.</summary>
    public double Tilt(MotiveDecision d, double p)
    {
        if (d.Pending is null)
            return double.NaN;
        double answer = double.IsNaN(p) ? d.Pending.FallbackP : p;
        return answer + (d.Pending.Hostile ? -1 : 1) * _o.MoodTilt * d.Mood.Outlook;
    }

    /// <summary>The close-call question for the model (laya.md, "Question catalog").</summary>
    public static string CloseCallProposition(string npc, Act act, bool hostile)
        => $"would {npc} {ActPhrase(act, hostile)} now?";

    /// <summary>An act toward the player in the model's words ("write the player a cold letter").</summary>
    public static string ActPhrase(Act act, bool hostile)
    {
        return act switch
        {
            Act.Emote => hostile ? "glare at the player" : "wave at the player",
            Act.Bubble => hostile ? "say something sharp to the player" : "call out to the player",
            Act.QueuedLine => hostile ? "tell the player off the next time they talk" : "bring something up the next time the player talks to them",
            Act.Letter => hostile ? "write the player a cold letter" : "write the player a letter",
            // The walk-up wordings are the spread eval's close_friendly and close_hostile, so the
            // viewer's spread panel can compare them with data/laya-calibration.json.
            Act.WalkUp => hostile ? "confront the player" : "walk over to greet the player",
            Act.Visit => "go looking for the player",
            Act.Interrupt => "interrupt the player",
            Act.AskForHelp => "ask the player for help",
            Act.FarmVisit => "ask to visit the player's farm",
            _ => "do something about the player",
        };
    }

    private static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
