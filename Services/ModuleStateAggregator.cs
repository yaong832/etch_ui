using etch_ui.Equipment.Models;
using etch_ui.Services.Scheduling;
using etch_ui.Services.Simulation;

namespace etch_ui.Services;

/// <summary>
/// 장비 전역 상태 + 가상 이송 + Load Lock 접촉 → 모듈별 상태 배열.
/// </summary>
public static class ModuleStateAggregator
{
    public sealed class Context
    {
        public required string EquipmentState { get; init; }
        public bool MaintenanceMode { get; init; }
        public bool HasLiveSensorData { get; init; }
        public bool InterlockOk { get; init; }
        public bool BenchMode { get; init; }
        public bool AccessSafe { get; init; }
        public bool AccessInputValid { get; init; }
        public string? AlarmCode { get; init; }
        public TmTransferSimulator? Transfer { get; init; }
    }

    public static IReadOnlyList<ModuleStateSnapshot> Build(Context ctx)
    {
        bool globalMaint = ctx.MaintenanceMode;
        bool globalWarning = ctx.EquipmentState.Equals("WARNING", StringComparison.OrdinalIgnoreCase);
        bool globalRunning = ctx.EquipmentState.Equals("RUNNING", StringComparison.OrdinalIgnoreCase);
        bool globalReady = ctx.EquipmentState.Equals("READY", StringComparison.OrdinalIgnoreCase);
        bool transferActive = ctx.Transfer is { IsActive: true };

        var list = new List<ModuleStateSnapshot>(13);

        // Load ports: FOUP A → LP1, LP2 spare, FOUP B → LP3
        list.Add(BuildLoadPort(EquipmentModuleId.LoadPort1, EquipmentRegion.FoupA, ctx, globalMaint, transferActive));
        list.Add(BuildLoadPort(EquipmentModuleId.LoadPort2, EquipmentRegion.FoupB, ctx, globalMaint, transferActive));
        list.Add(BuildLoadPort(EquipmentModuleId.LoadPort3, EquipmentRegion.FoupC, ctx, globalMaint, transferActive));

        list.Add(BuildBufferModule(ctx, globalMaint, globalRunning, transferActive));
        list.Add(BuildEfemRobot(ctx, globalMaint, transferActive));
        list.Add(BuildTransferModule(ctx, globalMaint, globalRunning, transferActive));
        list.Add(BuildPm(EquipmentModuleId.Pm1, EquipmentRegion.ChamberA, ctx, globalMaint, globalWarning, globalRunning, transferActive));
        list.Add(BuildPm(EquipmentModuleId.Pm2, EquipmentRegion.ChamberB, ctx, globalMaint, globalWarning, globalRunning, transferActive));
        list.Add(BuildPm(EquipmentModuleId.Pm3, EquipmentRegion.ChamberC, ctx, globalMaint, globalWarning, globalRunning, transferActive));
        list.Add(BuildPm(EquipmentModuleId.Pm4, EquipmentRegion.ChamberD, ctx, globalMaint, globalWarning, globalRunning, transferActive));

        list.Add(BuildEfem(ctx, globalMaint, globalRunning, transferActive));
        list.Add(BuildAligner(ctx, globalMaint, transferActive));
        list.Add(BuildSideStorage(ctx, globalMaint, transferActive));

        if (globalWarning)
        {
            for (int i = 0; i < list.Count; i++)
            {
                list[i] = ElevateForGlobalWarning(list[i]);
            }
        }

        return list;
    }

    /// <summary>전역 WARNING 시 알람·가동 중이 아닌 모듈을 경고(황색)로 통일.</summary>
    private static ModuleStateSnapshot ElevateForGlobalWarning(ModuleStateSnapshot snap) =>
        snap.State is ModuleOperationalState.Alarm
            or ModuleOperationalState.Maintenance
            or ModuleOperationalState.Processing
            or ModuleOperationalState.Running
            ? snap
            : new ModuleStateSnapshot
            {
                ModuleId = snap.ModuleId,
                State = ModuleOperationalState.Warning,
                DoorClosed = snap.DoorClosed,
                HasWafer = snap.HasWafer,
                Detail = snap.Detail ?? "환경·공정 경고"
            };

