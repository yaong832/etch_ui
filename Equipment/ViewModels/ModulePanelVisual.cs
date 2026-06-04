using System.Windows.Media;
using etch_ui.Equipment.Models;
using etch_ui.ViewModels;

namespace etch_ui.Equipment.ViewModels;

/// <summary>도식 위 단일 모듈 상태 뱃지·테두리.</summary>
public sealed class ModulePanelVisual : ViewModelBase
{
    private ModuleOperationalState _state = ModuleOperationalState.Idle;

    public ModuleOperationalState State
    {
        get => _state;
        set
        {
            if (SetField(ref _state, value))
            {
                Raise(nameof(StatusBrush));
                Raise(nameof(Badge));
            }
        }
    }

    public Brush StatusBrush => ModuleStateBrushes.For(_state);

    public string Badge => ModuleStateBrushes.BadgeFor(_state);
}

internal static class ModuleStateBrushes
{
    internal static Brush For(ModuleOperationalState state) =>
        state switch
        {
            ModuleOperationalState.Running or ModuleOperationalState.Processing =>
                new SolidColorBrush(Color.FromRgb(22, 163, 74)),
            ModuleOperationalState.Standby or ModuleOperationalState.Ready =>
                new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            ModuleOperationalState.Alarm => new SolidColorBrush(Color.FromRgb(220, 38, 38)),
            ModuleOperationalState.Warning => new SolidColorBrush(Color.FromRgb(217, 119, 6)),
            ModuleOperationalState.Maintenance => new SolidColorBrush(Color.FromRgb(124, 58, 237)),
            ModuleOperationalState.Offline => new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
        };

    internal static string BadgeFor(ModuleOperationalState state) =>
        state switch
        {
            ModuleOperationalState.Running => "RUN",
            ModuleOperationalState.Processing => "PROC",
            ModuleOperationalState.Standby => "STB",
            ModuleOperationalState.Ready => "RDY",
            ModuleOperationalState.Alarm => "ALM",
            ModuleOperationalState.Warning => "WRN",
            ModuleOperationalState.Maintenance => "MNT",
            ModuleOperationalState.Offline => "OFF",
            _ => "IDL"
        };
}
