namespace UnderGlass.Sim;

/// <summary>Observer facts captured at the start of an act. Purpose/item/effort are unknown
/// until a mechanic actually supplies them. A causal link is not proof of a private motive.</summary>
public sealed record EncounterContext(int ActId, int Tick, string Kind, string Actor, string? Target,
    string ActorRole, string Location, int DurationMinutes, int SourceActId,
    string? Activity = null, string? Item = null, double? Effort = null, double? Amount = null);

/// <summary>A recorded choice that caused this act, private to its actor. ReflectionId points
/// to the thought/decision receipt; it does not assert that the imagined interpretation is true.</summary>
public sealed record EncounterCause(int ActId, int Tick, string Actor, string Method,
    int SourceActId = -1, string? Motive = null, string? ReflectionId = null, string? ChoiceId = null);

/// <summary>One holder's account at acquisition, never corrected from observer truth.</summary>
public sealed record EncounterPerception(int ActId, int Tick, string Holder, string Kind,
    string? Actor, string? Target, string Role, string Source, double Confidence,
    IReadOnlyList<string> Chain);

public sealed record AppraisalReason(string Code, string Text, IReadOnlyList<int> Evidence);

/// <summary>Authored observer baseline, not affect or a gossip-selection score. Relationship
/// inputs and reasons are snapshots from this holder's knowledge at this minute.</summary>
public sealed record EncounterAppraisal(EncounterPerception Account, double? Regard,
    double Familiarity, double PersonalSignificance, double ListenerInterest, string Category,
    IReadOnlyList<AppraisalReason> Reasons);

/// <summary>Public, independently testable appraisal inputs contain only this holder's knowledge.</summary>
public sealed record EncounterHistory(int ActId, int Tick, string? Actor, string? Target, double Valence);

public static class EncounterAppraiser
{
    public const string Revision = "authored-context-1";
    public static EncounterAppraisal Of(EncounterPerception account, double valence, bool scandal,
        bool upheaval, bool sameHousehold, double? regard, double familiarity,
        IReadOnlyList<EncounterHistory> history)
    {
        bool involved = account.Role is "performed" or "received" or "underwent";
        string? other = account.Actor == account.Holder ? account.Target : account.Actor;
        var reasons = new List<AppraisalReason>();
        void Reason(string code, string text, params int[] evidence)
            => reasons.Add(new(code, text, evidence));
        // Identity uncertainty remains part of the judgment, even for an important event.
        if (account.Actor is null || account.Confidence < 0.9)
            Reason("uncertain-account", "Identity or observation is uncertain.", account.ActId);
        var pair = other is null ? Array.Empty<EncounterHistory>() : history
            .Where(h => h.ActId != account.ActId && h.Tick <= account.Tick
                && account.Tick - h.Tick <= 7 * Clock.MinutesPerDay
                && (h.Actor == account.Holder && h.Target == other
                    || h.Actor == other && h.Target == account.Holder))
            .GroupBy(h => h.ActId).Select(g => g.Last()).OrderBy(h => h.Tick).ThenBy(h => h.ActId).ToArray();
        var hurt = pair.LastOrDefault(h => h.Valence < 0);
        bool kind = valence > 0, tension = involved && kind && regard < -0.2;
        bool afterHurt = involved && kind && hurt is not null;
        bool repeated = pair.Any(h => Math.Sign(h.Valence) == Math.Sign(valence));
        double personal = involved ? 0.3 : 0.1, interest = 0;
        string category = "everyday";
        if (kind && sameHousehold && !afterHurt && !tension)
            Reason("household-care", "Ordinary care between housemates; no known recent conflict in this account.", account.ActId);
        if (repeated)
            Reason("repeated", "Similar conduct is already in this person's recent memory.", pair.Last(h => Math.Sign(h.Valence) == Math.Sign(valence)).ActId);
        if (afterHurt)
        {
            category = "relationship"; personal = 0.75; interest = 0.4;
            Reason("kindness-after-hurt", "Kindness follows a recent known conflict; reconciliation is not established.", hurt!.ActId, account.ActId);
        }
        if (tension)
        {
            category = "relationship"; personal = Math.Max(personal, 0.65); interest = Math.Max(interest, 0.3);
            Reason("kindness-with-strain", "This person's regard was strained when they learned of the kindness.", account.ActId);
        }
        if (valence < 0 && involved)
        {
            category = "relationship"; personal = repeated ? 0.55 : 0.75; interest = 0.3;
            Reason("direct-conflict", "This person took part in an adverse encounter.", account.ActId);
        }
        if (scandal || upheaval)
        {
            category = "public"; personal = Math.Max(personal, 0.6); interest = 0.8;
            Reason("public-consequence", "The known kind has a scandal or upheaval classification; reporting rules are separate.", account.ActId);
        }
        if (!involved && !scandal && !upheaval && familiarity >= 0.4 && !sameHousehold)
        {
            interest = repeated ? 0.1 : 0.25;
            Reason("known-person", "This listener knows a named participant; relevance alone does not make town news.", account.ActId);
        }
        if (reasons.Count == 0)
            Reason(involved ? "direct-involvement" : "ordinary-observation",
                involved ? "A personal encounter with no supported exceptional context."
                    : "An observed account with no supported exceptional context.", account.ActId);
        return new(account, regard, familiarity, personal, interest, category, reasons);
    }
}
