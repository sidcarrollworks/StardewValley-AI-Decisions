namespace UnderGlass.Sim;

/// <summary>
/// The year (phase 0d.6; spec X12): four seasons of 28 days, and day 0 of a run is spring 1, a
/// Monday, as in Stardew and as <see cref="Clock.Weekday"/> already counts. The festivals are
/// Stardew's, read from the game's Data/Festivals/FestivalDates (1.6.15) on Sid's PC on 2026-10-07;
/// each villager's birthday is on their card (DefaultTown, from the game's Data/Characters).
/// </summary>
public static class Calendar
{
    public const int Seasons = 4;
    public static readonly IReadOnlyList<string> SeasonNames = new[] { "spring", "summer", "fall", "winter" };

    public static int Season(int day) => day / Clock.DaysPerSeason % Seasons;

    public static int DayOfSeason(int day) => day % Clock.DaysPerSeason + 1;

    public static YearDay DateOf(int day) => new(Season(day), DayOfSeason(day));

    /// <summary>Stardew's festivals: the Egg Festival, the Flower Dance, the Luau, the Dance of the
    /// Moonlight Jellies, the Stardew Valley Fair, Spirit's Eve, the Festival of Ice and the Feast of
    /// the Winter Star.</summary>
    public static readonly IReadOnlyList<(YearDay Date, string Name)> Festivals = new[]
    {
        (new YearDay(0, 13), "Egg Festival"), (new YearDay(0, 24), "Flower Dance"),
        (new YearDay(1, 11), "Luau"), (new YearDay(1, 28), "Dance of the Moonlight Jellies"),
        (new YearDay(2, 16), "Stardew Valley Fair"), (new YearDay(2, 27), "Spirit's Eve"),
        (new YearDay(3, 8), "Festival of Ice"), (new YearDay(3, 25), "Feast of the Winter Star"),
    };

    /// <summary>The festival on this day of the run, or null.</summary>
    public static string? FestivalOn(int day)
    {
        YearDay d = DateOf(day);
        foreach (var (date, name) in Festivals)
            if (date == d)
                return name;
        return null;
    }

    public static bool IsBirthday(YearDay? birthday, int day) => birthday is { } b && b == DateOf(day);
}
