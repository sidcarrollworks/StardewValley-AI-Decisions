using System.Text.Json;

namespace UnderGlass.Sim;

/// <summary>Spoken lines for acts (roadmap, "The agreed order", step 2). Off by default: with it on,
/// every act an actor does to a chosen person gets one authored line chosen deterministically from
/// the tagged library and recorded beside the act. Speech is presentation: it reads the actor's
/// state and changes nothing in the world, so the pinned runs are byte-identical with it off and
/// every decision is identical with it on.</summary>
public sealed record SpeechOptions
{
    public bool Enabled { get; init; }
}

/// <summary>One authored line and where it fits. Empty lists mean "any". Archetypes name reusable
/// voices from the character pack; without a pack, only archetype-free lines are used.</summary>
public sealed record SpeechLine(string Id, string Kind, IReadOnlyList<string> Archetypes,
    IReadOnlyList<string> Relationship, IReadOnlyList<string> Mood, string Text,
    IReadOnlyList<string> Tags, string? Note = null);

public sealed record SpeechPack(int Schema, string Provenance, IReadOnlyList<SpeechLine> Lines);

/// <summary>What was said with an act, as recorded: the line as spoken (subject filled in), its
/// library id, and the bands the choice was made under so the observer can see why this line.</summary>
public sealed record Speech(int ActId, int Tick, string Speaker, string Line, string LineId,
    string? Archetype, string Relationship, string Mood);

/// <summary>The library is embedded from speech-catalog.json; editing that file and rebuilding
/// changes what people say, not what they do. Selection prefers lines that fit the speaker's
/// archetype, relationship and mood, weighted rather than exclusive, so a person has range; a line
/// written for the neighbouring relationship or mood band still fits, less well; the last few
/// lines the same speaker used for the same kind are avoided while anything else fits.</summary>
public static class SpeechCatalog
{
    public static readonly IReadOnlyList<string> Relationships = new[] { "household", "stranger", "acquaintance", "familiar", "close" };
    public static readonly IReadOnlyList<string> Moods = new[] { "low", "even", "high" };
    /// <summary>Acts that are not spoken: turning away from someone says nothing.</summary>
    public static readonly IReadOnlySet<string> Silent = new HashSet<string>(StringComparer.Ordinal) { "Snubbed", "TurnedAway" };
    public const int MinChars = 12, MaxChars = 110;
    /// <summary>Weights: a line in the speaker's own archetype, a tag that names the band exactly,
    /// a tag that names a neighbouring band. An untagged field counts 1.</summary>
    public const double ArchetypeWeight = 3.0, ExactWeight = 2.0, NeighbourWeight = 0.5;
    /// <summary>The bands a line may reach beyond its own, read as "the speaker's band, then the
    /// tags it may borrow from". Relationship steps one rung of familiarity; household borrows close
    /// lines, but a household line never leaves the household (sibling quarrels and chores are not
    /// said to a friend). Mood steps one band: a low line is never said in high spirits.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NearRelationship = new Dictionary<string, IReadOnlyList<string>>
    {
        ["household"] = new[] { "close" }, ["stranger"] = new[] { "acquaintance" }, ["acquaintance"] = new[] { "stranger", "familiar" },
        ["familiar"] = new[] { "acquaintance", "close" }, ["close"] = new[] { "familiar" },
    };
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NearMood = new Dictionary<string, IReadOnlyList<string>>
    {
        ["low"] = new[] { "even" }, ["even"] = new[] { "low", "high" }, ["high"] = new[] { "even" },
    };
    private static readonly Lazy<SpeechPack> Embedded = new(() =>
    {
        using Stream stream = typeof(SpeechCatalog).Assembly.GetManifestResourceStream("UnderGlass.Sim.speech-catalog.json")
            ?? throw new InvalidDataException("Missing embedded speech catalog.");
        using var reader = new StreamReader(stream);
        return Read(reader.ReadToEnd());
    });

    public static SpeechPack Default => Embedded.Value;

    /// <summary>The mood band for a squashed mood in (-1, 1).</summary>
    public static string MoodBand(double mood) => mood < -0.2 ? "low" : mood > 0.2 ? "high" : "even";

