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
            return null;
        double magnitude = p.Magnitude * Stresses.SensitivityFactor(t);
        double share = Stresses.PlasticShare(entry, p, diaryBefore, _o);
        bool severe = magnitude >= _o.SevereMagnitude;
        double retention = severe ? 1.0 : 0.5 + _o.RetentionOf(npc);
        string subject = StressorTable.TargetOf(entry);
        double before = Book.Of(npc, subject);
        Book.Apply(npc, entry, diaryBefore, t, _o);
        return new RegardNote(npc, subject, entry.Kind, magnitude, share, retention, severe,
            YieldCrossed: p.Yields && share > 0, before, Book.Of(npc, subject), Cause(entry));
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
        bool confirmed = FromSource(original, teller, heard.Subject);
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
    /// <c>Saw...</c> entry is a witness's, and a <c>Heard</c> is already second-hand. Today's
    /// gossip passes on only the teller's own entries, one hop, so every Heard <c>GiftReceived</c>
    /// or <c>QuestHelped</c> comes from the source.
    /// </summary>
    public static bool FromSource(string originalKind, string teller, string subject)
    {
        if (string.Equals(teller, subject, StringComparison.OrdinalIgnoreCase))
            return true;
        return !originalKind.StartsWith("Saw", StringComparison.Ordinal) && originalKind != "Heard";
    }

    private static string Cause(DiaryEntry entry)
        => string.IsNullOrEmpty(entry.Detail) ? entry.Kind : $"{entry.Kind} ({entry.Detail})";
}
