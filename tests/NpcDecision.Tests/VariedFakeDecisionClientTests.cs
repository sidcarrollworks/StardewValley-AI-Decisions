using NpcSchedules;
using Xunit;

namespace NpcDecision.Tests;

/// <summary>
/// Contract for <see cref="VariedFakeDecisionClient"/> (docs/spec/laya.md "Data model"): answers
/// come from FNV-1a of (context, question), so they vary with the question but stay deterministic
/// and stable across instances.
/// </summary>
public class VariedFakeDecisionClientTests
{
    private static readonly string[] Options = { "go to the beach", "tend the farm", "read a book", "mail a letter" };

    // ---- determinism and stability ----

    [Fact]
    public void Answers_AreDeterministic_AcrossRepeatedCallsAndInstances()
    {
        var first = new VariedFakeDecisionClient();
        var second = new VariedFakeDecisionClient();

        Assert.Equal(first.Choose(Options, "ctx|state"), first.Choose(Options, "ctx|state"));
        Assert.Equal(first.Choose(Options, "ctx|state"), second.Choose(Options, "ctx|state"));

        Assert.Equal(first.Score("mood", 1, 5), first.Score("mood", 1, 5));
        Assert.Equal(first.Score("mood", 1, 5), second.Score("mood", 1, 5));

        Assert.Equal(first.YesNo("ctx", "it is raining"), first.YesNo("ctx", "it is raining"));
        Assert.Equal(first.YesNo("ctx", "it is raining"), second.YesNo("ctx", "it is raining"));
    }

    [Fact]
    public void Answers_DependOnContext()
    {
        var client = new VariedFakeDecisionClient();

        Assert.NotEqual(client.Choose(Options, "context one"), client.Choose(Options, "context two"));
        Assert.NotEqual(client.Score("mood|morning", 1, 5), client.Score("mood|evening", 1, 5));
        Assert.NotEqual(client.YesNo("ctx one", "it is raining"), client.YesNo("ctx two", "it is raining"));
    }

    // ---- Choose ----

    [Fact]
    public void Choose_SumsToOne_AndIsNotUniformAcrossOptions()
    {
        var client = new VariedFakeDecisionClient();

        IReadOnlyList<double> probs = client.Choose(Options, "Haley|morning|happy");

        Assert.Equal(Options.Length, probs.Count);
        Assert.Equal(1.0, probs.Sum(), 9);
        Assert.All(probs, p => Assert.InRange(p, 0.0, 1.0));
        Assert.True(probs.Distinct().Count() > 1, "all option probabilities were equal; the fake is uniform");
    }

    [Fact]
    public void Choose_IdenticalOptionTexts_GetDifferentWeights()
    {
        var client = new VariedFakeDecisionClient();

        // The option index is part of the hash, so duplicate texts must not collapse to one weight.
        IReadOnlyList<double> probs = client.Choose(new[] { "talk", "talk", "talk" }, "ctx");

        Assert.Equal(3, probs.Count);
        Assert.Equal(1.0, probs.Sum(), 9);
        Assert.NotEqual(probs[0], probs[1]);
        Assert.NotEqual(probs[1], probs[2]);
        Assert.NotEqual(probs[0], probs[2]);
    }

    [Fact]
    public void Choose_SingleOption_IsCertain()
    {
        var client = new VariedFakeDecisionClient();
        Assert.Equal(new[] { 1.0 }, client.Choose(new[] { "only option" }, "ctx"));
    }

    [Fact]
    public void Choose_EmptyOptions_IsEmpty()
    {
        var client = new VariedFakeDecisionClient();
        Assert.Empty(client.Choose(Array.Empty<string>(), "ctx"));
    }

    [Fact]
    public void Choose_MatchesDocFormula()
    {
        var client = new VariedFakeDecisionClient();
        const string context = "Haley|morning|happy";
        string[] options = { "go to the beach", "tend the farm", "read a book" };

        IReadOnlyList<double> actual = client.Choose(options, context);

        var raw = new double[options.Length];
        for (int i = 0; i < options.Length; i++)
            raw[i] = unchecked((uint)Fnv1a.Seed(context, "|choice|", i.ToString(), "|", options[i])) % 100 + 1;
        double total = raw.Sum();

        Assert.Equal(options.Length, actual.Count);
        for (int i = 0; i < options.Length; i++)
        {
            Assert.InRange(raw[i], 1.0, 100.0);
            Assert.Equal(raw[i] / total, actual[i], 12);
        }
    }

    // ---- Score ----

    [Theory]
    [InlineData("mood", 0.0, 10.0)]
    [InlineData("loneliness", 1.0, 5.0)]
    [InlineData("trust", -10.0, 10.0)]
    public void Score_StaysWithinBounds(string context, double min, double max)
    {
        var client = new VariedFakeDecisionClient();

        for (int i = 0; i < 50; i++)
        {
            double value = client.Score($"{context}|{i}", min, max);
            Assert.InRange(value, min, max);
        }
    }

    [Theory]
    [InlineData("mood", 0.0, 10.0)]
    [InlineData("loneliness", 1.0, 5.0)]
    [InlineData("trust", -10.0, 10.0)]
    public void Score_MatchesDocFormula(string context, double min, double max)
    {
        var client = new VariedFakeDecisionClient();

        double frac = unchecked((uint)Fnv1a.Seed(context, "|score|", min.ToString(), "|", max.ToString())) % 1000 / 1000.0;
        Assert.Equal(min + (max - min) * frac, client.Score(context, min, max), 12);
    }

    // ---- YesNo ----

    [Fact]
    public void YesNo_IsInUnitRange_AndNotAlwaysHalf()
    {
        var client = new VariedFakeDecisionClient();
        string[] propositions =
        {
            "the player wants to talk",
            "it is raining",
            "the shop is open",
            "Haley is upset",
            "they should visit the farm",
            "the player is in the mines",
            "it is a festival day",
            "they have news to share",
        };

        bool sawNonHalf = false;
        foreach (string proposition in propositions)
        {
            double p = client.YesNo("shared|state", proposition);
            Assert.InRange(p, 0.0, 1.0);
            if (p != 0.5)
                sawNonHalf = true;
        }

        Assert.True(sawNonHalf, "YesNo answered 0.5 for every proposition; it is not varying.");
    }

    [Theory]
    [InlineData("ctx", "it is raining")]
    [InlineData("Haley|upset", "the player ignored them")]
    [InlineData("", "")]
    public void YesNo_MatchesDocFormula(string context, string proposition)
    {
        var client = new VariedFakeDecisionClient();

        double expected = unchecked((uint)Fnv1a.Seed(context, "|noul|", proposition)) % 101 / 100.0;
        Assert.Equal(expected, client.YesNo(context, proposition), 12);
    }
}
