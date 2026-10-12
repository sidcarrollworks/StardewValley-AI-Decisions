using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnderGlass.Sim;

/// <summary>Reusable tendencies and voice, not a donor character's biography or knowledge.</summary>
public sealed record InspirationArchetype(string Id, string Drives, string Coping, string Voice,
    string EvaluatorCue, IReadOnlyList<string> Sources);

/// <summary>Reviewed initial self-knowledge. Subjects selects relevance, not who else knows it.
/// Conditional material is not supported and must stay outside the runtime pack.</summary>
public sealed record CharacterBackground(string Id, string Text, IReadOnlyList<string> Subjects,
    IReadOnlyList<string> Sources, string? Condition = null);

public sealed record CharacterInspirationProfile(string Name, string ArchetypeId, string VoiceAccent,
    IReadOnlyList<CharacterBackground> Background,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Sources = null);

/// <summary>A copied private context receipt. Background never becomes a witnessed act, belief,
/// reflection source, social prior or gossip. A generic archetype has no personal background.</summary>
public sealed record CharacterInspiration(string PackHash, string ArchetypeId, string Voice,
    string EvaluatorCue, CharacterBackground? Background, IReadOnlyList<string> Sources,
    string? Provenance = null);

/// <summary>An explicit content pack, independent of TownData and disabled unless configured.
/// Sources are reviewable provenance, not runtime instructions or automatic evidence.</summary>
public sealed record CharacterInspirationPack(int Schema, string Provenance,
    IReadOnlyList<InspirationArchetype> Archetypes, IReadOnlyList<CharacterInspirationProfile> Characters)
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static CharacterInspirationPack Read(string json)
        => (JsonSerializer.Deserialize<CharacterInspirationPack>(json, ReadOptions)
            ?? throw new InvalidDataException("The inspiration pack is empty.")).CopyValidated();

    /// <summary>The complete pack, including source references, in deterministic order.</summary>
    [JsonIgnore]
    public string ContentHash => CopyValidated().HashValidated();

    /// <summary>Explicitly reuse an archetype without copying anyone's personal history.
    /// This does not assign it to a character or change a simulation.</summary>
    public CharacterInspiration ForArchetype(string archetypeId)
    {
        CharacterInspirationPack pack = CopyValidated();
        InspirationArchetype archetype = pack.Archetypes.FirstOrDefault(a => a.Id == archetypeId)
            ?? throw new ArgumentException($"Unknown inspiration archetype {archetypeId}.", nameof(archetypeId));
        return pack.Snapshot(archetype, "", null, pack.HashValidated());
    }

    /// <summary>Prepare the same copied private context that a simulation request would use.
    /// Subject is the actor's believed subject; unknown actors have no inherited profile.</summary>
    public CharacterInspiration? ForCharacter(string actor, string subject, long seed, string requestId)
    {
        CharacterInspirationPack pack = CopyValidated();
        return pack.Resolve(actor, subject, seed, requestId, pack.HashValidated());
    }

    internal CharacterInspirationPack CopyValidated()
    {
        if (Schema != 1) throw Invalid("Only inspiration pack schema 1 is supported.");
        Text(Provenance, 2000, "pack provenance");
        if (Archetypes is null || Archetypes.Count == 0 || Characters is null)
            throw Invalid("The pack needs archetypes and a character list.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var archetypes = new List<InspirationArchetype>();
        foreach (InspirationArchetype a in Archetypes)
        {
            if (a is null) throw Invalid("An archetype is null.");
            Text(a.Id, 100, "archetype ID");
            if (!ids.Add(a.Id)) throw Invalid($"Duplicate archetype {a.Id}.");
            Text(a.Drives, 300, $"{a.Id} drives");
            Text(a.Coping, 300, $"{a.Id} coping");
            Text(a.Voice, 300, $"{a.Id} voice");
            Text(a.EvaluatorCue, 90, $"{a.Id} evaluator cue");
            Text(ComposeVoice(a, ""), 360, $"{a.Id} composed voice");
            archetypes.Add(a with { Sources = Strings(a.Sources, false, $"{a.Id} sources") });
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        var characters = new List<CharacterInspirationProfile>();
        foreach (CharacterInspirationProfile p in Characters)
        {
            if (p is null) throw Invalid("A character profile is null.");
            Text(p.Name, 100, "character name");
            if (!names.Add(p.Name)) throw Invalid($"Duplicate character profile {p.Name}.");
            if (p.ArchetypeId is null || !ids.Contains(p.ArchetypeId)) throw Invalid($"Unknown archetype for {p.Name}.");
            if (p.VoiceAccent is null || p.VoiceAccent.Length > 300 || HasControl(p.VoiceAccent))
                throw Invalid($"Invalid voice accent for {p.Name}.");
            Text(ComposeVoice(archetypes.Single(a => a.Id == p.ArchetypeId), p.VoiceAccent), 360, $"{p.Name} composed voice");
            if (p.Background is null) throw Invalid($"Missing background list for {p.Name}.");
            var background = new List<CharacterBackground>();
            var backgroundIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterBackground b in p.Background)
            {
                if (b is null) throw Invalid($"Null background for {p.Name}.");
                Text(b.Id, 100, $"{p.Name} background ID");
                Text(b.Text, 120, $"{p.Name}/{b.Id} background text");
                if (!backgroundIds.Add(b.Id)) throw Invalid($"Duplicate background {p.Name}/{b.Id}.");
                if (b.Condition is not null) throw Invalid($"Conditional background {p.Name}/{b.Id} is not initial self-knowledge.");
                background.Add(b with
                {
                    Subjects = Strings(b.Subjects, true, $"{p.Name}/{b.Id} subjects"),
                    Sources = Strings(b.Sources, false, $"{p.Name}/{b.Id} sources"),
                });
            }
            characters.Add(p with
            {
                Background = Array.AsReadOnly(background.OrderBy(b => b.Id, StringComparer.Ordinal).ToArray()),
                Sources = p.Sources is null ? null : Strings(p.Sources, false, $"{p.Name} voice sources"),
            });
        }
        return this with
        {
            Archetypes = Array.AsReadOnly(archetypes.OrderBy(a => a.Id, StringComparer.Ordinal).ToArray()),
            Characters = Array.AsReadOnly(characters.OrderBy(p => p.Name, StringComparer.Ordinal).ToArray()),
        };
    }

    internal string HashValidated() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this)))).ToLowerInvariant();

    internal CharacterInspiration? Resolve(string actor, string subject, long seed, string requestId, string hash)
    {
        CharacterInspirationProfile? profile = Characters.FirstOrDefault(p => p.Name == actor);
        if (profile is null) return null;
        InspirationArchetype archetype = Archetypes.Single(a => a.Id == profile.ArchetypeId);
        CharacterBackground[] candidates = profile.Background.Where(b => b.Subjects.Contains(subject, StringComparer.Ordinal)).ToArray();
        if (candidates.Length == 0) candidates = profile.Background.Where(b => b.Subjects.Count == 0).ToArray();
        CharacterBackground? selected = candidates.Length == 0 ? null
            : candidates[Rng.Range(seed, 0, candidates.Length - 1, "inspiration-background", actor, subject, requestId)];
        return Snapshot(archetype, profile.VoiceAccent, selected, hash, profile.Sources);
    }

    private CharacterInspiration Snapshot(InspirationArchetype archetype, string accent, CharacterBackground? background,
        string hash, IReadOnlyList<string>? personalSources = null)
        => new(hash, archetype.Id, ComposeVoice(archetype, accent), archetype.EvaluatorCue, background,
            personalSources is null ? archetype.Sources : Array.AsReadOnly(archetype.Sources.Concat(personalSources)
                .Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToArray()), Provenance);

    private static string ComposeVoice(InspirationArchetype a, string accent)
        => $"Drives: {a.Drives} Coping: {a.Coping} Voice: {a.Voice}{(accent.Length > 0 ? " " + accent : "")}";

    private static IReadOnlyList<string> Strings(IReadOnlyList<string>? values, bool emptyAllowed, string name)
    {
        if (values is null || !emptyAllowed && values.Count == 0) throw Invalid($"Missing {name}.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            Text(value, 1000, name);
            if (!seen.Add(value)) throw Invalid($"Duplicate {name}: {value}.");
        }
        return Array.AsReadOnly(values.OrderBy(s => s, StringComparer.Ordinal).ToArray());
    }

    private static bool HasControl(string value) => value.Any(char.IsControl);
    private static void Text(string? value, int limit, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > limit || HasControl(value))
            throw Invalid($"Invalid {name}: expected nonempty text of at most {limit} characters without control characters.");
    }
    private static InvalidDataException Invalid(string message) => new(message);

    internal static bool Same(CharacterInspiration? a, CharacterInspiration? b)
        => a is null || b is null ? a is null && b is null
            : a.PackHash == b.PackHash && a.ArchetypeId == b.ArchetypeId && a.Voice == b.Voice
                && a.EvaluatorCue == b.EvaluatorCue && a.Provenance == b.Provenance
                && a.Sources.SequenceEqual(b.Sources) && SameBackground(a.Background, b.Background);

    private static bool SameBackground(CharacterBackground? a, CharacterBackground? b)
        => a is null || b is null ? a is null && b is null
            : a.Id == b.Id && a.Text == b.Text && a.Condition == b.Condition
                && a.Subjects.SequenceEqual(b.Subjects) && a.Sources.SequenceEqual(b.Sources);
}