    public static SpeechPack Read(string json)
    {
        SpeechPack pack = JsonSerializer.Deserialize<SpeechPack>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Empty speech catalog.");
        if (pack.Schema != 1 || string.IsNullOrWhiteSpace(pack.Provenance) || pack.Lines is null || pack.Lines.Count == 0)
            throw new InvalidDataException("Speech catalog needs schema 1, provenance and at least one line.");
        if (pack.Lines.Select(l => l.Id).Distinct(StringComparer.Ordinal).Count() != pack.Lines.Count)
            throw new InvalidDataException("Speech catalog line ids must be unique.");
        foreach (SpeechLine line in pack.Lines)
        {
            string remaining = (line.Text ?? "").Replace("{subject}", "", StringComparison.Ordinal);
            if (string.IsNullOrWhiteSpace(line.Id) || string.IsNullOrWhiteSpace(line.Kind) || line.Text is null
                || line.Text.Trim().Length < MinChars || line.Text.Length > MaxChars || line.Text != line.Text.Trim()
                || remaining.IndexOfAny(new[] { '{', '}', '$', '#', '%', '@' }) >= 0 || line.Text.Any(char.IsControl)
                || line.Archetypes is null || line.Relationship is null || line.Mood is null || line.Tags is null
                || line.Archetypes.Any(string.IsNullOrWhiteSpace) || line.Tags.Any(string.IsNullOrWhiteSpace)
                || line.Relationship.Any(r => !Relationships.Contains(r)) || line.Mood.Any(m => !Moods.Contains(m)))
                throw new InvalidDataException($"Invalid speech line {line.Id}.");
        }
        return pack with { Lines = Array.AsReadOnly(pack.Lines.OrderBy(l => l.Id, StringComparer.Ordinal)
            .Select(l => l with { Archetypes = Array.AsReadOnly(l.Archetypes.ToArray()), Relationship = Array.AsReadOnly(l.Relationship.ToArray()),
                Mood = Array.AsReadOnly(l.Mood.ToArray()), Tags = Array.AsReadOnly(l.Tags.ToArray()) }).ToArray()) };
    }

    /// <summary>How well a line fits a speaker, 0 when it does not. A line tagged for another
    /// archetype is out; a relationship or mood tag that names the speaker's band doubles the weight,
    /// one that only names a neighbouring band halves it, and one that names neither rules it out.
    /// The viewer's speech page mirrors this rule; `speech-weights.json` pins both sides.</summary>
    public static double Weight(SpeechLine line, string? archetype, string relationship, string mood)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Archetypes.Count > 0 && (archetype is null || !line.Archetypes.Contains(archetype))) return 0;
        return (line.Archetypes.Count > 0 ? ArchetypeWeight : 1.0)
            * Fit(line.Relationship, relationship, NearRelationship) * Fit(line.Mood, mood, NearMood);
    }

    private static double Fit(IReadOnlyList<string> tags, string band, IReadOnlyDictionary<string, IReadOnlyList<string>> near)
        => tags.Count == 0 ? 1.0
            : tags.Contains(band) ? ExactWeight
            : near.TryGetValue(band, out IReadOnlyList<string>? next) && next.Any(tags.Contains) ? NeighbourWeight
            : 0.0;

    /// <summary>The line for an act, or null when the library has nothing for the kind. Every line
    /// of the kind with a positive <see cref="Weight"/> is a candidate, drawn in proportion to its
    /// weight. The draw is keyed by the act id on its own stream, so speech never moves another
    /// system's dice.</summary>
    public static SpeechLine? Choose(SpeechPack pack, string kind, string? archetype, string relationship, string mood,
        long seed, int actId, IReadOnlyCollection<string>? avoid = null)
    {
        ArgumentNullException.ThrowIfNull(pack);
        if (Silent.Contains(kind)) return null;
        var fits = pack.Lines.Where(l => l.Kind == kind).Select(l => (Line: l, Weight: Weight(l, archetype, relationship, mood)))
            .Where(f => f.Weight > 0).ToArray();
        if (fits.Length == 0) return null;
        // The speaker's recent lines for this kind are left out while anything else fits.
        if (avoid is { Count: > 0 })
        {
            var fresh = fits.Where(f => !avoid.Contains(f.Line.Id)).ToArray();
            if (fresh.Length > 0) fits = fresh;
        }
        double roll = Rng.Unit(seed, "speech", actId.ToString(System.Globalization.CultureInfo.InvariantCulture)) * fits.Sum(f => f.Weight);
        foreach (var (line, weight) in fits)
        {
            roll -= weight;
            if (roll < 0) return line;
        }
        return fits[^1].Line;
    }

    public static string Render(SpeechLine line, string subject) => line.Text.Replace("{subject}", subject, StringComparison.Ordinal);
}
