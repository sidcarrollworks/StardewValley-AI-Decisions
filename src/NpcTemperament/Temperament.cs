namespace NpcTemperament;

/// <summary>
/// The game's own personality fields for one character, from Data/Characters (confirmed in the
/// 1.6.15 decompile, <c>StardewValley.GameData.Characters.CharacterData</c>). Values are the enum
/// names: Manner Neutral/Polite/Rude, SocialAnxiety Outgoing/Shy/Neutral, Optimism
/// Positive/Negative/Neutral, Age Adult/Teen/Child.
/// </summary>
public sealed record GameTraits(string Manner, string SocialAnxiety, string Optimism, string Age);

/// <summary>
/// One character's seed temperament. Every trait is 0..1 with 0.5 as the town's typical villager.
/// What each one drives is in docs/spec/temperament.md.
/// </summary>
public sealed record Temperament(
    double Warmth,
    double Sensitivity,
    double Forgiveness,
    double Chattiness,
    double Curiosity,
    double Boldness)
{
    /// <summary>The value for a character with no seed (a modded NPC): the town's middle.</summary>
    public static readonly Temperament Neutral = new(0.5, 0.5, 0.5, 0.5, 0.5, 0.5);

    public static readonly IReadOnlyList<string> TraitNames =
        new[] { "warmth", "sensitivity", "forgiveness", "chattiness", "curiosity", "boldness" };

    public double Get(string trait) => trait switch
    {
        "warmth" => Warmth,
        "sensitivity" => Sensitivity,
        "forgiveness" => Forgiveness,
        "chattiness" => Chattiness,
        "curiosity" => Curiosity,
        "boldness" => Boldness,
        _ => throw new ArgumentException($"unknown trait '{trait}'", nameof(trait)),
    };

    public Temperament With(string trait, double value) => trait switch
    {
        "warmth" => this with { Warmth = value },
        "sensitivity" => this with { Sensitivity = value },
        "forgiveness" => this with { Forgiveness = value },
        "chattiness" => this with { Chattiness = value },
        "curiosity" => this with { Curiosity = value },
        "boldness" => this with { Boldness = value },
        _ => throw new ArgumentException($"unknown trait '{trait}'", nameof(trait)),
    };
}
