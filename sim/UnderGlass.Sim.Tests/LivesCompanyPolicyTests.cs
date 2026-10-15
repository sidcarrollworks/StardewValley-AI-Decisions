using System.Globalization;
using System.Text.Json;
using UnderGlass.Sim.Lives;
using Xunit;

namespace UnderGlass.Sim.Tests;

public sealed class LivesCompanyPolicyTests
{
    [Fact]
    public void NeutralAndUnencumberedHasOnlyYesAndNo()
    {
        CompanyAnswer answer = CompanyPolicy.Answer("episode", 10, "Bo", 0, new(), .1);
        Assert.Equal(new[] { "yes", "no" }, answer.Options.Select(o => o.Id));
        Assert.Equal(new[] { .5, .5 }, answer.Options.Select(o => o.Probability));
        Assert.Equal("yes", answer.SelectedId);
        Assert.Equal("no", CompanyPolicy.Answer("episode", 10, "Bo", 0, new(), .5).SelectedId);
        Assert.Equal("Let’s play.", answer.Options[0].Line);
        Assert.Equal("No, I don’t want to play.", answer.Options[1].Line);
    }

    [Theory]
    [InlineData(.2, 0, 0)]
    [InlineData(0, .2, 0)]
    [InlineData(0, 0, .2)]
    public void EachTemporaryStateOpensNotNow(double tired, double busy, double low)
    {
        var state = new AskState(tired, busy, low);
        CompanyAnswer answer = CompanyPolicy.Answer("episode", 10, "Bo", 0, state, .5);
        Assert.Equal(new[] { "yes", "not-now", "no" }, answer.Options.Select(o => o.Id));
        Assert.Equal(1, answer.Options.Sum(o => o.Probability), 12);
        Assert.Equal(.2 * Math.E / (2 + .2 * Math.E), answer.Options[1].Probability, 12);
        Assert.Equal("not-now", answer.SelectedId);
        Assert.Equal("Not now. Maybe another time.", answer.Options[1].Line);
        Assert.Equal(state, answer.State);
    }

    [Fact]
    public void NotNowUsesTheStrongestTemporaryReasonWithoutSummingTheStateAgain()
    {
        CompanyAnswer answer = CompanyPolicy.Answer("episode", 10, "Bo", .3, new(.1, .7, .4), .2);
        double total = Math.Exp(1.5) + .7 * Math.Exp(3.5) + Math.Exp(-1.5);
        Assert.Equal(Math.Exp(1.5) / total, answer.Options[0].Probability, 12);
        Assert.Equal(.7 * Math.Exp(3.5) / total, answer.Options[1].Probability, 12);
        Assert.Equal(Math.Exp(-1.5) / total, answer.Options[2].Probability, 12);
    }

    [Fact]
    public void FiniteMarginsIncludingExtremeTailsNeverOverflow()
    {
        foreach (double margin in new[] { -double.MaxValue, -1000.0, 1000.0, double.MaxValue })
        {
            CompanyAnswer answer = CompanyPolicy.Answer("episode", 10, "Bo", margin, new(.2, .3, .4), Math.BitDecrement(1.0), 20);
            Assert.All(answer.Options, option => Assert.True(double.IsFinite(option.Probability)));
            Assert.Equal(1, answer.Options.Sum(option => option.Probability), 12);
            Assert.Equal(margin > 0 ? "yes" : "no", answer.SelectedId);
        }
    }

    [Fact]
    public void ZeroScaleIgnoresMarginsButRetainsTheStrengthOfTheTemporaryReason()
    {
        CompanyAnswer answer = CompanyPolicy.Answer("episode", 10, "Bo", double.MaxValue, new(1, 1, 1), .5, 0);
        Assert.All(answer.Options, option => Assert.Equal(1.0 / 3, option.Probability, 12));
        Assert.Equal("not-now", answer.SelectedId);
        CompanyAnswer pair = CompanyPolicy.Answer("episode", 10, "Bo", -double.MaxValue, new(), .5, 0);
        Assert.All(pair.Options, option => Assert.Equal(.5, option.Probability));
        CompanyAnswer mild = CompanyPolicy.Answer("episode", 10, "Bo", double.MaxValue, new(.25, 0, 0), .5, 0);
        Assert.Equal(1.0 / 9, mild.Options[1].Probability, 12);
        Assert.Equal(4.0 / 9, mild.Options[0].Probability, 12);
        Assert.Equal(4.0 / 9, mild.Options[2].Probability, 12);
    }

