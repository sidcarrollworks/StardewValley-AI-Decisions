using NpcDecision;
using Xunit;

namespace NpcDecision.Tests;

/// <summary>Pins the committed calibration table (data/laya-calibration.json): it must parse,
/// every question template the mod asks must have a row (or an explicit "not calibrated" one),
/// and the numbers must be sane. The corrections stay off by construction — the file carries
/// neither RelativeScale nor w, and nothing in the mod applies the table to a decision.</summary>
public class LayaCalibrationTests
{
    private static string PathOf => System.IO.Path.GetFullPath(System.IO.Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "laya-calibration.json"));

    [Fact]
    public void FileParsesAndEveryKnownTemplateHasARow()
    {
        LayaCalibration calibration = LayaCalibration.FromJson(File.ReadAllText(PathOf));
        foreach (string template in LayaCalibration.KnownTemplates)
        {
            LayaCalibration.QuestionRow? row = calibration.Of(template);
            Assert.True(row is not null, $"no calibration row for template '{template}' (add one or mark it not calibrated)");
            Assert.True(row.A is not null && row.B is not null, $"template '{template}' must calibrate both card variants");
        }
    }

    [Fact]
    public void NumbersAreWithinBoundsWhenCalibrated()
    {
        LayaCalibration calibration = LayaCalibration.FromJson(File.ReadAllText(PathOf));
        foreach ((string template, LayaCalibration.QuestionRow row) in calibration.Questions)
        {
            foreach (LayaCalibration.VariantRow variant in new[] { row.A, row.B })
            {
                if (variant.NotCalibrated)
                    continue;
                Assert.InRange(variant.Median!.Value, 0.0, 1.0);
                Assert.InRange(variant.Spread!.Value, 0.0, 1.0);
            }
            _ = template;
        }
    }

    [Fact]
    public void NotCalibratedRowsAreExplicitlyAllNull()
    {
        LayaCalibration calibration = LayaCalibration.FromJson(File.ReadAllText(PathOf));
        LayaCalibration.QuestionRow choose = calibration.Of("choose_bring_up")!;
        Assert.True(choose.A.NotCalibrated && choose.B.NotCalibrated,
            "the planner's choose question is not measured by the spread eval; it must stay an explicit 'not calibrated' row");
    }

    [Fact]
    public void NormalizeTemplateMatchesTheModsRealQuestions()
    {
        // Unit test of the mapper on the mod's exact wordings (the strings come from
        // InitiationLadder.AttentionProposition / IntentPlanner.SpeakProposition; the test that
        // calls the REAL builders and breaks on a wording change lives in
        // tests/NpcMinds.Tests/SpreadPanelTests.cs).
        Assert.Equal("attention_emote",
            LayaCalibration.NormalizeTemplate("should <npc> try to get the player's attention with Emote now?"));
        Assert.Equal("attention_bubble",
            LayaCalibration.NormalizeTemplate("should <npc> try to get the player's attention with Bubble now?"));
        Assert.Equal("attention_approach",
            LayaCalibration.NormalizeTemplate("should <npc> try to get the player's attention with Approach now?"));
        // IntentPlanner.cs:108 — "does {npc} have news for the player?"
        Assert.Equal("speak", LayaCalibration.NormalizeTemplate("does <npc> have news for the player?"));
        // motives.md — "would <npc> hold this against the player?" and the newcomer welcome.
        Assert.Equal("hold_against", LayaCalibration.NormalizeTemplate("would <npc> hold this against the player?"));
        Assert.Equal("welcome_newcomer",
            LayaCalibration.NormalizeTemplate("Would <npc> go out of their way to welcome a newcomer in person?"));
        // Unknown steps and unrelated questions stay unmapped.
        Assert.Null(LayaCalibration.NormalizeTemplate("should <npc> try to get the player's attention with Mail now?"));
        Assert.Null(LayaCalibration.NormalizeTemplate("anything else"));
        // The trait mapping follows the normalized id.
        Assert.Equal(("boldness", +1), LayaCalibration.TraitForTemplate(
            "should <npc> try to get the player's attention with Emote now?"));
        Assert.Equal(("chattiness", +1), LayaCalibration.TraitForTemplate("does <npc> have news for the player?"));
        Assert.Equal(("forgiveness", -1), LayaCalibration.TraitForTemplate("would <npc> hold this against the player?"));
        // The motives' walk-up close calls use the eval's wordings, so they compare with it; the
        // other close calls are unmeasured but still follow boldness.
        Assert.Equal("close_friendly", LayaCalibration.NormalizeTemplate("would <npc> walk over to greet the player now?"));
        Assert.Equal("close_hostile", LayaCalibration.NormalizeTemplate("would <npc> confront the player now?"));
        Assert.Null(LayaCalibration.NormalizeTemplate("would <npc> write the player a letter now?"));
        Assert.Equal(("boldness", +1), LayaCalibration.TraitForTemplate("would <npc> write the player a letter now?"));
    }
}
