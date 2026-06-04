using etch_ui.Equipment.Models;

namespace etch_ui.Services.Scheduling;

public sealed class TransferJob
{
    public required WaferTrack Wafer { get; init; }
    public required EquipmentRegion Pickup { get; init; }
    public required EquipmentRegion Dropoff { get; init; }
}
