namespace etch_ui.Equipment.Models;

/// <summary>단일 모듈 상태 스냅샷 (Flask POST · 웹 · AI feature).</summary>
public sealed class ModuleStateSnapshot
{
    public EquipmentModuleId ModuleId { get; init; }
    public ModuleOperationalState State { get; init; }
    public bool? DoorClosed { get; init; }
    public bool? HasWafer { get; init; }
    public string? Detail { get; init; }

    /// <summary>JSON 직렬화용 (camelCase id).</summary>
    public string Id => ModuleId.ToString();

    public string StateText => State.ToString();
}
