using System.Text.Json;
using UnderGlass.Sim.Lives;
using Xunit;
namespace UnderGlass.Sim.Tests;
public sealed class LiveHistoryStudioTests
{
    private static HistoryPack Pack(ReadingOrigin origin = ReadingOrigin.ExperimentalStated) => new(1,
        new[] { new HistorySource("s", "Studio experiment", HistorySourceKind.Authored) },
        new[] { new HistoryFact("f", HistoryKind.Practice, new[]{"s"}) },
        new[] { new HistoryAccount("a", "f", "Robin", "I enjoy games with Maru.", KnowledgeKind.Lived, new[]{"s"}, new[]{new HistoryCue("person:Maru")}) },
        new[] { new HistoryReading("r", "a", new(MemoryObjectKind.Person,"Maru"), ReadingStance.Warm,
            "I like her company.", origin, null, new(ReviewStatus.Reviewed,"Studio author","2026-10-15"), new[]{"s"}, Array.Empty<HistoryCue>()) });
    [Fact] public void Experimental_own_statement_seeds_but_external_interpretation_does_not()
    {
        var experimental = new PersonalMemory("Robin", Pack()).Snapshot();
        Assert.Equal(0.05, Assert.Single(experimental.Associations).Amount);
        Assert.Empty(new PersonalMemory("Robin", Pack(ReadingOrigin.Authored)).Snapshot().Associations);
        Assert.Empty(new PersonalMemory("Maru", Pack()).Snapshot().Associations);
    }
    [Fact] public void Experimental_past_cannot_invent_a_numeric_link()
    {
        var p = Pack();
        Assert.Throws<InvalidDataException>(()=>(p with {Facts=new[]{p.Facts[0] with{Kind=HistoryKind.Past}}}).CopyValidated());
        Assert.Throws<InvalidDataException>(()=>(p with {Sources=new[]{p.Sources[0] with{Kind=HistorySourceKind.Canon}}}).CopyValidated());
        Assert.Throws<InvalidDataException>(()=>(p with {Readings=new[]{p.Readings[0],p.Readings[0] with{Id="r2"}}}).CopyValidated());
    }
    [Fact] public async Task Live_snapshots_do_not_change_the_recording_and_contain_no_future_acts()
    {
        var o=new ReplayOptions {Days=1, Seed=7, LivesRouting=new(){Enabled=true}, History=Pack(), Speech=new(){Enabled=true}};
        string expected=await Replay.JsonAsync(o,new AuthoredReflectionMind());int count=0;
        string observed=await Replay.JsonAsync(o,new AuthoredReflectionMind(),observe:(at,read)=>{
            if(at%300!=0)return;
            using var doc=JsonDocument.Parse(read());
            Assert.Equal(at,doc.RootElement.GetProperty("live").GetProperty("at").GetInt32());
            Assert.All(doc.RootElement.GetProperty("acts").EnumerateArray(),a=>Assert.True(a[1].GetInt32()<=at));
            count++;
        });
        Assert.True(count>1);Assert.Equal(expected,observed);
    }
}
