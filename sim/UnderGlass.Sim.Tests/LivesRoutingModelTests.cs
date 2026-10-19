using System.Text.Json;
using UnderGlass.Minds;
using UnderGlass.Sim.Lives;
using Xunit;

namespace UnderGlass.Sim.Tests;

public sealed class LivesRoutingModelTests
{
    private static TownData Town(int close = 1440, Job? job = null) => new()
    {
        Cast = new[] { new Villager("Ann", "NoHome", "villager", new Temperament(1,.5,.5,.5), new Body(100,-1), job,
            new[] { new Haunt("Yard",new(4,3),0,1440,1), new Haunt("Hall",new(4,3),0,close,1) },
            new Dictionary<string,double>(), Array.Empty<string>()) },
        Places = new[]{"Yard","Hall","Work"}.Select(n=>new Location(n,false,Enumerable.Repeat("............",8).ToArray())).ToArray(),
        Links = Array.Empty<Link>(), Gatherings=Array.Empty<Gathering>(), Acts=Array.Empty<ActKind>(),
        Feelings=new FeelingOptions(), Authority=new AuthorityOptions{ElectConstable=false},
        Body=new BodyOptions{AwakeHoursAtRest=100_000, MorningMinutes=0}, Gossip=new GossipOptions{ChatChance=0}, Wander=0,
    };
    private sealed class Mind(bool delayed = false, bool fail = false) : ILivesDecisionMind
    {
        public List<LivesDecisionRequest> Requests { get; } = new();
        public async Task<LivesDecisionAnswer> EvaluateAsync(LivesDecisionRequest request, CancellationToken cancellationToken=default)
        {
            Requests.Add(request);
            if(delayed) await Task.Delay(1,cancellationToken);
            if(fail) return LivesDecisionEngine.Fallback(request,"Synthetic unavailable provider",called:true);
            string pick=request.Fallback.Alternatives.FirstOrDefault(a=>a.Option.Line=="Spend time at Hall.")?.Option.Id
                ?? request.Fallback.Alternatives[0].Option.Id;
            return new(request.Fallback.Alternatives.ToDictionary(a=>a.Option.Id,a=>a.Option.Id==pick?1d:0d),"laya",request.Model,Called:true);
        }
    }
    private static async Task<(SimResult Result,string[] Trace)> Run(ILivesDecisionMind mind, TownData? town=null, bool record=true)
    {
        var sim=new Simulation(7,town??Town());sim.ConfigureLivesRouting(new(){Enabled=true,Evaluate=true,Record=record},mind:mind);
        var trace=new List<string>();
        var result=await sim.RunAsync(1,new AuthoredReflectionMind(),(_,s)=>trace.Add(s.Where("Ann").Place));
        return (result,trace.ToArray());
    }
    [Fact]
    public async Task EmptyHistoryStillEvaluatesAndAppliedSelectionMovesThePersonNextMinute()
    {
        var mind=new Mind();var run=await Run(mind);
        Assert.NotEmpty(mind.Requests);
        Assert.All(mind.Requests,r=>{
            Assert.Empty(r.Fallback.Recall.Items);Assert.False(r.Fallback.Legacy);
            Assert.Contains(r.Fallback.Alternatives,a=>a.Option.Line=="Stay here for a while.");
            Assert.Equal("typed-decisions",r.Model);
        });
        var first=run.Result.LivesRouting[0];int at=first.Decision.Opportunity.At;
        Assert.Equal("Yard",run.Trace[at]);Assert.Equal("Hall",run.Trace[at+1]);
        Assert.Equal("applied",first.Resolution);Assert.Equal(at+1,first.ResolvedAt);
        Assert.Equal(first.SelectedOption,first.Evaluation!.SelectedId);
        Assert.True(mind.Requests.Count<30); // Reconsider after a stay, not each minute.
    }
    [Fact]
    public async Task LatencyRecordingAndExactTapeDoNotChangeMovement()
    {
        var first=await Run(new Mind());var slow=await Run(new Mind(delayed:true));
        var quiet=await Run(new Mind(),record:false);
        var tape=new RecordedLivesDecisionMind(first.Result.LivesRouting.Select(r=>r.Evaluation!));
        var replay=await Run(tape);tape.RequireConsumed();
        Assert.Equal(first.Trace,slow.Trace);Assert.Equal(first.Trace,quiet.Trace);Assert.Equal(first.Trace,replay.Trace);
        Assert.Empty(quiet.Result.LivesRouting);
        Assert.Equal(JsonSerializer.Serialize(first.Result.LivesRouting),JsonSerializer.Serialize(replay.Result.LivesRouting));
    }
    [Fact]
    public async Task FallbackKeepsRecordedWeightsAndOriginalDraw()
    {
        var run=await Run(new Mind(fail:true));
        Assert.All(run.Result.LivesRouting,r=>{Assert.Equal(r.Decision.SelectedId,r.SelectedOption);Assert.Equal("authored-fallback",r.Evaluation!.Answer.Backend);});
    }
    [Fact]
    public async Task APlaceClosingBeforeApplicationDoesNotReceiveTheChosenGoal()
    {
        var baseline=await Run(new Mind());int at=baseline.Result.LivesRouting[0].Decision.Opportunity.At;
        var run=await Run(new Mind(),Town(close:at+1));
        Assert.Equal("unavailable",run.Result.LivesRouting[0].Resolution);
        Assert.Equal("Yard",run.Trace[at+1]);
    }
    [Fact]
    public async Task AJobStartingBeforeApplicationSupersedesTheRoute()
    {
        var baseline=await Run(new Mind());int at=baseline.Result.LivesRouting[0].Decision.Opportunity.At;
        var run=await Run(new Mind(),Town(job:new Job("Work",new(4,3),at+1,at+100,Array.Empty<int>(),1,Commute:0)));
        Assert.Equal("superseded",run.Result.LivesRouting[0].Resolution);Assert.Equal("Work",run.Trace[at+1]);
    }
    private sealed class CancelMind(CancellationTokenSource source) : ILivesDecisionMind
    {
        public int At;
        public Task<LivesDecisionAnswer> EvaluateAsync(LivesDecisionRequest r,CancellationToken cancellationToken=default)
        {At=r.Fallback.Opportunity.At;source.Cancel();cancellationToken.ThrowIfCancellationRequested();throw new Exception();}
    }
    [Fact]
    public async Task CancellationNeverAdvancesToApplicationMinute()
    {
        using var source=new CancellationTokenSource();var mind=new CancelMind(source);var sim=new Simulation(7,Town());
        sim.ConfigureLivesRouting(new(){Enabled=true,Evaluate=true},mind:mind);int last=-1;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>sim.RunAsync(1,new AuthoredReflectionMind(),(m,_)=>last=m,source.Token));
        Assert.Equal(mind.At,last);
    }
    [Fact]
    public void EvaluationRequiresAnExplicitMindAndAsyncRun()
    {
        var sim=new Simulation(7,Town());Assert.Throws<ArgumentException>(()=>sim.ConfigureLivesRouting(new(){Enabled=true,Evaluate=true}));
        sim.ConfigureLivesRouting(new(){Enabled=true,Evaluate=true},mind:new Mind());
        Assert.Throws<InvalidOperationException>(()=>sim.Run(1));
        Assert.DoesNotContain("Evaluate",JsonSerializer.Serialize(new LivesRoutingOptions()));
    }
    [Fact]
    public async Task ReplayExportsRoutingReceiptsWithoutRequiringEvenings()
    {
        var options=new ReplayOptions{Seed=7,Days=1,Town=Town(),LivesRouting=new(){Enabled=true,Evaluate=true}};
        string json=await Replay.JsonAsync(options,new AuthoredReflectionMind(),livesMind:new Mind());
        using var doc=JsonDocument.Parse(json);var routes=doc.RootElement.GetProperty("livesRouting");
        Assert.Equal("typed-decisions",routes.GetProperty("backend").GetString());
        Assert.False(doc.RootElement.TryGetProperty("livesEvenings",out _));
        var receipts=routes.GetProperty("decisions").EnumerateArray().Select(r=>r.GetProperty("evaluation").Deserialize<LivesDecisionRecord>(
            new JsonSerializerOptions{PropertyNameCaseInsensitive=true,Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}})!).ToArray();
        var tape=new RecordedLivesDecisionMind(receipts);
        Assert.Equal(json,await Replay.JsonAsync(options,new AuthoredReflectionMind(),livesMind:tape));tape.RequireConsumed();
    }
}
