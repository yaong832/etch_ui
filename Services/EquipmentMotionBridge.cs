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

        if (transfer is null)
        {
            UpdateChamberLamps(running, warning, ready, lampRun);
        }

        // Load Lock = 실제 접촉 센서 (미측정 시 닫힘 표시만 기본)
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
            _motion.ApplyWaferInventory(transfer.ClusterState);
            _motion.VacuumBladeCapacity = transfer.VacuumBladeCapacity;
            _motion.EfemBladeCapacity = transfer.EfemBladeCapacity;
            bool vacActive = transfer.IsVacuumBusy || transfer.CarryingWafer;
            _motion.SetDualRobotTargets(
                transfer.IsEfemBusy ? transfer.EfemRegion : EquipmentRegion.EfemRobot,
                transfer.IsEfemBusy ? transfer.EfemExtension : 0.65,
                transfer.IsEfemBusy && transfer.EfemCarryingWafer,
                vacActive ? transfer.TmRegion : EquipmentRegion.TM,
                vacActive ? transfer.BladeExtension : 0.65,
                transfer.CarryingWafer,
                hardwareMode: false);
            _motion.ApplyVacuumMotion(
                vacActive ? transfer.TmRegion : EquipmentRegion.TM,
                vacActive ? transfer.BladeExtension : 0.65,
                transfer.CarryingWafer,
                transfer.VacuumFacingAngleDegrees,
                transfer.VacuumActiveBladeSlot,
                transfer.VacuumIsRotatingBlade);
            _motion.VacuumBladeSlotA = transfer.VacuumCarryingSlotA;
            _motion.VacuumBladeSlotB = transfer.VacuumCarryingSlotB;
            _motion.IsEfemRobotActive = transfer.IsEfemBusy;
            _motion.IsVacuumTmActive = vacActive;
            _motion.TmRegionLabel = FormatDualRobotLabel(transfer);

            _motion.FoupAHasWafer = transfer.HasWaferAt(EquipmentRegion.FoupA);
            _motion.FoupBHasWafer = transfer.HasWaferAt(EquipmentRegion.FoupB);
            _motion.FoupCHasWafer = transfer.HasWaferAt(EquipmentRegion.FoupC);
            _motion.AlignerHasWafer = transfer.HasWaferAt(EquipmentRegion.Aligner);
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
            _motion.ServoHint = transfer.PhaseHint;
            UpdateChamberLampsFromTransfer(transfer, running);
            return;
        }

        _motion.ResetWaferInventory();

        _motion.VacuumBladeCapacity = EquipmentCapacityConfig.Default.VacuumBladeSlotCount;
        _motion.EfemBladeCapacity = 1;
        _motion.VacuumBladeSlotA = false;
        _motion.VacuumBladeSlotB = false;

        _motion.ChamberADoorClosed = true;
        _motion.ChamberBDoorClosed = true;
        _motion.ChamberCDoorClosed = true;
        _motion.ChamberDDoorClosed = true;
        _motion.FoupAHasWafer = false;
        _motion.FoupBHasWafer = false;
        _motion.FoupCHasWafer = false;
        _motion.AlignerHasWafer = false;
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
            _motion.SetTargets(EquipmentRegion.TM, 0.65, false, hardwareMode: false, TransferRobotKind.VacuumTm);
            _motion.ServoHint = loadLockContactValid
                ? "가상 TM · 접촉 센서 연동"
                : "가상 TM · 접촉 미측정";
        }
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
            _motion.SetChamberLamp(1, ChamberLampVisual.Processing);
            _motion.SetChamberLamp(2, ChamberLampVisual.Off);
            _motion.SetChamberLamp(3, ChamberLampVisual.Off);
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

    private void UpdateChamberLampsFromTransfer(TmTransferSimulator transfer, bool globalRunning)
    {
        ClusterEquipmentState state = transfer.ClusterState;
        bool etchLineEngaged = EtchPmSelector.IsEtchLineEngaged(state, globalRunning);

        SetChamberLampFromState(0, EquipmentRegion.ChamberA, state, isEtchPm: false, etchLineEngaged, globalRunning);
        SetChamberLampFromState(1, EquipmentRegion.ChamberB, state, isEtchPm: true, etchLineEngaged, globalRunning);
        SetChamberLampFromState(2, EquipmentRegion.ChamberC, state, isEtchPm: true, etchLineEngaged, globalRunning);
        SetChamberLampFromState(3, EquipmentRegion.ChamberD, state, isEtchPm: true, etchLineEngaged, globalRunning);
    }

    private void SetChamberLampFromState(
        int chamberIndex,
        EquipmentRegion region,
        ClusterEquipmentState state,
        bool isEtchPm,
        bool etchLineEngaged,
        bool globalRunning)
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

        if (isEtchPm && etchLineEngaged && EtchPmSelector.IsNextPipelineReadySlot(region, state, globalRunning))
        {
            _motion.SetChamberLamp(chamberIndex, ChamberLampVisual.Ready);
            return;
        }

        if (!isEtchPm && globalRunning && chamber?.CurrentWafer is null)
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
