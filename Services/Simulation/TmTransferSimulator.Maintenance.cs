using etch_ui.Equipment.Models;
using etch_ui.Services.Scheduling;

namespace etch_ui.Services.Simulation;

public sealed partial class TmTransferSimulator
{
    public string DescribeMaintenanceState()
    {
        ClusterEquipmentState s = _state;
        int cap = s.Capacity.FoupSlotCount;
        int sideCap = s.Capacity.SideStorageSlotCount;
        int pmWafers = s.Chambers.Values.Count(ch => ch.CurrentWafer is not null);
        string foup = string.Join("·",
            s.FoupPorts.Select(p => $"{p.RemainingInFoup}/{cap}(↑{p.InFlightCount})"));
        return
            $"FOUP {foup}  |  BM {s.LoadLockBuffer.Count}/{s.LoadLockBuffer.Capacity}  |  "
            + $"Aligner {s.AlignerBuffer.Count}/{s.AlignerBuffer.Capacity}  |  "
            + $"Side {s.SideStorage.Count}/{sideCap}  |  PM웨이퍼 {pmWafers}  |  "
            + $"LOT {s.Lot.CompletedCount}/{s.Lot.TargetCount}";
    }

    public void MaintenanceResetVirtualLine()
    {
        ResetDemoLine();
        PhaseHint = "정비: 가상 라인 초기화";
    }

    public void MaintenanceClearChambers()
    {
        foreach (PmChamberState ch in _state.Chambers.Values)
        {
            ch.ClearWafer();
            ch.ReservedForIncoming = false;
        }

        PhaseHint = "정비: PM 웨이퍼 제거";
    }

    public void MaintenanceClearLoadLock() => MaintenanceClearBuffer(_state.LoadLockBuffer, "BM(Load Lock)");

    public void MaintenanceClearAligner() => MaintenanceClearBuffer(_state.AlignerBuffer, "Aligner");

    private void MaintenanceClearBuffer(WaferSlotBuffer buffer, string label)
    {
        buffer.Clear();
        PhaseHint = $"정비: {label} 비움";
    }

    public void MaintenanceClearSideStorage()
    {
        while (_state.SideStorage.TryDequeue(out _))
        {
        }

        PhaseHint = "정비: Side Stg 비움";
    }

    public int MaintenanceRemountAllFoups()
    {
        int cap = _state.Capacity.FoupSlotCount;
        foreach (FoupPortState port in _state.FoupPorts)
        {
            port.OnNewFoupMounted(cap);
            port.InFlightCount = 0;
        }

        PhaseHint = $"정비: FOUP 3개 재장착 ({cap}매)";
        return cap;
    }

    public int MaintenanceSideCassetteSwap() =>
        _state.PerformSideStorageCassetteSwap();

    public bool MaintenanceAdvanceOneTick(out string hint)
    {
        bool resumeRunning = _running;
        _running = true;
        if (!_efem.IsBusy && _efem.Queue.Count == 0 && _efem.Active is null)
        {
            TrySchedule(_efem, _efemScheduler);
        }

        if (!_vacuum.IsBusy && _vacuum.Queue.Count == 0 && _vacuum.Active is null)
        {
            TrySchedule(_vacuum, _vacuumScheduler);
        }

        Tick(1);
        hint = PhaseHint;
        if (!resumeRunning)
        {
            _running = false;
        }

        return true;
    }
}
