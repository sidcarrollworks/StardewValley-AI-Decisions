namespace NpcMinds.Tests;

/// <summary>File helpers for tests that read a log while its writer still holds the file open
/// (the playtest log keeps the day's file open). File.ReadAllLines opens with just
/// FileShare.Read, which collides with the writer on Windows; a FileShare.ReadWrite reader
/// does not.</summary>
internal static class TestFiles
{
    public static string[] ReadLines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
            lines.Add(line);
        return lines.ToArray();
    }
}
