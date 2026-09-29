using NpcMemory;

namespace NpcIntents;

/// <summary>
/// Turns a cited diary entry into a short natural-language line. Templated — no text model (Laya
/// only decides WHAT and WHO, never the words). The result must pass
/// <see cref="LineSanitizer.Sanitize"/> before it reaches any dialogue string.
/// </summary>
public interface ILineRenderer
{
    /// <summary>Render an entry from yesterday (the overnight-intent case).</summary>
    string Render(string npc, string voice, DiaryEntry entry);

    /// <summary>Render an entry that happened <paramref name="daysAgo"/> calendar days before the
    /// line is delivered, so an old entry is never called "yesterday". Renderers that do not
    /// phrase time can keep the default.</summary>
    string Render(string npc, string voice, DiaryEntry entry, int daysAgo) => Render(npc, voice, entry);
}
