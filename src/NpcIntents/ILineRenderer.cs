using NpcMemory;

namespace NpcIntents;

/// <summary>
/// Turns a cited diary entry into a short natural-language line. Templated — no text model (Jev
/// and Laya only decide WHAT and WHO, never the words). The result must pass
/// <see cref="LineSanitizer.Sanitize"/> before it reaches any dialogue string.
/// </summary>
public interface ILineRenderer
{
    string Render(string npc, string voice, DiaryEntry entry);
}
