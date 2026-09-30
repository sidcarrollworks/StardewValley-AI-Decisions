using System.Net;
using System.Text;
using Xunit;

namespace NpcDecision.Tests;

public class BudgetViewTests
{
    private static readonly byte[] NoulAnswer = Encoding.UTF8.GetBytes(
        "{\"answers\":{\"q\":{\"noul\":0.5}}}");

    private sealed class StubHandler : HttpMessageHandler
    {
        public int Posts;
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
                Posts++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(NoulAnswer) };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Send(request, cancellationToken));
    }

    [Fact]
    public void WithBudget_CancelledViewBudgetAbortsTheCall_BaseClientKeepsWorking()
    {
        var handler = new StubHandler();
        using var baseClient = new LayaDecisionClient(handler, new LayaOptions { Timeout = TimeSpan.FromSeconds(30) });
        using var viewCts = new CancellationTokenSource();
        LayaDecisionClient view = baseClient.WithBudget(viewCts.Token);
        viewCts.Cancel();

        // The view's call aborts on its own cancelled token...
        Assert.Throws<OperationCanceledException>(() => view.YesNo("state", "p?"));
        Assert.Equal(1, baseClient.Calls); // ...and the shared stats counted the attempt
        Assert.Equal(1, baseClient.CallFallbacks);

        // ...while the base client (never given the view's token) still answers fine.
        Assert.Equal(0.5, baseClient.YesNo("state", "p?"));
        Assert.Equal(2, baseClient.Calls);
        Assert.Equal(1, baseClient.CallFallbacks);
    }

    [Fact]
    public void WithBudget_ViewAndBaseShareLatencyStats()
    {
        var handler = new StubHandler();
        using var baseClient = new LayaDecisionClient(handler, new LayaOptions { Timeout = TimeSpan.FromSeconds(30) });
        LayaDecisionClient view = baseClient.WithBudget(default);

        view.YesNo("state", "p?");
        baseClient.YesNo("state", "p?");

        (double median, double p95) = baseClient.Latency();
        Assert.True(median >= 0 && p95 >= 0); // two samples recorded; values are timing-dependent
        Assert.Equal(2, baseClient.Calls);
    }
}
