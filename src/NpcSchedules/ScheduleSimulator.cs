using System.Globalization;

namespace NpcSchedules;

/// <summary>
/// Resolves which schedule key applies on a given day and evaluates the script chain,
/// mirroring NPC.TryLoadSchedule and NPC.parseMasterScheduleImpl in decompiled 1.6.
/// Deliberately not modelled: marriage keys (and marriageJob), passive-festival keys, the
/// GreenRain key (year-1 green rain), and the island-resort schedule. The 'bus' key is
/// honoured for Pam when the player has the ccVault mail flag.
/// </summary>
public sealed class ScheduleSimulator
{
    private readonly string npc;
    private readonly Dictionary<string, string> schedules;
    private readonly ExtractorOptions options;
    private readonly Random rng;
    private readonly List<string> warnings;

    public ScheduleSimulator(string npc, Dictionary<string, string> schedules, ExtractorOptions options,
        Random rng, List<string> warnings)
    {
        this.npc = npc;
        this.schedules = schedules;
        this.options = options;
        this.rng = rng;
        this.warnings = warnings;
    }

    private static readonly string[] DayNames = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    public static string DayName(int dayOfMonth) => DayNames[(dayOfMonth - 1) % 7];

    /// <summary>Hearts level: friendship points / 250 (game: Utility.GetAllPlayerFriendshipLevel / 250).</summary>
    public int HeartLevel => Math.Max(0, options.Hearts * 250) / 250;

    private bool HeartsMet(string npcName, int level)
        => options.Friends.TryGetValue(npcName, out int hearts) && hearts >= level;

    /// <summary>
    /// Pick the schedule key for one day (unmarried path of TryLoadSchedule), or null if the
    /// NPC has no schedule that day. Key selection is deterministic given the seeded RNG.
    /// </summary>
    public string? PickDayKey(string season, int day, bool rainy, out string? logReason)
    {
        logReason = null;
        string dayName = DayName(day);
        int heartLevel = HeartLevel;

        if (schedules.ContainsKey($"{season}_{day}"))
            return $"{season}_{day}";

        for (int tryHearts = heartLevel; tryHearts > 0; tryHearts--)
        {
            string key = $"{day}_{tryHearts}";
            if (schedules.ContainsKey(key))
                return key;
        }

        if (schedules.ContainsKey(day.ToString(CultureInfo.InvariantCulture)))
            return day.ToString(CultureInfo.InvariantCulture);

        if (npc == "Pam" && options.MailReceived.Contains("ccVault") && schedules.ContainsKey("bus"))
            return "bus";

        if (rainy)
        {
            // game: Game1.random.NextBool() — unseeded coin flip; the tool uses its seeded RNG
            if (schedules.ContainsKey("rain2") && rng.Next(2) == 0)
                return "rain2";
            if (schedules.ContainsKey("rain"))
                return "rain";
        }

        // NOTE: the game's loop decrements twice per iteration (see NPC.cs: `tryHearts--;` both
        // in the header and body), so it tries heartLevel, heartLevel-2, heartLevel-4, ... ≥ 1.
        // Mirrored exactly so the tool and the game pick the same key.
        for (int tryHearts = heartLevel; tryHearts > 0; tryHearts--)
        {
            string key = $"{season}_{dayName}_{tryHearts}";
            if (schedules.ContainsKey(key))
                return key;
            tryHearts--;
        }

        if (schedules.ContainsKey($"{season}_{dayName}"))
            return $"{season}_{dayName}";

        for (int tryHearts = heartLevel; tryHearts > 0; tryHearts--)
        {
            string key = $"{dayName}_{tryHearts}";
            if (schedules.ContainsKey(key))
                return key;
            tryHearts--;
        }

        if (schedules.ContainsKey(dayName))
            return dayName;

        if (schedules.ContainsKey(season))
            return season;

        if (schedules.ContainsKey($"spring_{dayName}"))
            return $"spring_{dayName}";

        if (schedules.ContainsKey("spring"))
            return "spring";

        // game: ClearSchedule(); return false — no schedule today
        return null;
    }

