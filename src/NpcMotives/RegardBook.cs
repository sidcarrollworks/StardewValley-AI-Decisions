using System.Text.Json;
using NpcMemory;
using NpcTemperament;

namespace NpcMotives;

/// <summary>
/// The plastic part of feelings, saved (docs/spec/motives.md, "Regard"): one signed number per
/// (observer, subject), -1..1, sparse. It survives the diary's 500-entry trim, so a grudge never
/// vanishes with the entries that caused it. The negative part of an NPC's regard for the player
/// is its grudge. The mod saves it under the additive save-data key <c>regard</c>
/// (persistence.md); game thread only.
/// </summary>
public sealed class RegardBook
{
    private readonly Dictionary<string, double> _values = new(StringComparer.OrdinalIgnoreCase);

    private static string Key(string observer, string subject) => observer + "|" + subject;

    /// <summary>Regard of <paramref name="observer"/> for <paramref name="subject"/>; 0 when none.</summary>
    public double Of(string observer, string subject)
        => _values.TryGetValue(Key(observer, subject), out double v) ? v : 0;

    /// <summary>The grudge: the negative part of regard, 0..1.</summary>
    public double GrudgeOf(string observer, string subject) => Math.Max(0, -Of(observer, subject));

    public int Count => _values.Count;

    /// <summary>Every pair with a regard, in key order (the playtest log's daily snapshot).</summary>
    public IReadOnlyList<(string Observer, string Subject, double Value)> Pairs()
        => _values.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv =>
            {
                int bar = kv.Key.IndexOf('|');
                return (kv.Key[..bar], kv.Key[(bar + 1)..], kv.Value);
            })
            .ToList();

    public void Set(string observer, string subject, double value)
    {
        double v = Math.Clamp(value, -1, 1);
        if (Math.Abs(v) < 1e-6)
            _values.Remove(Key(observer, subject));
        else
            _values[Key(observer, subject)] = v;
    }

    /// <summary>
    /// Applies one new diary entry's lasting mark: valence x magnitude (after sensitivity) x the
    /// plastic share x the retention factor (0.5 + retention), except that a severe stress
    /// (magnitude after sensitivity at or above <see cref="MotiveOptions.SevereMagnitude"/>)
    /// ignores retention: everyone remembers being stood up on their birthday. Call once per entry,
    /// when it is appended. Returns the change (0 when the entry leaves no mark).
    /// <paramref name="diaryBefore"/> is the observer's diary without this entry (for the yield
    /// count).
    /// </summary>
    public double Apply(string observer, DiaryEntry entry, IReadOnlyList<DiaryEntry> diaryBefore,
        Temperament t, MotiveOptions o)
    {
        StressorProfile? p = StressorTable.Of(entry);
        if (p is null)
            return 0;
        double share = Stresses.PlasticShare(entry, p, diaryBefore, o);
        if (share <= 0)
            return 0;
        double magnitude = p.Magnitude * Stresses.SensitivityFactor(t);
        double retention = magnitude >= o.SevereMagnitude ? 1.0 : 0.5 + o.RetentionOf(observer);
        double delta = p.Valence * magnitude * share * retention;
        string subject = StressorTable.TargetOf(entry);
        Set(observer, subject, Of(observer, subject) + delta);
        return delta;
    }

    /// <summary>Confirmed hearsay (ledger-gossip.md): a <c>Heard</c> story becomes lasting when
    /// confirmed first-hand, by someone in the event, or (by half) by two independent tellers.
    /// <paramref name="fraction"/> is 1 or 0.5. The mark is the hearsay stress's plastic share:
    /// the original's magnitude x <see cref="MotiveOptions.HearsayFactor"/>, so a story moves the
    /// listener half as much as it moved the person it happened to.</summary>
    public double ApplyConfirmed(string observer, DiaryEntry heard, double fraction, Temperament t, MotiveOptions o)
    {
        IReadOnlyDictionary<string, string> d = DiaryDetail.Parse(heard.Detail);
        if (!d.TryGetValue("kind", out string? original))
            return 0;
        var asOriginal = new DiaryEntry(heard.AbsoluteTick, heard.Subject, original, heard.Detail);
        StressorProfile? p = StressorTable.Of(asOriginal);
        if (p is null || p.Plastic <= 0)
            return 0;
        double magnitude = p.Magnitude * o.HearsayFactor * Stresses.SensitivityFactor(t);
        double retention = magnitude >= o.SevereMagnitude ? 1.0 : 0.5 + o.RetentionOf(observer);
        double delta = p.Valence * magnitude * p.Plastic * retention * Math.Clamp(fraction, 0, 1);
        Set(observer, heard.Subject, Of(observer, heard.Subject) + delta);
        return delta;
    }

    /// <summary>The 6:00 drift: negative regard heals toward 0 by
    /// <c>RegardHealRate x (0.5 + forgiveness)</c>; positive regard fades by
    /// <see cref="MotiveOptions.RegardFadeRate"/>. Observers' temperaments come from
    /// <paramref name="temperamentOf"/>.</summary>
    public void Drift(Func<string, Temperament> temperamentOf, MotiveOptions o)
    {
        foreach (string key in _values.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList())
        {
            double v = _values[key];
            string observer = key[..key.IndexOf('|')];
            double next = v < 0
                ? Math.Min(0, v + o.RegardHealRate * (0.5 + temperamentOf(observer).Forgiveness))
                : Math.Max(0, v - o.RegardFadeRate);
            if (Math.Abs(next) < 1e-6)
                _values.Remove(key);
            else
                _values[key] = next;
        }
    }

    /// <summary>Seeds NPC pairs once (town-life.md): only pairs with no regard yet.</summary>
    public int Seed(IEnumerable<(string Observer, string Subject, double Value)> seeds)
    {
        int seeded = 0;
        foreach ((string observer, string subject, double value) in seeds)
        {
            if (_values.ContainsKey(Key(observer, subject)))
                continue;
            Set(observer, subject, value);
            seeded++;
        }
        return seeded;
    }

    public string ToJson()
        => JsonSerializer.Serialize(_values.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value, 4)));

    public static RegardBook FromJson(string? json)
    {
        var book = new RegardBook();
        if (string.IsNullOrWhiteSpace(json))
            return book;
        Dictionary<string, double>? values;
        try
        {
            values = JsonSerializer.Deserialize<Dictionary<string, double>>(json);
        }
        catch (JsonException)
        {
            return book; // a damaged value loads empty rather than failing the whole save load
        }
        if (values is null)
            return book;
        foreach ((string key, double value) in values)
        {
            int bar = key.IndexOf('|');
            if (bar <= 0 || bar == key.Length - 1 || double.IsNaN(value))
                continue; // a malformed key never loads
            book._values[key] = Math.Clamp(value, -1, 1);
        }
        return book;
    }
}
