using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>The viewer page itself (sim/viewer/index.html), read as text where no browser is needed.</summary>
public class ViewerPageTests
{
    [Fact]
    public void PlaybackSticksOutsideTheMapAndKeepsMomentNavigationWithIt()
    {
        string html = File.ReadAllText(Path.Combine(GlossaryTests.Repo(), "sim", "viewer", "index.html"));
        int bar = html.IndexOf("id=\"playbackBar\"", StringComparison.Ordinal);
        int map = html.IndexOf("id=\"townview\"", StringComparison.Ordinal);
        int mapEnd = html.IndexOf("</header>", map, StringComparison.Ordinal);
        int barEnd = html.IndexOf("</section>", bar, StringComparison.Ordinal);
        Assert.True(map < mapEnd && mapEnd < bar && bar < barEnd);
        string controls = html[bar..barEnd];
        Assert.Contains("id=\"play\"", controls);
        Assert.Contains("id=\"previousMoment\"", controls);
        Assert.Contains("id=\"nextMoment\"", controls);
        Assert.Contains(".playback { position: sticky; top: 0;", html);
        Assert.Contains("scroll-padding-top: var(--playback-height", html);
        Assert.Contains("ResizeObserver", html);
    }

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
