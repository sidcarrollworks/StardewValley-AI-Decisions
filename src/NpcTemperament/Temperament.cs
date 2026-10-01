namespace NpcTemperament;

/// <summary>
/// The game's own personality fields for one character, from Data/Characters (confirmed in the
/// 1.6.15 decompile, <c>StardewValley.GameData.Characters.CharacterData</c>). Values are the enum
/// names: Manner Neutral/Polite/Rude, SocialAnxiety Outgoing/Shy/Neutral, Optimism
/// Positive/Negative/Neutral, Age Adult/Teen/Child.
/// </summary>
public sealed record GameTraits(string Manner, string SocialAnxiety, string Optimism, string Age);

/// <summary>
/// One character's seed temperament. Every value is 0..1 with 0.5 as the town's typical villager.
/// Two groups (docs/spec/temperament.md): six behaviour traits (what the character does: warmth ..
/// boldness) and six emotion biases after Ekman's basic emotions (how they tend to feel: anger ..
/// surprise). Fields are positional; add new ones only at the end, with defaults.
/// </summary>
public sealed record Temperament(
    double Warmth,
    double Sensitivity,
    double Forgiveness,
    double Chattiness,
    double Curiosity,
    double Boldness,
    double Anger = 0.5,
    double Disgust = 0.5,
    double Fear = 0.5,
    double Happiness = 0.5,
    double Sadness = 0.5,
    double Surprise = 0.5)
{
    /// <summary>The value for a character with no seed (a modded NPC): the town's middle.</summary>
    public static readonly Temperament Neutral = new(0.5, 0.5, 0.5, 0.5, 0.5, 0.5);

    public static readonly IReadOnlyList<string> BehaviourTraits =
        new[] { "warmth", "sensitivity", "forgiveness", "chattiness", "curiosity", "boldness" };

    /// <summary>Ekman's six basic emotions, as tendencies.</summary>
    public static readonly IReadOnlyList<string> EmotionTraits =
        new[] { "anger", "disgust", "fear", "happiness", "sadness", "surprise" };

    public static readonly IReadOnlyList<string> TraitNames = BehaviourTraits.Concat(EmotionTraits).ToArray();

    public double Get(string trait) => trait switch
    {
        "warmth" => Warmth,
        "sensitivity" => Sensitivity,
        "forgiveness" => Forgiveness,
        "chattiness" => Chattiness,
        "curiosity" => Curiosity,
        "boldness" => Boldness,
        "anger" => Anger,
        "disgust" => Disgust,
        "fear" => Fear,
        "happiness" => Happiness,
        "sadness" => Sadness,
        "surprise" => Surprise,
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
        "anger" => this with { Anger = value },
        "disgust" => this with { Disgust = value },
        "fear" => this with { Fear = value },
        "happiness" => this with { Happiness = value },
        "sadness" => this with { Sadness = value },
        "surprise" => this with { Surprise = value },
        _ => throw new ArgumentException($"unknown trait '{trait}'", nameof(trait)),
    };
}
