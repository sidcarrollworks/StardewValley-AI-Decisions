namespace UnderGlass.Sim;

/// <summary>Quiet reflection is an opt-in experiment, separate from the ordinary desire gate.</summary>
public sealed record ReflectionOptions
{
    public bool Enabled { get; init; }
    public int QuietMinutes { get; init; } = 30;
    public double DailyChance { get; init; } = 0.35;
    public int IntentionDays { get; init; } = 3;
    /// <summary>At most two continuations of one deferred idea, each requiring new known events.
    /// Zero keeps deferral final; it does not disable independent thoughts about new sources.</summary>
    public int MaxReconsiderations { get; init; } = 2;
    public int ReconsiderAfterDays { get; init; } = 1;
    /// <summary>Optional waking inspiration from a recorded sleep; zero preserves quiet-only runs.
    /// A missed dream leaves the daytime opportunity available, with one submitted thought per day.</summary>
    public double DreamChance { get; init; }
}

/// <summary>An authored line with an executable act; an empty Kind means defer or reject.</summary>
public sealed record ReflectionChoice(string Id, string Kind, string Line);

/// <summary>The kind as the character remembers it, and their part in it. Valence is the known
/// kind's signed affect, not access to anyone else's feelings or to an act's hidden true actor.</summary>
public sealed record ReflectionSourceFacts(string Kind, bool OwnDeed, double Valence, double Regard);

/// <summary>An authored possibility, distinct from the character's eventual response. Callers
/// can replace Proposal on a copied request to compare evaluations of the same scene.</summary>
public sealed record ReflectionProposal(string Id, string Thought, string SuggestedChoice, IReadOnlyList<string> Tags);

/// <summary>A link to an earlier deferred idea, not an assertion that the idea was true.
/// PriorThought is a bounded excerpt; the full original remains in the record named by PriorId.</summary>
public sealed record ReflectionContinuity(string RootId, string PriorId, int Revision, int PriorSourceActId,
    string PriorThought, string Reason);

/// <summary>The person's own sleep interval that prompted an imagined possibility on waking.
/// No dream is an observed world event; a null opportunity on a request means ordinary quiet time.</summary>
public sealed record ReflectionOpportunity(string Kind, int SleptAt, int WokeAt);

/// <summary>What the actor knows about where and when (contextual-events spec, "use of the new
/// context in model requests"): the remembered encounter's place and age, the actor's own place and
/// time of day now, their relationship as they know it, the pair's record this week and the actor's
/// own account of why the encounter mattered. Every field is copied from the actor's records at
/// request time, never from hidden world truth, so a mistaken identity stays mistaken. Reasons are
/// short phrases from the actor's appraisal codes; Summary is the one line the evaluator gets.</summary>
public sealed record ReflectionScene(string SourcePlace, int DaysAgo, string Place, string TimeOfDay,
    string Relationship, int KindThisWeek, int UnkindThisWeek, string? LastBetween, IReadOnlyList<string> Reasons)
{
    public string Summary => $"{ReflectionText.Age(DaysAgo)} at {SourcePlace}; now {Place}, {TimeOfDay}; {Relationship}; "
        + $"this week {KindThisWeek} kind/{UnkindThisWeek} unkind" + (LastBetween is null ? "" : $", last {LastBetween}")
        + (Reasons.Count == 0 ? "" : "; " + string.Join(", ", Reasons));
}

/// <summary>The words a request uses for what the actor knows. Pure and public so the vocabulary is
/// testable: every catalog kind an actor can do to someone has a verb, so the generator never sees
/// a bare kind name to guess from ("StoodUpFor" was once read as "stood me up").</summary>
public static class ReflectionText
{
    public static string MemoryVerb(string kind) => kind switch
    {
        "GaveGift" => "gave a gift to", "HelpedSomeone" => "helped", "Argued" => "argued with",
        "Squabbled" => "squabbled with", "Stole" => "stole from",
        "Thanked" => "thanked", "Complimented" => "complimented", "Joked" => "joked with",
        "PlayedGame" => "played a game with", "TreatedToDrink" => "bought a drink for",
        "Welcomed" => "welcomed", "Apologised" => "apologised to",
        "Mocked" => "mocked", "StoodUpFor" => "stood up for", "Comforted" => "comforted",
        "Snubbed" => "snubbed", "TurnedAway" => "turned away from",
        _ => "interacted with",
    };

