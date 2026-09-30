using NpcIntents;
using NpcMemory;
using Xunit;

namespace NpcIntents.Tests;

/// <summary>
/// Tests for the templated line renderer: first-person phrasing for "Saw" entries (second person
/// for the player), the neutral fallback kind, omission of a missing location, determinism, and
/// compulsory sanitization of dialogue-command characters.
/// </summary>
public class LineRendererTests
{
    private readonly LineRenderer _renderer = new();

    private static DiaryEntry E(string subject, string kind, string? detail = null)
        => new(120, subject, kind, detail);

    // ---------------------------------------------------------------- Saw + Player

    [Fact]
    public void Saw_PlayerSubject_RendersYouAndTheLocation()
    {
        string line = _renderer.Render("Abigail", "spirited and playful", E("Player", "Saw", "the beach"));

        Assert.Equal("I saw you at the beach yesterday.", line);
    }

    [Theory]
    [InlineData("Player")]
    [InlineData("player")]
    [InlineData("PLAYER")]
    [InlineData("pLaYeR")]
    public void Saw_PlayerSubject_IsCaseInsensitive(string subject)
    {
        string line = _renderer.Render("Abigail", "spirited and playful", E(subject, "Saw", "the beach"));

        Assert.Equal("I saw you at the beach yesterday.", line);
        Assert.DoesNotContain("Player", line);
    }

    [Theory]
    [InlineData("Saw")]
    [InlineData("saw")]
    [InlineData("SAW")]
    public void Saw_Kind_IsCaseInsensitive(string kind)
    {
        string line = _renderer.Render("Abigail", "spirited and playful", E("Player", kind, "the beach"));

        Assert.Equal("I saw you at the beach yesterday.", line);
    }

    // ---------------------------------------------------------------- Saw + other subject

    [Fact]
    public void Saw_OtherSubject_RendersTheSubjectNameAndLocation()
    {
        string line = _renderer.Render("Abigail", "spirited and playful", E("Pierre", "Saw", "the general store"));

        Assert.Equal("I saw Pierre at the general store yesterday.", line);
        Assert.DoesNotContain("you", line);
    }

    [Fact]
    public void Saw_OtherSubject_KeepsTheSubjectSpellingVerbatim()
    {
        // Any subject that merely contains "Player" is still a third party, not the player.
        string line = _renderer.Render("Abigail", "spirited", E("PlayerTwo", "Saw", "the farm"));

        Assert.Equal("I saw PlayerTwo at the farm yesterday.", line);
    }

    // ---------------------------------------------------------------- Fallback kind

    [Fact]
    public void NonSawKind_FallsBackToThinkingAboutTheSubject()
    {
        string line = _renderer.Render("Abigail", "spirited and playful", E("Sebastian", "SpokeWith", "the mines"));

        Assert.Equal("I've been thinking about Sebastian.", line);
        Assert.False(string.IsNullOrWhiteSpace(line));
    }

    [Fact]
    public void FallbackKind_AddressesThePlayerAsYou()
    {
        // NPC diaries now hold entries about the player of any kind (the ladder writes some), so the
        // fallback must never say "Player" out loud.
        string line = _renderer.Render("Abigail", "spirited", E("Player", "ReceivedGift", "quartz"));

        Assert.Equal("I've been thinking about you.", line);
    }

    [Theory]
    [InlineData(1, "I tried to get your attention yesterday. You must have been busy.")]
    [InlineData(3, "I tried to get your attention the other day. You must have been busy.")]
    public void IgnoredBy_ThePlayer_SaysSoWithTheRightDay(int daysAgo, string expected)
    {
        ILineRenderer renderer = _renderer;

        Assert.Equal(expected, renderer.Render("Abigail", "spirited", E("Player", "IgnoredBy", "Emote"), daysAgo));
    }

    [Fact]
    public void FallbackKind_WithNullDetail_StillRenders()
    {
        string line = _renderer.Render("Abigail", "spirited", E("Sebastian", "SpokeWith"));

        Assert.Equal("I've been thinking about Sebastian.", line);
    }

    // ---------------------------------------------------------------- Missing detail

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Saw_WithoutLocation_OmitsTheAtClause(string? detail)
    {
        Assert.Equal("I saw you yesterday.", _renderer.Render("Abigail", "spirited", E("Player", "Saw", detail)));
        Assert.Equal("I saw Pierre yesterday.", _renderer.Render("Abigail", "spirited", E("Pierre", "Saw", detail)));
    }

    // ---------------------------------------------------------------- Sanitization

