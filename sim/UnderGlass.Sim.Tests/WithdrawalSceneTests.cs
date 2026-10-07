using UnderGlass.Sim;
using Xunit;

namespace UnderGlass.Sim.Tests;

/// <summary>
/// Phase 0d.6 in small scenes (spec section 8): what is measured, hurts at home, households
/// arguing, contagion, being left out and recovering, patience, the coercion ratchet, expression,
/// and missing people. The convention is DesireSceneTests': one room (or two, for someone kept
/// apart), nobody tires, sensitivity and retention 0.5, the scenes' own feeling rows, scheduled
/// acts, and the gate with every added rule off unless a test turns one on.
/// </summary>
public class WithdrawalSceneTests
{
    private const int D = Clock.MinutesPerDay;
    private static readonly int Ten = Clock.At(10);

    internal static Haunt At(int x, int y, int from = 0, int to = D, string place = "Room") => new(place, new Tile(x, y), from, to, 1);

    /// <summary>Someone who spends the whole day at one spot and never tires.</summary>
    internal static Villager V(string name, string household, int x, int y, double bold = 0.5, double chat = 1.0, double self = 0.6,
        Dictionary<string, double>? acts = null, Dictionary<string, Kin>? family = null, string place = "Room")
        => new(name, household, "villager", new Temperament(chat, bold, 0.5, self), new Body(100, -1), null,
            new[] { At(x, y, place: place) }, acts ?? new Dictionary<string, double>(), Array.Empty<string>(), 30, family);

    internal static Location Room(string name = "Room") => new(name, false, Enumerable.Repeat(new string('.', 40), 6).ToList());

    internal static BodyOptions Awake() => new() { AwakeHoursAtRest = 100_000 };

    internal static readonly Affect GiftRow = new(Patient.Target, 0.2, 0.3, 1, TargetIs.Chosen, Tilt: 1);
    internal static readonly Affect ArguedRow = new(Patient.Target, -0.3, 0.3, 1, TargetIs.Chosen);
    internal static readonly Affect HelpRow = new(Patient.Target, 0.3, 0.3, 1, TargetIs.Chosen, Tilt: 1);

    internal static List<ActKind> Kinds(double gift = 0, double argue = 0) => new()
    {
        new("GaveGift", 1.5, 1, 1, 1, gift, Array.Empty<string>(), Affect: GiftRow),
        new("Argued", 3.0, -1, 2, 10, argue, Array.Empty<string>(), MinAge: 13, Affect: ArguedRow),
        new("HelpedSomeone", 2.0, 1, 2, 5, 0, Array.Empty<string>(), MinAge: 10, Affect: HelpRow),
    };

    internal static Dictionary<string, double> Does(params string[] kinds) => kinds.ToDictionary(k => k, _ => 1.0);

    /// <summary>X0: a gift from someone in another household counts as kindness received and one
    /// from a housemate does not; a day beside someone from another household is a day with
    /// company and a day beside a housemate is not; one's own gift that is never returned is
    /// unanswered, but a gift that returns someone else's is not counted at all; and measuring
    /// changes nothing the town does.</summary>
    [Fact]
    public void WhatIsMeasured()
    {
        // Ann and her housemate Cy share one end of the room with Bob (another house); Dee (another
        // house) stands at the far end, out of company.
        var cast = new[] { V("Ann", "A", 3, 2), V("Cy", "A", 5, 2), V("Bob", "B", 4, 3), V("Dee", "C", 30, 2, bold: 0.0) };
        var scheduled = new List<(int, string, string)> { (Ten, "Bob", "GaveGift"), (Ten + 30, "Cy", "GaveGift") };
        SimResult Run(FeelingOptions o) => new Simulation(1, cast, new[] { Room() }, Kinds(), feelings: o, body: Awake(),
            scheduled: scheduled, wander: 0).Run(10);
        SimResult r = Run(FeelingOptions.WithDesire());

        PersonDays ann = r.Daily["Ann"];
        Act fromBob = Assert.Single(r.Acts, a => a.Actor == "Bob" && a.Kind == "GaveGift");
        Assert.Equal("Ann", fromBob.Target);
        // Cy's gift went to Ann or Bob; only Bob's to Ann counts for her.
        int fromStrangers = r.Acts.Count(a => a.Kind == "GaveGift" && a.Target == "Ann" && a.Actor is "Bob" or "Dee");
        Assert.Equal(fromStrangers, ann.KindIn.Sum());
        Assert.True(ann.Met[0]);
        Assert.DoesNotContain(true, r.Daily["Dee"].Met);
        // Ann returned Bob's gift; that return is never returned again, and is not counted.
        Assert.Contains(r.Acts, a => a.Actor == "Ann" && a.Kind == "GaveGift" && a.Target == "Bob" && a.About == fromBob.Id);
        Assert.Equal(0, ann.KindOut.Sum());
        // Bob's own gift was returned: counted, and answered.
        Assert.Equal((1, 0), (r.Daily["Bob"].KindOut.Sum(), r.Daily["Bob"].Unanswered.Sum()));
        // Free time out is counted: Ann's room is not her home.
        Assert.True(ann.OutMinutes[1] > 20 * 60);
        Assert.All(r.Daily.Values.SelectMany(d => d.LeftOut), e => Assert.InRange(e, 0, 1));
        Assert.Empty(r.Watched);
    }

    /// <summary>X0: one's own kindness to someone outside the household that is never returned is
    /// unanswered once its week is up, and being left out reads it against the town: Ann's gift to
    /// shy Dee goes unanswered while Bob's and Cy's gifts to each other are returned.</summary>
    [Fact]
    public void AKindnessNeverReturnedIsUnanswered()
    {
        var cast = new[] { V("Ann", "A", 3, 2), V("Dee", "C", 4, 2, bold: 0.0), V("Bob", "B", 20, 2), V("Cy", "D", 21, 2) };
        SimResult r = new Simulation(1, cast, new[] { Room() }, Kinds(), feelings: FeelingOptions.WithDesire(), body: Awake(),
            scheduled: new[] { (Ten, "Ann", "GaveGift"), (Ten, "Bob", "GaveGift"), (Ten + 60, "Cy", "GaveGift") }, wander: 0).Run(10);

        Assert.Empty(r.Acts.Where(a => a.Actor == "Dee"));
        PersonDays ann = r.Daily["Ann"];
        Assert.Equal((1, 1), (ann.KindOut.Sum(), ann.Unanswered.Sum()));
        Assert.Equal((0, 0), (r.Daily["Bob"].Unanswered.Sum(), r.Daily["Cy"].Unanswered.Sum()));
        Assert.Equal(1, r.Daily["Bob"].KindOut.Sum());
        int day = Array.IndexOf(ann.Unanswered, 1);
        Assert.InRange(day, 7, 8);
        // The town's median share is 0 (Bob 0, Cy 0, Ann 1): Ann's part is the whole 0.3.
        Assert.True(ann.LeftOut[day] >= 0.3 - 1e-12, $"{ann.LeftOut[day]}");
        Assert.True(ann.LeftOut[day] > r.Daily["Bob"].LeftOut[day]);
    }
}
