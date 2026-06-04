using System.Windows.Media;
using etch_ui.Equipment.Helpers;
using etch_ui.Equipment.Models;
using etch_ui.Services.Scheduling;
using etch_ui.ViewModels;

namespace etch_ui.Equipment.ViewModels;

/// <summary>장비 도식(TM·챔버·FOUP) 바인딩 — 16ms 보간은 EquipmentMotionAnimator가 갱신.</summary>
public sealed class EquipmentMotionViewModel : ViewModelBase
{
    private double _vacuumBladeAngleDegrees;
    private double _vacuumBladeExtension = 0.65;
    private bool _vacuumCarryingWafer;
    private double _efemBladeAngleDegrees;
    private double _efemBladeExtension = 0.65;
    private bool _efemCarryingWafer;
    private Brush _waferBrush = Brushes.Wheat;
    private string _tmRegionLabel = "TM";
    private string _servoHint = "시뮬/논리";
    private bool _isEfemRobotActive;
    private bool _isVacuumTmActive;

    private bool _loadLockDoorClosed = true;
    private bool _chamberADoorClosed = true;
    private bool _chamberBDoorClosed = true;
    private bool _chamberCDoorClosed = true;
    private bool _chamberDDoorClosed = true;
    private bool _foupAHasWafer;
    private bool _foupBHasWafer;
    private bool _foupCHasWafer;
    private bool _alignerHasWafer;
    private bool _sideStorageHasWafer;
    private bool _externalProcessHasWafer;
    private bool _chamberAHasWafer;
    private bool _chamberBHasWafer;
    private bool _chamberCHasWafer;
    private bool _chamberDHasWafer;
    private Brush _chamberALampBrush = new SolidColorBrush(Color.FromRgb(230, 230, 235));
    private Brush _chamberBLampBrush = new SolidColorBrush(Color.FromRgb(230, 230, 235));
    private Brush _chamberCLampBrush = new SolidColorBrush(Color.FromRgb(230, 230, 235));
    private Brush _chamberDLampBrush = new SolidColorBrush(Color.FromRgb(230, 230, 235));
    private int _vacuumBladeCapacity = 1;
    private int _efemBladeCapacity = 1;
    private string _foup1InventoryText = "—";
    private string _foup2InventoryText = "—";
    private string _foup3InventoryText = "—";
    private string _alignerInventoryText = "—";
    private string _sideStorageInventoryText = "—";
    private string _waferInventorySummary = "웨이퍼 잔량 · RUNNING 후 표시";

    public double VacuumBladeAngleDegrees
    {
        get => _vacuumBladeAngleDegrees;
        set => SetField(ref _vacuumBladeAngleDegrees, value);
    }

    public double VacuumBladeExtension
    {
        get => _vacuumBladeExtension;
        set => SetField(ref _vacuumBladeExtension, value);
    }

    public bool VacuumCarryingWafer
    {
        get => _vacuumCarryingWafer;
        set => SetField(ref _vacuumCarryingWafer, value);
    }

    public double EfemBladeAngleDegrees
    {
        get => _efemBladeAngleDegrees;
        set => SetField(ref _efemBladeAngleDegrees, value);
    }

    public double EfemBladeExtension
    {
        get => _efemBladeExtension;
        set => SetField(ref _efemBladeExtension, value);
    }

