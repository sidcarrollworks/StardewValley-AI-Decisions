using System.Text.RegularExpressions;

namespace NpcTemperament;

/// <summary>The mood a page's portrait code shows (Characters/Dialogue <c>$h</c>, <c>$s</c>, ...).</summary>
public enum Mood { None, Happy, Sad, Unique, Love, Angry }

/// <summary>One page of a character's dialogue: the words the player reads, and the portrait mood.</summary>
public sealed record DialoguePage(string Text, Mood Mood);

/// <summary>
/// Splits a raw Characters/Dialogue value into readable pages. Pure and deterministic.
/// Format (stardew-source-notes.md, "Dialogue"): <c>#</c> separates segments, <c>$b</c> and
/// <c>$e</c> break pages, <c>$h $s $u $l $a</c> or <c>$0..$5</c> set the portrait, <c>@</c> is
/// the player's name, <c>^</c> separates the male and female variants of a line.
/// </summary>
public static class DialogueText
{
    // Commands whose segment holds no line the character says (the player's answers, branches).
    // $q, $r and $y are confirmed in the 1.6.15 decompile (Dialogue.parseDialogueString: $r takes the
    // NEXT segment as the player's answer). $p/$d/$c/$k/$t/$v are the same switch, verified as the
    // dialoguePrerequisite/dialogueDependingOnWorldState/dialogueChance/dialogueKill/
    // dialogueStartConversationTopic/dialogueEvent constants (Dialogue.cs:41-68); skipping the whole
    // segment is safe (we only lose a few lines).
    private static readonly Regex CommandSegment = new(@"^\s*\$(q|r|p|d|y|c|k|t|v|action|query)\b", RegexOptions.CultureInvariant);
    private static readonly Regex Portrait = new(@"\$(h|s|u|l|a|neutral|\d+)(?![A-Za-z])", RegexOptions.CultureInvariant);
    private static readonly Regex Tokens = new(@"\$[a-z]\b|%\w+|\[[^\]]*\]|\{[^}]*\}|\*[^*]*\*", RegexOptions.CultureInvariant);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.CultureInvariant);

    public static IReadOnlyList<DialoguePage> Pages(string raw)
    {
        var pages = new List<DialoguePage>();
        if (string.IsNullOrWhiteSpace(raw))
            return pages;

        bool playerAnswer = false;
        foreach (string segment in raw.Split('#'))
        {
            // the segment after "$r id points reply" is the player's answer, not the character's line
            if (playerAnswer)
            {
                playerAnswer = false;
                continue;
            }
            if (CommandSegment.IsMatch(segment))
            {
                playerAnswer = segment.TrimStart().StartsWith("$r ", StringComparison.Ordinal);
                continue;
            }
            string s = segment;
            int gender = s.IndexOf('^');
            if (gender >= 0)
                s = s[..gender];

            Mood mood = Mood.None;
            foreach (Match m in Portrait.Matches(s))
                mood = MoodOf(m.Groups[1].Value);
            s = Portrait.Replace(s, " ");
            s = Tokens.Replace(s, " ").Replace("@", "you");
            s = Spaces.Replace(s, " ").Trim();
            if (s.Any(char.IsLetter))
                pages.Add(new DialoguePage(s, mood));
        }
        return pages;
    }

    private static Mood MoodOf(string code) => code switch
    {
        "h" or "1" => Mood.Happy,
        "s" or "2" => Mood.Sad,
        "u" or "3" => Mood.Unique,
        "l" or "4" => Mood.Love,
        "a" or "5" => Mood.Angry,
        _ => Mood.None,
    };
}
