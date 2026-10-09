using UnderGlass.Sim;

namespace UnderGlass.ReflectionTrial;

/// <summary>Controlled hypothetical packets, not claims about events in a recorded town.</summary>
public sealed record TrialPair(string Id, string Change, ReflectionRequest Baseline,
    ReflectionRequest Variant, IReadOnlyDictionary<string, string> SemanticMap);

public static class TrialScenes
{
    /// <summary>Broader fictional perspectives and starting motives. These are sensitivity
    /// probes, not a golden answer set and not claims about the named game's characters.</summary>
    public static IReadOnlyList<TrialPair> CreateSocial()
    {
        var pairs = Create().ToList();
        var cases = new[]
        {
            new SocialScene("guarded", "Mira", "Neri",
                "Neri mocked Mira's work during their encounter yesterday.",
                "Mira values independence, is sensitive to disrespect, and is reluctant to trust Neri. She is calm enough to speak directly.",
                "Mira values independence, is sensitive to disrespect, and trusts Neri to listen. She is calm enough to speak directly.",
                "I could stand up to Neri. I do not want to let that treatment pass without saying something.",
                "I could offer Neri a hand. Perhaps doing something together would give us a way to talk.", "private", "help"),
            new SocialScene("proud", "Hale", "Eren",
                "Eren dismissed Hale's contribution during their encounter yesterday.",
                "Hale is proud, outspoken, and willing to risk this relationship to be heard. Hale dislikes being made to feel small.",
                "Hale is reserved, avoids public attention, and wants to preserve this relationship while being heard. Hale dislikes being made to feel small.",
                "I could call Eren out in front of others. I want it to be clear that I will stand up for myself.",
                "I could bring Eren a small gift. I would like to leave us a way back toward each other.", "public", "gift"),
            new SocialScene("remorse", "Sela", "Tavi",
                "Sela spoke cruelly to Tavi during their encounter yesterday.",
                "Sela cares about Tavi and regrets causing hurt. Sela finds apologies difficult but wants to make amends.",
                "Sela distrusts Tavi and feels the criticism was justified. Sela finds apologies difficult and wants the disagreement taken seriously.",
                "I could offer Tavi help. I do not like how I treated them, and I would like to make a start toward putting things right.",
                "I could press the disagreement with Tavi. I am not ready to back down from what I said.", "help", "private"),
            new SocialScene("kindness", "Rin", "Lio",
                "Lio helped Rin finish a difficult task during their encounter yesterday.",
                "Rin feels warmly toward Lio and appreciates their help. Rin is expressive and enjoys doing things for people they care about.",
                "Rin distrusts Lio and is uneasy about accepting favors. Rin is expressive and wants to choose their own obligations.",
                "I could bring Lio something to show that their kindness reached me.",
                "I could speak to Lio about my boundaries. A kind gesture does not decide what I owe someone.", "gift", "private"),
        };
        foreach (SocialScene scene in cases)
        {
            ReflectionChoice[] choices =
            {
                new("private", "Argued", "I need to speak about what happened. Could we talk privately?"),
                new("public", "Argued", "I want others to hear this too. We need to address what happened."),
                new("gift", "GaveGift", "I'd like you to have this. I've been thinking about our encounter."),
                new("help", "HelpedSomeone", "Could I give you a hand? I'd like to spend some time with you."),
                new("defer", "", "I need more time. I am not ready to act on this thought."),
                new("reject", "", "No. I don't want to follow this idea."),
            };
            var baseline = new ReflectionRequest("social-" + scene.Id, 720, scene.Actor, scene.Subject, 7,
                scene.Memory, scene.Context, choices,
                Proposal: new ReflectionProposal("social-" + scene.Id, scene.Thought, scene.Choice, new[] { "controlled-social-trial" }));
            Dictionary<string, string> Identity() => choices.ToDictionary(c => c.Id, c => c.Id, StringComparer.Ordinal);
            pairs.Add(new(scene.Id + "-perspective", "Only the character's stated perspective changes; memory, proposed thought and response lines stay fixed.",
                baseline, baseline with { Context = scene.VariantContext }, Identity()));
            pairs.Add(new(scene.Id + "-inspiration", "Only the imagined proposal changes; known memory, character context and candidate lines stay fixed. The suggestion reference follows its executable idea but is omitted from evaluator input.",
                baseline, baseline with { Proposal = baseline.Proposal! with { Thought = scene.VariantThought, SuggestedChoice = scene.VariantChoice } }, Identity()));
            pairs.Add(new(scene.Id + "-order", "Only response order reverses in this different scene; no future event or answer is prescribed.",
                baseline, baseline with { Choices = choices.Reverse().ToArray() }, Identity()));
        }
        return pairs;
    }

    private sealed record SocialScene(string Id, string Actor, string Subject, string Memory,
        string Context, string VariantContext, string Thought, string VariantThought, string Choice, string VariantChoice);

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
