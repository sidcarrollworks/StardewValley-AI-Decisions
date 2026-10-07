using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>The desire gate's pure arithmetic (phase 0d, rule 10; spec section 2.2, tests D2-D12):
/// effective boldness, cost, the close-call chance and its mood tilt, the call bands, a motive's
/// fading event part, Fond, Pity, stance and the tone of a first greeting. Pure, like
/// FeelingMathTests.</summary>
public class DesireMathTests
{
    private const int D = Clock.MinutesPerDay;

    /// <summary>What the tests weigh with: steering with the gate, PowerWeight 0, the added rules off.</summary>
    private static readonly FeelingOptions O = FeelingOptions.WithDesire();

    private static double ArgueCost(double fear = 0) => DesireMath.Cost(O.ArgueForm, true, fear, O);

    private static IEnumerable<double> Grid(double from, double to, int steps)
        => Enumerable.Range(0, steps + 1).Select(i => from + (to - from) * i / steps);

    private static int Rank(string call) => call switch { "no" => 0, "close" => 1, "clear" => 2, _ => throw new ArgumentException(call) };

    /// <summary>The logistic the Pade form stands in for; the test may use Exp, the library may not.</summary>
    private static double Logistic(double margin) => 1 / (1 + Math.Exp(-8 * margin));

    /// <summary>The defaults the rest of the class relies on.</summary>
    [Fact]
    public void TheGateConstants()
    {
        Assert.Equal(0.0, O.PowerWeight);           // the spec's 1 is to be measured at its step 5
        Assert.Equal(0.0, new FeelingOptions().PowerWeight);
        Assert.Equal(0.5, O.FamiliarityWeight);
        Assert.Equal(0.5, O.IntensityWeight);
        Assert.Equal(0.5, O.StanceWeight);
        Assert.Equal(0.15, O.ClearBand);
        Assert.Equal(0.1, O.MoodTilt);
        Assert.Equal(0.3, O.HostileSurcharge);
        Assert.Equal(0.4, DesireMath.Cost(O.GiftCost, false, 0, O), 12);
        Assert.Equal(0.5, DesireMath.Cost(O.HelpCost, false, 0, O), 12);
        Assert.Equal(0.8, ArgueCost(), 12);
        Assert.Equal(0.5, DesireMath.Cost(O.SnubForm, true, 0, O), 12);
    }

    /// <summary>D2. The extremes of boldness give finite, ordered answers, and nothing throws.</summary>
    [Fact]
    public void D2_TheExtremesOfBoldness()
    {
        Assert.Equal(-0.78, DesireMath.Effective(0.02, 0, true, 0, 0, 0.5, O) - ArgueCost(), 12);
        Assert.Equal(0.18, DesireMath.Effective(0.98, 0, true, 0, 0, 0.5, O) - ArgueCost(), 12);
        Assert.Equal("no", DesireMath.Call(DesireMath.Effective(0.02, 0, true, 0, 0, 0.5, O) - ArgueCost(), O));
        Assert.Equal("clear", DesireMath.Call(DesireMath.Effective(0.98, 0, true, 0, 0, 0.5, O) - ArgueCost(), O));

        foreach (double stance in new[] { -1.0, -0.5, 0, 0.5, 1.0 })
            foreach (bool hostile in new[] { false, true })
            {
                double shy = DesireMath.Effective(0.02, stance, hostile, 0.25, 0.45, 0.5, O);
                double bold = DesireMath.Effective(0.98, stance, hostile, 0.25, 0.45, 0.5, O);
                Assert.True(double.IsFinite(shy) && double.IsFinite(bold), $"stance {stance} hostile {hostile}");
                Assert.True(shy < bold, $"stance {stance} hostile {hostile}: {shy} >= {bold}");
                Assert.Equal(0.96, bold - shy, 12);    // boldness enters linearly, with weight 1
            }

        // The same through a character set on a simulation, clamped there and read back.
        var sim = new Simulation(1);
        sim.SetTrait("Penny", Trait.Boldness, 0.02);
        sim.SetTrait("Alex", Trait.Boldness, 0.98);
        double pennyEff = DesireMath.Effective(sim.TraitOf("Penny", Trait.Boldness), 0, true, 0, 0, 0.5, O);
        double alexEff = DesireMath.Effective(sim.TraitOf("Alex", Trait.Boldness), 0, true, 0, 0, 0.5, O);
        Assert.Equal(-0.78, pennyEff - ArgueCost(), 12);
        Assert.Equal(0.18, alexEff - ArgueCost(), 12);

        sim.SetTrait("Penny", Trait.Boldness, -5);
        sim.SetTrait("Alex", Trait.Boldness, 5);
        foreach (double stance in new[] { -1.0, 1.0 })
        {
            double low = DesireMath.Effective(sim.TraitOf("Penny", Trait.Boldness), stance, true, 0, 0, 0.5, O);
            double high = DesireMath.Effective(sim.TraitOf("Alex", Trait.Boldness), stance, true, 0, 0, 0.5, O);
            Assert.Equal(0.5 * stance, low, 12);       // boldness clamped to 0
            Assert.Equal(1 + 0.5 * stance, high, 12);  // boldness clamped to 1
        }
    }

