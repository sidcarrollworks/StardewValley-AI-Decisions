using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>The viewer page itself (sim/viewer/index.html), read as text where no browser is needed.</summary>
public class ViewerPageTests
{
    /// <summary>The whole-town map draws its plan from a picture that is clear between places, so
    /// each frame clears the canvas first. Without that, a zoom or a pan left the earlier frames
    /// showing in the gaps, names and rings in trails (Sid, 2026-10-08).</summary>
    [Fact]
    public void TheTownMapClearsEachFrameBeforeDrawingThePlan()
    {
        string html = File.ReadAllText(Path.Combine(GlossaryTests.Repo(), "sim", "viewer", "index.html"));
        int start = html.IndexOf("function drawTownMap()", StringComparison.Ordinal);
        Assert.True(start >= 0, "the viewer has no drawTownMap");
        int plan = html.IndexOf("g.drawImage(TOWN.back", start, StringComparison.Ordinal);
        Assert.True(plan > start, "drawTownMap no longer draws TOWN.back: check what clears the canvas now, and update this test");
        Assert.Contains("g.clearRect(0, 0, c.width, c.height)", html[start..plan]);
    }
}
