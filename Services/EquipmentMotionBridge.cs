using System.Windows.Media;
using etch_ui.Equipment.Helpers;
using etch_ui.Equipment.Models;
using etch_ui.Equipment.ViewModels;
using etch_ui.Services.Scheduling;
using etch_ui.Services.Simulation;

namespace etch_ui.Services;

/// <summary>
/// 가상 장비 도식(TM·챔버·FOUP) 갱신.
/// 실장비: Load Lock 접촉 센서만 도어 표시. TM·챔버 도어·이송은 <see cref="TmTransferSimulator"/>.
/// </summary>
public sealed class EquipmentMotionBridge
{
    private readonly EquipmentMotionViewModel _motion;
    private int _hwPollTick;

    public EquipmentMotionBridge(EquipmentMotionViewModel motion)
    {
        _motion = motion;
    }

    public void Sync(
        bool loadLockContactClosed,
        bool loadLockContactValid,
        string equipmentState,
        bool lampReady,
        bool lampRun,
        bool lampWarn,
        bool lampAlarm,
        TmTransferSimulator? transfer,
        IReadOnlyList<ModuleStateSnapshot>? moduleSnapshots = null)
    {
        if (moduleSnapshots is { Count: > 0 })
        {
            _motion.ApplyModuleSnapshots(moduleSnapshots);
        }

        _hwPollTick++;

        bool running = equipmentState.Equals("RUNNING", StringComparison.OrdinalIgnoreCase);
        bool warning = equipmentState.Equals("WARNING", StringComparison.OrdinalIgnoreCase);
        bool ready = equipmentState.Equals("READY", StringComparison.OrdinalIgnoreCase);

        if (!loadLockContactValid)
        {
            _motion.LoadLockDoorClosed = true;
        }
        else
        {
            _motion.LoadLockDoorClosed = loadLockContactClosed;
        }

        if (transfer is not null)
        {
            SyncTransferMotion(transfer, equipmentState);
            return;
        }

        UpdateChamberLamps(running, warning, ready, lampRun);
        _motion.ResetWaferInventory();

        _motion.VacuumBladeCapacity = EquipmentCapacityConfig.Default.VacuumBladeSlotCount;
        _motion.EfemBladeCapacity = EquipmentCapacityConfig.Default.EfemBladeSlotCount;
        _motion.VacuumBladeSlotA = false;
        _motion.VacuumBladeSlotB = false;
        _motion.EfemBladeSlotA = false;
        _motion.EfemBladeSlotB = false;
        _motion.EfemActiveBladeSlot = VacuumDualBladePlanner.FrontBladeSlot;

        _motion.ChamberADoorClosed = true;
        _motion.ChamberBDoorClosed = true;
        _motion.ChamberCDoorClosed = true;
        _motion.ChamberDDoorClosed = true;
        _motion.FoupAHasWafer = false;
        _motion.FoupBHasWafer = false;
        _motion.FoupCHasWafer = false;
        _motion.AlignerHasWafer = false;
        _motion.LoadLockHasWafer = false;
        _motion.SideStorageHasWafer = false;
        _motion.ExternalProcessHasWafer = false;
        _motion.ChamberAHasWafer = false;
        _motion.ChamberBHasWafer = false;
        _motion.ChamberCHasWafer = false;
        _motion.ChamberDHasWafer = false;

        if (running)
        {
            _motion.SetTargets(EquipmentRegion.TM, 0.65, false, hardwareMode: false, TransferRobotKind.VacuumTm);
            _motion.ServoHint = "가상 TM · RUNNING (이송 대기)";
        }
        else if (ready)
        {
            _motion.SetTargets(EquipmentRegion.ChamberB, 0.65, false, hardwareMode: false, TransferRobotKind.VacuumTm);
            _motion.ServoHint = "가상 TM · READY";
        }
        else
        {
            ParkVirtualTmHome(loadLockContactValid);
        }
    }

