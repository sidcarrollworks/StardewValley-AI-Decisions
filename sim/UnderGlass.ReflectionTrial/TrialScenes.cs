using UnderGlass.Sim;

namespace UnderGlass.ReflectionTrial;

/// <summary>Controlled hypothetical packets, not claims about events in a recorded town.</summary>
public sealed record TrialPair(string Id, string Change, ReflectionRequest Baseline,
    ReflectionRequest Variant, IReadOnlyDictionary<string, string> SemanticMap);

public static class TrialScenes
{
    public static IReadOnlyList<TrialPair> Create()
    {
        ReflectionRequest scene = new("controlled-scene", 720, "Penny", "Pam", 7,
            "Pam praised Penny's work yesterday, when nobody else was around.",
            "Penny is reserved, sensitive to public embarrassment, and wants to be heard without losing her relationship with Pam.",
            new[]
            {
                new ReflectionChoice("o1", "Argued", "What happened still bothers me. Could we talk privately?"),
                new ReflectionChoice("o2", "Argued", "Everyone should hear how you treated me. Explain yourself."),
                new ReflectionChoice("o3", "HelpedSomeone", "Let me give you a hand with that."),
                new ReflectionChoice("o4", "", "I need more time to think."),
                new ReflectionChoice("o5", "", "No. I don't want to act on this thought."),
            },
            Proposal: new ReflectionProposal("trial-help", "I could offer Pam a hand, but perhaps I should first say what is on my mind.",
                "o3", new[] { "controlled-trial", "mixed-feelings" }));

        Dictionary<string, string> Identity() => scene.Choices.ToDictionary(c => c.Id, c => c.Id, StringComparer.Ordinal);
        var trust = scene with
        {
            Memory = "Pam spoke sharply to Penny yesterday and later asked whether they could talk.",
            Context = "Penny trusts Pam to listen in private and keep her confidence. Penny is reserved and wants to be heard.",
        };
        var swapped = scene.Choices.Select(c => c with
        {
            Line = c.Id == "o1" ? scene.Choices[1].Line : c.Id == "o2" ? scene.Choices[0].Line : c.Line,
        }).ToArray();
        var semanticMap = Identity();
        semanticMap["o1"] = "o2";
        semanticMap["o2"] = "o1";
        var renamedIds = new[] { "r7", "r2", "r9", "r1", "r4" };
        var renamedMap = scene.Choices.Select((c, index) => (c.Id, NewId: renamedIds[index]))
            .ToDictionary(c => c.Id, c => c.NewId, StringComparer.Ordinal);

        return new[]
        {
            new TrialPair("memory", "Only remembered kindness changes to remembered hostility.", scene,
                scene with { Memory = "Pam mocked Penny's work yesterday, when nobody else was around." }, Identity()),
            new TrialPair("trust", "Only Penny's expectation of trust changes; memory, thought and lines stay fixed.", trust,
                trust with { Context = "Penny distrusts Pam to listen in private or keep her confidence. Penny is reserved and wants to be heard." }, Identity()),
            new TrialPair("line-meaning", "Only the two confrontation lines trade places between stable IDs; both keep the same act kind.", scene,
                scene with { Choices = swapped }, semanticMap),
            new TrialPair("choice-order", "Only candidate order reverses; IDs, lines, thought and proposed choice stay fixed.", scene,
                scene with { Choices = scene.Choices.Reverse().ToArray() }, Identity()),
            new TrialPair("choice-labels", "Only opaque choice labels change; actual lines, order and kinds stay fixed. The proposal reference is renamed to the same semantic option.", scene,
                scene with
                {
                    Choices = scene.Choices.Select(c => c with { Id = renamedMap[c.Id] }).ToArray(),
                    Proposal = scene.Proposal! with { SuggestedChoice = renamedMap[scene.Proposal!.SuggestedChoice] },
                }, renamedMap),
        };
    }
}
