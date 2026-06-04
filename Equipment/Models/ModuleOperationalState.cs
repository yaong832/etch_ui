namespace etch_ui.Equipment.Models;

/// <summary>모듈별 공통 운전 상태 (WPF·Flask·AI 공유).</summary>
public enum ModuleOperationalState
{
    Offline,
    Idle,
    Standby,
    Ready,
    Running,
    Processing,
    Complete,
    Warning,
    Alarm,
    Maintenance
}