    /// <summary>가상 이송 도식·웨이퍼·TM 목표 (1Hz Sync + 16ms 모션 프레임 공용).</summary>
    public void SyncTransferMotion(TmTransferSimulator transfer, string equipmentState)
    {
        bool running = equipmentState.Equals("RUNNING", StringComparison.OrdinalIgnoreCase);
        bool warning = equipmentState.Equals("WARNING", StringComparison.OrdinalIgnoreCase);
        bool operational = running || warning;

        _motion.ApplyWaferInventory(transfer.ClusterState);
        _motion.VacuumBladeCapacity = transfer.VacuumBladeCapacity;
        _motion.EfemBladeCapacity = transfer.EfemBladeCapacity;
        bool vacActive = transfer.IsVacuumBusy || transfer.CarryingWafer;
        bool efemActive = transfer.IsEfemBusy || transfer.EfemCarryingWafer;
        _motion.SetDualRobotTargets(
            efemActive ? transfer.EfemRegion : null,
            transfer.IsEfemBusy ? transfer.EfemExtension : 0.65,
            transfer.EfemCarryingWafer,
            vacActive ? transfer.TmRegion : null,
            vacActive ? transfer.BladeExtension : 0.65,
            transfer.CarryingWafer,
            hardwareMode: false,
            transfer.EfemActiveBladeSlot,
            transfer.VacuumActiveBladeSlot);
        if (efemActive)
        {
            _motion.ApplyEfemMotion(
                transfer.IsEfemBusy ? transfer.EfemRegion : EquipmentRegion.EfemRobot,
                transfer.IsEfemBusy ? transfer.EfemExtension : 0.65,
                transfer.EfemCarryingWafer,
                transfer.EfemFacingAngleDegrees,
                transfer.EfemActiveBladeSlot);
        }

        _motion.ApplyVacuumMotion(
            vacActive ? transfer.TmRegion : EquipmentRegion.TM,
            vacActive ? transfer.BladeExtension : 0.65,
            transfer.CarryingWafer,
            transfer.VacuumFacingAngleDegrees,
            transfer.VacuumActiveBladeSlot,
            transfer.VacuumIsRotatingBlade);
        _motion.VacuumBladeSlotA = transfer.VacuumCarryingSlotA;
        _motion.VacuumBladeSlotB = transfer.VacuumCarryingSlotB;
        _motion.EfemBladeSlotA = transfer.EfemCarryingSlotA;
        _motion.EfemBladeSlotB = transfer.EfemCarryingSlotB;
        _motion.EfemActiveBladeSlot = transfer.EfemActiveBladeSlot;
        _motion.IsEfemRobotActive = efemActive;
        _motion.IsVacuumTmActive = vacActive;
        _motion.TmRegionLabel = FormatDualRobotLabel(transfer);

        _motion.FoupAHasWafer = transfer.HasWaferAt(EquipmentRegion.FoupA);
        _motion.FoupBHasWafer = transfer.HasWaferAt(EquipmentRegion.FoupB);
        _motion.FoupCHasWafer = transfer.HasWaferAt(EquipmentRegion.FoupC);
        _motion.AlignerHasWafer = transfer.HasWaferAt(EquipmentRegion.Aligner);
        _motion.LoadLockHasWafer = transfer.HasWaferAt(EquipmentRegion.LoadLock);
        _motion.SideStorageHasWafer = transfer.HasWaferAt(EquipmentRegion.SideStorage);
        _motion.ExternalProcessHasWafer = transfer.HasWaferAt(EquipmentRegion.SideStorage);
        _motion.ChamberADoorClosed = transfer.IsVirtualDoorClosed(EquipmentRegion.ChamberA);
        _motion.ChamberBDoorClosed = transfer.IsVirtualDoorClosed(EquipmentRegion.ChamberB);
        _motion.ChamberCDoorClosed = transfer.IsVirtualDoorClosed(EquipmentRegion.ChamberC);
        _motion.ChamberDDoorClosed = transfer.IsVirtualDoorClosed(EquipmentRegion.ChamberD);

        _motion.ChamberAHasWafer = transfer.HasWaferAt(EquipmentRegion.ChamberA);
        _motion.ChamberBHasWafer = transfer.HasWaferAt(EquipmentRegion.ChamberB);
        _motion.ChamberCHasWafer = transfer.HasWaferAt(EquipmentRegion.ChamberC);
        _motion.ChamberDHasWafer = transfer.HasWaferAt(EquipmentRegion.ChamberD);
        bool alarm = equipmentState.Equals("ALARM", StringComparison.OrdinalIgnoreCase);
        _motion.ServoHint = alarm
            ? $"알람 · 이송 정지 · {transfer.PhaseHint}"
            : transfer.PhaseHint;
        UpdateChamberLampsFromTransfer(transfer, operational && !alarm);
    }

    private void ParkVirtualTmHome(bool loadLockContactValid)
    {
        _motion.SetDualRobotTargets(null, 0.65, false, null, 0.65, false, hardwareMode: false);
        _motion.SetTargets(EquipmentRegion.TM, 0.65, false, hardwareMode: false, TransferRobotKind.VacuumTm);
        _motion.ServoHint = loadLockContactValid
            ? "정지 · TM 홈 (Load Lock 측)"
            : "정지 · TM 홈 (접촉 미측정)";
    }

