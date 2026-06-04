namespace etch_ui.Security;

/// <summary>알람 이력 조회용 행.</summary>
public sealed class AlarmHistoryRow
{
    public long Id { get; init; }
    public string AlarmCode { get; init; } = string.Empty;
    public string OccurredAtDisplay { get; init; } = string.Empty;
    public string? ResolvedAtDisplay { get; init; }
    public string? ResolvedBy { get; init; }
    public string? Note { get; init; }

    public string SummaryDisplay { get; init; } = string.Empty;

    public string StatusDisplay => ResolvedAtDisplay is null ? "발생" : "해제";
}