    public static string TimeOfDay(int minute) => Clock.OfDay(minute) switch
    {
        < 6 * 60 => "night", < 12 * 60 => "morning", < 17 * 60 => "afternoon", < 21 * 60 => "evening", _ => "night",
    };

    /// <summary>How the actor knows the subject, from their own familiarity and household; the
    /// bands are presentation, not a mechanic.</summary>
    public static string Relationship(double familiarity, bool sameHousehold, double newAt)
        => sameHousehold ? "household" : familiarity < newAt ? "stranger" : familiarity < 0.4 ? "acquaintance"
            : familiarity < 0.7 ? "familiar" : "close";

    public static string Age(int daysAgo) => daysAgo <= 0 ? "today" : daysAgo == 1 ? "yesterday" : $"{daysAgo} days ago";

    /// <summary>A place key as the actor would say it. Homes are keyed "Home:{household}": the
    /// actor's own is "home", the subject's is theirs, any other is named as a home so the model
    /// knows it is private ground. Public places keep their key.</summary>
    public static string PlaceWords(string key, string ownHousehold, string subject, string? subjectHousehold)
    {
        if (!key.StartsWith("Home:", StringComparison.Ordinal)) return key;
        string household = key["Home:".Length..];
        return household == ownHousehold ? "home" : household == subjectHousehold ? $"{subject}'s home" : $"the {household} home";
    }

    /// <summary>An appraisal reason code as the actor would put it. Codes that mean "nothing
    /// exceptional" are left out; an unknown code is passed through rather than dressed up.</summary>
    public static string? ReasonPhrase(string code) => code switch
    {
        "household-care" => "ordinary care at home", "repeated" => "not the first such act lately",
        "kindness-after-hurt" => "after a recent hurt between you", "kindness-with-strain" => "while on bad terms",
        "direct-conflict" => "a direct clash", "public-consequence" => "the sort of thing the town talks about",
        "uncertain-account" => "unsure who it was",
        "direct-involvement" or "ordinary-observation" or "known-person" => null,
        _ => code,
    };
}

/// <summary>A copy of one person's known context. Providers must never inspect the simulation.</summary>
public sealed record ReflectionRequest(string Id, int Tick, string Actor, string Subject, int SourceActId,
    string Memory, string Context, IReadOnlyList<ReflectionChoice> Choices,
    ReflectionSourceFacts? Source = null, ReflectionProposal? Proposal = null, ReflectionContinuity? Continuity = null,
    ReflectionOpportunity? Opportunity = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    CharacterInspiration? Character = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    ReflectionScene? Scene = null);

/// <summary>One independently inspectable evaluator pass, including its temporary-label mapping
/// back to the engine's offered choices. Multiple passes do not mean multiple acceptance draws.</summary>
public sealed record ReflectionEvaluation(int Pass, string Prompt, string? Response,
    IReadOnlyDictionary<string, IReadOnlyList<string>> LabelsToChoiceIds,
    IReadOnlyDictionary<string, double>? Weights = null, string? Error = null);

/// <summary>Imagination proposes; the evaluator weights the actual lines. The engine samples once.
/// Backend and Note identify real calls versus authored/fallback material; they are recorded.</summary>
public sealed record ReflectionAnswer(string Thought, string SuggestedChoice,
    IReadOnlyDictionary<string, double> Weights, string Backend, string? Note = null,
    string? LayaPrompt = null, string? GenerationPrompt = null, string? LayaResponse = null,
    IReadOnlyList<ReflectionEvaluation>? Evaluations = null, string? GenerationResponse = null);

