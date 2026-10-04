using System.Globalization;

namespace NpcMemory;

/// <summary>
/// Gossip juiciness (docs/spec/ledger-gossip.md, "Juiciness"; D25): how gossip-worthy a story
/// still is for a teller, whether it is worth volunteering to a listener, and what the listener
/// gets. Pure functions of diary entries; the base juiciness per kind comes from the caller (the
/// stressor table in <c>NpcMotives</c>, so feeling and gossip value are set in one place).
/// </summary>
/// <remarks>
/// A story is a diary entry the teller has: their own (first-hand) or a <c>Heard</c>. A Heard's
/// detail carries the original kind and keys plus <c>from</c> (who told it), <c>of</c> (whose
/// diary it started in), <c>b</c> (base juiciness), <c>j</c> (juiciness when told), <c>at</c>
/// (the tick it was told) and <c>hops</c>. Heards written before juiciness lack these; they read
/// as one hop from their teller, who was the owner.
/// </remarks>
public static class Gossip
{
    public const string HeardKind = "Heard";

    /// <summary>How juicy the story is for its teller now, or null when it isn't gossip at all
    /// (no base juiciness: a plain <c>Saw</c>, <c>TriedToReach</c>...). Fades by whole days since
    /// the teller got it, so a story keeps its juiciness through the day it happened.</summary>
    public static double? Current(DiaryEntry story, int nowTick, Func<DiaryEntry, double?> baseOf, ChatOptions o)
    {
        double start, baseJ;
        int since;
        if (IsHeard(story))
        {
            IReadOnlyDictionary<string, string> d = DiaryDetail.Parse(story.Detail);
            if (BaseOf(story, baseOf) is not { } b0)
                return null;
            double? b = b0;
            baseJ = b.Value;
            start = Number(d, "j") ?? b.Value * o.RetellFactor; // pre-juiciness Heards: one retelling
            since = Int(d, "at") ?? story.AbsoluteTick;
        }
        else
        {
            if (baseOf(story) is not { } b)
                return null;
            baseJ = start = b;
            since = story.AbsoluteTick;
        }

        int days = Math.Max(0, GameClock.DayIndex(nowTick) - GameClock.DayIndex(since));
        double fade = baseJ >= o.ScandalBase ? o.ScandalFadePerDay : o.FadePerDay;
        return Math.Max(0, start - fade * days);
    }

    /// <summary>The story's base juiciness: the original kind's, kept in a Heard's <c>b</c> key.</summary>
    public static double? BaseOf(DiaryEntry story, Func<DiaryEntry, double?> baseOf)
    {
        if (!IsHeard(story))
            return baseOf(story);
        if (Original(story) is not { } original || IsHeard(original))
            return null;
        return Number(DiaryDetail.Parse(story.Detail), "b") ?? baseOf(original);
    }

    /// <summary>The original event behind a story: the entry itself, or for a Heard the original
    /// kind with its own keys (the Heard's bookkeeping keys dropped).</summary>
    public static DiaryEntry? Original(DiaryEntry story)
    {
        if (!IsHeard(story))
            return story;
        IReadOnlyDictionary<string, string> d = DiaryDetail.Parse(story.Detail);
        if (!d.TryGetValue("kind", out string? kind) || string.IsNullOrEmpty(kind))
            return null;
        var keys = DiaryDetail.Parse(story.Detail)
            .Where(p => !BookKeys.Contains(p.Key))
            .Select(p => (p.Key, p.Value))
            .ToArray();
        return new DiaryEntry(story.AbsoluteTick, story.Subject, kind, keys.Length == 0 ? null : DiaryDetail.Format(keys));
    }

    /// <summary>Whose diary the story started in: the teller for their own entry; for a Heard the
    /// <c>of</c> key, or its teller for a Heard from before juiciness (always one hop).</summary>
    public static string OwnerOf(DiaryEntry story, string teller)
    {
        if (!IsHeard(story))
            return teller;
        IReadOnlyDictionary<string, string> d = DiaryDetail.Parse(story.Detail);
        if (d.TryGetValue("of", out string? of) && !string.IsNullOrEmpty(of))
            return of;
        return d.TryGetValue("from", out string? from) && !string.IsNullOrEmpty(from) ? from : teller;
    }

    /// <summary>How many tellings the story is from its owner (0 for the owner's own entry).</summary>
    public static int HopsOf(DiaryEntry story)
    {
        if (!IsHeard(story))
            return 0;
        return Int(DiaryDetail.Parse(story.Detail), "hops") ?? 1;
    }

    /// <summary>Everyone in the story: its subject, the person it started with, and a giver
    /// named in the detail. A listener in the story already knows it.</summary>
    public static IReadOnlyList<string> PeopleIn(DiaryEntry story, string teller)
    {
        var people = new List<string>();
        void add(string? who)
        {
            if (!string.IsNullOrEmpty(who) && !people.Contains(who, StringComparer.OrdinalIgnoreCase))
                people.Add(who);
        }
        add(story.Subject);
        add(OwnerOf(story, teller));
        if (Original(story) is { } original && DiaryDetail.Parse(original.Detail).TryGetValue("giver", out string? giver))
            add(giver);
        return people;
    }

    /// <summary>The same event, whatever the route: original tick, kind, subject and owner.</summary>
    public static string EventKey(DiaryEntry story, string teller)
    {
        string kind = Original(story)?.Kind ?? story.Kind;
        return $"{story.AbsoluteTick}|{kind}|{story.Subject}|{OwnerOf(story, teller)}".ToLowerInvariant();
    }

    /// <summary>
    /// The Heard entry a listener gets when <paramref name="teller"/> tells
    /// <paramref name="story"/> at <paramref name="nowTick"/>, with juiciness
    /// <paramref name="tellerJuiciness"/> x <see cref="ChatOptions.RetellFactor"/>.
    /// </summary>
    public static DiaryEntry Retold(DiaryEntry story, string teller, double tellerJuiciness, double baseJuiciness,
        int nowTick, ChatOptions o)
    {
        DiaryEntry original = Original(story) ?? story;
        var pairs = new List<(string Key, string Value)>
        {
            ("from", teller),
            ("kind", original.Kind),
            ("subject", original.Subject),
            ("of", OwnerOf(story, teller)),
            ("b", Format(baseJuiciness)),
            ("j", Format(tellerJuiciness * o.RetellFactor)),
            ("at", nowTick.ToString(CultureInfo.InvariantCulture)),
            ("hops", (HopsOf(story) + 1).ToString(CultureInfo.InvariantCulture)),
        };
        foreach ((string k, string v) in DiaryDetail.Parse(original.Detail))
            if (!BookKeys.Contains(k))
                pairs.Add((k, v));
        return new DiaryEntry(original.AbsoluteTick, original.Subject, HeardKind, DiaryDetail.Format(pairs.ToArray()));
    }

    public static bool IsHeard(DiaryEntry e) => string.Equals(e.Kind, HeardKind, StringComparison.OrdinalIgnoreCase);

    /// <summary>The Heard keys that are bookkeeping, not part of the original event.</summary>
    private static readonly HashSet<string> BookKeys = new(StringComparer.OrdinalIgnoreCase)
        { "from", "kind", "subject", "of", "b", "j", "at", "hops" };

    private static string Format(double v) => Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture);

    private static double? Number(IReadOnlyDictionary<string, string> d, string key)
        => d.TryGetValue(key, out string? s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : null;

    private static int? Int(IReadOnlyDictionary<string, string> d, string key)
        => d.TryGetValue(key, out string? s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : null;
}
