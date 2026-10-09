using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>Small source-level guards for the viewer's optional model record. Interactive timeline
/// and rendering behavior are also checked in a browser; no JavaScript runtime is needed by the
/// simulation's test suite.</summary>
public class ReflectionViewerTests
{
    private static string Page() => File.ReadAllText(Path.Combine(GlossaryTests.Repo(), "sim", "viewer", "index.html"));

    private static string InnerLifeCode()
    {
        string page = Page();
        int start = page.IndexOf("// ---- inner life", StringComparison.Ordinal);
        Assert.True(start >= 0, "the viewer has no inner-life renderer");
        int end = page.IndexOf("// ---- stories", start, StringComparison.Ordinal);
        Assert.True(end > start, "the inner-life renderer must have a reviewable boundary");
        return page[start..end];
    }

    [Fact]
    public void OlderVersionOneRecordingsNeedNoReflectionField()
    {
        string page = Page();
        Assert.Contains("m.reflections = run.reflections ?? [];", page);
        Assert.Contains("This run has no inner-life recording", page);
        Assert.Contains("--reflection authored", page);
        Assert.Contains("--reflection laya", page);
        Assert.Contains("event.status === \"acted\" && event.actId >= 0", page);
        Assert.Contains("if (act.tick > t) return \"\";", page);
        Assert.Contains("R.reflectionActs.has(act.id) ? \"from a thought\" : act.gate ? \"from a motive\"", page);
        Assert.Contains("R.actText(a) + (actOrigin(a, t)", page);
    }

    [Fact]
    public void ClockBoundaryHidesTheAnswerUntilItWasConsideredAndFiltersLaterEvents()
    {
        string code = InnerLifeCode();
        Assert.Contains("if (record.request.tick > t) return null;", code);
        Assert.Contains("record.events.filter((e) => e.tick <= t)", code);
        Assert.Contains("events.some((e) => e.status === \"considered\")", code);
        Assert.Contains("answer: considered ? record.answer : null", code);
        Assert.Contains("choice: considered ? record.choice : null", code);
        // Rendering must use the time-filtered view; reading the raw answer here would leak it.
        string renderer = code[code.IndexOf("function reflectionCard", StringComparison.Ordinal)..];
        Assert.DoesNotContain("record.answer", renderer);
        Assert.DoesNotContain("record.choice", renderer);
        Assert.DoesNotContain("record.events", renderer);
    }

    [Fact]
    public void ModelContentIsPassedOnlyToPlainTextElements()
    {
        string code = InnerLifeCode();
        Assert.DoesNotContain("innerHTML", code);
        Assert.DoesNotContain("outerHTML", code);
        Assert.DoesNotContain("insertAdjacentHTML", code);
        Assert.Contains("text: answer.thought", code);
        Assert.Contains("text: request.context", code);
        Assert.Contains("text: c.line", code);
        Assert.Contains("text: answer.note", code);
        Assert.Contains("text: answer[field]", code);
        Assert.Contains("else if (k === \"text\") e.textContent = v;", Page());
    }

    [Fact]
    public void EvidenceNavigationUsesTheRecordedEventTimeAndCannotJumpIntoTheFuture()
    {
        string code = InnerLifeCode();
        Assert.Contains("a.id === actId && a.tick <= t", code);
        Assert.Contains("setPlaying(false); setTime(act.tick);", code);
        Assert.Contains("reflectionJump(request.sourceActId, S.t", code);
        Assert.Contains("event.tick > t", code);
        Assert.Contains("setPlaying(false); setTime(event.tick);", code);
        Assert.Contains("reflectionEventJump(event, S.t)", code);
        Assert.DoesNotContain("reflectionJump(event.actId", code);
    }
}
