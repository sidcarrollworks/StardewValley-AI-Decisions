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
        if (_errandSpeech.TryGetValue(act.Id, out var errand))
        {
            // An errand (needs spec): the asking and its answer, or the hand-over and the thanks.
            Speak(act, errand.First, errand.Second, errand.FirstKind, reply: false, errand.Item);
            Speak(act, errand.Second, errand.First, errand.SecondKind, reply: true, errand.Item);
            return;
        }
        if (_answered.TryGetValue(act.Id, out var invitation))
        {
            // An invitation (invitation spec): the asker's offer, then the answer. On a yes the act is
            // the game or drink, begun by the asker; on a no it is the refusal, begun by the one asked.
            string asker = invitation.Accepted ? act.Actor : act.Target, asked = invitation.Accepted ? act.Target : act.Actor;
            Speak(act, asker, asked, invitation.Kind, reply: false);
            Speak(act, asked, asker, invitation.Kind + (invitation.Accepted ? ".Accepted" : ".Refused"), reply: true);
            return;
        }
        Speak(act, actor.V.Name, act.Target, kind.Name, reply: false);
    }

    /// <summary>One line from the library, chosen from the speaker's side and recorded on the act.
    /// Item: the thing an errand is for, as said ("some flour").</summary>
    private void Speak(Act act, string speaker, string subject, string kind, bool reply, string? item = null)
    {
        string? archetype = _inspirationPack?.Characters.FirstOrDefault(c => c.Name == speaker)?.ArchetypeId;
        if (_speech.NewcomerVoice && speaker == "Newcomer") archetype = "newcomer";
        string relationship = ReflectionText.Relationship(Familiarity(speaker, subject), HouseholdOf(speaker) == HouseholdOf(subject), _fo.Acts.NewAt);
        string mood = SpeechCatalog.MoodBand(MoodOf(_index[speaker]));
        if (!_lastSaid.TryGetValue((speaker, kind), out List<string>? recent)) _lastSaid[(speaker, kind)] = recent = new();
        SpeechLine? line = SpeechCatalog.Choose(_speech.Catalog ?? SpeechCatalog.Default, kind, archetype, relationship, mood, _seed, act.Id, recent,
            reply ? "reply" : null);
        if (line is null) return;
        recent.Add(line.Id);
        if (recent.Count > Remembered) recent.RemoveAt(0);
        _speechLines.Add(new Speech(act.Id, act.Tick, speaker, SpeechCatalog.Render(line, subject, item), line.Id, archetype, relationship, mood, reply));
    }
}
