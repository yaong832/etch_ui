namespace etch_ui.Services.Scheduling;

public readonly record struct WaferTimelineEntry(int WaferId, string Location, string Stage, string Detail);
