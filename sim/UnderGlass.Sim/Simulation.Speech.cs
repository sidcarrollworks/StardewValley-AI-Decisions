namespace UnderGlass.Sim;

/// <summary>What people say while they act (roadmap, "The agreed order", step 2). A line is chosen
/// when an act toward a chosen person begins, from the speaker's side only: their archetype from
/// the character pack when one is configured, how well they know the other person, and their mood
/// band. It is recorded beside the act and read by nothing in the simulation, so it changes no
/// decision, no feeling and no log line; with speech off, nothing here runs.</summary>
public sealed partial class Simulation
{
    private SpeechOptions _speech = new();
    private readonly List<Speech> _speechLines = new();
    /// <summary>The last few library ids each speaker used for each kind, newest last.</summary>
    private readonly Dictionary<(string Speaker, string Kind), List<string>> _lastSaid = new();
    private const int Remembered = 3;

    public void ConfigureSpeech(SpeechOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (_speechLines.Count > 0) throw new InvalidOperationException("Configure speech before running the simulation.");
        _speech = options;
    }

    private void Say(Act act, ActKind kind, Person actor)
    {
        if (!_speech.Enabled || act.Target is null || !_index.ContainsKey(act.Target) || SpeechCatalog.Silent.Contains(kind.Name)) return;
        string speaker = actor.V.Name, subject = act.Target;
        string? archetype = _inspirationPack?.Characters.FirstOrDefault(c => c.Name == speaker)?.ArchetypeId;
        string relationship = ReflectionText.Relationship(Familiarity(speaker, subject), HouseholdOf(speaker) == HouseholdOf(subject), _fo.Acts.NewAt);
        string mood = SpeechCatalog.MoodBand(MoodOf(_index[speaker]));
        if (!_lastSaid.TryGetValue((speaker, kind.Name), out List<string>? recent)) _lastSaid[(speaker, kind.Name)] = recent = new();
        SpeechLine? line = SpeechCatalog.Choose(SpeechCatalog.Default, kind.Name, archetype, relationship, mood, _seed, act.Id, recent);
        if (line is null) return;
        recent.Add(line.Id);
        if (recent.Count > Remembered) recent.RemoveAt(0);
        _speechLines.Add(new Speech(act.Id, act.Tick, speaker, SpeechCatalog.Render(line, subject), line.Id, archetype, relationship, mood));
    }
}
