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
}
