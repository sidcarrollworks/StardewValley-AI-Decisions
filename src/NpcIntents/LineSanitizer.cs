namespace NpcIntents;

/// <summary>
/// Strips characters that Stardew dialogue treats as commands or substitutions (`#`, `$`, `%`,
/// `{`, `[`). Generated text must never reach a dialogue string un-sanitized — these characters
/// can run trigger actions or grant items.
/// </summary>
public static class LineSanitizer
{
    public static string Sanitize(string text)
        => text
            .Replace("#", "")
            .Replace("$", "")
            .Replace("%", "")
            .Replace("{", "")
            .Replace("[", "");
}
