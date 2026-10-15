using System.Text.Json;
using UnderGlass.Sim;

// The web runner consumes this description from the same binary it executes.
internal static class ReplayCliSchema
{
    private record Option(string Flag, string Label, string Type, string Group, string? Default = null,
        string[]? Choices = null, bool Repeat = false);
    public static string Json()
    {
        var options = new[] {
            new Option("seed", "Seed", "integer", "Run", "1"),
            new Option("days", "Days", "integer", "Run", "28"),
            new Option("town", "Town", "town", "Run", "pelican", Towns.Names.ToArray()),
            new Option("reflection", "Private reflection", "choice", "Mind", "", new[]{"", "authored", "laya", "hybrid", "tape"}),
            new Option("speech", "Record speech", "boolean", "Speech"),
            new Option("newcomer-voice", "Use Newcomer test voice", "boolean", "Speech"),
            new Option("speech-catalog", "Speech catalog", "path", "Speech"),
            new Option("character-inspiration", "Character inspiration pack", "path", "Mind"),
            new Option("laya-url", "Laya service", "url", "Models"),
            new Option("laya-model", "Laya model", "text", "Models", "typed-decisions"),
            new Option("evaluation", "Laya evaluation", "choice", "Models", "balanced", new[]{"raw", "canonical", "balanced"}),
            new Option("llm-url", "Inspiration service", "url", "Models"),
            new Option("llm-model", "Inspiration model", "text", "Models"),
            new Option("reflection-tape", "Recorded reflection tape", "path", "Models"),
            new Option("reflection-chance", "Daily reflection chance", "number", "Mind", "0.35"),
            new Option("dreams", "Enable dreams (15%)", "boolean", "Mind"),
            new Option("dream-chance", "Dream chance override", "number", "Mind"),
            new Option("catalog", "Action catalog slices", "catalog", "Actions", "", ActOptions.Switches.ToArray()),
            new Option("feel", "Feelings", "choice", "Advanced", "", new[]{"", "off", "observe", "on"}),
            new Option("desire", "Desires", "choice", "Advanced", "", new[]{"", "off", "observe", "on"}),
            new Option("tensions", "Starting tension depth", "number", "Advanced"),
            new Option("0d6", "Social dynamics", "steps", "Advanced", "", new[]{"b", "c", "d", "e", "f", "g", "h", "i", "j", "t", "m"}),
            new Option("inject", "Inject the placed scandal", "boolean", "Advanced"),
            new Option("no-nights", "Omit night records", "boolean", "Advanced"),
            new Option("fo", "Feeling overrides", "override", "Overrides", Repeat:true),
            new Option("ao", "Action overrides", "override", "Overrides", Repeat:true),
            new Option("trait", "Character trait overrides", "trait", "Overrides", Repeat:true),
            new Option("lives-routing", "History-aware destinations", "boolean", "Histories"),
            new Option("history", "Starting histories", "path", "Histories"),
            new Option("compare-history", "Compare without history IDs", "text", "Histories", Repeat:true),
            new Option("no-lives-record", "Omit routing records", "boolean", "Advanced"),
            new Option("lives-company", "Seek company and play games", "boolean", "Histories"),
            new Option("no-company-record", "Omit company records", "boolean", "Advanced"),
            new Option("lives-evenings", "Plan shared evenings", "boolean", "Histories"),
            new Option("no-evening-record", "Omit evening records", "boolean", "Advanced"),
            new Option("evening-people", "Evening participants (blank: whole cast)", "text", "Histories"),
            new Option("lives-laya", "Evening Laya service", "url", "Models"),
            new Option("lives-laya-model", "Evening Laya model", "text", "Models"),
            new Option("lives-tape", "Evening decision tape", "path", "Models"),
            new Option("live-output", "Live recording", "output", "Output"),
            new Option("live-control", "Live controls", "output", "Output"),
            new Option("out", "JSON recording", "output", "Output"),
            new Option("html", "Standalone dashboard", "output", "Output"),
        };
        object Knobs(object value) => value.GetType().GetProperties()
            .Where(p => p.CanWrite && new[]{typeof(bool),typeof(int),typeof(double)}.Contains(p.PropertyType))
            .OrderBy(p=>p.Name, StringComparer.Ordinal)
            .Select(p=>new { name=p.Name, type=p.PropertyType==typeof(bool)?"boolean":p.PropertyType==typeof(int)?"integer":"number", value=p.GetValue(value) }).ToArray();
        return JsonSerializer.Serialize(new { options, knobs = new { fo=Knobs(DefaultTown.Feelings()), ao=Knobs(DefaultTown.Feelings().Acts) },
            places=TownData.Default().Places.Select(p=>p.Name).ToArray(), traits=Enum.GetNames<Trait>(), characters=DefaultTown.Cast().Select(v=>v.Name).ToArray(),
            stepLabels=new[]{"Household hurt", "Mood contagion", "Exclusion", "Personal dials", "Recovery", "Patience and coercion", "Show feelings", "Mishap hurt", "Combative discount", "Tone", "Missing people"} },
            new JsonSerializerOptions { PropertyNamingPolicy=JsonNamingPolicy.CamelCase });
    }
}
