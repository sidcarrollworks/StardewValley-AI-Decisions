using NpcMemory;
using NpcTemperament;

namespace NpcMotives;

/// <summary>
/// The game thread's owner of regard (docs/spec/motives.md, "Regard"). The mod sets
/// <see cref="OnNoted"/> as <see cref="MemoryStore.Noting"/>, so each new diary entry leaves its
/// lasting mark exactly once, when it is written; it runs the 6:00 drift; and it reports what each
/// entry did, for the playtest log's <c>stress</c> and <c>regard</c> records. Hearsay told by the
/// person it happened to is confirmed at once (ledger-gossip.md, "from the source"); hearsay from
/// a witness stays elastic. Game thread only; the worker sees regard as copied numbers.
/// </summary>
public sealed class RegardKeeper
{
    private readonly Func<string, Temperament> _temperamentOf;
    private readonly MotiveOptions _o;

    public RegardKeeper(RegardBook book, Func<string, Temperament> temperamentOf, MotiveOptions? options = null)
    {
        Book = book ?? throw new ArgumentNullException(nameof(book));
        _temperamentOf = temperamentOf ?? throw new ArgumentNullException(nameof(temperamentOf));
        _o = options ?? new MotiveOptions();
    }

    public RegardBook Book { get; }

    /// <summary>
    /// What one new entry does: its stress on <paramref name="npc"/> and the lasting mark it
    /// leaves in regard. Null for entries that stir nothing (a plain <c>Saw</c>,
    /// <c>TriedToReach</c>, a neutral gift). <paramref name="diaryBefore"/> is the diary without
    /// the entry (the hook's third argument).
    /// </summary>
    public RegardNote? OnNoted(string npc, DiaryEntry entry, IReadOnlyList<DiaryEntry> diaryBefore)
    {
        Temperament t = _temperamentOf(npc);
        if (entry.Kind == "Heard")
            return OnHeard(npc, entry, t);

        StressorProfile? p = StressorTable.Of(entry);
        if (p is null)
            return ConfirmOnly(npc, entry, diaryBefore, t);
        double magnitude = p.Magnitude * Stresses.SensitivityFactor(t);
        double share = Stresses.PlasticShare(entry, p, diaryBefore, _o);
        bool severe = magnitude >= _o.SevereMagnitude;
        double retention = severe ? 1.0 : 0.5 + _o.RetentionOf(npc);
        string subject = StressorTable.TargetOf(entry);
        double before = Book.Of(npc, subject);
        Book.Apply(npc, entry, diaryBefore, t, _o);
        string cause = Cause(entry);
        // Seeing it first-hand confirms what the villager only heard (ledger-gossip.md).
        foreach (DiaryEntry heard in ConfirmedBy(entry, diaryBefore, _o))
        {
            Book.ApplyConfirmed(npc, heard, 1, t, _o);
            string teller = DiaryDetail.Parse(heard.Detail).TryGetValue("from", out string? f) ? f : "someone";
            cause += $"; saw for themselves what {teller} told them";
        }
        return new RegardNote(npc, subject, entry.Kind, magnitude, share, retention, severe,
            YieldCrossed: p.Yields && share > 0, before, Book.Of(npc, subject), cause);
    }

    /// <summary>An entry that stirs nothing itself (a gift seen) can still confirm hearsay.</summary>
    private RegardNote? ConfirmOnly(string npc, DiaryEntry entry, IReadOnlyList<DiaryEntry> diaryBefore, Temperament t)
    {
        IReadOnlyList<DiaryEntry> confirmed = ConfirmedBy(entry, diaryBefore, _o);
        if (confirmed.Count == 0)
            return null;
        string subject = confirmed[0].Subject;
        double before = Book.Of(npc, subject);
        var tellers = new List<string>();
        foreach (DiaryEntry heard in confirmed)
        {
            Book.ApplyConfirmed(npc, heard, 1, t, _o);
            tellers.Add(DiaryDetail.Parse(heard.Detail).TryGetValue("from", out string? f) ? f : "someone");
        }
        return new RegardNote(npc, subject, entry.Kind, 0, 0, 1, false, false, before, Book.Of(npc, subject),
            $"{Cause(entry)}; saw for themselves what {string.Join(" and ", tellers)} told them");
    }

