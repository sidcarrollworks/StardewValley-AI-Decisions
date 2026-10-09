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

    private static string ObserverCode()
    {
        string page = Page();
        int start = page.IndexOf("// ---- observer focus", StringComparison.Ordinal);
        Assert.True(start >= 0, "the viewer has no observer focus renderer");
        int end = page.IndexOf("// ---- inner life", start, StringComparison.Ordinal);
        Assert.True(end > start);
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
        Assert.Contains("[\"layaResponse\", \"Exact Laya response\"]", code);
        Assert.Contains("[\"generationResponse\", \"Exact imagination response and server metadata\"]", code);
        Assert.Contains("text: request.proposal.id", code);
        Assert.Contains("(request.proposal.tags ?? []).join", code);
        Assert.DoesNotContain("request.proposal.thought", code);
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

    [Fact]
    public void ObserverFocusReadsActsAndThoughtsOnlyAtTheCurrentClock()
    {
        string code = ObserverCode();
        Assert.Contains("if (act.tick > t) break;", code);
        Assert.Contains("reflectionAt(record, t)", code);
        Assert.Contains("if (!view) continue;", code);
        Assert.Contains("if (!view.answer)", code);
        Assert.DoesNotContain("record.answer", code);
        Assert.DoesNotContain("innerHTML", code);
        Assert.Contains("text: item.title", code);
        Assert.Contains("text: item.line", code);
    }

    [Fact]
    public void MeaningfulMomentControlsExposeOnlyNavigationAndNeverPreviewFutureText()
    {
        string page = Page(), code = ObserverCode();
        int start = page.IndexOf("m.moments =", StringComparison.Ordinal);
        int end = page.IndexOf("m.life =", start, StringComparison.Ordinal);
        string index = page[start..end];
        Assert.DoesNotContain("answer", index);
        Assert.DoesNotContain("thought", index);
        Assert.DoesNotContain("e.text", index);
        Assert.Contains("moment.tick > t", code);
        Assert.Contains("moments[i].tick < t", code);
        Assert.Contains("observerScope(moment.people, person, hood)", code);
        Assert.DoesNotContain("$(\"nextMoment\").textContent", code);
    }

    [Fact]
    public void OutcomeProseDoesNotReadAResolutionBeforeItHappens()
    {
        string code = InnerLifeCode();
        Assert.Contains("e.tick <= t && e.resolved >= 0 && e.resolved <= t", code);
        Assert.Contains("reflectionEventText(event, request, S.t)", code);
        Assert.Contains("responded with kindness", code);
        Assert.Contains("No matching response was recorded", code);
    }

    [Fact]
    public void EarlierThoughtLinksAndMultipassReceiptsKeepTheSamePrivacyBoundary()
    {
        string code = InnerLifeCode();
        Assert.Contains("r.request.id === request.continuity.priorId && r.request.tick <= S.t", code);
        Assert.Contains("reflectionAt(prior, S.t)", code);
        Assert.Contains("text: pass.prompt", code);
        Assert.Contains("text: pass.response", code);
        Assert.Contains("text: JSON.stringify(pass.labelsToChoiceIds", code);
        int guard = code.IndexOf("if (answer) {", code.IndexOf("const detail =", StringComparison.Ordinal), StringComparison.Ordinal);
        int receipts = code.IndexOf("answer.evaluations?.length", StringComparison.Ordinal);
        Assert.True(guard >= 0 && receipts > guard);
        Assert.DoesNotContain("innerHTML", code);
    }

    [Fact]
    public void StoriesHideFutureEncountersSpreadAndAuthorityUnlessAnalysisIsExplicitlyEnabled()
    {
        string page = Page();
        int start = page.IndexOf("// ---- stories", StringComparison.Ordinal);
        int end = page.IndexOf("// ---- the population", start, StringComparison.Ordinal);
        string code = page[start..end];
        Assert.Contains("Whole-run analysis (includes future events)", page);
        Assert.Contains("const storyKey = `${S.tab}|${Math.floor(t)}|", page);
        Assert.Contains("$(\"storyWholeRun\").checked ? R.end : S.t", code);
        Assert.Contains("a.tick > cutoff || !inHood(a.actor)", code);
        Assert.Contains("if (a.tick > cutoff)", code);
        Assert.Contains(".filter((b) => b[0] <= cutoff)", code);
        Assert.Contains(".filter((telling) => telling[0] <= cutoff)", code);
        Assert.Contains("if (e.tick <= cutoff)", code);
        Assert.Contains("if (v[0] <= cutoff && v[1] === a.id)", code);
        Assert.Contains("if (c[0] <= cutoff && c[1] === a.id)", code);
        Assert.DoesNotContain("R.reach", code);
        Assert.DoesNotContain("a.witnesses", code);
        // Following an encounter returns to observation even if analysis was previously enabled.
        Assert.Contains("$(\"storyWholeRun\").checked = false", ObserverCode());
    }

    [Fact]
    public void WakingDreamLabelsAndGenerationMetadataRequireAConsideredAnswer()
    {
        string code = InnerLifeCode();
        Assert.Contains("return !!view.answer && view.request.opportunity?.kind === \"dream\"", code);
        Assert.Contains("text: isWakingDream(view) ? \"Dream on waking\" : \"Imagined\"", code);
        Assert.Contains("An imagined possibility recorded on waking; it is not a remembered encounter.", code);
        int detail = code.IndexOf("const detail =", StringComparison.Ordinal);
        int guard = code.IndexOf("if (answer) {", detail, StringComparison.Ordinal);
        int receipt = code.IndexOf("[\"generationResponse\"", detail, StringComparison.Ordinal);
        Assert.True(guard >= 0 && receipt > guard);
        Assert.Contains("text: answer[field]", code);
        Assert.Contains("isWakingDream(view) && t - view.request.tick <= 30", ObserverCode());
    }

    [Fact]
    public void ReconsiderationShowsTheHumanReasonInsteadOfTheAuditRequestId()
    {
        string code = InnerLifeCode();
        int start = code.IndexOf("if (event.status === \"reconsidered\")", StringComparison.Ordinal);
        int fallback = code.IndexOf("if (event.status !== \"outcome\")", start, StringComparison.Ordinal);
        string humanText = code[start..fallback];
        Assert.Contains("return request.continuity?.reason", humanText);
        Assert.Contains("A later encounter brought the earlier idea back.", humanText);
        Assert.DoesNotContain("event.text", humanText);
        Assert.DoesNotContain("priorId", humanText);
        Assert.Contains("reconsidered: \"Reconsidered\"", code);
        // The original record still identifies the earlier thought for navigation and audit.
        Assert.Contains("r.request.id === request.continuity.priorId", code);
        Assert.DoesNotContain("event.text =", code);
    }
}
