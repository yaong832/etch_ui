namespace etch_ui.Services.Scheduling;

/// <summary>UI 타임라인용 버퍼·챔버 웨이퍼 스냅샷.</summary>
public readonly record struct WaferBufferEntrySnapshot(
    WaferTrack Wafer,
    string Location,
    string Status,
    int RemainingTicks = 0);