    /// <summary>D3. The close-call chance is logistic(8m) to within 1e-5 on the band, with no Exp.</summary>
    [Fact]
    public void D3_TheCloseCallChance()
    {
        for (int i = -15; i <= 15; i++)
        {
            double m = i / 100.0;
            Assert.True(Math.Abs(DesireMath.CloseCallChance(m) - Logistic(m)) <= 1e-5, $"m {m}");
            Assert.Equal(1.0, DesireMath.CloseCallChance(m) + DesireMath.CloseCallChance(-m), 12);   // odd about 0.5
        }
        Assert.Equal(0.5, DesireMath.CloseCallChance(0), 12);
        Assert.Equal(0.598688, DesireMath.CloseCallChance(0.05), 6);
        Assert.Equal(0.689975, DesireMath.CloseCallChance(0.1), 6);
        Assert.Equal(0.768531, DesireMath.CloseCallChance(0.15), 6);
        Assert.Equal(0.310025, DesireMath.CloseCallChance(-0.1), 6);
    }

    /// <summary>D4. Mood tilts a close call toward kindness when glad, toward hostility when sad,
    /// and the result stays a chance.</summary>
    [Fact]
    public void D4_MoodTiltsTheCall()
    {
        Assert.Equal(0.6, DesireMath.Tilted(0.5, 1, false, O), 12);
        Assert.Equal(0.4, DesireMath.Tilted(0.5, 1, true, O), 12);
        Assert.Equal(0.4, DesireMath.Tilted(0.5, -1, false, O), 12);
        Assert.Equal(0.6, DesireMath.Tilted(0.5, -1, true, O), 12);
        Assert.Equal(0.5, DesireMath.Tilted(0.5, 0, true, O), 12);
        Assert.Equal(1.0, DesireMath.Tilted(0.95, 1, false, O));
        Assert.Equal(0.0, DesireMath.Tilted(0.05, 1, true, O));
        Assert.Equal(1.0, DesireMath.Tilted(0.98, -1, true, O));
        Assert.Equal(0.0, DesireMath.Tilted(0.02, -1, false, O));
        foreach (double p in Grid(0, 1, 20))
            foreach (double mood in Grid(-1, 1, 20))
                foreach (bool hostile in new[] { false, true })
                    Assert.InRange(DesireMath.Tilted(p, mood, hostile, O), 0, 1);
    }

    /// <summary>D5. Clear means more than 0.15 either way; 0.15 itself is a close call.</summary>
    [Fact]
    public void D5_TheCallBands()
    {
        Assert.Equal("clear", DesireMath.Call(0.151, O));
        Assert.Equal("close", DesireMath.Call(0.15, O));
        Assert.Equal("close", DesireMath.Call(0, O));
        Assert.Equal("close", DesireMath.Call(-0.15, O));
        Assert.Equal("no", DesireMath.Call(-0.151, O));
    }

