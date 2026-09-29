using System.Globalization;

namespace NpcSchedules;

/// <summary>
/// Splits a schedule script into its command zone (GOTO / NOT / MAIL) and its points,
/// mirroring NPC.parseMasterScheduleImpl / NPC.SplitScheduleCommands in decompiled 1.6:
/// fields are split on '/', empties removed and trimmed; the first field may be an initial
/// command; MAIL consumes the next field as a conditional command, and after a NOT that does
/// not fire the next field may be a GOTO.
/// </summary>
public static class ScriptParser
{
    public static string[] SplitFields(string script)
        => script.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Split a field into space-separated tokens the way the game's ArgUtility does
    /// (multiple spaces collapsed).
    /// </summary>
    public static string[] SplitTokens(string field)
        => field.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Parse the points of an already-command-resolved script. Returns points in script order.
    /// On any parse failure, returns what parsed so far plus an error message; the game would
    /// give the NPC an empty schedule in that case.
    /// </summary>
    public static (List<SchedulePoint> Points, SchedulePoint? Spawn, string? Error) ParsePoints(
        string[] fields, int firstPointIndex, string homeLocation)
    {
        var points = new List<SchedulePoint>();
        SchedulePoint? spawn = null;
        string previousLocation = homeLocation;

        for (int i = firstPointIndex; i < fields.Length; i++)
        {
            string[] tokens = SplitTokens(fields[i]);
            if (tokens.Length == 0)
                continue;

            // time, optional 'a' prefix for arrive-by
            bool arriveBy = false;
            string timeString = tokens[0];
            if (timeString.Length > 1 && timeString[0] == 'a')
            {
                arriveBy = true;
                timeString = timeString.Substring(1);
            }
            if (!int.TryParse(timeString, NumberStyles.Integer, CultureInfo.InvariantCulture, out int time))
                return (points, spawn, $"unparseable time '{tokens[0]}' in point '{fields[i]}'");

            int index = 1;
            if (index >= tokens.Length)
                return (points, spawn, $"point '{fields[i]}' has no destination");

            string rawLocation = tokens[index];
            string location;
            int x;
            int y;
            if (string.Equals(rawLocation, "bed", StringComparison.OrdinalIgnoreCase))
            {
                // The game resolves 'bed' to the last entry of the default (else spring) script;
                // the caller passes that already-resolved home location. Married NPCs get BusStop
                // instead, which this tool does not model. Any x/y tokens after 'bed' are ignored
                // by the game too.
                location = homeLocation;
                x = 0;
                y = 0;
                index++;
                var point0 = new SchedulePoint(time, arriveBy, location, x, y, 2, null, null)
                {
                    RawLocation = rawLocation,
                };
                if (time == 0)
                    spawn = point0;
                else
                    points.Add(point0);
                previousLocation = location;
                continue;
            }
            else if (int.TryParse(rawLocation, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                // location omitted: stay on the previous map (game: first point -> default map)
                location = previousLocation;
            }
            else
            {
                location = rawLocation;
                index++;
            }

            if (index + 1 >= tokens.Length)
                return (points, spawn, $"point '{fields[i]}' has no tile coordinates");
            if (!int.TryParse(tokens[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
                || !int.TryParse(tokens[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
                return (points, spawn, $"unparseable tile coordinates in point '{fields[i]}'");
            index += 2;

            int facing = 2;
            if (index < tokens.Length && int.TryParse(tokens[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedFacing))
            {
                facing = parsedFacing;
                index++;
            }

            string? animation = null;
            string? message = null;
            if (index < tokens.Length)
            {
                if (tokens[index].Length > 0 && tokens[index][0] == '"')
                {
                    // dialogue without animation
                    message = string.Join(" ", tokens.Skip(index));
                }
                else
                {
                    animation = tokens[index];
                    index++;
                    if (index < tokens.Length && tokens[index].Length > 0 && tokens[index][0] == '"')
                        message = string.Join(" ", tokens.Skip(index));
                }
            }

            var point = new SchedulePoint(time, arriveBy, location, x, y, facing, animation, message)
            {
                RawLocation = rawLocation,
            };

            if (time == 0)
            {
                // spawn point: sets where the NPC starts the day, not a route
                spawn = point;
                previousLocation = location;
                continue;
            }

            points.Add(point);
            previousLocation = location;
        }

        return (points, spawn, null);
    }

    /// <summary>Get the home location the game would use for a 'bed' point: the last point of the
    /// default schedule, else spring. Returns null if neither parses.</summary>
    public static string? ResolveBedHome(Dictionary<string, string> schedules)
    {
        foreach (string key in new[] { "default", "spring" })
        {
            if (!schedules.TryGetValue(key, out string? script))
                continue;
            string[] fields = SplitFields(script);
            if (fields.Length == 0)
                continue;
            string[] tokens = SplitTokens(fields[^1]);
            if (tokens.Length >= 2 && !tokens[0].StartsWith("GOTO") && !tokens[0].StartsWith("NOT")
                && !tokens[0].StartsWith("MAIL") && !int.TryParse(tokens[1], out _))
                return tokens[1];
        }
        return null;
    }
}
