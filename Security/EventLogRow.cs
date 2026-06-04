namespace etch_ui.Security;

public sealed class EventLogRow
{
    public long Id { get; init; }
    public string CreatedAtDisplay { get; init; } = "";
    public string? Username { get; init; }
    public string? State { get; init; }
    public string? Code { get; init; }
    public string Message { get; init; } = "";
}