public interface IReflectionMind
{
    Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>A timestamped change, never a fabricated memory or witnessed act.</summary>
public sealed record ReflectionEvent(int Tick, string Status, string Text, int ActId = -1);

/// <summary>A thought and its inspectable provenance. Events are filtered by the replay clock.</summary>
public sealed record ReflectionRecord(ReflectionRequest Request, ReflectionAnswer Answer, string Choice,
    IReadOnlyList<ReflectionEvent> Events);

/// <summary>Explicit authored baseline for offline comparisons, not an imitation of model output.</summary>
public sealed class AuthoredReflectionMind : IReflectionMind
{
    public Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReflectionProposal proposal = request.Proposal ?? ReflectionCatalog.Propose(request);
        if (string.IsNullOrWhiteSpace(proposal.Thought) || proposal.Thought.Length > 300
            || !request.Choices.Any(c => c.Id == proposal.SuggestedChoice && c.Kind.Length > 0))
            throw new ArgumentException("The prepared reflection proposal must contain a short thought and an offered executable choice.", nameof(request));
        return Task.FromResult(new ReflectionAnswer(proposal.Thought, proposal.SuggestedChoice,
            request.Choices.ToDictionary(c => c.Id, c => c.Id == proposal.SuggestedChoice ? 3.0 : 1.0), "authored",
            $"Authored proposal {proposal.Id}; weights favor its proposed response."));
    }
}

/// <summary>Re-use recorded decisions only when the complete request matches. A model change or
/// an edited town must not silently apply answers to a different memory or set of lines.</summary>
public sealed class RecordedReflectionMind : IReflectionMind
{
    private readonly Dictionary<string, ReflectionRecord> _records;
    public RecordedReflectionMind(IEnumerable<ReflectionRecord> records)
        => _records = records.ToDictionary(r => r.Request.Id, StringComparer.Ordinal);

    public Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_records.TryGetValue(request.Id, out ReflectionRecord? record)
            || record.Request.Tick != request.Tick || record.Request.Actor != request.Actor
            || record.Request.Subject != request.Subject || record.Request.SourceActId != request.SourceActId
            || record.Request.Memory != request.Memory || record.Request.Context != request.Context
            || record.Request.Source != request.Source || !SameProposal(record.Request.Proposal, request.Proposal)
            || record.Request.Continuity != request.Continuity
            || record.Request.Opportunity != request.Opportunity
            || !CharacterInspirationPack.Same(record.Request.Character, request.Character)
            || !SameScene(record.Request.Scene, request.Scene)
            || !record.Request.Choices.SequenceEqual(request.Choices))
            throw new InvalidOperationException($"Reflection tape does not match request {request.Id}. Use the same seed, town and options.");
        return Task.FromResult(record.Answer);
    }

    private static bool SameProposal(ReflectionProposal? a, ReflectionProposal? b) => a is null || b is null
        ? a is null && b is null
        : a.Id == b.Id && a.Thought == b.Thought && a.SuggestedChoice == b.SuggestedChoice && a.Tags.SequenceEqual(b.Tags);

    // A recording made before scenes were supplied (Scene null) cannot answer a request that
    // carries one: the model would have seen less than the request says it saw.
    private static bool SameScene(ReflectionScene? a, ReflectionScene? b) => a is null || b is null
        ? a is null && b is null
        : a.SourcePlace == b.SourcePlace && a.DaysAgo == b.DaysAgo && a.Place == b.Place && a.TimeOfDay == b.TimeOfDay
            && a.Relationship == b.Relationship && a.KindThisWeek == b.KindThisWeek && a.UnkindThisWeek == b.UnkindThisWeek
            && a.LastBetween == b.LastBetween && a.Reasons.SequenceEqual(b.Reasons);
}