    [Fact]
    public void AnArbitrarilySmallTemporaryReasonDoesNotCreateAnAbruptThirdOfTheAnswers()
    {
        foreach (double tiny in new[] { 1e-6, 1e-12, double.Epsilon })
        {
            CompanyAnswer answer = CompanyPolicy.Answer("episode", 10, "Bo", 0, new(tiny, 0, 0), .25);
            Assert.Equal(new[] { "yes", "not-now", "no" }, answer.Options.Select(option => option.Id));
            Assert.InRange(answer.Options[1].Probability, 0, tiny);
            Assert.InRange(Math.Abs(answer.Options[0].Probability - .5), 0, tiny);
            Assert.InRange(Math.Abs(answer.Options[2].Probability - .5), 0, tiny);
            Assert.Equal(1, answer.Options.Sum(option => option.Probability), 12);
        }
    }

    [Fact]
    public void RepeatingTheSameInputReturnsTheSameAnswerWithoutAnotherDraw()
    {
        var first = CompanyPolicy.Answer("episode", 100, "Bo", .15, new(.3, 0, 0), .42);
        var second = CompanyPolicy.Answer("episode", 100, "Bo", .15, new(.3, 0, 0), .42);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal(.42, first.Unit);
        Assert.Equal(.15, first.Margin);
    }

