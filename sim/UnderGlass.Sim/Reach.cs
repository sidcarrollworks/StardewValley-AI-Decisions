namespace UnderGlass.Sim;

/// <summary>Where a festival is held in a grown town (acts-batch2 spec, b2-4).</summary>
public sealed record Venue(string Place, Tile Center, int Radius, int From, int To);

/// <summary>A story made public on purpose (acts-batch2 spec, b2-5). How: confronted, read-out,
/// board, called-in.</summary>
public sealed record Announcement(int ActId, int Tick, string By, string How, string Place, IReadOnlyList<string> Heard);

/// <summary>A festival day's crowd (acts-batch2 spec, b2-4).</summary>
public sealed record FestivalDay(int Day, string Name, string Place, int Came, int Awake, int Peak);
