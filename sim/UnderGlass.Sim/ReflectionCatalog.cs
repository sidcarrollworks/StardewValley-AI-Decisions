using System.Text.Json;

namespace UnderGlass.Sim;

/// <summary>One editable imagined motive, with explicit applicability rather than inference from
/// prose. Regard intervals overlap deliberately: people can have contradictory possibilities.</summary>
public sealed record ReflectionCatalogEntry(string Id, string Perspective, string Tone,
    double MinRegard, double MaxRegard, string Choice, string Thought, IReadOnlyList<string> Tags);

/// <summary>The small authored catalog is embedded from reflection-catalog.json. Editing that
/// file and rebuilding changes content, not mechanics. It only reads a request's copied facts.</summary>
public static class ReflectionCatalog
{
    private sealed record Responses(string Id, string Gift, string QuietGift, string Help,
        string QuietHelp, string Confront, string QuietConfront);
    private sealed record Pack(int Version, IReadOnlyList<ReflectionCatalogEntry> Thoughts,
        IReadOnlyList<Responses> Responses);
    private static readonly Lazy<Pack> Content = new(Read);

    public static IReadOnlyList<ReflectionCatalogEntry> Entries => Content.Value.Thoughts;

    public static ReflectionProposal Propose(ReflectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ReflectionChoice[] acts = request.Choices.Where(c => c.Kind.Length > 0)
            .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        if (acts.Length == 0) throw new ArgumentException("Reflection needs an offered executable act.", nameof(request));
        var ids = acts.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        string perspective = request.Source is null ? "any" : request.Source.OwnDeed ? "own" : "received";
        string tone = Tone(request.Source);
        double regard = request.Source?.Regard ?? 0;
        var candidates = Entries.Where(e => e.Tone == tone && (e.Perspective == "any" || e.Perspective == perspective)
            && regard >= e.MinRegard && regard <= e.MaxRegard && ids.Contains(e.Choice)).ToArray();
        // A custom town may not offer the act a specific motive needs. Use a grounded neutral
        // possibility about the supplied memory; never invent an unsupported action.
        if (candidates.Length == 0) candidates = Entries.Where(e => e.Tone == "neutral" && e.Choice == "*").ToArray();
        candidates = candidates.OrderBy(e => e.Id, StringComparer.Ordinal).ToArray();
        // Request IDs contain the simulation seed, tick, actor and source. This independent
        // stream neither consumes another system's draw nor depends on catalog file ordering.
        ReflectionCatalogEntry chosen = candidates[Rng.Range(0, 0, candidates.Length - 1, "reflection-proposal", request.Id)];
        string choice = chosen.Choice == "*"
            ? acts[Rng.Range(0, 0, acts.Length - 1, "reflection-neutral-choice", request.Id)].Id : chosen.Choice;
        string thought = chosen.Thought.Replace("{actor}", request.Actor, StringComparison.Ordinal)
            .Replace("{subject}", request.Subject, StringComparison.Ordinal)
            .Replace("{memory}", Clip(request.Memory, 110), StringComparison.Ordinal);
        if (thought.Length > 300) throw new ArgumentException("Rendered authored thought exceeds 300 characters.", nameof(request));
        return new ReflectionProposal(chosen.Id, thought, choice, Array.AsReadOnly(chosen.Tags.ToArray()));
    }

    /// <summary>All alternatives fit the remembered kind of encounter, even when their motives
    /// differ. Temperament changes voice; it does not eliminate an executable alternative.</summary>
    public static IReadOnlyDictionary<string, string> Lines(ReflectionSourceFacts? source, Temperament character)
    {
        string tone = Tone(source);
        string id = tone == "neutral" ? "neutral" : $"{(source!.OwnDeed ? "own" : "received")}-{tone}";
        Responses r = Content.Value.Responses.Single(r => r.Id == id);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["gift"] = character.Expression < 0.5 ? r.QuietGift : r.Gift,
            ["help"] = character.Chattiness < 0.5 ? r.QuietHelp : r.Help,
            ["confront"] = character.Boldness < 0.5 ? r.QuietConfront : r.Confront,
            ["defer"] = "I need to think about this another time.",
            ["reject"] = "No. I don't want to act on this thought.",
        };
    }

    private static string Tone(ReflectionSourceFacts? source) => source is { Valence: > 0 } ? "kindness"
        : source is { Valence: < 0 } ? "hostility" : "neutral";

    private static string Clip(string text, int limit) => text.Length <= limit ? text : text[..(limit - 1)] + "…";

    private static Pack Read()
    {
        using Stream stream = typeof(ReflectionCatalog).Assembly.GetManifestResourceStream("UnderGlass.Sim.reflection-catalog.json")
            ?? throw new InvalidDataException("Missing embedded reflection catalog.");
        Pack p = JsonSerializer.Deserialize<Pack>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Empty reflection catalog.");
        if (p.Version != 1 || p.Thoughts is null || p.Responses is null || p.Thoughts.Count == 0
            || p.Thoughts.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() != p.Thoughts.Count)
            throw new InvalidDataException("Reflection catalog needs version 1 and unique thought IDs.");
        foreach (ReflectionCatalogEntry e in p.Thoughts)
        {
            string remaining = e.Thought?.Replace("{actor}", "").Replace("{subject}", "").Replace("{memory}", "") ?? "";
            if (string.IsNullOrWhiteSpace(e.Id) || e.Perspective is not ("own" or "received" or "any")
                || e.Tone is not ("kindness" or "hostility" or "neutral") || !double.IsFinite(e.MinRegard)
                || !double.IsFinite(e.MaxRegard) || e.MinRegard < -1 || e.MaxRegard > 1 || e.MinRegard > e.MaxRegard
                || e.Choice is not ("gift" or "help" or "confront" or "*") || string.IsNullOrWhiteSpace(e.Thought)
                || e.Thought.Length > 220 || remaining.Contains('{') || remaining.Contains('}')
                || e.Tags is null || e.Tags.Count == 0 || e.Tags.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException($"Invalid reflection catalog thought {e.Id}.");
        }
        if (!p.Thoughts.Any(e => e.Perspective == "any" && e.Tone == "neutral" && e.Choice == "*"))
            throw new InvalidDataException("Reflection catalog needs a grounded neutral fallback.");
        string[] expected = { "own-kindness", "received-kindness", "own-hostility", "received-hostility", "neutral" };
        if (!p.Responses.Select(r => r.Id).Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal))
            || p.Responses.Any(r => new[] { r.Gift, r.QuietGift, r.Help, r.QuietHelp, r.Confront, r.QuietConfront }
                .Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 120 || s.Contains('{') || s.Contains('}'))))
            throw new InvalidDataException("Reflection catalog needs five complete, compact response profiles.");
        return p with { Thoughts = Array.AsReadOnly(p.Thoughts.Select(e => e with { Tags = Array.AsReadOnly(e.Tags.ToArray()) }).ToArray()),
            Responses = Array.AsReadOnly(p.Responses.ToArray()) };
    }
}