    /// <summary>D6. Effective boldness: familiarity and intensity at half weight, power by
    /// PowerWeight, a combative stance raising only hostile daring, a withdrawn one lowering all.</summary>
    [Fact]
    public void D6_EffectiveBoldness()
    {
        Assert.Equal(0.85, DesireMath.Effective(0.5, 0, true, 0.25, 0.45, 0.5, O), 12);
        Assert.Equal(0.85, DesireMath.Effective(0.5, 0, true, 0.25, 0.45, 0.6, O), 12);  // PowerWeight 0: power ignored
        var powered = new FeelingOptions { PowerWeight = 1 };
        Assert.Equal(0.95, DesireMath.Effective(0.5, 0, true, 0.25, 0.45, 0.6, powered), 12);
        Assert.Equal(0.75, DesireMath.Effective(0.5, 0, true, 0.25, 0.45, 0.4, powered), 12);

        Assert.Equal(0.6, DesireMath.Effective(0.5, -0.4, false, 0.4, 0.2, 0.5, O), 12);  // withdrawn: less kind daring
        Assert.Equal(0.6, DesireMath.Effective(0.5, -0.4, true, 0.4, 0.2, 0.5, O), 12);   // and less hostile daring
        Assert.Equal(1.0, DesireMath.Effective(0.5, 0.4, true, 0.4, 0.2, 0.5, O), 12);    // combative: more hostile daring
        Assert.Equal(0.8, DesireMath.Effective(0.5, 0.4, false, 0.4, 0.2, 0.5, O), 12);   // but no more kind daring
        Assert.Equal(0.8, DesireMath.Effective(0.5, 0, false, 0.4, 0.2, 0.5, O), 12);
    }

    /// <summary>D7. Cost: the form, plus the surcharge and fear for hostile acts only.</summary>
    [Fact]
    public void D7_Cost()
    {
        Assert.Equal(0.8, DesireMath.Cost(0.5, true, 0, O), 12);
        Assert.Equal(0.7, DesireMath.Cost(0.2, true, 0.2, O), 12);
        Assert.Equal(0.4, DesireMath.Cost(0.4, false, 0.2, O), 12);
        Assert.Equal(0.5, DesireMath.Cost(0.5, false, 0, O), 12);
    }

    /// <summary>D8. A motive's event part fades linearly over MotiveDays. Intensity adds the grudge
    /// to hostile motives only; a friendly motive at mild dislike is the event alone. The re-stir
    /// is private to the simulation and rebuilt here: it adds to what is left.</summary>
    [Fact]
    public void D8_TheMotiveFades()
    {
        const int m0 = 10 * 60;
        int window = O.MotiveDays * D;
        Assert.Equal(7 * D, window);
        Assert.Equal(0.3, DesireMath.EventPart(0.3, m0, m0, window), 12);
        Assert.Equal(0.15, DesireMath.EventPart(0.3, m0, m0 + 7 * D / 2, window), 12);
        Assert.Equal(0.0, DesireMath.EventPart(0.3, m0, m0 + 7 * D, window), 12);
        Assert.Equal(0.0, DesireMath.EventPart(0.3, m0, m0 + 10 * D, window), 12);   // never negative
        Assert.Equal(0.15, DesireMath.EventPart(0.3, m0, m0 + 30, O.PityMinutes), 12); // pity fades within the hour

        double regard = -0.2;
        double Hostile(int m) => DesireMath.Intensity(DesireMath.EventPart(0.3, m0, m, window), true, regard);
        double Friendly(int m) => DesireMath.Intensity(DesireMath.EventPart(0.3, m0, m, window), false, regard);
        Assert.Equal(0.5, Hostile(m0), 12);
        Assert.Equal(0.35, Hostile(m0 + 7 * D / 2), 12);
        Assert.Equal(0.0, Hostile(m0 + 7 * D), 12);                       // the grudge alone moves nobody
        Assert.Equal(0.3, Friendly(m0), 12);                              // no grudge in a kindness
        Assert.Equal(0.15, Friendly(m0 + 7 * D / 2), 12);
        Assert.Equal(0.3, DesireMath.Intensity(0.3, true, 0.4), 12);      // liking adds nothing
        Assert.Equal(1.0, DesireMath.Intensity(0.9, true, -0.5), 12);     // capped at 1

        double restirred = Math.Min(1, DesireMath.EventPart(0.3, m0, m0 + 7 * D / 2, window) + 0.3);
        Assert.Equal(0.45, restirred, 12);
    }