    public bool EfemCarryingWafer
    {
        get => _efemCarryingWafer;
        set => SetField(ref _efemCarryingWafer, value);
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

    /// <summary>true면 EFEM TM이 이송 중(도식 강조).</summary>
    public bool IsEfemRobotActive
    {
        get => _isEfemRobotActive;
        set => SetField(ref _isEfemRobotActive, value);
    }

    /// <summary>true면 진공 TM이 이송 중(도식 강조).</summary>
    public bool IsVacuumTmActive
    {
        get => _isVacuumTmActive;
        set => SetField(ref _isVacuumTmActive, value);
    }

    public bool LoadLockDoorClosed
    {
        get => _loadLockDoorClosed;
        set => SetField(ref _loadLockDoorClosed, value);
    }

    public bool ChamberADoorClosed { get => _chamberADoorClosed; set => SetField(ref _chamberADoorClosed, value); }
    public bool ChamberBDoorClosed { get => _chamberBDoorClosed; set => SetField(ref _chamberBDoorClosed, value); }
    public bool ChamberCDoorClosed { get => _chamberCDoorClosed; set => SetField(ref _chamberCDoorClosed, value); }
    public bool ChamberDDoorClosed { get => _chamberDDoorClosed; set => SetField(ref _chamberDDoorClosed, value); }

    public bool FoupAHasWafer { get => _foupAHasWafer; set => SetField(ref _foupAHasWafer, value); }
    public bool FoupBHasWafer { get => _foupBHasWafer; set => SetField(ref _foupBHasWafer, value); }
    public bool FoupCHasWafer { get => _foupCHasWafer; set => SetField(ref _foupCHasWafer, value); }
    public bool AlignerHasWafer { get => _alignerHasWafer; set => SetField(ref _alignerHasWafer, value); }
    public bool SideStorageHasWafer { get => _sideStorageHasWafer; set => SetField(ref _sideStorageHasWafer, value); }
    public bool ExternalProcessHasWafer { get => _externalProcessHasWafer; set => SetField(ref _externalProcessHasWafer, value); }

    public bool ChamberAHasWafer { get => _chamberAHasWafer; set => SetField(ref _chamberAHasWafer, value); }
    public bool ChamberBHasWafer { get => _chamberBHasWafer; set => SetField(ref _chamberBHasWafer, value); }
    public bool ChamberCHasWafer { get => _chamberCHasWafer; set => SetField(ref _chamberCHasWafer, value); }
    public bool ChamberDHasWafer { get => _chamberDHasWafer; set => SetField(ref _chamberDHasWafer, value); }

    public Brush ChamberALampBrush { get => _chamberALampBrush; set => SetField(ref _chamberALampBrush, value); }
    public Brush ChamberBLampBrush { get => _chamberBLampBrush; set => SetField(ref _chamberBLampBrush, value); }
    public Brush ChamberCLampBrush { get => _chamberCLampBrush; set => SetField(ref _chamberCLampBrush, value); }
    public Brush ChamberDLampBrush { get => _chamberDLampBrush; set => SetField(ref _chamberDLampBrush, value); }
    public int VacuumBladeCapacity
    {
        get => _vacuumBladeCapacity;
        set
        {
            if (SetField(ref _vacuumBladeCapacity, value))
            {
                Raise(nameof(IsVacuumDualBlade));
            }
        }
    }

    public int EfemBladeCapacity
    {
        get => _efemBladeCapacity;
        set
        {
            if (SetField(ref _efemBladeCapacity, value))
            {
                Raise(nameof(IsEfemDualBlade));
            }
        }
    }

    public bool IsVacuumDualBlade => VacuumBladeCapacity >= 2;
    public bool IsEfemDualBlade => EfemBladeCapacity >= 2;

    public string Foup1InventoryText { get => _foup1InventoryText; set => SetField(ref _foup1InventoryText, value); }
    public string Foup2InventoryText { get => _foup2InventoryText; set => SetField(ref _foup2InventoryText, value); }
    public string Foup3InventoryText { get => _foup3InventoryText; set => SetField(ref _foup3InventoryText, value); }
    public string AlignerInventoryText { get => _alignerInventoryText; set => SetField(ref _alignerInventoryText, value); }
    public string SideStorageInventoryText { get => _sideStorageInventoryText; set => SetField(ref _sideStorageInventoryText, value); }
    public string WaferInventorySummary { get => _waferInventorySummary; set => SetField(ref _waferInventorySummary, value); }

    public void ApplyWaferInventory(ClusterEquipmentState state)
    {
        int foupMax = state.Capacity.FoupSlotCount;
        int alignMax = state.Capacity.AlignerSlotCount;
        int sideMax = state.Capacity.SideStorageSlotCount;

        Foup1InventoryText = FormatSlot(state.FoupPorts[0].RemainingInFoup, foupMax);
        Foup2InventoryText = FormatSlot(state.FoupPorts[1].RemainingInFoup, foupMax);
        Foup3InventoryText = FormatSlot(state.FoupPorts[2].RemainingInFoup, foupMax);
        AlignerInventoryText = FormatSlot(state.AlignerBuffer.Count, alignMax);
        SideStorageInventoryText = FormatSlot(state.SideStorage.Count, sideMax);

        WaferInventorySummary =
            $"LP1 {Foup1InventoryText} · LP2 {Foup2InventoryText} · LP3 {Foup3InventoryText}  |  "
            + $"Aligner {AlignerInventoryText}  |  Side Stg {SideStorageInventoryText}";
    }

    public void ResetWaferInventory()
    {
        Foup1InventoryText = "—";
        Foup2InventoryText = "—";
        Foup3InventoryText = "—";
        AlignerInventoryText = "—";
        SideStorageInventoryText = "—";
        WaferInventorySummary = "웨이퍼 잔량 · RUNNING 후 표시";
    }

    private static string FormatSlot(int count, int capacity) => $"{count}/{capacity}";

    /// <summary>로봇별 목표값 (다른 로봇 상태는 유지).</summary>
    public void SetRobotTargets(
        TransferRobotKind robot,
        EquipmentRegion region,
        double extension,
        bool carrying,
        bool hardwareMode)
    {
        double angle = RegionAngleHelper.ToDegrees(region, robot, hardwareMode);
        if (robot == TransferRobotKind.EfemAtmospheric)
        {
            EfemTargetAngleDegrees = angle;
            EfemTargetExtension = extension;
            EfemTargetCarrying = carrying;
        }
        else
        {
            VacuumTargetAngleDegrees = angle;
            VacuumTargetExtension = extension;
            VacuumTargetCarrying = carrying;
        }

        IsEfemRobotActive = robot == TransferRobotKind.EfemAtmospheric;
        TmRegionLabel = RegionAngleHelper.FormatLabel(region, robot);
    }

    /// <summary>목표값 설정(애니메이터가 현재값으로 보간) — 단일 로봇 모드.</summary>
    public void SetTargets(
        EquipmentRegion region,
        double extension,
        bool carrying,
        bool hardwareMode,
        TransferRobotKind robot)
    {
        TargetRegion = region;
        TargetRobot = robot;
        SetRobotTargets(robot, region, extension, carrying, hardwareMode);
        if (robot == TransferRobotKind.EfemAtmospheric)
        {
            VacuumTargetCarrying = false;
        }
        else
        {
            EfemTargetCarrying = false;
        }
    }

    /// <summary>EFEM·진공 TM 동시 이송 목표.</summary>
    public void SetDualRobotTargets(
        EquipmentRegion? efemRegion,
        double efemExtension,
        bool efemCarrying,
        EquipmentRegion? vacuumRegion,
        double vacuumExtension,
        bool vacuumCarrying,
        bool hardwareMode)
    {
        if (efemRegion is not null)
        {
            SetRobotTargets(
                TransferRobotKind.EfemAtmospheric,
                efemRegion.Value,
                efemExtension,
                efemCarrying,
                hardwareMode);
        }
        else
        {
            EfemTargetCarrying = false;
        }

        if (vacuumRegion is not null)
        {
            SetRobotTargets(
                TransferRobotKind.VacuumTm,
                vacuumRegion.Value,
                vacuumExtension,
                vacuumCarrying,
                hardwareMode);
        }
        else
        {
            VacuumTargetCarrying = false;
        }

        IsEfemRobotActive = efemRegion is not null;
        if (efemRegion is not null && vacuumRegion is null)
        {
            TmRegionLabel = RegionAngleHelper.FormatLabel(efemRegion.Value, TransferRobotKind.EfemAtmospheric);
        }
        else if (vacuumRegion is not null)
        {
            TmRegionLabel = RegionAngleHelper.FormatLabel(vacuumRegion.Value, TransferRobotKind.VacuumTm);
        }
    }

    internal EquipmentRegion TargetRegion { get; private set; } = EquipmentRegion.TM;
    internal TransferRobotKind TargetRobot { get; private set; } = TransferRobotKind.VacuumTm;
    internal double VacuumTargetAngleDegrees { get; private set; } = -125;
    internal double VacuumTargetExtension { get; private set; } = 0.65;
    internal bool VacuumTargetCarrying { get; private set; }
    internal double EfemTargetAngleDegrees { get; private set; } = -90;
    internal double EfemTargetExtension { get; private set; } = 0.65;
    internal bool EfemTargetCarrying { get; private set; }

    internal void ApplyInterpolatedFrame(
        double vacuumAngleDeg,
        double vacuumExtension,
        bool vacuumCarrying,
        double efemAngleDeg,
        double efemExtension,
        bool efemCarrying)
    {
        VacuumBladeAngleDegrees = vacuumAngleDeg;
        VacuumBladeExtension = vacuumExtension;
        VacuumCarryingWafer = vacuumCarrying;

        EfemBladeAngleDegrees = efemAngleDeg;
        EfemBladeExtension = efemExtension;
        EfemCarryingWafer = efemCarrying;
    }

    public void SetChamberLamp(int chamberIndex, ChamberLampVisual visual)
    {
        Brush brush = visual switch
        {
            ChamberLampVisual.Ready => new SolidColorBrush(Color.FromRgb(37, 99, 235)),
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
            case 3: ChamberDLampBrush = brush; break;
        }
    }

    public ModulePanelVisual EfemPanel { get; } = new();
    public ModulePanelVisual EfemRobotPanel { get; } = new();
    public ModulePanelVisual AlignerPanel { get; } = new();
    public ModulePanelVisual SideStoragePanel { get; } = new();
    public ModulePanelVisual LoadPort1Panel { get; } = new();
    public ModulePanelVisual LoadPort2Panel { get; } = new();
    public ModulePanelVisual LoadPort3Panel { get; } = new();
    public ModulePanelVisual BufferPanel { get; } = new();
    public ModulePanelVisual TmPanel { get; } = new();
    public ModulePanelVisual Pm1Panel { get; } = new();
    public ModulePanelVisual Pm2Panel { get; } = new();
    public ModulePanelVisual Pm3Panel { get; } = new();
    public ModulePanelVisual Pm4Panel { get; } = new();

    public void ApplyModuleSnapshots(IReadOnlyList<ModuleStateSnapshot> snapshots)
    {
        foreach (ModuleStateSnapshot s in snapshots)
        {
            ModulePanelVisual? panel = s.ModuleId switch
            {
                EquipmentModuleId.Efem => EfemPanel,
                EquipmentModuleId.EfemRobot => EfemRobotPanel,
                EquipmentModuleId.Aligner => AlignerPanel,
                EquipmentModuleId.SideStorage => SideStoragePanel,
                EquipmentModuleId.LoadPort1 => LoadPort1Panel,
                EquipmentModuleId.LoadPort2 => LoadPort2Panel,
                EquipmentModuleId.LoadPort3 => LoadPort3Panel,
                EquipmentModuleId.BufferModule => BufferPanel,
                EquipmentModuleId.TransferModule => TmPanel,
                EquipmentModuleId.Pm1 => Pm1Panel,
                EquipmentModuleId.Pm2 => Pm2Panel,
                EquipmentModuleId.Pm3 => Pm3Panel,
                EquipmentModuleId.Pm4 => Pm4Panel,
                _ => null
            };
            if (panel != null)
            {
                panel.State = s.State;
            }
        }
    }
}
