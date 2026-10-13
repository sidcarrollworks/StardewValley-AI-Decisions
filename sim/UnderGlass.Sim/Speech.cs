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
/// archetype, relationship and mood, weighted rather than exclusive, so a person has range; the
/// last few lines the same speaker used for the same kind are avoided while anything else fits.</summary>
public static class SpeechCatalog
{
    public static readonly IReadOnlyList<string> Relationships = new[] { "household", "stranger", "acquaintance", "familiar", "close" };
    public static readonly IReadOnlyList<string> Moods = new[] { "low", "even", "high" };
    /// <summary>Acts that are not spoken: turning away from someone says nothing.</summary>
    public static readonly IReadOnlySet<string> Silent = new HashSet<string>(StringComparer.Ordinal) { "Snubbed", "TurnedAway" };
    public const int MinChars = 12, MaxChars = 110;
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

    /// <summary>The line for an act, or null when the library has nothing for the kind. Lines that
    /// name the archetype weigh 3, a matching relationship or mood doubles the weight each; a
    /// line tagged for a different archetype, relationship or mood is out. The draw is keyed by
    /// the act id on its own stream, so speech never moves another system's dice.</summary>
    public static SpeechLine? Choose(SpeechPack pack, string kind, string? archetype, string relationship, string mood,
        long seed, int actId, IReadOnlyCollection<string>? avoid = null)
    {
        ArgumentNullException.ThrowIfNull(pack);
        if (Silent.Contains(kind)) return null;
        var fits = pack.Lines.Where(l => l.Kind == kind
            && (l.Archetypes.Count == 0 || archetype is not null && l.Archetypes.Contains(archetype))
            && (l.Relationship.Count == 0 || l.Relationship.Contains(relationship))
            && (l.Mood.Count == 0 || l.Mood.Contains(mood))).ToArray();
        if (fits.Length == 0) return null;
        // The speaker's recent lines for this kind are left out while anything else fits.
        if (avoid is { Count: > 0 })
        {
            var fresh = fits.Where(l => !avoid.Contains(l.Id)).ToArray();
            if (fresh.Length > 0) fits = fresh;
        }
        double[] weights = fits.Select(l => (l.Archetypes.Count > 0 ? 3.0 : 1.0) * (l.Relationship.Count > 0 ? 2.0 : 1.0)
            * (l.Mood.Count > 0 ? 2.0 : 1.0)).ToArray();
        double roll = Rng.Unit(seed, "speech", actId.ToString(System.Globalization.CultureInfo.InvariantCulture)) * weights.Sum();
        for (int i = 0; i < fits.Length; i++)
        {
            roll -= weights[i];
            if (roll < 0) return fits[i];
        }
        return fits[^1];
    }

    public static string Render(SpeechLine line, string subject) => line.Text.Replace("{subject}", subject, StringComparison.Ordinal);
}
