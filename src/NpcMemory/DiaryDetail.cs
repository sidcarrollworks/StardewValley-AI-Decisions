namespace NpcMemory;

/// <summary>
/// Key-value Detail strings for diary kinds (docs/spec/diary.md): `key=value;key=value`.
/// Kept small so <see cref="DiaryEntry"/> never grows. Parsing is lenient: unknown or malformed
/// pairs are ignored, so a plain Detail (like a Saw location) parses to an empty map. Formatting
/// strips the delimiters and the dialogue-command characters that NpcIntents.LineSanitizer
/// removes (`# $ % { [`), because NpcMemory cannot reference NpcIntents.
/// </summary>
public static class DiaryDetail
{
    public static IReadOnlyDictionary<string, string> Parse(string? detail)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(detail))
            return map;

        foreach (string pair in detail.Split(';'))
        {
            int eq = pair.IndexOf('=');
            if (eq <= 0)
                continue; // no '=' or an empty key
            string key = pair[..eq].Trim();
            string value = pair[(eq + 1)..].Trim();
            if (key.Length == 0 || value.Length == 0)
                continue;
            map[key] = value;
        }
        return map;
    }

    public static string Format(params (string Key, string Value)[] pairs)
    {
        var parts = new List<string>(pairs.Length);
        foreach ((string key, string value) in pairs)
        {
            string cleanKey = Sanitize(key);
            if (cleanKey.Length == 0)
                continue; // an empty key would parse back to nothing; drop it
            parts.Add(cleanKey + "=" + Sanitize(value));
        }
        return string.Join(";", parts);
    }

    private static string Sanitize(string s)
        => s
            .Replace(";", "")
            .Replace("=", "")
            .Replace("#", "")
            .Replace("$", "")
            .Replace("%", "")
            .Replace("{", "")
            .Replace("[", "");
}