    /// <summary>전역 ALARM — 원인 모듈만 ALM 뱃지 (A002 BM·TM, A003 TM, A004 BM, A005/A006 EFEM 등).</summary>
    private static bool ModuleShowsAlarm(Context ctx, EquipmentModuleId moduleId)
    {
        if (!ctx.EquipmentState.Equals("ALARM", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(ctx.AlarmCode))
        {
            return false;
        }

        return ctx.AlarmCode switch
        {
            "A001" => moduleId is EquipmentModuleId.BufferModule or EquipmentModuleId.Efem,
            "A002" => moduleId is EquipmentModuleId.BufferModule or EquipmentModuleId.TransferModule,
            "A003" => moduleId is EquipmentModuleId.TransferModule,
            "A004" => moduleId is EquipmentModuleId.BufferModule,
            "A005" or "A006" => moduleId is EquipmentModuleId.Efem,
            _ => false
        };
    }

    private static ModuleStateSnapshot BuildLoadPort(
        EquipmentModuleId id,
        EquipmentRegion region,
        Context ctx,
        bool globalMaint,
        bool transferActive,
        bool forceEmpty = false)
    {
        if (globalMaint)
        {
            return Snap(id, ModuleOperationalState.Maintenance, detail: "유지보수");
        }

        if (!transferActive && forceEmpty)
        {
            return Snap(id, ModuleOperationalState.Idle, hasWafer: false, detail: "대기");
        }

        bool hasWafer = !forceEmpty && ctx.Transfer is not null && ctx.Transfer.HasWaferAt(region);
        bool pickupOpen = ctx.Transfer is not null
                          && !ctx.Transfer.IsVirtualDoorClosed(region)
                          && ctx.Transfer.IsActive;

        ModuleOperationalState st = ModuleShowsAlarm(ctx, id) ? ModuleOperationalState.Alarm
            : pickupOpen ? ModuleOperationalState.Running
            : hasWafer ? ModuleOperationalState.Standby
            : ModuleOperationalState.Idle;

        string? foupDetail = BuildFoupDetail(ctx, region, pickupOpen);
        string? alarmDetail = st == ModuleOperationalState.Alarm ? ctx.AlarmCode : null;
        return Snap(id, st, hasWafer: hasWafer, detail: alarmDetail ?? foupDetail);
    }

    private static string? BuildFoupDetail(Context ctx, EquipmentRegion region, bool pickupOpen)
    {
        if (ctx.Transfer is null)
        {
            return pickupOpen ? "픽업 도어 열림(가상)" : null;
        }

        int? idx = region switch
        {
            EquipmentRegion.FoupA => 0,
            EquipmentRegion.FoupB => 1,
            EquipmentRegion.FoupC => 2,
            _ => null
        };
        if (idx is null)
        {
            return pickupOpen ? "픽업 도어 열림(가상)" : null;
        }

        var ports = ctx.Transfer.ClusterState.FoupPorts;
        if (idx.Value < 0 || idx.Value >= ports.Length)
        {
            return pickupOpen ? "픽업 도어 열림(가상)" : null;
        }

        var p = ports[idx.Value];
        string flow = $"잔량 {p.RemainingInFoup} / 25 · InFlight {p.InFlightCount}";
        return pickupOpen ? $"픽업 도어 열림(가상) · {flow}" : flow;
    }

    private static ModuleStateSnapshot BuildBufferModule(
        Context ctx,
        bool globalMaint,
        bool globalRunning,
        bool transferActive)
    {
        if (globalMaint)
        {
            return Snap(EquipmentModuleId.BufferModule, ModuleOperationalState.Maintenance, detail: "유지보수");
        }

        if (ctx.BenchMode)
        {
            bool demoDoorClosed = ctx.Transfer is null || ctx.Transfer.IsVirtualDoorClosed(EquipmentRegion.LoadLock);
            int demoBmCount = ctx.Transfer?.ClusterState.LoadLockBuffer.Count ?? 0;
            int demoBmCap = ctx.Transfer?.ClusterState.Capacity.LoadLockSlotCount ?? 2;
            if (ModuleShowsAlarm(ctx, EquipmentModuleId.BufferModule))
            {
                return Snap(EquipmentModuleId.BufferModule, ModuleOperationalState.Alarm,
                    doorClosed: demoDoorClosed,
                    hasWafer: demoBmCount > 0,
                    detail: ctx.AlarmCode);
            }

            return Snap(EquipmentModuleId.BufferModule, ModuleOperationalState.Standby,
                doorClosed: demoDoorClosed,
                hasWafer: demoBmCount > 0,
                detail: $"데모 · 슬롯 {demoBmCount}/{demoBmCap}");
        }

        if (!ctx.HasLiveSensorData)
        {
            return Snap(EquipmentModuleId.BufferModule, ModuleOperationalState.Offline,
                doorClosed: null, detail: "접촉 미측정");
        }

        bool doorClosed = ctx.Transfer is null || ctx.Transfer.IsVirtualDoorClosed(EquipmentRegion.LoadLock);
        bool doorPhase = transferActive && !doorClosed;
        int bmCount = ctx.Transfer?.ClusterState.LoadLockBuffer.Count ?? 0;
        int bmCap = ctx.Transfer?.ClusterState.Capacity.LoadLockSlotCount ?? 2;
        bool hasWafer = bmCount > 0;

        bool accessFault = ctx.HasLiveSensorData && ctx.AccessInputValid && !ctx.AccessSafe;
        ModuleOperationalState st = ModuleShowsAlarm(ctx, EquipmentModuleId.BufferModule) || accessFault
            ? ModuleOperationalState.Alarm
            : globalRunning || transferActive ? ModuleOperationalState.Running
            : ModuleOperationalState.Standby;

        string? detail = ctx.Transfer is not null
            ? LoadLockAdmissionPolicy.DescribeBmStatus(ctx.Transfer.ClusterState)
            : ctx.AccessInputValid
                ? (ctx.AccessSafe ? $"접촉 닫힘 · 슬롯 {bmCount}/{bmCap}" : "접촉 열림(실측)")
                : "접촉 미측정";

        if (doorPhase)
        {
            detail = "슬릿 열림(가상) · " + detail;
        }

        return Snap(EquipmentModuleId.BufferModule, st,
            doorClosed: doorClosed,
            hasWafer: hasWafer,
            detail: detail);
    }

    private static ModuleStateSnapshot BuildEfemRobot(
        Context ctx,
        bool globalMaint,
        bool transferActive)
    {
        if (globalMaint)
        {
            return Snap(EquipmentModuleId.EfemRobot, ModuleOperationalState.Maintenance, detail: "유지보수");
        }

        if (ModuleShowsAlarm(ctx, EquipmentModuleId.EfemRobot))
        {
            return Snap(EquipmentModuleId.EfemRobot, ModuleOperationalState.Alarm, detail: ctx.AlarmCode);
        }

        if (ctx.Transfer is { IsEfemBusy: true } transfer)
        {
            return Snap(EquipmentModuleId.EfemRobot, ModuleOperationalState.Running,
                hasWafer: transfer.EfemCarryingWafer,
                detail: $"대기압 TM · {transfer.EfemRegion}");
        }

        return Snap(EquipmentModuleId.EfemRobot, ModuleOperationalState.Standby, detail: "EFEM TM 대기");
    }

    private static ModuleStateSnapshot BuildTransferModule(
        Context ctx,
        bool globalMaint,
        bool globalRunning,
        bool transferActive)
    {
        if (globalMaint)
        {
            return Snap(EquipmentModuleId.TransferModule, ModuleOperationalState.Maintenance);
        }

        if (ModuleShowsAlarm(ctx, EquipmentModuleId.TransferModule))
        {
            return Snap(EquipmentModuleId.TransferModule, ModuleOperationalState.Alarm,
                hasWafer: ctx.Transfer?.CarryingWafer,
                detail: ctx.AlarmCode);
        }

        if (ctx.Transfer is { IsVacuumBusy: true } transfer)
        {
            return Snap(EquipmentModuleId.TransferModule, ModuleOperationalState.Running,
                hasWafer: transfer.CarryingWafer,
                detail: $"진공 TM · {transfer.TmRegion}");
        }

        if (globalRunning)
        {
            return Snap(EquipmentModuleId.TransferModule, ModuleOperationalState.Standby, detail: "이송 대기");
        }

        return Snap(EquipmentModuleId.TransferModule, ModuleOperationalState.Idle);
    }

    private static ModuleStateSnapshot BuildPm(
        EquipmentModuleId pmId,
        EquipmentRegion region,
        Context ctx,
        bool globalMaint,
        bool globalWarning,
        bool globalRunning,
        bool transferActive,
        bool virtualOnly = false)
    {
        if (globalMaint)
        {
            return Snap(pmId, ModuleOperationalState.Maintenance);
        }

        if (virtualOnly)
        {
            return Snap(pmId, ModuleOperationalState.Idle, detail: "미사용(표시만)");
        }

        bool hasWafer = ctx.Transfer is not null && ctx.Transfer.HasWaferAt(region);
        PmChamberState? chamber = ctx.Transfer?.ClusterState.GetChamber(region);
        bool isProcessing = chamber?.CurrentWafer is not null && chamber.RemainingProcessTicks > 0;
        bool isEtchPm = EtchPmSelector.IsEtchRegion(region);
        bool doorClosed = ctx.Transfer is null || ctx.Transfer.IsVirtualDoorClosed(region);
        bool doorPhase = ctx.Transfer is not null && !doorClosed
                         && (transferActive || globalRunning);

        ModuleOperationalState st;
        string? detail;
        if (ModuleShowsAlarm(ctx, pmId))
        {
            st = ModuleOperationalState.Alarm;
            detail = ctx.AlarmCode;
        }
        else if (doorPhase)
        {
            st = ModuleOperationalState.Running;
            detail = "슬릿 열림(가상)";
        }
        else if (isProcessing)
        {
            st = ModuleOperationalState.Processing;
            detail = isEtchPm ? "Etch" : "Strip (PR)";
        }
        else if (chamber?.CurrentWafer is not null)
        {
            st = ModuleOperationalState.Standby;
            detail = isEtchPm ? "Etch 완료" : "Strip 완료";
        }
        else if (chamber?.ReservedForIncoming == true)
        {
            st = ModuleOperationalState.Standby;
            detail = isEtchPm ? "Etch 투입 대기" : "투입 대기";
        }
        else if (isEtchPm
                 && ctx.Transfer is not null
                 && EtchPmSelector.IsNextPipelineReadySlot(region, ctx.Transfer.ClusterState, globalRunning))
        {
            st = ModuleOperationalState.Ready;
            detail = "Etch 준비 · 다음 슬롯";
        }
        else if (hasWafer)
        {
            st = ModuleOperationalState.Standby;
            detail = isEtchPm ? "Etch" : "Strip (PR)";
        }
        else
        {
            st = ModuleOperationalState.Idle;
            detail = null;
        }

        string label = pmId switch
        {
            EquipmentModuleId.Pm1 => "Strip (PR)",
            EquipmentModuleId.Pm2 => "Etch",
            EquipmentModuleId.Pm3 => "Etch",
            EquipmentModuleId.Pm4 => "Etch",
            _ => "Etch"
        };

        return Snap(pmId, st, doorClosed: doorClosed, hasWafer: hasWafer,
            detail: detail ?? (hasWafer ? label : null));
    }

    private static ModuleStateSnapshot BuildEfem(
        Context ctx,
        bool globalMaint,
        bool globalRunning,
        bool transferActive)
    {
        if (globalMaint)
        {
            return Snap(EquipmentModuleId.Efem, ModuleOperationalState.Maintenance);
        }

        ModuleOperationalState st = ModuleShowsAlarm(ctx, EquipmentModuleId.Efem)
            ? ModuleOperationalState.Alarm
            : transferActive || globalRunning ? ModuleOperationalState.Standby
            : ModuleOperationalState.Idle;

        string? detail = st == ModuleOperationalState.Alarm ? ctx.AlarmCode : "EFEM (대기압 구역)";
        return Snap(EquipmentModuleId.Efem, st, detail: detail);
    }

    private static ModuleStateSnapshot BuildAligner(Context ctx, bool globalMaint, bool transferActive)
    {
        if (globalMaint)
        {
            return Snap(EquipmentModuleId.Aligner, ModuleOperationalState.Maintenance);
        }

        bool atAligner = ctx.Transfer is not null && ctx.Transfer.HasWaferAt(EquipmentRegion.Aligner);
        bool alignPhase = transferActive && ctx.Transfer is not null
            && (ctx.Transfer.TmRegion == EquipmentRegion.Aligner || atAligner);
        int alignCount = ctx.Transfer?.ClusterState.AlignerBuffer.Count ?? 0;
        int alignCap = ctx.Transfer?.ClusterState.Capacity.AlignerSlotCount ?? EquipmentCapacityConfig.DefaultAlignerSlotCount;

        ModuleOperationalState st = ModuleShowsAlarm(ctx, EquipmentModuleId.Aligner)
            ? ModuleOperationalState.Alarm
            : alignPhase ? ModuleOperationalState.Running
            : atAligner ? ModuleOperationalState.Standby
            : ModuleOperationalState.Idle;

        string? detail = alignPhase
            ? "정렬(가상)"
            : alignCount > 0
                ? $"슬롯 {alignCount}/{alignCap}"
                : null;

        return Snap(EquipmentModuleId.Aligner, st, hasWafer: atAligner, detail: detail);
    }

    private static ModuleStateSnapshot BuildSideStorage(Context ctx, bool globalMaint, bool transferActive)
    {
        if (globalMaint)
        {
            return Snap(EquipmentModuleId.SideStorage, ModuleOperationalState.Maintenance);
        }

        bool atSide = ctx.Transfer is not null && ctx.Transfer.HasWaferAt(EquipmentRegion.SideStorage);
        bool fumePhase = transferActive && ctx.Transfer is not null
            && (ctx.Transfer.TmRegion == EquipmentRegion.SideStorage || atSide);
        int sideCount = ctx.Transfer?.ClusterState.SideStorage.Count ?? 0;
        int sideCap = ctx.Transfer?.ClusterState.Capacity.SideStorageSlotCount ?? 25;

        ModuleOperationalState st = ModuleShowsAlarm(ctx, EquipmentModuleId.SideStorage)
            ? ModuleOperationalState.Alarm
            : fumePhase ? ModuleOperationalState.Running
            : atSide ? ModuleOperationalState.Standby
            : ModuleOperationalState.Idle;

        string? detail = fumePhase
            ? "Fume 제거(가상)"
            : sideCount >= sideCap
                ? $"카세트 {sideCount}/{sideCap} · 교체 대기"
                : sideCount > 0
                    ? $"카세트 {sideCount}/{sideCap}"
                    : null;

        return Snap(EquipmentModuleId.SideStorage, st, hasWafer: atSide, detail: detail);
    }

    private static ModuleStateSnapshot Snap(
        EquipmentModuleId id,
        ModuleOperationalState state,
        bool? doorClosed = null,
        bool? hasWafer = null,
        string? detail = null) =>
        new()
        {
            ModuleId = id,
            State = state,
            DoorClosed = doorClosed,
            HasWafer = hasWafer,
            Detail = detail
        };
}