    /// <summary>D9. Fond: love above LoveAt, growing with the days apart up to FondDays.</summary>
    [Fact]
    public void D9_Fond()
    {
        Assert.Equal(0.3 * 3 / 7, DesireMath.Fond(0.5, 3, O), 12);
        Assert.Equal(0.128571, DesireMath.Fond(0.5, 3, O), 6);
        Assert.Equal(0.0857, DesireMath.Fond(0.5, 2, O), 4);
        Assert.Equal(0.3, DesireMath.Fond(0.5, 7, O), 12);
        Assert.Equal(0.3, DesireMath.Fond(0.5, 14, O), 12);
        Assert.Equal(0.0, DesireMath.Fond(0.15, 30, O), 12);
        Assert.Equal(0.0, DesireMath.Fond(0.2, 30, O), 12);
        Assert.Equal(0.0, DesireMath.Fond(0.5, 0, O), 12);
        Assert.True(DesireMath.Fond(0.5, 3, O) >= O.GiftMin);
        Assert.True(DesireMath.Fond(0.5, 2, O) < O.GiftMin);
    }

    /// <summary>D10. Pity: the size of the mishap, sensitivity, love (hate does not lessen it), clarity.</summary>
    [Fact]
    public void D10_Pity()
    {
        Assert.Equal(0.2, DesireMath.Pity(-0.1, 1.0, 0, 1, O), 12);
        Assert.Equal(0.32, DesireMath.Pity(-0.1, 1.0, 0.6, 1, O), 12);
        Assert.Equal(0.1, DesireMath.Pity(-0.1, 1.0, 0, 0.5, O), 12);
        Assert.Equal(0.2, DesireMath.Pity(-0.1, 1.0, -0.6, 1, O), 12);
        Assert.Equal(0.3, DesireMath.Pity(-0.1, 1.5, 0, 1, O), 12);
    }

    /// <summary>D11. Hurt moves the bold toward combat and the shy toward withdrawal; kindness pulls
    /// a stance toward 0 and never across it.</summary>
    [Fact]
    public void D11_Stance()
    {
        Assert.Equal(-0.18, DesireMath.StanceAfterHurt(0, 0.3, 0.2), 12);
        Assert.Equal(0.0, DesireMath.StanceAfterHurt(0, 0.3, 0.5), 12);
        Assert.Equal(0.288, DesireMath.StanceAfterHurt(0, 0.3, 0.98), 12);
        Assert.Equal(-1.0, DesireMath.StanceAfterHurt(-0.9, 1, 0.02), 12);
        Assert.Equal(1.0, DesireMath.StanceAfterHurt(0.9, 1, 0.98), 12);

        Assert.Equal(-0.144, DesireMath.StanceAfterKindness(-0.18, 0.2), 12);
        Assert.Equal(0.0, DesireMath.StanceAfterKindness(0.5, 2), 12);

        foreach (double s in Grid(-1, 1, 40))
            foreach (double f in Grid(0, 2, 20))
            {
                double after = DesireMath.StanceAfterKindness(s, f);
                Assert.True(s * after >= 0, $"stance {s} felt {f}: crossed to {after}");
                Assert.True(Math.Abs(after) <= Math.Abs(s) + 1e-12, $"stance {s} felt {f}: grew to {after}");
                foreach (double b in Grid(0.02, 0.98, 12))
                    Assert.InRange(DesireMath.StanceAfterHurt(s, f, b), -1, 1);
            }
    }

