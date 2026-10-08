namespace UnderGlass.Sim;

/// <summary>What a promise is for (acts-batch2 spec 2.1, 2.6; rule 13). Append only.</summary>
public enum PromiseKind { Date, Party, Tab, Loan }

/// <summary>How a promise stands. Append only.</summary>
public enum PromiseState { Open, Kept, Broken, Forgiven, Lapsed, Refused }

/// <summary>
/// A promise (rule 13; acts-batch2 spec 2.6). Date, Party: By comes to Place in [DueFrom, DueTo),
/// and To is there. Tab, Loan: By owes To Amount by DueTo. Late: settled after DueTo. Made, MadeAct
/// -1: made before the run (old debts). No rule makes one until batch 2's slices (b2-2 on).
/// </summary>
public sealed record Promise(int Id, PromiseKind Kind, string By, string To, int Made, int MadeAct,
    string Place, Tile Spot, int DueFrom, int DueTo, double Amount = 0,
    PromiseState State = PromiseState.Open, int Settled = -1, int SettledAct = -1, bool Late = false);

/// <summary>Something dropped and perhaps found (acts-batch2 spec, b2-14): its owner, where, its
/// value, when dropped, who holds it, whether they kept it, and when that came out.</summary>
public sealed record LostItem(int Id, string Owner, string Place, Tile At, double Value, int Dropped,
    string? Holder = null, bool Kept = false, int Exposed = -1);
