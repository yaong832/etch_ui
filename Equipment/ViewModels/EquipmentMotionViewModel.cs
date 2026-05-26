using System.Windows.Media;
using etch_ui.Equipment.Helpers;
using etch_ui.Equipment.Models;
using etch_ui.ViewModels;

namespace etch_ui.Equipment.ViewModels;

/// <summary>장비 도식(TM·챔버·FOUP) 바인딩 — 16ms 보간은 EquipmentMotionAnimator가 갱신.</summary>
public sealed class EquipmentMotionViewModel : ViewModelBase
{
    private double _bladeAngleDegrees;
    private double _bladeExtension = 0.65;
    private bool _carryingWafer;
    private Brush _waferBrush = Brushes.Wheat;
    private string _tmRegionLabel = "TM";
    private string _servoHint = "시뮬/논리";

    private bool _loadLockDoorClosed = true;
    private bool _chamberADoorClosed = true;
    private bool _chamberBDoorClosed = true;
    private bool _chamberCDoorClosed = true;
    private bool _chamberAHasWafer;
    private bool _chamberBHasWafer;
    private bool _chamberCHasWafer;
    private Brush _chamberALampBrush = new SolidColorBrush(Color.FromRgb(230, 230, 235));
    private Brush _chamberBLampBrush = new SolidColorBrush(Color.FromRgb(230, 230, 235));
    private Brush _chamberCLampBrush = new SolidColorBrush(Color.FromRgb(230, 230, 235));

    public double BladeAngleDegrees
    {
        get => _bladeAngleDegrees;
        set => SetField(ref _bladeAngleDegrees, value);
    }

    public double BladeExtension
    {
        get => _bladeExtension;
        set => SetField(ref _bladeExtension, value);
    }

    public bool CarryingWafer
    {
        get => _carryingWafer;
        set => SetField(ref _carryingWafer, value);
    }

    public Brush WaferBrush
    {
        get => _waferBrush;
        set => SetField(ref _waferBrush, value);
    }

    public string TmRegionLabel
    {
        get => _tmRegionLabel;
        set => SetField(ref _tmRegionLabel, value);
    }

    public string ServoHint
    {
        get => _servoHint;
        set => SetField(ref _servoHint, value);
    }

    public bool LoadLockDoorClosed
    {
        get => _loadLockDoorClosed;
        set => SetField(ref _loadLockDoorClosed, value);
    }

    public bool ChamberADoorClosed { get => _chamberADoorClosed; set => SetField(ref _chamberADoorClosed, value); }
    public bool ChamberBDoorClosed { get => _chamberBDoorClosed; set => SetField(ref _chamberBDoorClosed, value); }
    public bool ChamberCDoorClosed { get => _chamberCDoorClosed; set => SetField(ref _chamberCDoorClosed, value); }

    public bool ChamberAHasWafer { get => _chamberAHasWafer; set => SetField(ref _chamberAHasWafer, value); }
    public bool ChamberBHasWafer { get => _chamberBHasWafer; set => SetField(ref _chamberBHasWafer, value); }
    public bool ChamberCHasWafer { get => _chamberCHasWafer; set => SetField(ref _chamberCHasWafer, value); }

    public Brush ChamberALampBrush { get => _chamberALampBrush; set => SetField(ref _chamberALampBrush, value); }
    public Brush ChamberBLampBrush { get => _chamberBLampBrush; set => SetField(ref _chamberBLampBrush, value); }
    public Brush ChamberCLampBrush { get => _chamberCLampBrush; set => SetField(ref _chamberCLampBrush, value); }

    /// <summary>목표값 설정(애니메이터가 현재값으로 보간).</summary>
    public void SetTargets(
        EquipmentRegion region,
        double extension,
        bool carrying,
        bool hardwareMode)
    {
        TargetRegion = region;
        TargetExtension = extension;
        TargetCarrying = carrying;
        TargetAngleDegrees = RegionAngleHelper.ToDegrees(region, hardwareMode);
        TmRegionLabel = RegionAngleHelper.FormatLabel(region);
    }

    internal EquipmentRegion TargetRegion { get; private set; } = EquipmentRegion.TM;
    internal double TargetAngleDegrees { get; private set; }
    internal double TargetExtension { get; private set; } = 0.65;
    internal bool TargetCarrying { get; private set; }

    internal void ApplyInterpolatedFrame(double angleDeg, double extension, bool carrying)
    {
        BladeAngleDegrees = angleDeg;
        BladeExtension = extension;
        CarryingWafer = carrying;
    }

    public void SetChamberLamp(int chamberIndex, ChamberLampVisual visual)
    {
        Brush brush = visual switch
        {
            ChamberLampVisual.Processing => Brushes.ForestGreen,
            ChamberLampVisual.CompletedBlinkOn => Brushes.ForestGreen,
            ChamberLampVisual.CompletedBlinkOff => new SolidColorBrush(Color.FromRgb(230, 230, 235)),
            _ => new SolidColorBrush(Color.FromRgb(230, 230, 235))
        };

        switch (chamberIndex)
        {
            case 0: ChamberALampBrush = brush; break;
            case 1: ChamberBLampBrush = brush; break;
            case 2: ChamberCLampBrush = brush; break;
        }
    }
}