    private void UpdateChamberLamps(bool running, bool warning, bool ready, bool lampRun)
    {
        if (running || lampRun)
        {
            _motion.SetChamberLamp(0, ChamberLampVisual.Processing);
            _motion.SetChamberLamp(1, ChamberLampVisual.Processing);
            _motion.SetChamberLamp(2, ChamberLampVisual.Processing);
            _motion.SetChamberLamp(3, ChamberLampVisual.Processing);
            return;
        }

        if (warning)
        {
            bool on = (_hwPollTick / 10) % 2 == 0;
            _motion.SetChamberLamp(0, on ? ChamberLampVisual.CompletedBlinkOn : ChamberLampVisual.CompletedBlinkOff);
            _motion.SetChamberLamp(1, on ? ChamberLampVisual.CompletedBlinkOn : ChamberLampVisual.CompletedBlinkOff);
            _motion.SetChamberLamp(2, on ? ChamberLampVisual.CompletedBlinkOn : ChamberLampVisual.CompletedBlinkOff);
            _motion.SetChamberLamp(3, on ? ChamberLampVisual.CompletedBlinkOn : ChamberLampVisual.CompletedBlinkOff);
            return;
        }

        if (ready)
        {
            _motion.SetChamberLamp(1, ChamberLampVisual.Processing);
            _motion.SetChamberLamp(0, ChamberLampVisual.Off);
            _motion.SetChamberLamp(2, ChamberLampVisual.Off);
            _motion.SetChamberLamp(3, ChamberLampVisual.Off);
            return;
        }

        _motion.SetChamberLamp(0, ChamberLampVisual.Off);
        _motion.SetChamberLamp(1, ChamberLampVisual.Off);
        _motion.SetChamberLamp(2, ChamberLampVisual.Off);
        _motion.SetChamberLamp(3, ChamberLampVisual.Off);
    }

    private void UpdateChamberLampsFromTransfer(TmTransferSimulator transfer, bool globalOperational)
    {
        ClusterEquipmentState state = transfer.ClusterState;
        bool etchLineEngaged = EtchPmSelector.IsEtchLineEngaged(state, globalOperational);

        SetChamberLampFromState(0, EquipmentRegion.ChamberA, state, isEtchPm: false, etchLineEngaged, globalOperational);
        SetChamberLampFromState(1, EquipmentRegion.ChamberB, state, isEtchPm: true, etchLineEngaged, globalOperational);
        SetChamberLampFromState(2, EquipmentRegion.ChamberC, state, isEtchPm: true, etchLineEngaged, globalOperational);
        SetChamberLampFromState(3, EquipmentRegion.ChamberD, state, isEtchPm: true, etchLineEngaged, globalOperational);
    }

    private void SetChamberLampFromState(
        int chamberIndex,
        EquipmentRegion region,
        ClusterEquipmentState state,
        bool isEtchPm,
        bool etchLineEngaged,
        bool globalOperational)
    {
        PmChamberState? chamber = state.GetChamber(region);
        if (chamber?.CurrentWafer is not null && chamber.RemainingProcessTicks > 0)
        {
            _motion.SetChamberLamp(chamberIndex, ChamberLampVisual.Processing);
            return;
        }

        if (chamber?.CurrentWafer is not null)
        {
            _motion.SetChamberLamp(chamberIndex, ChamberLampVisual.CompletedBlinkOn);
            return;
        }

        if (isEtchPm && etchLineEngaged && EtchPmSelector.IsNextPipelineReadySlot(region, state, globalOperational))
        {
            _motion.SetChamberLamp(chamberIndex, ChamberLampVisual.Ready);
            return;
        }

        if (!isEtchPm && globalOperational && chamber?.CurrentWafer is null)
        {
            _motion.SetChamberLamp(chamberIndex, ChamberLampVisual.Off);
            return;
        }

        _motion.SetChamberLamp(chamberIndex, ChamberLampVisual.Off);
    }

    private static string FormatDualRobotLabel(TmTransferSimulator transfer)
    {
        string efem = transfer.IsEfemBusy
            ? $"EFEM·TM → {RegionAngleHelper.FormatLabel(transfer.EfemRegion, TransferRobotKind.EfemAtmospheric)}"
            : "EFEM·TM · 대기";
        string vac = transfer.IsVacuumBusy
            ? $"진공 TM → {RegionAngleHelper.FormatLabel(transfer.TmRegion, TransferRobotKind.VacuumTm)}"
            : "진공 TM · 대기";
        return $"{efem}  |  {vac}";
    }
}