    [Fact]
    public void AnswerOptionsCannotBeMutatedThroughTheReceipt()
    {
        var answer = CompanyPolicy.Answer("episode", 1, "Bo", 0, new(.2, 0, 0), .5);
        Assert.Throws<NotSupportedException>(() => ((IList<CompanyAnswerOption>)answer.Options).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<CompanyAnswerOption>)answer.Options)[0] = new("other", "Other", 1));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonfiniteMarginIsRejected(double margin) =>
        Assert.Throws<ArgumentException>(() => CompanyPolicy.Answer("episode", 1, "Bo", margin, new(), .5));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-.01)]
    [InlineData(1)]
    public void InvalidUnitIsRejected(double unit) =>
        Assert.Throws<ArgumentException>(() => CompanyPolicy.Answer("episode", 1, "Bo", 0, new(), unit));

    [Fact]
    public void InvalidStateScaleTimeAndNamesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => CompanyPolicy.Answer("episode", 1, "Bo", 0, new(double.NaN, 0, 0), .5));
        Assert.Throws<ArgumentException>(() => CompanyPolicy.Answer("episode", 1, "Bo", 0, new(0, -1, 0), .5));
        Assert.Throws<ArgumentException>(() => CompanyPolicy.Answer("episode", 1, "Bo", 0, new(0, 0, 1.1), .5));
        Assert.Throws<ArgumentException>(() => CompanyPolicy.Answer("episode", 1, "Bo", 0, new(), .5, double.NaN));
        Assert.Throws<ArgumentException>(() => CompanyPolicy.Answer("episode", 1, "Bo", 0, new(), .5, 21));
        Assert.Throws<ArgumentException>(() => CompanyPolicy.Answer("episode", 1, "Bo", 0, new(), .5, -.01));
        Assert.Throws<ArgumentException>(() => CompanyPolicy.Answer("episode", -1, "Bo", 0, new(), .5));
        Assert.Throws<ArgumentException>(() => CompanyPolicy.Answer("", 1, "Bo", 0, new(), .5));
        Assert.Throws<ArgumentException>(() => CompanyPolicy.Answer("episode", 1, "broken\ud800", 0, new(), .5));
    }

    [Fact]
    public void SettingsDefaultsAndLimitsAreExplicit()
    {
        var options = new CompanyOptions();
        Assert.False(options.Enabled);
        Assert.True(options.Record);
        Assert.Equal(.1, options.SeekBase);
        Assert.Equal(.1, options.OfferBase);
        Assert.Equal(120, options.TravelMinutes);
        Assert.Equal(15, options.SearchMinutes);
        Assert.Equal(4320, options.SightAgeMinutes);
        Assert.Equal(3, options.MaxCandidates);
        Assert.Equal(5, options.AnswerScale);
        options.Validate();
        (options with { SeekBase = double.MaxValue, OfferBase = double.Epsilon, TravelMinutes = 1440,
            SearchMinutes = 1440, SightAgeMinutes = 4320, MaxCandidates = 1, AnswerScale = 20 }).Validate();
        (options with { TravelMinutes = 1, SearchMinutes = 1, SightAgeMinutes = 1, AnswerScale = 0 }).Validate();
    }

    [Fact]
    public void InvalidSettingsFailBeforeTheEpisodeStarts()
    {
        var options = new CompanyOptions();
        CompanyOptions[] invalid =
        {
            options with { SeekBase = 0 }, options with { SeekBase = double.PositiveInfinity },
            options with { OfferBase = -1 }, options with { OfferBase = double.NaN },
            options with { TravelMinutes = 0 }, options with { TravelMinutes = 1441 },
            options with { SearchMinutes = -1 }, options with { SearchMinutes = 1441 },
            options with { SightAgeMinutes = 0 }, options with { SightAgeMinutes = 4321 },
            options with { MaxCandidates = 0 }, options with { MaxCandidates = 4 },
            options with { AnswerScale = -1 }, options with { AnswerScale = 21 }, options with { AnswerScale = double.NaN },
        };
        Assert.All(invalid, value => Assert.Throws<ArgumentException>(value.Validate));
    }

    [Fact]
    public void AnswerAvailabilityActivityAndWhereaboutsUseDifferentExpectationKeys()
    {
        Assert.Equal(new(ExpectationKind.Answers, "Bo", "company:PlayedGame"), CompanyPolicy.AnswerKey("Bo"));
        Assert.Equal(new(ExpectationKind.Availability, "Bo", "company:PlayedGame"), CompanyPolicy.AvailabilityKey("Bo"));
        Assert.Equal(new(ExpectationKind.Activity, "Bo", "PlayedGame:Hall"), CompanyPolicy.ActivityKey("Bo", "Hall"));
        Assert.Equal(new(ExpectationKind.Whereabouts, "Bo", "Hall:18-21:weekday"), CompanyPolicy.WhereaboutsKey("Bo", "Hall", 18 * 60));
        Assert.NotEqual(CompanyPolicy.AnswerKey("Bo"), CompanyPolicy.AvailabilityKey("Bo"));
        Assert.NotEqual(CompanyPolicy.ActivityKey("Bo", "Hall"), CompanyPolicy.ActivityKey("Bo", "Yard"));
    }

    [Theory]
    [InlineData(0, "Hall:0-3:weekday")]
    [InlineData(179, "Hall:0-3:weekday")]
    [InlineData(180, "Hall:3-6:weekday")]
    [InlineData(1439, "Hall:21-24:weekday")]
    [InlineData(1440, "Hall:0-3:weekday")]
    [InlineData(7200, "Hall:0-3:weekend")]
    [InlineData(10080, "Hall:0-3:weekday")]
    public void WhereaboutsBucketsFollowTheSimulationClock(int minute, string expected) =>
        Assert.Equal(expected, CompanyPolicy.WhereaboutsKey("Bo", "Hall", minute).Context);

    [Fact]
    public void KeysRejectInvalidNamesAndTimeAndDoNotDependOnCurrentCulture()
    {
        Assert.Throws<ArgumentException>(() => CompanyPolicy.AnswerKey(""));
        Assert.Throws<ArgumentException>(() => CompanyPolicy.AvailabilityKey("bad\nname"));
        Assert.Throws<ArgumentException>(() => CompanyPolicy.ActivityKey("Bo", ""));
        Assert.Throws<ArgumentException>(() => CompanyPolicy.ActivityKey("Bo", new string('x', 200)));
        Assert.Throws<ArgumentException>(() => CompanyPolicy.WhereaboutsKey("Bo", new string('x', 200), 0));
        Assert.Throws<ArgumentException>(() => CompanyPolicy.WhereaboutsKey("Bo", "Hall", -1));
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Assert.Equal("Hall:18-21:weekday", CompanyPolicy.WhereaboutsKey("Bo", "Hall", 18 * 60).Context);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }
}