    /// <summary>
    /// Evaluate a schedule key's script through GOTO / NOT friendship / MAIL commands.
    /// Returns null when the NPC has no schedule for the day (GOTO NO_SCHEDULE).
    /// </summary>
    public ParsedScript? Evaluate(string key, string season, List<string> visited)
    {
        if (visited.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            warnings.Add($"schedule chain for '{npc}' loops ({string.Join(" -> ", visited)} -> {key}); game gives the NPC an empty schedule");
            return new ParsedScript { EffectiveKey = key, KeyChain = visited.Append(key).ToList(), ParseFailed = true };
        }
        visited.Add(key);

        if (!schedules.TryGetValue(key, out string? script) || script == null)
        {
            if (key.Equals("spring", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"spring schedule for '{npc}' is missing; game gives the NPC an empty schedule");
                return new ParsedScript { EffectiveKey = key, KeyChain = visited.ToList(), ParseFailed = true };
            }
            warnings.Add($"schedule '{key}' for '{npc}' is missing; game falls back to spring");
            return Evaluate("spring", season, visited);
        }

        string[] fields = ScriptParser.SplitFields(script);
        if (fields.Length == 0)
            return new ParsedScript { EffectiveKey = key, KeyChain = visited.ToList() };

        int routesToSkip = 0;
        string first = fields[0];

        if (first.Contains("GOTO"))
            return FollowGoto(key, first, season, visited, missingIsEmpty: false);

        if (first.Contains("NOT"))
        {
            string[] tokens = ScriptParser.SplitTokens(first);
            if (tokens.Length > 1 && tokens[1].Equals("friendship", StringComparison.OrdinalIgnoreCase))
            {
                bool conditionMet = false;
                for (int i = 2; i + 1 < tokens.Length; i += 2)
                {
                    if (int.TryParse(tokens[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int level)
                        && HeartsMet(tokens[i], level))
                    {
                        conditionMet = true;
                        break;
                    }
                }
                if (conditionMet)
                {
                    // game: parseMasterScheduleImpl("spring", ...) — straight to spring, not default
                    return Evaluate("spring", season, visited);
                }
                routesToSkip = 1;
            }
            // 'NOT' without 'friendship' is ignored by the game; fall through with routesToSkip 0
        }
        else if (first.Contains("MAIL"))
        {
            string[] tokens = ScriptParser.SplitTokens(first);
            if (tokens.Length < 2)
            {
                warnings.Add($"MAIL command without an ID in '{npc}' schedule '{key}'; game fails to parse this script");
                return new ParsedScript { EffectiveKey = key, KeyChain = visited.ToList(), ParseFailed = true };
            }
            string mailId = tokens[1];
            bool received = options.MailReceived.Contains(mailId);
            routesToSkip = received ? 2 : 1;
        }

        if (routesToSkip < fields.Length && fields[routesToSkip].Contains("GOTO"))
            return FollowGoto(key, fields[routesToSkip], season, visited, missingIsEmpty: true);

        // parse the points; any failure matches the game's behaviour (exception -> empty schedule)
        string homeLocation = ScriptParser.ResolveBedHome(schedules) ?? "";
        (List<SchedulePoint> points, SchedulePoint? spawn, string? error) =
            ScriptParser.ParsePoints(fields, routesToSkip, homeLocation);
        if (error != null)
        {
            warnings.Add($"schedule '{key}' for '{npc}' failed to parse ({error}); game gives the NPC an empty schedule");
            return new ParsedScript { EffectiveKey = key, KeyChain = visited.ToList(), ParseFailed = true };
        }

        // the game keys its schedule dictionary by time, so a duplicate time throws at parse time
        var byTime = new Dictionary<int, SchedulePoint>();
        foreach (SchedulePoint point in points)
        {
            if (!byTime.TryAdd(point.Time, point))
            {
                warnings.Add($"schedule '{key}' for '{npc}' has two points at time {point.Time}; game fails to parse this script");
                return new ParsedScript { EffectiveKey = key, KeyChain = visited.ToList(), ParseFailed = true };
            }
        }

        // arrival-time clamp: the game computes time = max(walk-adjusted time, previousTime).
        // Walk time is ignored here (project simplification), but the previousTime clamp is kept.
        var ordered = new List<SchedulePoint>();
        int previousTime = 610;
        foreach (SchedulePoint point in points)
        {
            if (point.Time < previousTime)
                warnings.Add($"schedule '{key}' for '{npc}': point at {point.Time} falls before the previous point's time; game clamps it to {previousTime}");
            var adjusted = point with { Time = Math.Max(point.Time, previousTime) };
            ordered.Add(adjusted);
            previousTime = adjusted.Time;
        }
        ordered.Sort((a, b) => a.Time.CompareTo(b.Time));

        return new ParsedScript
        {
            EffectiveKey = key,
            Points = ordered,
            Spawn = spawn,
            KeyChain = visited.ToList(),
        };
    }

    private ParsedScript? FollowGoto(string fromKey, string field, string season, List<string> visited, bool missingIsEmpty)
    {
        string[] tokens = ScriptParser.SplitTokens(field);
        if (tokens.Length < 2)
        {
            warnings.Add($"GOTO without a key in '{npc}' schedule '{fromKey}'; game fails to parse this script");
            return new ParsedScript { EffectiveKey = fromKey, KeyChain = visited.ToList(), ParseFailed = true };
        }

        string newKey = tokens[1];
        if (newKey.Equals("season", StringComparison.OrdinalIgnoreCase))
        {
            newKey = season;
            if (!schedules.ContainsKey(newKey))
                newKey = "spring";
        }
        else if (newKey.Equals("no_schedule", StringComparison.OrdinalIgnoreCase))
        {
            // game: followSchedule = false; return null — no schedule today
            return new ParsedScript { EffectiveKey = fromKey, KeyChain = visited.ToList(), NoSchedule = true };
        }

        if (!schedules.ContainsKey(newKey))
        {
            if (missingIsEmpty)
                warnings.Add($"GOTO references missing schedule '{newKey}' in '{npc}' schedule '{fromKey}'; game gives the NPC an empty schedule");
            else
                warnings.Add($"GOTO references missing schedule '{newKey}' in '{npc}' schedule '{fromKey}'; game falls back to spring");
            return missingIsEmpty
                ? new ParsedScript { EffectiveKey = fromKey, KeyChain = visited.ToList() }
                : Evaluate("spring", season, visited);
        }

        ParsedScript? result = Evaluate(newKey, season, visited);
        // initial GOTO whose target fails to parse: the game logs the exception and falls back to spring
        if (result != null && result.ParseFailed && !missingIsEmpty)
            return Evaluate("spring", season, visited);
        return result;
    }
}
