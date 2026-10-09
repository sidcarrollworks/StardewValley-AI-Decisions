using System.Globalization;

namespace UnderGlass.Sim;

/// <summary>Private possibilities, never evidence. Network work is performed only between simulation
/// minutes by RunAsync; answers take effect at the next minute regardless of wall-clock latency.</summary>
public sealed partial class Simulation
{
    private ReflectionOptions _reflection = new();
    private readonly List<ReflectionRequest> _reflectionRequests = new();
    private readonly List<InnerThought> _thoughts = new();
    private readonly Dictionary<string, int> _quietSince = new();
    private readonly HashSet<(string Actor, int Day)> _reflectionDays = new();
    private readonly HashSet<(string Actor, int Source)> _reflectedMemories = new();

    private sealed class InnerThought
    {
        public required ReflectionRequest Request;
        public required ReflectionAnswer Answer;
        public required ReflectionChoice Choice;
        public readonly List<ReflectionEvent> Events = new();
        public int Applied = -1;
        public int ActId = -1;
        public bool Finished;
    }

    public void ConfigureReflection(ReflectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.QuietMinutes < 1 || options.IntentionDays < 1 || !double.IsFinite(options.DailyChance)
            || options.DailyChance is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(options));
        if (options.Enabled && !Acting)
            throw new ArgumentException("Reflection requires feelings and desire to act, not observe or off.", nameof(options));
        _reflection = options;
    }

    private IReadOnlyList<ReflectionRecord> ReflectionRecords() => _thoughts
        .Select(t => new ReflectionRecord(t.Request, t.Answer, t.Choice.Id, t.Events.ToArray())).ToArray();

    // Called after ordinary acts finish; a sustained idle stretch supplies the opportunity.
    private void Reflect(int m)
    {
        if (!_reflection.Enabled) return;
        foreach (Person p in _people)
        {
            string actor = p.V.Name;
            // Small idle wandering around a haunt is still unhurried free time. Travel to a
            // different destination, work and an encounter interrupt the quiet stretch.
            if (!Free(p, m) || p.Place != p.GoalPlace || Working(p, m))
            {
                _quietSince.Remove(actor);
                continue;
            }
            if (!_quietSince.TryGetValue(actor, out int since)) _quietSince[actor] = since = m;
            if (p.Walking || m - since < _reflection.QuietMinutes || _reflectionDays.Contains((actor, Clock.Day(m)))
                || _thoughts.Any(t => !t.Finished && t.Request.Actor == actor)) continue;

            var memories = ReflectionMemories(actor, m).Where(x => !_reflectedMemories.Contains((actor, x.Source)))
                .OrderByDescending(x => x.Tick).ThenBy(x => x.Source).Take(6).ToArray();
            if (memories.Length == 0) continue;
            _reflectionDays.Add((actor, Clock.Day(m))); // one opportunity, not repeated rolls until yes
            if (Rng.Unit(_seed, "reflect", actor, Clock.Day(m).ToString(CultureInfo.InvariantCulture)) >= _reflection.DailyChance)
                continue;
            int pick = Math.Min(memories.Length - 1, (int)(Rng.Unit(_seed, "reflect-memory", actor,
                Clock.Day(m).ToString(CultureInfo.InvariantCulture)) * memories.Length));
            var memory = memories[pick];
            // The row is looked up by the remembered kind, never via the source act's true
            // actor or hidden details. Unrecognized kinds stay neutral rather than guessed.
            double valence = _kindByName.TryGetValue(memory.Kind, out ActKind? knownKind)
                ? knownKind.Affect?.Joy ?? knownKind.Valence : 0;
            var source = new ReflectionSourceFacts(memory.Kind, memory.OwnDeed, valence, St(actor, memory.Subject));
            var choices = ReflectionChoices(actor, source);
            if (!choices.Any(c => c.Kind.Length > 0)) continue;
            _reflectedMemories.Add((actor, memory.Source));
            int h = _index[actor];
            Temperament c = CharacterOf(h);
            string context = FormattableString.Invariant($"{actor}, age {p.V.Age}: mood {MoodOf(h):0.00}; regard for {memory.Subject} {St(actor, memory.Subject):0.00}. Bold {c.Boldness:0.00}; understanding {c.Understanding:0.00}; sensitive {c.Sensitivity:0.00}; self-regard {c.SelfRegard:0.00}; expression {c.Expression:0.00}. Quiet free time. Choose for this person, not for a well-behaved town.");
            var request = new ReflectionRequest($"{_seed}:{m}:{actor}:{memory.Source}", m, actor,
                memory.Subject, memory.Source, memory.Text, context, choices.AsReadOnly(), source);
            _reflectionRequests.Add(request with { Proposal = ReflectionCatalog.Propose(request) });
        }
    }

    private IEnumerable<(int Source, int Tick, string Subject, string Text, string Kind, bool OwnDeed)> ReflectionMemories(string actor, int m)
    {
        int earliest = m - 7 * Clock.MinutesPerDay;
        // The person's own deeds, and only deeds completed already: no truth about other minds.
        // Patient.Actor rows reverse the usual roles (the person warned is Act.Actor,
        // the official is Target). Exclude these on both paths until they have explicit
        // perspective templates; otherwise the recipient falsely remembers doing the harm.
        foreach (Act a in _acts.Where(a => a.Actor == actor && a.Target is not null && a.Target != actor
                     && a.Tick >= earliest && !_watching.ContainsKey(a.Id) && KindOf(a).Affect?.Patient != Patient.Actor))
            yield return (a.Id, a.Tick, a.Target!, $"At {Clock.Format(a.Tick)}, you {MemoryVerb(a.Kind)} {a.Target} ({a.Kind}). You took part.", a.Kind, true);
        // Use the believed actor and target, never substitute the world's true identity.
        foreach (Belief b in _beliefs[actor].Values.OrderBy(b => b.ActId))
        {
            if (b.Source != Source.Witnessed || b.Actor is null || b.Actor == actor || b.Target != actor
                || b.GotTick < earliest || !_index.ContainsKey(b.Actor)
                || !_kindByName.TryGetValue(b.Kind, out ActKind? kind) || kind.Affect?.Patient == Patient.Actor) continue;
            yield return (b.ActId, b.GotTick, b.Actor,
                $"At {Clock.Format(b.GotTick)}, you remember {b.Actor} {MemoryVerb(b.Kind)} you ({b.Kind}); confidence {b.Confidence.ToString("0.00", CultureInfo.InvariantCulture)}.", b.Kind, false);
        }
    }

    private static string MemoryVerb(string kind) => kind switch
    {
        "GaveGift" => "gave a gift to", "HelpedSomeone" => "helped", "Argued" => "argued with",
        "Thanked" => "thanked", "Complimented" => "complimented", "Apologised" => "apologised to",
        "Mocked" => "mocked", "Comforted" => "comforted", "TreatedToDrink" => "bought a drink for",
        _ => "interacted with",
    };

    private List<ReflectionChoice> ReflectionChoices(string actor, ReflectionSourceFacts source)
    {
        var list = new List<ReflectionChoice>();
        int h = _index[actor];
        // Source-aware authored alternatives are read by the evaluator, never chosen by
        // ordinary gate cost ordering. A kindness does not assume a preceding argument.
        IReadOnlyDictionary<string, string> lines = ReflectionCatalog.Lines(source, CharacterOf(h));
        foreach (var (id, kind) in new[] { ("gift", "GaveGift"), ("help", "HelpedSomeone"), ("confront", "Argued") })
            if (_kindByName.TryGetValue(kind, out ActKind? k) && k.FitsAge(_cast[h].Age) && k.Affect?.Target == TargetIs.Chosen)
                list.Add(new ReflectionChoice(id, kind, lines[id]));
        list.Add(new ReflectionChoice("defer", "", lines["defer"]));
        list.Add(new ReflectionChoice("reject", "", lines["reject"]));
        return list;
    }

    private async Task AnswerReflections(IReflectionMind mind, CancellationToken cancellationToken)
    {
        foreach (ReflectionRequest request in _reflectionRequests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReflectionAnswer answer = await mind.ReflectAsync(request, cancellationToken).ConfigureAwait(false);
            if (answer is null || answer.Weights is null || string.IsNullOrWhiteSpace(answer.Thought) || answer.Thought.Length > 300
                || !request.Choices.Any(c => c.Id == answer.SuggestedChoice && c.Kind.Length > 0)
                || answer.Weights.Count != request.Choices.Count
                || request.Choices.Any(c => !answer.Weights.TryGetValue(c.Id, out double w) || !double.IsFinite(w) || w < 0)
                || !double.IsFinite(answer.Weights.Values.Sum()) || answer.Weights.Values.Sum() <= 0)
            {
                answer = (await new AuthoredReflectionMind().ReflectAsync(request, cancellationToken)) with
                    { Backend = "authored-fallback", Note = "Invalid thought, suggestion or choice weights." };
            }
            // Normalize a copied table and draw once. No argmax and no retry until acceptance.
            double sum = answer.Weights.Values.Sum();
            // Recorded probabilities are already normalized. Avoid a second rounding change
            // when feeding a tape back in, preserving exact receipts and the sampled choice.
            double divisor = Math.Abs(sum - 1) < 1e-12 ? 1 : sum;
            answer = answer with { Weights = request.Choices.ToDictionary(c => c.Id, c => answer.Weights[c.Id] / divisor) };
            double roll = Rng.Unit(_seed, "reflection-choice", request.Id);
            ReflectionChoice selected = request.Choices[^1];
            foreach (ReflectionChoice c in request.Choices)
            {
                roll -= answer.Weights[c.Id];
                if (roll < 0) { selected = c; break; }
            }
            _thoughts.Add(new InnerThought { Request = request, Answer = answer, Choice = selected });
        }
        _reflectionRequests.Clear();
    }

    // Before ordinary choices, at the next simulation minute. Acceptance doesn't teleport a target
    // or overrule physical limits; this prototype waits for an ordinary encounter.
    private void ReflectAct(int m)
    {
        if (!_reflection.Enabled) return;
        foreach (InnerThought t in _thoughts.Where(t => !t.Finished))
        {
            if (m <= t.Request.Tick) continue;
            string actor = t.Request.Actor, subject = t.Request.Subject;
            if (t.Applied < 0)
            {
                t.Applied = m;
                t.Events.Add(new(m, "considered", t.Answer.Thought));
                string status = t.Choice.Kind.Length == 0 ? t.Choice.Id == "defer" ? "deferred" : "rejected"
                    : t.Choice.Id == t.Answer.SuggestedChoice ? "accepted" : "reshaped";
                t.Events.Add(new(m, status, t.Choice.Line));
                _log.Add($"{m} reflection {actor} {status} toward {subject} source {t.Request.SourceActId}");
                if (t.Choice.Kind.Length == 0) { t.Finished = true; continue; }
                t.Events.Add(new(m, "waiting", $"Intends to {PlanVerb(t.Choice.Kind)} {subject} when an opportunity arises."));
            }
            if (t.ActId >= 0)
            {
                LifeEvent? life = _life.LastOrDefault(l => l.Person == actor && l.ActId == t.ActId && l.Role == LifeRole.Did);
                if (life is not null && life.Outcome != Outcome.Open)
                {
                    t.Events.Add(new(m, "outcome", $"The encounter's recorded outcome: {life.Outcome}.", t.ActId));
                    t.Finished = true;
                }
                continue;
            }
            if (m - t.Applied >= _reflection.IntentionDays * Clock.MinutesPerDay)
            {
                t.Events.Add(new(m, "expired", "The intention faded before a suitable encounter."));
                t.Finished = true;
                continue;
            }
            int h = _index[actor], s = _index[subject];
            Person p = _people[h], other = _people[s];
            ActKind kind = _kindByName[t.Choice.Kind];
            if (!Free(p, m) || !Free(other, m) || p.Walking || other.Walking || Working(p, m) || Working(other, m)
                || !InReach(p, other, m) || !kind.FitsAge(p.V.Age)
                || kind.Allowed.Count > 0 && !kind.Allowed.Contains(p.Place)
                || !Fits(kind, h, s, m) || _slotsUsed.GetValueOrDefault((h, s, Clock.Day(m))) >= _fo.SlotsPerDay
                || IsHeavyHostile(kind) && _lastHostile.TryGetValue((h, s), out int last)
                    && m - last < _fo.HostileCooldownDays * Clock.MinutesPerDay) continue;
            t.ActId = _acts.Count;
            _slotsUsed[(h, s, Clock.Day(m))] = _slotsUsed.GetValueOrDefault((h, s, Clock.Day(m))) + 1;
            _pursuedActs.Add(t.ActId);
            // The recorded thought explains this choice. Don't retrofit the strongest
            // sentiment as its cause; that can contradict the actual model decision.
            Begin(m, kind, p, false, subject, t.Request.SourceActId, fromReflection: true);
            t.Events.Add(new(m, "acted", $"{actor} to {subject}: “{t.Choice.Line}” ({kind.Name})", t.ActId));
            _log.Add($"{m} reflection {actor} acted {kind.Name} toward {subject} act {t.ActId} source {t.Request.SourceActId}");
        }
    }

    private static string PlanVerb(string kind) => kind switch
        { "GaveGift" => "give a gift to", "HelpedSomeone" => "help", _ => "confront" };

    private static bool Working(Person p, int m) => p.V.Job is { } job && job.WorksOn(Clock.Weekday(m))
        && Clock.OfDay(m) >= job.Start && Clock.OfDay(m) < job.End;
}
