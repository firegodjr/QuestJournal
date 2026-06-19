namespace QuestJournal.Core.Model;

public enum QuestStatus
{
    /// <summary>
    /// Value-0 default meaning "no status / not applicable". Carried by the history schema's
    /// unset <c>OldStatus</c>/<c>NewStatus</c> fields (e.g. on Added/Removed changes) and
    /// persisted as the string <c>"None"</c>; removing it makes every prior history line fail
    /// enum parsing, so the durable log silently reads as empty. Keep it first.
    /// </summary>
    None,
    Open,
    Active,
    Cancelled,
    Warning,
    Completed,
    Comment,
}