    [Fact]
    public void Render_StripsDialogueCommandCharacters_FromThisIsDetail()
    {
        string line = _renderer.Render("Abigail", "spirited", E("Player", "Saw", "a#b $c %d {e [f"));

        Assert.DoesNotContain("#", line);
        Assert.DoesNotContain("$", line);
        Assert.DoesNotContain("%", line);
        Assert.DoesNotContain("{", line);
        Assert.DoesNotContain("[", line);
        // Sanitize deletes the characters outright (no space inserted): "a#b $c %d {e [f" -> "ab c d e f".
        Assert.Equal("I saw you at ab c d e f yesterday.", line);
    }

    [Fact]
    public void Render_StripsDialogueCommandCharacters_FromTheFallbackSubject()
    {
        string line = _renderer.Render("Abigail", "spirited", E("Sea#bas$t%i{a[n", "SpokeWith"));

        Assert.Equal("I've been thinking about Seabastian.", line);
    }

    [Fact]
    public void Render_AlwaysSanitizes_EvenWhenNothingIsStripped()
    {
        // Sanitizing a clean line is a no-op; the rendered text is unchanged.
        const string clean = "I saw you at the beach yesterday.";

        Assert.Equal(clean, LineSanitizer.Sanitize(clean));
        Assert.Equal(clean, _renderer.Render("Abigail", "spirited", E("Player", "Saw", "the beach")));
    }

    // ---------------------------------------------------------------- Determinism

    [Fact]
    public void SameInput_ProducesSameOutput()
    {
        var entry = E("Player", "Saw", "the beach");
        string first = _renderer.Render("Abigail", "spirited and playful", entry);

        for (int i = 0; i < 10; i++)
            Assert.Equal(first, _renderer.Render("Abigail", "spirited and playful", entry));
    }

    [Fact]
    public void EqualButDistinctEntries_ProduceTheSameOutput()
    {
        string a = _renderer.Render("Abigail", "spirited", E("Pierre", "Saw", "the beach"));
        string b = _renderer.Render("Abigail", "spirited", E("Pierre", "Saw", "the beach"));

        Assert.Equal(a, b);
    }

    [Fact]
    public void SpeakerAndVoice_DoNotAffectTheCurrentTemplates()
    {
        var entry = E("Player", "Saw", "the beach");

        string line = _renderer.Render("Abigail", "spirited and playful", entry);

        Assert.Equal(line, _renderer.Render("Shane", "gruff and guarded", entry));
        Assert.Equal(line, _renderer.Render("Abigail", "a completely different voice", entry));
    }

    // ---------------------------------------------------------------- Enriched kinds (PR #5)

    [Fact]
    public void Talked_WithThePlayer_SaysSoWithTheRightDay()
    {
        Assert.Equal("It was nice talking with you yesterday.",
            _renderer.Render("Abigail", "spirited", E("Player", "Talked")));
        Assert.Equal("It was nice talking with you earlier today.",
            _renderer.Render("Abigail", "spirited", E("Player", "Talked"), daysAgo: 0));
    }

    [Fact]
    public void PassedBy_ThePlayer_SaysSoWithTheRightDay()
    {
        Assert.Equal("You walked right past me yesterday.",
            _renderer.Render("Abigail", "spirited", E("Player", "PassedBy")));
        Assert.Equal("You walked right past me the other day.",
            _renderer.Render("Abigail", "spirited", E("Player", "PassedBy"), daysAgo: 3));
    }

    [Fact]
    public void BirthdayForgotten_ThePlayer_SaysSoWithTheRightDay()
    {
        Assert.Equal("My birthday was yesterday, you know.",
            _renderer.Render("Abigail", "spirited", E("Player", "BirthdayForgotten")));
        Assert.Equal("My birthday was a while back, you know.",
            _renderer.Render("Abigail", "spirited", E("Player", "BirthdayForgotten"), daysAgo: 9));
    }

    [Theory]
    [InlineData("Talked")]
    [InlineData("PassedBy")]
    [InlineData("BirthdayForgotten")]
    public void EnrichedKinds_AboutAnotherSubject_FallBack(string kind)
    {
        string line = _renderer.Render("Abigail", "spirited", E("Pierre", kind));

        Assert.Equal("I've been thinking about Pierre.", line);
    }

    [Fact]
    public void EnrichedKinds_AreCaseInsensitive()
    {
        Assert.Equal("It was nice talking with you yesterday.",
            _renderer.Render("Abigail", "spirited", E("Player", "tAlKeD")));
        Assert.Equal("You walked right past me yesterday.",
            _renderer.Render("Abigail", "spirited", E("Player", "passedby")));
        Assert.Equal("My birthday was yesterday, you know.",
            _renderer.Render("Abigail", "spirited", E("Player", "BIRTHDAYFORGOTTEN")));
    }

    // ---------------------------------------------------------------- Interface

    [Fact]
    public void Render_IsReachableThroughTheInterface()
    {
        ILineRenderer renderer = new LineRenderer();

        Assert.Equal("I saw you at the beach yesterday.", renderer.Render("Abigail", "spirited", E("Player", "Saw", "the beach")));
    }
}