    /// <summary>D12. The tone of a day's first meeting: warm from power and love, curt from
    /// weakness, poor understanding and dislike; always a chance.</summary>
    [Fact]
    public void D12_Tone()
    {
        var (warm, curt) = DesireMath.Tone(0.5, 0.5, 0, O);
        Assert.Equal(0.03, warm, 12);
        Assert.Equal(0.03, curt, 12);
        (warm, curt) = DesireMath.Tone(0.2, 0.2, -0.5, O);
        Assert.Equal(0.006, warm, 12);
        Assert.Equal(0.1152, curt, 12);

        foreach (double p in Grid(0, 1, 10))
            foreach (double u in Grid(0, 1, 10))
                foreach (double r in Grid(-1, 1, 10))
                {
                    var (w, c) = DesireMath.Tone(p, u, r, O);
                    Assert.InRange(w, 0, 1);
                    Assert.InRange(c, 0, 1);
                    Assert.True(w + c <= 1, $"p {p} u {u} r {r}");
                }
    }

    /// <summary>R14: every outcome changes monotonically with boldness. For every act's cost (with
    /// and without fear), every stance, familiarity, intensity, power and mood, effective boldness
    /// rises with boldness from 0.02 to 0.98, the call never steps back (no, close, clear), and the
    /// tilted close-call chance never falls.</summary>
    [Fact]
    public void EffectiveAndTheGateAreMonotonicInBoldness()
    {
        var acts = new (string Name, double Form, bool Hostile)[]
        {
            ("GaveGift", O.GiftCost, false), ("HelpedSomeone", O.HelpCost, false),
            ("Argued", O.ArgueForm, true), ("Snubbed", O.SnubForm, true),
        };
        var options = new[] { O, new FeelingOptions { PowerWeight = 1 } };
        var boldness = Enumerable.Range(2, 97).Select(i => i / 100.0).ToArray();   // 0.02 .. 0.98
        var seen = new Dictionary<string, HashSet<string>>();

        foreach (var o in options)
            foreach (var (name, form, hostile) in acts)
                foreach (double fear in hostile ? new[] { 0, 0.2, 0.5 } : new[] { 0.0 })
                    foreach (double stance in new[] { -1, -0.4, 0, 0.4, 1 })
                        foreach (double fam in new[] { 0, 0.25, 0.6, 1 })
                            foreach (double intensity in new[] { 0, 0.2, 0.45, 1 })
                                foreach (double power in new[] { 0, 0.5, 1 })
                                    foreach (double mood in new[] { -1, 0, 1 })
                                    {
                                        double cost = DesireMath.Cost(form, hostile, fear, o);
                                        double lastEff = double.NegativeInfinity, lastP = -1;
                                        int lastRank = -1;
                                        foreach (double b in boldness)
                                        {
                                            double eff = DesireMath.Effective(b, stance, hostile, fam, intensity, power, o);
                                            double margin = eff - cost;
                                            string call = DesireMath.Call(margin, o);
                                            double p = DesireMath.Tilted(DesireMath.CloseCallChance(margin), mood, hostile, o);
                                            string where = $"{name} fear {fear} stance {stance} fam {fam} I {intensity} power {power} mood {mood} b {b}";
                                            Assert.True(double.IsFinite(eff), where);
                                            Assert.True(eff > lastEff, where);
                                            Assert.True(Rank(call) >= lastRank, where);
                                            if (call == "close")
                                                Assert.True(p >= lastP, where);
                                            lastEff = eff;
                                            lastRank = Rank(call);
                                            lastP = call == "close" ? p : lastP;
                                            (seen.TryGetValue(name, out var calls) ? calls : seen[name] = new HashSet<string>()).Add(call);
                                        }
                                    }

        // The sweep is not vacuous: each act's cost meets every call somewhere in it.
        foreach (var (name, _, _) in acts)
            Assert.Equal(new[] { "clear", "close", "no" }, seen[name].OrderBy(c => c));
    }
}
