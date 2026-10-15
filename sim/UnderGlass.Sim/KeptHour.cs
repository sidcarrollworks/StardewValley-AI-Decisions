namespace UnderGlass.Sim;

/// <summary>
/// A habitual bedtime (character data; D54). On the days it covers (an empty Weekdays covers every
/// weekday, an empty Seasons every season), from At (a minute of the day) on, going to bed is one of
/// this person's free-time choices, weighted <see cref="HabitOptions.BedtimeHabitWeight"/>, until they
/// next sleep. A preference, not a rule: the body still sends them to bed when tired, sleep still ends
/// when the bar is full (design rule 1), and they can be asked until they are asleep. A villager's
/// list is in order and the first entry covering a day is that day's. Source: where it comes from (a
/// fixture and its schedule key); null when the hour is ours (E).
/// </summary>
public sealed record KeptHour(int At, IReadOnlyList<int> Weekdays, IReadOnlyList<int> Seasons, string? Source = null)
{
    /// <summary>Whether this kept hour applies on <paramref name="day"/> (days from day 0).</summary>
    public bool Covers(int day) => Covers(day % Clock.DaysPerWeek, Calendar.Season(day));

    public bool Covers(int weekday, int season) => (Weekdays.Count == 0 || Weekdays.Contains(weekday))
        && (Seasons.Count == 0 || Seasons.Contains(season));

    /// <summary>The first of <paramref name="hours"/> covering <paramref name="day"/>, with its index;
    /// null when none does (the body alone decides that night).</summary>
    public static (KeptHour Hour, int Index)? For(IReadOnlyList<KeptHour>? hours, int day)
    {
        if (hours is null || day < 0)
            return null;
        for (int i = 0; i < hours.Count; i++)
            if (hours[i].Covers(day))
                return (hours[i], i);
        return null;
    }
}
