namespace NpcTemperament;

/// <summary>One character's inputs to the scorer.</summary>
public sealed record CharacterInput(string Name, GameTraits Traits, DialogueFeatures Features);

/// <summary>A scored character: the final seed plus the game-trait part on its own, for review.</summary>
public sealed record ScoredCharacter(string Name, Temperament Seed, Temperament FromTraits, DialogueFeatures Features);

/// <summary>
/// Turns game traits plus dialogue features into seed temperaments. Pure and deterministic:
/// the same inputs always give the same numbers (characters are processed in name order).
///
/// Each trait = 0.5 + game-trait offset + Spread x mean(signed z-scores of its dialogue features),
/// clamped to 0..1 and rounded to 2 places. z-scores are against the whole input set, so 0.5
/// means "typical for this town". The recipe is <see cref="Recipes"/>; the reasoning is in
/// docs/spec/temperament.md.
/// </summary>
public static class TemperamentScorer
{
    /// <summary>How far dialogue can move a trait: at most 2 x Spread either way.</summary>
    public const double Spread = 0.15;

    /// <summary>Characters with fewer pages than this get the game-trait part only.</summary>
    public const int MinPages = 20;

    private const double ZClamp = 2.0;

    /// <summary>Per trait: the dialogue features (+1 raises the trait, -1 lowers it).</summary>
    public static readonly IReadOnlyDictionary<string, (Func<DialogueFeatures, double> Feature, int Sign)[]> Recipes =
        new Dictionary<string, (Func<DialogueFeatures, double>, int)[]>
        {
            ["warmth"] = new (Func<DialogueFeatures, double>, int)[]
                { (f => f.Happy, +1), (f => f.Love, +1), (f => f.Thanks, +1), (f => f.Welcome, +1), (f => f.Dismiss, -1), (f => f.Angry, -1) },
            ["sensitivity"] = new (Func<DialogueFeatures, double>, int)[]
                { (f => f.Sad, +1), (f => f.Sorry, +1), (f => f.Trailing, +1), (f => f.Happy, -1) },
            ["forgiveness"] = new (Func<DialogueFeatures, double>, int)[]
                { (f => f.Angry, -1), (f => f.Dismiss, -1), (f => f.Thanks, +1), (f => f.Happy, +1) },
            ["chattiness"] = new (Func<DialogueFeatures, double>, int)[]
                { (f => f.WordsPerPage, +1), (f => f.Gossip, +1), (f => f.Exclaim, +1), (f => f.Trailing, -1) },
            ["curiosity"] = new (Func<DialogueFeatures, double>, int)[]
                { (f => f.Question, +1), (f => f.Gossip, +1) },
            ["boldness"] = new (Func<DialogueFeatures, double>, int)[]
                { (f => f.Exclaim, +1), (f => f.Trailing, -1), (f => f.Sorry, -1) },
        };

    /// <summary>The game-trait offsets (added to 0.5 before dialogue).</summary>
    public static Temperament TraitOffsets(GameTraits t)
    {
        double polite = t.Manner == "Polite" ? 1 : t.Manner == "Rude" ? -1 : 0;
        double outgoing = t.SocialAnxiety == "Outgoing" ? 1 : t.SocialAnxiety == "Shy" ? -1 : 0;
        double positive = t.Optimism == "Positive" ? 1 : t.Optimism == "Negative" ? -1 : 0;
        double child = t.Age == "Child" ? 1 : 0;
        return new Temperament(
            Warmth: 0.05 * polite + 0.05 * positive,
            Sensitivity: -0.05 * outgoing - 0.05 * positive,
            Forgiveness: 0.08 * polite + 0.04 * positive,
            Chattiness: 0.10 * outgoing,
            Curiosity: 0.05 * outgoing + 0.05 * child,
            Boldness: 0.15 * outgoing);
    }

    public static IReadOnlyList<ScoredCharacter> Score(IEnumerable<CharacterInput> inputs)
    {
        var list = inputs.OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
        var scored = list.Where(c => c.Features.Pages >= MinPages).Select(c => c.Features).ToList();
        var result = new List<ScoredCharacter>();

        foreach (var c in list)
        {
            Temperament offsets = TraitOffsets(c.Traits);
            Temperament fromTraits = Temperament.Neutral;
            Temperament seed = Temperament.Neutral;
            foreach (string trait in Temperament.TraitNames)
            {
                double baseValue = 0.5 + offsets.Get(trait);
                double dialogue = c.Features.Pages >= MinPages ? DialoguePart(trait, c.Features, scored) : 0;
                fromTraits = fromTraits.With(trait, Round(baseValue));
                seed = seed.With(trait, Round(baseValue + dialogue));
            }
            result.Add(new ScoredCharacter(c.Name, seed, fromTraits, c.Features));
        }
        return result;
    }

    private static double DialoguePart(string trait, DialogueFeatures f, List<DialogueFeatures> population)
    {
        var recipe = Recipes[trait];
        double sum = 0;
        foreach (var (feature, sign) in recipe)
            sum += sign * Z(feature(f), population.Select(feature).ToList());
        return Spread * sum / recipe.Length;
    }

    private static double Z(double value, List<double> population)
    {
        if (population.Count < 2)
            return 0;
        double mean = population.Average();
        double sd = Math.Sqrt(population.Sum(v => (v - mean) * (v - mean)) / population.Count);
        if (sd < 1e-9)
            return 0;
        return Math.Clamp((value - mean) / sd, -ZClamp, ZClamp);
    }

    private static double Round(double v) => Math.Round(Math.Clamp(v, 0, 1), 2, MidpointRounding.AwayFromZero);
}
