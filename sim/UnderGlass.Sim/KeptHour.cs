namespace UnderGlass.Sim;

/// <summary>
/// A habitual bedtime (character data; D54). On the days it covers (an empty Weekdays covers every
/// weekday, an empty Seasons every season), from At on, going to bed is one of this person's
/// free-time choices, weighted <see cref="HabitOptions.BedtimeHabitWeight"/>, until they next sleep. A
/// preference, not a rule: the body still sends them to bed when tired, sleep still ends when the bar
/// is full (design rule 1), and they can be asked until they are asleep. A villager's list is in order
/// and the first entry covering a day is that day's.
///
/// At is a minute of the evening's day, from noon (<see cref="FirstMinute"/>, 720) to 11:59 the next
/// morning (<see cref="LastMinute"/>, 2159), written the way canon writes its schedule times: 24:00 is
/// 1440 and 00:30 after it 1470. So the day it covers is the day whose evening it ends, and it always
/// falls in that day's night (noon to noon, <see cref="NightTable.NightOf"/>).
///
/// Source: where it comes from (a fixture and its schedule key); null when the hour is ours (E).
/// </summary>
public sealed record KeptHour(int At, IReadOnlyList<int> Weekdays, IReadOnlyList<int> Seasons, string? Source = null)
{
    /// <summary>The range of <see cref="At"/>: noon of the evening's day to 11:59 the next morning.</summary>
    public const int FirstMinute = Clock.MinutesPerDay / 2, LastMinute = Clock.MinutesPerDay + Clock.MinutesPerDay / 2 - 1;

    /// <summary>Whether this kept hour applies on <paramref name="day"/> (days from day 0; the evening's day).</summary>
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
