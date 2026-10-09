using System.Text.Json;
using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

public class EncounterTests
{
    private static EncounterPerception Account(string actor = "Evelyn", string holder = "George",
        string? target = "George", string role = "received", int tick = 200, int id = 2)
        => new(id, tick, holder, "HelpedSomeone", actor, target, role, "Witnessed", 1, Array.Empty<string>());
    private static EncounterAppraisal Appraise(EncounterPerception? account = null, bool household = true,
        double? regard = 0.6, double familiarity = 1, params EncounterHistory[] history)
        => EncounterAppraiser.Of(account ?? Account(), 1, false, false, household, regard, familiarity, history);

    [Fact]
    public void RoutineReciprocalCareIsPersonalWithoutBeingTownNews()
    {
        var appraisal = Appraise(history: new[] { new EncounterHistory(0, 100, "George", "Evelyn", 1) });
        Assert.Equal("everyday", appraisal.Category);
        Assert.True(appraisal.PersonalSignificance > 0);
        Assert.Equal(0, appraisal.ListenerInterest);
        Assert.Contains(appraisal.Reasons, r => r.Code == "household-care");
        Assert.Contains(appraisal.Reasons, r => r.Code == "repeated" && r.Evidence.Contains(0));
    }

    [Fact]
    public void HouseholdCareAfterKnownConflictNamesEvidenceWithoutClaimingReconciliation()
    {
        var appraisal = Appraise(history: new[] { new EncounterHistory(0, 100, "Evelyn", "George", -1) });
        Assert.Equal("relationship", appraisal.Category);
        var reason = Assert.Single(appraisal.Reasons.Where(r => r.Code == "kindness-after-hurt"));
        Assert.Equal(new[] { 0, 2 }, reason.Evidence);
        Assert.Contains("reconciliation is not established", reason.Text);
    }

    [Fact]
    public void FutureUnrelatedAndStaleConflictsDoNotExplainPresentKindness()
    {
        var appraisal = Appraise(history: new[] {
            new EncounterHistory(0, 201, "Evelyn", "George", -1),
            new EncounterHistory(1, 100, "Pierre", "Penny", -1),
            new EncounterHistory(3, 200 - 8 * Clock.MinutesPerDay, "Evelyn", "George", -1) });
        Assert.Equal("everyday", appraisal.Category);
        Assert.DoesNotContain(appraisal.Reasons, r => r.Code == "kindness-after-hurt");
    }

    [Fact]
    public void DifferentListenersHaveDifferentInterestAndPrivateHistoryIsNotPassedIn()
    {
        var known = Appraise(Account(holder: "Penny", role: "witness"), false, 0, 0.7);
        var stranger = Appraise(Account(holder: "Pierre", role: "witness"), false, 0, 0);
        Assert.True(known.ListenerInterest > stranger.ListenerInterest);
        Assert.Equal("everyday", known.Category);
        Assert.DoesNotContain(known.Reasons, r => r.Code == "kindness-after-hurt");
    }

    [Fact]
    public void MistakenIdentityIsNotCorrectedAndUnknownIdentityDoesNotGainKnownHistory()
    {
        var wrong = Appraise(Account(actor: "Pierre"), false, 0, 0.5,
            new EncounterHistory(0, 100, "Evelyn", "George", -1));
        Assert.Equal("Pierre", wrong.Account.Actor);
        Assert.Equal("everyday", wrong.Category);
        var unknown = Appraise(Account() with { Actor = null, Confidence = 0 }, false, null, 0);
        Assert.Null(unknown.Account.Actor);
        Assert.Null(unknown.Regard);
        Assert.Contains(unknown.Reasons, r => r.Code == "uncertain-account");
    }

    [Fact]
    public void RelearningOneEventIsNotASecondOccurrence()
    {
        var appraisal = Appraise(history: new[] { new EncounterHistory(2, 100, "Evelyn", "George", 1) });
        Assert.DoesNotContain(appraisal.Reasons, r => r.Code == "repeated");
    }

    private static TownData Scene()
    {
        Villager Person(string name, int x) => new(name, "house", "villager",
            new Temperament(0.5, 0.5, 0.5, 0.5), new Body(100, -1), null,
            new[] { new Haunt("Room", new Tile(x, 2), 0, Clock.MinutesPerDay, 1) },
            new Dictionary<string, double>(), Array.Empty<string>());
        var feelings = FeelingOptions.WithDesire();
        feelings.AnswerOn = feelings.ReturnOn = feelings.MakeUpOn = feelings.RetaliateOn = false;
        feelings.AvoidOn = feelings.WithdrawOn = false;
        return new() {
            Cast = new[] { Person("Evelyn", 3), Person("George", 5), Person("Penny", 7) },
            Places = new[] { new Location("Room", false, Enumerable.Repeat(new string('.', 20), 6).ToList()) },
            Links = Array.Empty<Link>(), Gatherings = Array.Empty<Gathering>(),
            Acts = new[] { new ActKind("HelpedSomeone", 2, 1, 2, 5, 0, Array.Empty<string>(),
                Affect: new Affect(Patient.Target, 0.3, 0.3, 1, TargetIs.Chosen)) },
            Feelings = feelings, Authority = new AuthorityOptions { ElectConstable = false },
            Body = new BodyOptions { AwakeHoursAtRest = 100_000 }, Gossip = new GossipOptions { ChatChance = 0 }, Wander = 0,
        };
    }

