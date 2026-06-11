namespace etch_ui.Equipment.ViewModels;

public sealed class WaferTimelineRow
{
    public required int WaferId { get; init; }
    public required string Location { get; init; }
    public required string Stage { get; init; }
    public required string Detail { get; init; }

    public string DisplayText => $"#{WaferId} · {Location} · {Stage}"
        + (string.IsNullOrEmpty(Detail) ? string.Empty : $" ({Detail})");
}