    /// <summary>
    /// The hearsay a first-hand entry confirms (ledger-gossip.md, "first-hand"): unconfirmed
    /// <c>Heard</c> stories in the diary, heard within <see cref="MotiveOptions.ConfirmWindowDays"/>
    /// before the entry, of the same act by the same person (<see cref="ActOf"/>), that no earlier
    /// first-hand entry has confirmed already. Pure: read from the diary, so nothing is saved.
    /// </summary>
    public static IReadOnlyList<DiaryEntry> ConfirmedBy(DiaryEntry firstHand, IReadOnlyList<DiaryEntry> diaryBefore, MotiveOptions o)
    {
        if (Gossip.IsHeard(firstHand) || ActOf(firstHand) is not { } act)
            return Array.Empty<DiaryEntry>();
        int since = firstHand.AbsoluteTick - o.ConfirmWindowDays * GameClock.TicksPerDay;
        var confirmed = new List<DiaryEntry>();
        foreach (DiaryEntry heard in diaryBefore)
        {
            if (!Gossip.IsHeard(heard))
                continue;
            int at = Gossip.HeardAt(heard);
            if (at < since || at > firstHand.AbsoluteTick)
                continue;
            if (Gossip.Original(heard) is not { } original || ActOf(original) != act)
                continue;
            IReadOnlyDictionary<string, string> d = DiaryDetail.Parse(heard.Detail);
            string teller = d.TryGetValue("from", out string? f) ? f : "";
            if (FromSource(original.Kind, teller, heard.Subject, Gossip.OwnerOf(heard, teller)))
                continue; // already lasting when it was heard
            bool seenBefore = diaryBefore.Any(e => !Gossip.IsHeard(e) && e.AbsoluteTick >= at
                && e.AbsoluteTick <= firstHand.AbsoluteTick && !ReferenceEquals(e, firstHand) && ActOf(e) == act);
            if (!seenBefore)
                confirmed.Add(heard);
        }
        return confirmed;
    }

    /// <summary>"The same kind of act by the same person": a gift the player gave (received or
    /// seen) by whether it pleased; any other kind by its kind and who it is about. Null for
    /// entries that are no act (a plain <c>Saw</c>).</summary>
    public static string? ActOf(DiaryEntry e)
    {
        IReadOnlyDictionary<string, string> d = DiaryDetail.Parse(e.Detail);
        string taste = d.TryGetValue("taste", out string? t) ? t : "";
        string pleased = taste is "Love" or "Like" ? "pleased" : taste is "Dislike" or "Hate" ? "displeased" : "neutral";
        switch (e.Kind)
        {
            case "GiftReceived":
                return $"gift|{e.Subject}|{pleased}".ToLowerInvariant();
            case "SawGift":
                return $"gift|{StressorTable.TargetOf(e)}|{pleased}".ToLowerInvariant();
            default:
                return StressorTable.Of(e) is null ? null : $"{e.Kind}|{StressorTable.TargetOf(e)}".ToLowerInvariant();
        }
    }

    /// <summary>The 6:00 drift (grudges heal by forgiveness, warmth fades slowly).</summary>
    public void Drift() => Book.Drift(_temperamentOf, _o);

    /// <summary>After a friendship penalty (shadow: a would-be penalty), regard moves up by
    /// <paramref name="amount"/> so it takes more bad acts to repeat it.</summary>
    public RegardNote Relieve(string npc, string subject, double amount)
    {
        double before = Book.Of(npc, subject);
        Book.Set(npc, subject, before + amount);
        return new RegardNote(npc, subject, "relief", 0, 0, 1, false, false, before, Book.Of(npc, subject),
            "held a grudge; it eased afterwards");
    }

    private RegardNote? OnHeard(string npc, DiaryEntry heard, Temperament t)
    {
        IReadOnlyDictionary<string, string> d = DiaryDetail.Parse(heard.Detail);
        if (!d.TryGetValue("kind", out string? original))
            return null;
        StressorProfile? p = StressorTable.Of(new DiaryEntry(heard.AbsoluteTick, heard.Subject, original, heard.Detail));
        if (p is null)
            return null;
        string teller = d.TryGetValue("from", out string? from) ? from : "someone";
        string owner = Gossip.OwnerOf(heard, teller);
        bool confirmed = FromSource(original, teller, heard.Subject, owner);
        double magnitude = p.Magnitude * _o.HearsayFactor * Stresses.SensitivityFactor(t);
        bool severe = magnitude >= _o.SevereMagnitude;
        double retention = severe ? 1.0 : 0.5 + _o.RetentionOf(npc);
        double before = Book.Of(npc, heard.Subject);
        if (confirmed)
            Book.ApplyConfirmed(npc, heard, 1, t, _o);
        return new RegardNote(npc, heard.Subject, "Heard:" + original, magnitude, confirmed ? p.Plastic : 0, retention,
            severe, false, before, Book.Of(npc, heard.Subject),
            confirmed ? $"heard from {teller}, who was in it" : $"heard from {teller}; not confirmed");
    }

    /// <summary>
    /// The teller was in the event (ledger-gossip.md): the subject told it, or the teller's own
    /// entry was something done to them (a gift they got, a quest the player did for them). A
    /// <c>Saw...</c> entry is a witness's, and a <c>Heard</c> is already second-hand. A retold
    /// story (D25) comes from the source only when its teller is the person it started with
    /// (<paramref name="owner"/>); null means the teller (a Heard from before juiciness).
    /// </summary>
    public static bool FromSource(string originalKind, string teller, string subject, string? owner = null)
    {
        if (string.Equals(teller, subject, StringComparison.OrdinalIgnoreCase))
            return true;
        if (owner is not null && !string.Equals(teller, owner, StringComparison.OrdinalIgnoreCase))
            return false; // second-hand or further: hearsay until confirmed
        return !originalKind.StartsWith("Saw", StringComparison.Ordinal) && originalKind != "Heard";
    }

    private static string Cause(DiaryEntry entry)
        => string.IsNullOrEmpty(entry.Detail) ? entry.Kind : $"{entry.Kind} ({entry.Detail})";
}