    [Fact]
    public void EngineCapturesRolesAndUnknownDetailsWithoutChangingDeterministicRun()
    {
        var sim = new Simulation(1, Scene());
        sim.Place(new[] { new Scenario("care", "HelpedSomeone", "Evelyn", "George", Day: 0, From: 600, To: 601) });
        var result = sim.Run(1);
        var context = Assert.Single(result.Encounters);
        Assert.Null(context.Activity); Assert.Null(context.Item); Assert.Null(context.Effort); Assert.Null(context.Amount);
        var performed = Assert.Single(result.Appraisals.Where(x => x.Account.Holder == "Evelyn"));
        var received = Assert.Single(result.Appraisals.Where(x => x.Account.Holder == "George"));
        Assert.Equal("performed", performed.Account.Role); Assert.Equal("received", received.Account.Role);
        Assert.Equal(600, performed.Account.Tick); Assert.Equal(604, received.Account.Tick);
        Assert.Empty(result.EncounterCauses);
        Assert.Equal("everyday", received.Category);
        // Model and bulk simulation decisions remain seeded and unchanged (existing pins check hashes).
        var again = new Simulation(1, Scene());
        again.Place(new[] { new Scenario("care", "HelpedSomeone", "Evelyn", "George", Day: 0, From: 600, To: 601) });
        Assert.Equal(Metrics.LogHash(result), Metrics.LogHash(again.Run(1)));
    }

    [Fact]
    public void RecordingUsesAnAdditiveVersionedSchemaAndDeterministicSnapshots()
    {
        string json = Replay.Json(new ReplayOptions { Seed = 7, Days = 1 });
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("encounterVersion").GetInt32());
        Assert.Equal(EncounterAppraiser.Revision, doc.RootElement.GetProperty("appraisalRevision").GetString());
        Assert.NotEmpty(doc.RootElement.GetProperty("encounters").EnumerateArray());
        Assert.NotEmpty(doc.RootElement.GetProperty("appraisals").EnumerateArray());
        Assert.Equal(json, Replay.Json(new ReplayOptions { Seed = 7, Days = 1 }));
    }

    private sealed class HelpMind : IReflectionMind
    {
        public Task<ReflectionAnswer> ReflectAsync(ReflectionRequest request, CancellationToken cancellationToken = default)
        {
            var choice = request.Choices.First(c => c.Kind == "HelpedSomeone");
            return Task.FromResult(new ReflectionAnswer("Perhaps I could return that kindness.", choice.Id,
                request.Choices.ToDictionary(c => c.Id, c => c.Id == choice.Id ? 1.0 : 0.0), "test"));
        }
    }

    [Fact]
    public async Task PrivateReflectionCauseLinksToReceiptWithoutEnteringWitnessAccounts()
    {
        var sim = new Simulation(1, Scene());
        sim.ConfigureReflection(new ReflectionOptions { Enabled = true, DailyChance = 1, QuietMinutes = 5 });
        sim.Place(new[] { new Scenario("care", "HelpedSomeone", "Evelyn", "George", Day: 0, From: 600, To: 601) });
        var result = await sim.RunAsync(1, new HelpMind());
        Assert.NotEmpty(result.EncounterCauses);
        foreach (var cause in result.EncounterCauses)
        {
            var receipt = Assert.Single(result.Reflections.Where(x => x.Request.Id == cause.ReflectionId));
            Assert.Equal(receipt.Choice, cause.ChoiceId);
            Assert.Contains(receipt.Events, x => x.Status == "acted" && x.ActId == cause.ActId);
            var witnesses = result.Appraisals.Where(x => x.Account.ActId == cause.ActId && x.Account.Holder != cause.Actor).ToArray();
            Assert.NotEmpty(witnesses);
            // The perceived contract carries no private cause, thought, source link or interpretation.
            string accounts = JsonSerializer.Serialize(witnesses);
            Assert.DoesNotContain("Perhaps I could", accounts);
            Assert.DoesNotContain(cause.ReflectionId!, accounts);
        }
    }

    [Fact]
    public void UndergoingAnOfficialActIsNotMislabelledAsPerformingIt()
    {
        var warning = new ActKind("WarnedByMayor", 2, -1, 1, 5, 0, Array.Empty<string>(),
            Affect: new Affect(Patient.Actor, -0.1, 0.3, 0.4, TargetIs.Chosen));
        var sim = new Simulation(1, Scene() with { Acts = new[] { warning } });
        sim.Place(new[] { new Scenario("warning", warning.Name, "George", "Evelyn", Day: 0, From: 600, To: 601) });
        var result = sim.Run(1);
        Assert.Equal("underwent", Assert.Single(result.Encounters).ActorRole);
        Assert.Equal("underwent", Assert.Single(result.Appraisals.Where(x => x.Account.Holder == "George")).Account.Role);
        Assert.Empty(result.EncounterCauses);
    }

    [Fact]
    public void RelationshipSnapshotsDoNotUseFinalRegard()
    {
        var sim = new Simulation(1, Scene());
        sim.Place(new[] { new Scenario("care", "HelpedSomeone", "Evelyn", "George", Day: 0, From: 600, To: 601) });
        var result = sim.Run(1);
        var snapshot = Assert.Single(result.Appraisals.Where(x => x.Account.Holder == "George"));
        Assert.NotNull(snapshot.Regard);
        Assert.True(result.Regard[("George", "Evelyn")] > snapshot.Regard);
    }
}
