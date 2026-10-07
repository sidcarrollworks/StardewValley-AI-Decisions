namespace UnderGlass.Sim;

/// <summary>
/// Each person's character (phase 0d; Sid, 2026-10-07; design rule 18): the six temperament weights
/// as they are now. Every rule reads traits from here, never from the cast card, so a later
/// plasticity rule can change them. It starts as the cast's temperament; nothing in the simulator
/// changes it yet.
/// </summary>
public sealed partial class Simulation
{
    private Temperament[] _character = null!;
    private Dictionary<string, Temperament> _charactersAtStart = null!;

    private void StartCharacter() => _character = _cast.Select(v => v.Temperament).ToArray();

    internal Temperament CharacterOf(int i) => _character[i];

    /// <summary>Someone's character now.</summary>
    public Temperament Character(string who) => _character[_index[who]];

    public double TraitOf(string who, Trait t) => Get(_character[_index[who]], t);

    /// <summary>Set one trait, clamped to [0, 1]: for tests, the runner and phase 0e. Call before Run.</summary>
    public void SetTrait(string who, Trait t, double value)
        => _character[_index[who]] = With(_character[_index[who]], t, Math.Clamp(value, 0, 1));

    public static double Get(Temperament c, Trait t) => t switch
    {
        Trait.Chattiness => c.Chattiness,
        Trait.Boldness => c.Boldness,
        Trait.Understanding => c.Understanding,
        Trait.SelfRegard => c.SelfRegard,
        Trait.Sensitivity => c.Sensitivity,
        _ => c.Retention,
    };

    public static Temperament With(Temperament c, Trait t, double v) => t switch
    {
        Trait.Chattiness => c with { Chattiness = v },
        Trait.Boldness => c with { Boldness = v },
        Trait.Understanding => c with { Understanding = v },
        Trait.SelfRegard => c with { SelfRegard = v },
        Trait.Sensitivity => c with { Sensitivity = v },
        _ => c with { Retention = v },
    };

    private Dictionary<string, Temperament> Characters() => _names.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => _character[x.i]);
}
