using System.Text.RegularExpressions;

namespace NpcTemperament;

/// <summary>
/// Counted signals from one character's dialogue. Every rate is "share of pages that ...", so
/// characters with many lines and few lines compare fairly. See docs/spec/temperament.md.
/// </summary>
public sealed record DialogueFeatures(
    int Pages,
    double Happy,
    double Sad,
    double Angry,
    double Love,
    double Question,
    double Exclaim,
    double Trailing,
    double WordsPerPage,
    double Thanks,
    double Sorry,
    double Welcome,
    double Dismiss,
    double Gossip)
{
    private static readonly Regex ThanksWords = Words("thank", "thanks", "appreciate", "grateful");
    private static readonly Regex SorryWords = Words("sorry", "apologize", "apologise", "forgive", "my fault");
    private static readonly Regex WelcomeWords = Words(
        "good to see you", "nice to see you", "glad to see you", "great to see you", "missed you",
        "miss you", "come by", "stop by", "drop by", "come visit", "glad you", "happy to see");
    private static readonly Regex DismissWords = Words(
        "leave me alone", "go away", "whatever", "don't bother", "not interested", "get lost",
        "none of your business", "what do you want", "why are you talking", "i'm busy");
    private static readonly Regex GossipWords = Words(
        "heard", "rumor", "rumour", "gossip", "did you know", "apparently", "people say", "they say");

    /// <summary>Counts the features over a character's pages. <paramref name="others"/> are the
    /// other characters' names: mentioning one counts as talking about people (gossip).</summary>
    public static DialogueFeatures Compute(IEnumerable<DialoguePage> pages, IEnumerable<string> others)
    {
        var list = pages.ToList();
        Regex? names = others.Any() ? Words(RegexOptions.None, others.OrderBy(n => n, StringComparer.Ordinal).ToArray()) : null;
        int n = list.Count;
        if (n == 0)
            return new DialogueFeatures(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        double Rate(Func<DialoguePage, bool> test) => (double)list.Count(test) / n;
        return new DialogueFeatures(
            Pages: n,
            Happy: Rate(p => p.Mood == Mood.Happy),
            Sad: Rate(p => p.Mood == Mood.Sad),
            Angry: Rate(p => p.Mood == Mood.Angry),
            Love: Rate(p => p.Mood == Mood.Love),
            Question: Rate(p => p.Text.Contains('?')),
            Exclaim: Rate(p => p.Text.Contains('!')),
            Trailing: Rate(p => p.Text.Contains("...")),
            WordsPerPage: list.Average(p => (double)p.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length),
            Thanks: Rate(p => ThanksWords.IsMatch(p.Text)),
            Sorry: Rate(p => SorryWords.IsMatch(p.Text)),
            Welcome: Rate(p => WelcomeWords.IsMatch(p.Text)),
            Dismiss: Rate(p => DismissWords.IsMatch(p.Text)),
            Gossip: Rate(p => GossipWords.IsMatch(p.Text) || (names != null && names.IsMatch(p.Text))));
    }

    private static Regex Words(params string[] phrases) => Words(RegexOptions.IgnoreCase, phrases);

    // Names match case-sensitively, so "a sandy beach" is not Sandy.
    private static Regex Words(RegexOptions options, params string[] phrases)
        => new(@"\b(" + string.Join("|", phrases.Select(Regex.Escape)) + @")\b",
            options | RegexOptions.CultureInvariant);
}
