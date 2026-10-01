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
    double Gossip,
    double HappyWords = 0,
    double SadWords = 0,
    double AngerWords = 0,
    double FearWords = 0,
    double DisgustWords = 0,
    double SurpriseWords = 0)
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

    // Emotion words (Ekman's six). Fear, disgust and surprise have no portrait code in the game,
    // so their words are their only signal (docs/spec/temperament.md, "Emotion biases").
    private static readonly Regex HappyWordList = Words(
        "glad", "happy", "wonderful", "great", "fun", "haha", "yay", "excited", "delighted", "lovely");
    private static readonly Regex SadWordList = Words(
        "sad", "lonely", "cry", "crying", "depressed", "sigh", "alone", "tears", "miserable", "heartbroken");
    private static readonly Regex AngerWordList = Words(
        "angry", "mad", "annoying", "annoyed", "furious", "hate", "stupid", "ugh", "idiot", "shut up", "irritating");
    private static readonly Regex FearWordList = Words(
        "scared", "afraid", "nervous", "worried", "worry", "frightened", "terrified", "anxious");
    private static readonly Regex DisgustWordList = Words(
        "gross", "disgusting", "eww", "ew", "yuck", "nasty", "revolting", "stinks", "smelly", "filthy");
    private static readonly Regex SurpriseWordList = Words(
        "wow", "whoa", "oh my", "no way", "can't believe", "surprised", "unexpected", "what a surprise");

    /// <summary>Counts the features over a character's pages. <paramref name="others"/> are the
    /// other characters' names: mentioning one counts as talking about people (gossip).</summary>
    public static DialogueFeatures Compute(IEnumerable<DialoguePage> pages, IEnumerable<string> others)
    {
        var list = pages.ToList();
        Regex? names = others.Any() ? Words(RegexOptions.None, others.OrderBy(n => n, StringComparer.Ordinal).ToArray()) : null;
        int n = list.Count;
        if (n == 0)
            return new DialogueFeatures(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        bool Has(Regex words, DialoguePage p) => words.IsMatch(p.Text);

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
            Gossip: Rate(p => GossipWords.IsMatch(p.Text) || (names != null && names.IsMatch(p.Text))),
            HappyWords: Rate(p => Has(HappyWordList, p)),
            SadWords: Rate(p => Has(SadWordList, p)),
            AngerWords: Rate(p => Has(AngerWordList, p)),
            FearWords: Rate(p => Has(FearWordList, p)),
            DisgustWords: Rate(p => Has(DisgustWordList, p)),
            SurpriseWords: Rate(p => Has(SurpriseWordList, p)));
    }

    private static Regex Words(params string[] phrases) => Words(RegexOptions.IgnoreCase, phrases);

    // Names match case-sensitively, so "a sandy beach" is not Sandy.
    private static Regex Words(RegexOptions options, params string[] phrases)
        => new(@"\b(" + string.Join("|", phrases.Select(Regex.Escape)) + @")\b",
            options | RegexOptions.CultureInvariant);
}
